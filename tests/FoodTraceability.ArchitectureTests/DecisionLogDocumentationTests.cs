using System.Globalization;
using System.Text.RegularExpressions;

namespace FoodTraceability.ArchitectureTests;

public sealed partial class DecisionLogDocumentationTests
{
    private const string DecisionLogPath = "docs/DECISIONS.md";

    private static readonly string[] GoverningDocumentPaths =
    [
        "AGENTS.md",
        "ARCHITECTURE.md",
        "DEVELOPMENT_PLAN.md"
    ];

    [Fact]
    public void DecisionLogExists()
    {
        Assert.True(
            File.Exists(GetRepositoryFilePath(DecisionLogPath)),
            $"{DecisionLogPath} must exist.");
    }

    [Fact]
    public void DecisionIdsAreUniqueAndContiguous()
    {
        var decisionIds = GetDecisionIds(ReadRepositoryFile(DecisionLogPath));

        Assert.NotEmpty(decisionIds);
        Assert.DoesNotContain(decisionIds.GroupBy(id => id), group => group.Count() > 1);
        Assert.Equal(Enumerable.Range(1, decisionIds.Max()).ToArray(), decisionIds);
    }

    [Fact]
    public void NextFreeDecisionIdMatchesLog()
    {
        var decisionLog = ReadRepositoryFile(DecisionLogPath);
        var decisionIds = GetDecisionIds(decisionLog);
        var nextFreeIdMatch = NextFreeDecisionIdRegex().Match(decisionLog);

        Assert.True(nextFreeIdMatch.Success, "The decision log must declare the next free decision ID.");

        var nextFreeId = int.Parse(nextFreeIdMatch.Groups["number"].Value, CultureInfo.InvariantCulture);

        Assert.Equal(decisionIds.Max() + 1, nextFreeId);
    }

    [Fact]
    public void EveryDecisionHasAStatus()
    {
        var decisionLog = ReadRepositoryFile(DecisionLogPath);
        var decisionHeadings = DecisionHeadingRegex().Matches(decisionLog).Cast<Match>().ToArray();
        var nextFreeIdHeading = NextFreeDecisionIdRegex().Match(decisionLog);

        Assert.NotEmpty(decisionHeadings);
        Assert.True(nextFreeIdHeading.Success, "The decision log must declare the next free decision ID.");

        for (var index = 0; index < decisionHeadings.Length; index++)
        {
            var sectionStart = decisionHeadings[index].Index + decisionHeadings[index].Length;
            var sectionEnd = index + 1 < decisionHeadings.Length
                ? decisionHeadings[index + 1].Index
                : nextFreeIdHeading.Index;
            var section = decisionLog[sectionStart..sectionEnd];
            var statusLines = DecisionStatusRegex().Matches(section).Cast<Match>().ToArray();

            Assert.Single(statusLines);
        }
    }

    [Fact]
    public void PermissionListsMatchTheDecisionLog()
    {
        const string permissionListMarker =
            "Kanonische Permission-Liste für Pilot 1 gemäß D-18 in `docs/DECISIONS.md`:";
        const string decisionMarker = "## D-18 – Kanonische Permission-Liste Pilot 1 v1";

        var agentsPermissions = ExtractPermissionsFromTextCodeBlock(
            ReadRepositoryFile("AGENTS.md"),
            permissionListMarker);
        var architecturePermissions = ExtractPermissionsFromTextCodeBlock(
            ReadRepositoryFile("ARCHITECTURE.md"),
            permissionListMarker);
        var decisionPermissions = ExtractPermissionsFromTextCodeBlock(
            ReadRepositoryFile(DecisionLogPath),
            decisionMarker);

        Assert.Equal(26, decisionPermissions.Length);
        Assert.Equal(decisionPermissions.Length, decisionPermissions.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(decisionPermissions, agentsPermissions);
        Assert.Equal(decisionPermissions, architecturePermissions);
    }

    [Fact]
    public void NoShipmentPermissionsRemain()
    {
        Assert.DoesNotContain("shipment.", ReadRepositoryFile("AGENTS.md"), StringComparison.Ordinal);
        Assert.DoesNotContain("shipment.", ReadRepositoryFile("ARCHITECTURE.md"), StringComparison.Ordinal);
    }

    [Fact]
    public void GoverningDocumentsReferenceTheDecisionLog()
    {
        foreach (var documentPath in GoverningDocumentPaths)
        {
            Assert.Contains(DecisionLogPath, ReadRepositoryFile(documentPath), StringComparison.Ordinal);
        }
    }

    private static int[] GetDecisionIds(string decisionLog)
    {
        return DecisionHeadingRegex()
            .Matches(decisionLog)
            .Select(match => int.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture))
            .ToArray();
    }

    private static string[] ExtractPermissionsFromTextCodeBlock(string document, string marker)
    {
        var markerIndex = document.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(markerIndex >= 0, $"Could not find permission-list marker '{marker}'.");

        var fenceStart = document.IndexOf("```text", markerIndex, StringComparison.Ordinal);
        Assert.True(fenceStart >= 0, $"Could not find a text code block after '{marker}'.");

        var contentStart = document.IndexOf('\n', fenceStart) + 1;
        Assert.True(contentStart > 0, $"Could not find the content of the text code block after '{marker}'.");

        var fenceEnd = document.IndexOf("```", contentStart, StringComparison.Ordinal);
        Assert.True(fenceEnd >= 0, $"Could not find the end of the text code block after '{marker}'.");

        return PermissionCodeRegex()
            .Matches(document[contentStart..fenceEnd])
            .Select(match => match.Groups["permission"].Value)
            .ToArray();
    }

    private static string ReadRepositoryFile(string relativePath)
    {
        return File.ReadAllText(GetRepositoryFilePath(relativePath));
    }

    private static string GetRepositoryFilePath(string relativePath)
    {
        return Path.Combine(FindRepositoryRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
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

    [GeneratedRegex("(?m)^## D-(?<number>\\d{2}) – [^\\r\\n]+$")]
    private static partial Regex DecisionHeadingRegex();

    [GeneratedRegex("(?m)^## Nächste freie ID\\s*\\r?\\n(?:\\s*\\r?\\n)*`D-(?<number>\\d{2})`\\s*$")]
    private static partial Regex NextFreeDecisionIdRegex();

    [GeneratedRegex("(?m)^\\*\\*Status:\\*\\*\\s+(?<status>ENTSCHIEDEN|OFFEN)[^\\r\\n]*$")]
    private static partial Regex DecisionStatusRegex();

    [GeneratedRegex("(?<![a-z0-9_.])(?<permission>[a-z]+(?:\\.[a-z]+)+)(?![a-z0-9_.])")]
    private static partial Regex PermissionCodeRegex();
}
