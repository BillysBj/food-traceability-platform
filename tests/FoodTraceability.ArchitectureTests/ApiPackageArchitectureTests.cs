using System.Xml.Linq;

namespace FoodTraceability.ArchitectureTests;

public sealed class ApiPackageArchitectureTests
{
    [Fact]
    public void ApiDoesNotReferenceMicrosoftOpenApi()
    {
        var projectPath = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "FoodTraceability.Api",
            "FoodTraceability.Api.csproj");
        var packageReferences = XDocument.Load(projectPath)
            .Descendants()
            .Where(element => element.Name.LocalName == "PackageReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>();

        Assert.DoesNotContain(
            packageReferences,
            package => string.Equals(
                package,
                "Microsoft.AspNetCore.OpenApi",
                StringComparison.OrdinalIgnoreCase));
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
