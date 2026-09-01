using System.Text.Json;

namespace FoodTraceability.ArchitectureTests;

public sealed class LoggingConfigurationTests
{
    [Fact]
    public void ConsoleOutputTemplateIncludesException()
    {
        var appSettingsPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FoodTraceability.Api",
            "appsettings.json");
        using var document = JsonDocument.Parse(File.ReadAllText(appSettingsPath));
        var writeTo = document.RootElement
            .GetProperty("Serilog")
            .GetProperty("WriteTo")
            .EnumerateArray();
        var consoleSink = Assert.Single(
            writeTo,
            sink => sink.GetProperty("Name").GetString() == "Console");
        var outputTemplate = consoleSink
            .GetProperty("Args")
            .GetProperty("outputTemplate")
            .GetString();

        Assert.Contains("{Exception}", outputTemplate, StringComparison.Ordinal);
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
