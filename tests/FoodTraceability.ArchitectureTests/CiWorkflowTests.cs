using System.Text.RegularExpressions;

namespace FoodTraceability.ArchitectureTests;

public sealed partial class CiWorkflowTests
{
    [Fact]
    public void CiWorkflowFileExists()
    {
        Assert.True(File.Exists(GetWorkflowFilePath()), ".github/workflows/ci.yml must exist.");
    }

    [Fact]
    public void CiWorkflowRunsOnUbuntu()
    {
        Assert.Matches(UbuntuRunnerRegex(), ReadWorkflowFile());
    }

    [Fact]
    public void CiWorkflowRunsTheFullTestSuiteWithoutTraitFilter()
    {
        var workflow = ReadWorkflowFile();

        Assert.Matches(FullSolutionTestCommandRegex(), workflow);
        Assert.DoesNotContain("--filter", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Category!=Database", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CiWorkflowUsesSdkVersionFromGlobalJson()
    {
        var root = FindRepositoryRoot();
        var workflow = ReadWorkflowFile();
        var globalJson = File.ReadAllText(Path.Combine(root, "global.json"));
        var sdkVersionMatch = SdkVersionRegex().Match(globalJson);

        Assert.Contains("actions/setup-dotnet@v4", workflow, StringComparison.Ordinal);
        Assert.Matches(GlobalJsonFileRegex(), workflow);
        Assert.True(sdkVersionMatch.Success, "global.json must declare an SDK version.");
        Assert.DoesNotContain(sdkVersionMatch.Groups["version"].Value, workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void CiWorkflowDeclaresLeastPrivilegePermissions()
    {
        var permissionsMatch = PermissionsBlockRegex().Match(ReadWorkflowFile());

        Assert.True(permissionsMatch.Success, "The workflow must declare top-level permissions.");
        Assert.Equal("contents: read", permissionsMatch.Groups["body"].Value.Trim());
    }

    [Fact]
    public void CiWorkflowContainsNoSecrets()
    {
        var workflow = ReadWorkflowFile();

        Assert.DoesNotContain("secrets.", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(CredentialLiteralRegex(), workflow);
    }

    private static string ReadWorkflowFile()
    {
        return File.ReadAllText(GetWorkflowFilePath());
    }

    private static string GetWorkflowFilePath()
    {
        return Path.Combine(FindRepositoryRoot(), ".github", "workflows", "ci.yml");
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

    [GeneratedRegex("(?m)^\\s*runs-on:\\s*ubuntu(?:-[A-Za-z0-9.-]+)?\\s*$")]
    private static partial Regex UbuntuRunnerRegex();

    [GeneratedRegex("(?m)^\\s*run:\\s*dotnet test FoodTraceability\\.sln --no-build -c Release\\s*$")]
    private static partial Regex FullSolutionTestCommandRegex();

    [GeneratedRegex("(?m)^\\s*global-json-file:\\s*global\\.json\\s*$")]
    private static partial Regex GlobalJsonFileRegex();

    [GeneratedRegex("\"version\"\\s*:\\s*\"(?<version>\\d+\\.\\d+\\.\\d+)\"")]
    private static partial Regex SdkVersionRegex();

    [GeneratedRegex("(?ms)^permissions:\\s*\\r?\\n(?<body>(?:[ \\t]+[^\\r\\n]*(?:\\r?\\n|$))*)")]
    private static partial Regex PermissionsBlockRegex();

    [GeneratedRegex("(?im)^\\s*(?:password|token|api[_-]?key|client[_-]?secret)\\s*:\\s*[^#\\r\\n]+$")]
    private static partial Regex CredentialLiteralRegex();
}
