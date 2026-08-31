using System.Text.RegularExpressions;
using System.Xml.Linq;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FoodTraceability.ArchitectureTests;

public sealed class PersistenceArchitectureTests
{
    private static readonly string[] EfCorePackagePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "EFCore."
    ];

    [Fact]
    public void DomainAndApplicationProjectsDoNotReferenceEfCore()
    {
        var projects = LoadProjectGraph();
        var guardedProjects = projects.Values
            .Where(project =>
                project.Name.EndsWith(".Domain", StringComparison.Ordinal)
                || project.Name.EndsWith(".Application", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(20, guardedProjects.Length);

        foreach (var guardedProject in guardedProjects)
        {
            var reachableProjects = GetReachableProjects(guardedProject, projects);
            var forbiddenPackages = reachableProjects
                .SelectMany(project => project.PackageReferences.Select(package => (project.Name, Package: package)))
                .Where(reference => EfCorePackagePrefixes.Any(prefix =>
                    reference.Package.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            Assert.True(
                forbiddenPackages.Length == 0,
                $"{guardedProject.Name} transitively reaches EF Core packages: "
                + string.Join(", ", forbiddenPackages.Select(reference => $"{reference.Name}:{reference.Package}")));
        }
    }

    [Fact]
    public void BuildingBlocksRemainsPackageFree()
    {
        var root = FindRepositoryRoot();
        var projectPath = Path.Combine(
            root,
            "src",
            "BuildingBlocks",
            "FoodTraceability.BuildingBlocks",
            "FoodTraceability.BuildingBlocks.csproj");
        var document = XDocument.Load(projectPath);

        Assert.Empty(GetElements(document, "PackageReference"));
    }

    [Fact]
    public void PlatformPersistenceIsReferencedOnlyWhereAllowed()
    {
        var root = FindRepositoryRoot();
        var projects = LoadProjectGraph();
        var persistenceProject = projects.Values.Single(project =>
            string.Equals(project.Name, "FoodTraceability.Platform.Persistence", StringComparison.Ordinal));
        var referringProjects = projects.Values
            .Where(project => project.ProjectReferences.Contains(persistenceProject.Path, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        Assert.NotEmpty(referringProjects);

        foreach (var referringProject in referringProjects)
        {
            var relativePath = Path.GetRelativePath(root, referringProject.Path).Replace('\\', '/');
            var isAllowed = string.Equals(
                    referringProject.Name,
                    "FoodTraceability.Api",
                    StringComparison.Ordinal)
                || referringProject.Name.EndsWith(".Infrastructure", StringComparison.Ordinal)
                || relativePath.StartsWith("tests/", StringComparison.Ordinal);

            Assert.True(isAllowed, $"{referringProject.Name} must not reference Platform.Persistence.");
        }
    }

    [Fact]
    public void MigrationScriptDeclaresIcuCollations()
    {
        using var context = CreatePlatformDbContext();
        var script = context.Database.GenerateCreateScript();

        Assert.Matches(
            new Regex(
                "CREATE COLLATION\\s+\\\"?en\\\"?\\s*\\(.*?PROVIDER\\s*=\\s*icu",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            script);
        Assert.Matches(
            new Regex(
                "CREATE COLLATION\\s+\\\"?el\\\"?\\s*\\(.*?PROVIDER\\s*=\\s*icu",
                RegexOptions.IgnoreCase | RegexOptions.Singleline),
            script);
    }

    [Fact]
    public void MigrationScriptCreatesNoBusinessTables()
    {
        using var context = CreatePlatformDbContext();
        var script = context.Database.GenerateCreateScript();
        var createdTables = Regex.Matches(
            script,
            "CREATE TABLE\\s+(?<table>(?:\\\"[^\\\"]+\\\"|[a-z_][a-z0-9_]*)(?:\\.(?:\\\"[^\\\"]+\\\"|[a-z_][a-z0-9_]*))?)",
            RegexOptions.IgnoreCase);

        Assert.All(
            createdTables.Cast<Match>(),
            match => Assert.Contains(
                PersistenceConventions.MigrationsHistoryTableName,
                match.Groups["table"].Value,
                StringComparison.Ordinal));
    }

    [Fact]
    public void MigrationsHistoryTableIsSnakeCase()
    {
        using var context = CreatePlatformDbContext();
        var historyRepository = context.GetService<IHistoryRepository>();
        var script = historyRepository.GetCreateScript();

        Assert.Equal("__ef_migrations_history", PersistenceConventions.MigrationsHistoryTableName);
        Assert.Matches(
            new Regex(
                $"CREATE TABLE\\s+\\\"?{PlatformDbContext.MigrationsHistorySchema}\\\"?\\.\\\"?{PersistenceConventions.MigrationsHistoryTableName}\\\"?",
                RegexOptions.IgnoreCase),
            script);
    }

    private static PlatformDbContext CreatePlatformDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            "Host=localhost;Database=architecture_tests;Username=unused",
            PlatformDbContext.MigrationsHistorySchema);

        return new PlatformDbContext(optionsBuilder.Options);
    }

    private static IReadOnlyCollection<ProjectNode> GetReachableProjects(
        ProjectNode startingProject,
        IReadOnlyDictionary<string, ProjectNode> projects)
    {
        var reachableProjects = new List<ProjectNode>();
        var pendingPaths = new Stack<string>();
        var visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pendingPaths.Push(startingProject.Path);

        while (pendingPaths.TryPop(out var projectPath))
        {
            if (!visitedPaths.Add(projectPath))
            {
                continue;
            }

            var project = projects[projectPath];
            reachableProjects.Add(project);

            foreach (var referencedProjectPath in project.ProjectReferences)
            {
                pendingPaths.Push(referencedProjectPath);
            }
        }

        return reachableProjects;
    }

    private static IReadOnlyDictionary<string, ProjectNode> LoadProjectGraph()
    {
        var root = FindRepositoryRoot();
        return new[] { "src", "tests" }
            .SelectMany(directory => Directory.EnumerateFiles(
                Path.Combine(root, directory),
                "*.csproj",
                SearchOption.AllDirectories))
            .Select(projectPath =>
            {
                var fullPath = Path.GetFullPath(projectPath);
                var document = XDocument.Load(fullPath);
                var projectDirectory = Path.GetDirectoryName(fullPath)
                    ?? throw new InvalidOperationException($"Project path has no directory: {fullPath}");
                var projectReferences = GetElements(document, "ProjectReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .OfType<string>()
                    .Select(reference => Path.GetFullPath(Path.Combine(projectDirectory, reference)))
                    .ToArray();
                var packageReferences = GetElements(document, "PackageReference")
                    .Select(element => element.Attribute("Include")?.Value)
                    .OfType<string>()
                    .ToArray();

                return new ProjectNode(
                    fullPath,
                    Path.GetFileNameWithoutExtension(fullPath),
                    projectReferences,
                    packageReferences);
            })
            .ToDictionary(project => project.Path, StringComparer.OrdinalIgnoreCase);
    }

    private static XElement[] GetElements(XDocument document, string localName)
    {
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == localName)
            .ToArray();
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (!File.Exists(Path.Combine(current.FullName, "FoodTraceability.sln")))
        {
            current = current.Parent
                ?? throw new InvalidOperationException("Could not locate the repository root.");
        }

        return current.FullName;
    }

    private sealed record ProjectNode(
        string Path,
        string Name,
        string[] ProjectReferences,
        string[] PackageReferences);
}
