using System.Text.RegularExpressions;

namespace FoodTraceability.ArchitectureTests;

public sealed partial class DevelopmentPlanDocumentationTests
{
    private const string DevelopmentPlanPath = "DEVELOPMENT_PLAN.md";
    private const string EpicListsStartMarker = "# EPIC 0 – Foundation";
    private const string EpicListsEndMarker = "# Spätere Epics";
    private const string VocabularyStartMarker = "## Roadmap-Statusvokabular";
    private const string VocabularyEndMarker = "## Plan-Id und Repository-Id";
    private const string MilestoneStatusStartMarker = "## Milestone-Status";

    private static readonly string[] RoadmapStatuses =
    [
        "DONE",
        "IN_PROGRESS",
        "NOT_STARTED",
        "DEFERRED",
        "SUPERSEDED"
    ];

    [Fact]
    public void EveryPlannedTaskHasAStatus()
    {
        var taskLines = GetEpicTaskLines();

        Assert.NotEmpty(taskLines);

        foreach (var taskLine in taskLines)
        {
            var statusMatches = RoadmapStatusMarkerRegex()
                .Matches(taskLine)
                .Cast<Match>()
                .ToArray();

            var statusMatch = Assert.Single(statusMatches);
            Assert.Contains(statusMatch.Groups["status"].Value, RoadmapStatuses);
        }
    }

    [Fact]
    public void EveryUsedStatusValueIsDefined()
    {
        var developmentPlan = ReadRepositoryFile(DevelopmentPlanPath);
        var vocabularySection = ExtractSection(
            developmentPlan,
            VocabularyStartMarker,
            VocabularyEndMarker);
        var definedStatuses = VocabularyDefinitionRegex()
            .Matches(vocabularySection)
            .Select(match => match.Groups["status"].Value)
            .ToArray();
        var usedStatuses = RoadmapStatusMarkerRegex()
            .Matches(developmentPlan)
            .Select(match => match.Groups["status"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(RoadmapStatuses, definedStatuses);
        Assert.Empty(usedStatuses.Except(definedStatuses, StringComparer.Ordinal));
    }

    [Fact]
    public void DeferredTasksStateAReason()
    {
        var deferredTaskLines = GetRoadmapTaskLines()
            .Where(taskLine => GetRoadmapStatus(taskLine) == "DEFERRED")
            .ToArray();

        Assert.All(deferredTaskLines, taskLine => Assert.Matches(DeferredReasonRegex(), taskLine));
    }

    [Fact]
    public void SupersededTasksNameTheReplacingTask()
    {
        var supersededTaskLines = GetRoadmapTaskLines()
            .Where(taskLine => GetRoadmapStatus(taskLine) == "SUPERSEDED")
            .ToArray();

        Assert.All(
            supersededTaskLines,
            taskLine => Assert.Matches(ReplacingTaskReferenceRegex(), taskLine));
    }

    [Fact]
    public void EveryMilestoneHasAReachedState()
    {
        var milestoneRows = GetMilestoneRows();
        var expectedMilestoneIds = Enumerable.Range(0, 13).Select(number => $"M{number}").ToArray();

        Assert.Equal(expectedMilestoneIds, milestoneRows.Select(row => row.Id).ToArray());
        Assert.All(
            expectedMilestoneIds,
            milestoneId => Assert.Single(milestoneRows, row => row.Id == milestoneId));
    }

    [Fact]
    public void MilestonesOneToFourAreMarkedNotReached()
    {
        var milestoneRows = GetMilestoneRows().ToDictionary(row => row.Id, row => row.State);

        foreach (var milestoneId in new[] { "M1", "M2", "M3", "M4" })
        {
            Assert.Equal("NICHT ERREICHT", milestoneRows[milestoneId]);
        }
    }

    private static string[] GetEpicTaskLines()
    {
        var developmentPlan = ReadRepositoryFile(DevelopmentPlanPath);
        var epicLists = ExtractSection(
            developmentPlan,
            EpicListsStartMarker,
            EpicListsEndMarker);

        return TaskLineRegex()
            .Matches(epicLists)
            .Select(match => match.Value)
            .ToArray();
    }

    private static string[] GetRoadmapTaskLines()
    {
        return TaskLineRegex()
            .Matches(ReadRepositoryFile(DevelopmentPlanPath))
            .Select(match => match.Value)
            .Where(taskLine => RoadmapStatusMarkerRegex().IsMatch(taskLine))
            .ToArray();
    }

    private static string GetRoadmapStatus(string taskLine)
    {
        var statusMatch = RoadmapStatusMarkerRegex().Match(taskLine);

        Assert.True(statusMatch.Success, $"Task line has no roadmap status: {taskLine}");

        return statusMatch.Groups["status"].Value;
    }

    private static (string Id, string State)[] GetMilestoneRows()
    {
        var developmentPlan = ReadRepositoryFile(DevelopmentPlanPath);
        var milestoneSection = ExtractSection(
            developmentPlan,
            MilestoneStatusStartMarker,
            EpicListsEndMarker);

        return MilestoneRowRegex()
            .Matches(milestoneSection)
            .Select(match => (
                match.Groups["id"].Value,
                match.Groups["state"].Value))
            .ToArray();
    }

    private static string ExtractSection(string document, string startMarker, string endMarker)
    {
        var sectionStart = document.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(sectionStart >= 0, $"Could not find section marker '{startMarker}'.");

        var sectionEnd = document.IndexOf(endMarker, sectionStart + startMarker.Length, StringComparison.Ordinal);
        Assert.True(sectionEnd >= 0, $"Could not find section marker '{endMarker}'.");

        return document[sectionStart..sectionEnd];
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

    [GeneratedRegex("(?m)^- \\*\\*(?<id>[A-Z][A-Z0-9]*-\\d{3}[a-z]?)\\*\\*[^\\r\\n]*$")]
    private static partial Regex TaskLineRegex();

    [GeneratedRegex("\\*\\*Roadmap-Status:\\s*(?<status>[A-Z_]+)\\*\\*")]
    private static partial Regex RoadmapStatusMarkerRegex();

    [GeneratedRegex("(?m)^- \\*\\*(?<status>[A-Z_]+)\\*\\* — [^\\r\\n]+$")]
    private static partial Regex VocabularyDefinitionRegex();

    [GeneratedRegex("\\*\\*Grund:\\*\\*\\s+\\S")]
    private static partial Regex DeferredReasonRegex();

    [GeneratedRegex("\\*\\*Ersetzt durch:\\*\\*\\s+(?:Repository-Task|Plan-Task)\\s+\\*\\*[A-Z][A-Z0-9]*-\\d{3}[a-z]?\\*\\*")]
    private static partial Regex ReplacingTaskReferenceRegex();

    [GeneratedRegex("(?m)^- \\*\\*(?<id>M(?:[0-9]|1[0-2])) – [^*\\r\\n]+\\*\\* — \\*\\*(?<state>ERREICHT|NICHT ERREICHT)\\*\\*\\.[^\\r\\n]*$")]
    private static partial Regex MilestoneRowRegex();
}
