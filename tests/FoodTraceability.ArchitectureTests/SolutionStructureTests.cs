using System.Xml.Linq;

namespace FoodTraceability.ArchitectureTests;

public sealed class SolutionStructureTests
{
    private const string BuildingBlocksProjectName = "FoodTraceability.BuildingBlocks";
    private const string ModuleProjectPrefix = "FoodTraceability.Modules.";

    [Fact]
    public void AllExpectedProjectsExist()
    {
        var root = FindRepositoryRoot();
        var expectedProjects = new[]
        {
            "src/FoodTraceability.Api/FoodTraceability.Api.csproj",
            "src/BuildingBlocks/FoodTraceability.BuildingBlocks/FoodTraceability.BuildingBlocks.csproj",
            "src/Platform/FoodTraceability.Platform.Persistence/FoodTraceability.Platform.Persistence.csproj",
            "src/Modules/Identity/FoodTraceability.Modules.Identity.Domain/FoodTraceability.Modules.Identity.Domain.csproj",
            "src/Modules/Identity/FoodTraceability.Modules.Identity.Application/FoodTraceability.Modules.Identity.Application.csproj",
            "src/Modules/Identity/FoodTraceability.Modules.Identity.Infrastructure/FoodTraceability.Modules.Identity.Infrastructure.csproj",
            "src/Modules/Organizations/FoodTraceability.Modules.Organizations.Domain/FoodTraceability.Modules.Organizations.Domain.csproj",
            "src/Modules/Organizations/FoodTraceability.Modules.Organizations.Application/FoodTraceability.Modules.Organizations.Application.csproj",
            "src/Modules/Organizations/FoodTraceability.Modules.Organizations.Infrastructure/FoodTraceability.Modules.Organizations.Infrastructure.csproj",
            "src/Modules/Catalog/FoodTraceability.Modules.Catalog.Domain/FoodTraceability.Modules.Catalog.Domain.csproj",
            "src/Modules/Catalog/FoodTraceability.Modules.Catalog.Application/FoodTraceability.Modules.Catalog.Application.csproj",
            "src/Modules/Catalog/FoodTraceability.Modules.Catalog.Infrastructure/FoodTraceability.Modules.Catalog.Infrastructure.csproj",
            "src/Modules/Traceability/FoodTraceability.Modules.Traceability.Domain/FoodTraceability.Modules.Traceability.Domain.csproj",
            "src/Modules/Traceability/FoodTraceability.Modules.Traceability.Application/FoodTraceability.Modules.Traceability.Application.csproj",
            "src/Modules/Traceability/FoodTraceability.Modules.Traceability.Infrastructure/FoodTraceability.Modules.Traceability.Infrastructure.csproj",
            "src/Modules/Quality/FoodTraceability.Modules.Quality.Domain/FoodTraceability.Modules.Quality.Domain.csproj",
            "src/Modules/Quality/FoodTraceability.Modules.Quality.Application/FoodTraceability.Modules.Quality.Application.csproj",
            "src/Modules/Quality/FoodTraceability.Modules.Quality.Infrastructure/FoodTraceability.Modules.Quality.Infrastructure.csproj",
            "src/Modules/Documents/FoodTraceability.Modules.Documents.Domain/FoodTraceability.Modules.Documents.Domain.csproj",
            "src/Modules/Documents/FoodTraceability.Modules.Documents.Application/FoodTraceability.Modules.Documents.Application.csproj",
            "src/Modules/Documents/FoodTraceability.Modules.Documents.Infrastructure/FoodTraceability.Modules.Documents.Infrastructure.csproj",
            "src/Modules/Logistics/FoodTraceability.Modules.Logistics.Domain/FoodTraceability.Modules.Logistics.Domain.csproj",
            "src/Modules/Logistics/FoodTraceability.Modules.Logistics.Application/FoodTraceability.Modules.Logistics.Application.csproj",
            "src/Modules/Logistics/FoodTraceability.Modules.Logistics.Infrastructure/FoodTraceability.Modules.Logistics.Infrastructure.csproj",
            "src/Modules/PublicTrace/FoodTraceability.Modules.PublicTrace.Domain/FoodTraceability.Modules.PublicTrace.Domain.csproj",
            "src/Modules/PublicTrace/FoodTraceability.Modules.PublicTrace.Application/FoodTraceability.Modules.PublicTrace.Application.csproj",
            "src/Modules/PublicTrace/FoodTraceability.Modules.PublicTrace.Infrastructure/FoodTraceability.Modules.PublicTrace.Infrastructure.csproj",
            "src/Modules/Audit/FoodTraceability.Modules.Audit.Domain/FoodTraceability.Modules.Audit.Domain.csproj",
            "src/Modules/Audit/FoodTraceability.Modules.Audit.Application/FoodTraceability.Modules.Audit.Application.csproj",
            "src/Modules/Audit/FoodTraceability.Modules.Audit.Infrastructure/FoodTraceability.Modules.Audit.Infrastructure.csproj",
            "src/Modules/Industries/OliveOil/FoodTraceability.Modules.Industries.OliveOil.Domain/FoodTraceability.Modules.Industries.OliveOil.Domain.csproj",
            "src/Modules/Industries/OliveOil/FoodTraceability.Modules.Industries.OliveOil.Application/FoodTraceability.Modules.Industries.OliveOil.Application.csproj",
            "src/Modules/Industries/OliveOil/FoodTraceability.Modules.Industries.OliveOil.Infrastructure/FoodTraceability.Modules.Industries.OliveOil.Infrastructure.csproj",
            "tests/FoodTraceability.UnitTests/FoodTraceability.UnitTests.csproj",
            "tests/FoodTraceability.IntegrationTests/FoodTraceability.IntegrationTests.csproj",
            "tests/FoodTraceability.ArchitectureTests/FoodTraceability.ArchitectureTests.csproj"
        };
        var actualProjects = new[] { "src", "tests" }
            .SelectMany(directory => Directory.EnumerateFiles(Path.Combine(root, directory), "*.csproj", SearchOption.AllDirectories))
            .Select(path =>
            {
                _ = XDocument.Load(path);
                return Path.GetRelativePath(root, path).Replace('\\', '/');
            })
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(expectedProjects.Length, actualProjects.Count);
        Assert.Empty(expectedProjects.Except(actualProjects, StringComparer.Ordinal));
        Assert.Empty(actualProjects.Except(expectedProjects, StringComparer.Ordinal));
    }

