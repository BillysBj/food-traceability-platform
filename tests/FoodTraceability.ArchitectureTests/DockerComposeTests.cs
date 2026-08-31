using System.Text.RegularExpressions;

namespace FoodTraceability.ArchitectureTests;

public sealed partial class DockerComposeTests
{
    [Fact]
    public void ComposeFileExists()
    {
        Assert.True(File.Exists(GetComposeFilePath()), "docker-compose.yml must exist in the repository root.");
    }

    [Fact]
    public void PostgresImageIsPinned()
    {
        var composeFile = ReadComposeFile();
        var imageMatch = PostgresImageRegex().Match(composeFile);

        Assert.True(imageMatch.Success, "The PostgreSQL image must use an explicit version tag.");
        Assert.Equal("17", imageMatch.Groups["tag"].Value);
        Assert.DoesNotContain("latest", imageMatch.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ComposeFileContainsNoPlaintextSecrets()
    {
        var composeFile = ReadComposeFile();
        var passwordAssignments = PostgresPasswordRegex().Matches(composeFile);
        var passwordAssignment = Assert.Single(passwordAssignments.Cast<Match>());

        Assert.Matches(VariableReferenceRegex(), passwordAssignment.Groups["value"].Value);
    }

    [Fact]
    public void ComposeFileDeclaresNamedVolume()
    {
        var composeFile = ReadComposeFile();
        var mountMatch = PostgresDataMountRegex().Match(composeFile);

        Assert.True(mountMatch.Success, "A named volume must be mounted at the PostgreSQL data directory.");

        var volumeName = Regex.Escape(mountMatch.Groups["name"].Value);
        Assert.Matches($"(?m)^  {volumeName}:\\s*$", composeFile);
    }

    [Fact]
    public void ComposeFileDeclaresHealthcheck()
    {
        var composeFile = ReadComposeFile();

        Assert.Matches(HealthcheckRegex(), composeFile);
        Assert.Contains("pg_isready", composeFile, StringComparison.Ordinal);
    }

    [Fact]
    public void DatabaseIsCreatedAsUtf8()
    {
        Assert.Matches(Utf8InitDbArgumentRegex(), ReadComposeFile());
    }

    [Fact]
    public void EnvExampleCoversAllComposeVariables()
    {
        var root = FindRepositoryRoot();
        var composeVariables = ComposeVariableRegex()
            .Matches(ReadComposeFile())
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);
        var envVariables = EnvVariableRegex()
            .Matches(File.ReadAllText(Path.Combine(root, ".env.example")))
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(composeVariables);
        Assert.Empty(composeVariables.Except(envVariables, StringComparer.Ordinal));
    }

    [Fact]
    public void NoDatabaseInitScriptsArePresent()
    {
        var root = FindRepositoryRoot();
        var initScriptDirectories = Directory
            .EnumerateDirectories(root, "*", SearchOption.AllDirectories)
            .Where(path => string.Equals(
                Path.GetFileName(path),
                "docker-entrypoint-initdb.d",
                StringComparison.OrdinalIgnoreCase));

        Assert.Empty(initScriptDirectories);
        Assert.DoesNotContain("docker-entrypoint-initdb.d", ReadComposeFile(), StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadComposeFile()
    {
        return File.ReadAllText(GetComposeFilePath());
    }

    private static string GetComposeFilePath()
    {
        return Path.Combine(FindRepositoryRoot(), "docker-compose.yml");
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

    [GeneratedRegex("(?m)^\\s*image:\\s*postgres:(?<tag>[^\\s#]+)\\s*(?:#.*)?$")]
    private static partial Regex PostgresImageRegex();

    [GeneratedRegex("(?m)^\\s*POSTGRES_PASSWORD:\\s*(?<value>\\S+)\\s*$")]
    private static partial Regex PostgresPasswordRegex();

    [GeneratedRegex("^\\$\\{[A-Z_][A-Z0-9_]*(?::-[^}]*)?\\}$")]
    private static partial Regex VariableReferenceRegex();

    [GeneratedRegex("(?m)^\\s{6}-\\s*(?<name>[A-Za-z0-9_.-]+):/var/lib/postgresql/data/?\\s*$")]
    private static partial Regex PostgresDataMountRegex();

    [GeneratedRegex("(?m)^\\s{4}healthcheck:\\s*$")]
    private static partial Regex HealthcheckRegex();

    [GeneratedRegex("(?m)^\\s*POSTGRES_INITDB_ARGS:\\s*['\"]?--encoding=UTF8['\"]?\\s*$")]
    private static partial Regex Utf8InitDbArgumentRegex();

    [GeneratedRegex("\\$\\{(?<name>[A-Z_][A-Z0-9_]*)(?::-[^}]*)?\\}")]
    private static partial Regex ComposeVariableRegex();

    [GeneratedRegex("(?m)^\\s*(?<name>[A-Z_][A-Z0-9_]*)=")]
    private static partial Regex EnvVariableRegex();
}