    [Fact]
    public void DomainProjectsHaveNoPackageReferences()
    {
        var domainProjects = GetDomainProjects();

        Assert.Equal(10, domainProjects.Length);
        foreach (var domainProject in domainProjects)
        {
            var document = XDocument.Load(domainProject);
            var packageReferences = Elements(document, "PackageReference");

            Assert.Empty(packageReferences);
        }
    }

    [Fact]
    public void DomainProjectsOnlyReferenceBuildingBlocks()
    {
        var domainProjects = GetDomainProjects();

        Assert.Equal(10, domainProjects.Length);
        foreach (var domainProject in domainProjects)
        {
            var document = XDocument.Load(domainProject);
            var projectReferences = Elements(document, "ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .OfType<string>()
                .ToArray();

            var projectReference = Assert.Single(projectReferences);
            Assert.Equal(BuildingBlocksProjectName, GetReferencedProjectName(projectReference));
        }
    }

    [Fact]
    public void ModulesDoNotReferenceOtherModules()
    {
        var root = FindRepositoryRoot();
        var moduleProjects = Directory.EnumerateFiles(
            Path.Combine(root, "src", "Modules"),
            "*.csproj",
            SearchOption.AllDirectories);

        foreach (var moduleProject in moduleProjects)
        {
            var sourceModule = GetModuleKey(Path.GetFileNameWithoutExtension(moduleProject));
            var document = XDocument.Load(moduleProject);
            var projectReferences = Elements(document, "ProjectReference")
                .Select(element => element.Attribute("Include")?.Value)
                .OfType<string>();

            foreach (var projectReference in projectReferences)
            {
                var referencedProjectName = GetReferencedProjectName(projectReference);
                if (!referencedProjectName.StartsWith(ModuleProjectPrefix, StringComparison.Ordinal))
                {
                    continue;
                }

                var referencedModule = GetModuleKey(referencedProjectName);
                Assert.Equal(sourceModule, referencedModule);
            }
        }
    }

    private static XElement[] Elements(XDocument document, string localName)
    {
        return document
            .Descendants()
            .Where(element => element.Name.LocalName == localName)
            .ToArray();
    }

    private static string[] GetDomainProjects()
    {
        var root = FindRepositoryRoot();
        return Directory
            .EnumerateFiles(Path.Combine(root, "src", "Modules"), "*.Domain.csproj", SearchOption.AllDirectories)
            .ToArray();
    }

    private static string GetModuleKey(string projectName)
    {
        Assert.StartsWith(ModuleProjectPrefix, projectName, StringComparison.Ordinal);
        var segments = projectName[ModuleProjectPrefix.Length..].Split('.');

        return segments[0] == "Industries"
            ? string.Join('.', segments.Take(2))
            : segments[0];
    }

    private static string GetReferencedProjectName(string projectReference)
    {
        var normalizedReference = projectReference.Replace('\\', '/');
        return Path.GetFileNameWithoutExtension(normalizedReference)
            ?? throw new InvalidOperationException("A project reference has no project file name.");
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
}
