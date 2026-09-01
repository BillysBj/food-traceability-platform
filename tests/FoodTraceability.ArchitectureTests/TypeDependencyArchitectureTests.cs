using System.Reflection;
using NetArchTest.Rules;

namespace FoodTraceability.ArchitectureTests;

public sealed class TypeDependencyArchitectureTests
{
    private static readonly string[] ForbiddenFrameworkNamespaces =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore"
    ];

    private static readonly Assembly[] ModuleAssemblies = LoadModuleAssemblies();

    [Fact]
    public void DomainTypesDoNotDependOnInfrastructureFrameworks()
    {
        var domainAssemblies = GetLayerAssemblies("Domain");

        Assert.Equal(10, domainAssemblies.Length);
        AssertRuleSucceeds(
            Types.InAssemblies(domainAssemblies)
                .That()
                .ResideInNamespaceMatching(@"^FoodTraceability\.Modules\..*\.Domain(?:\..*)?$")
                .ShouldNot()
                .HaveDependencyOnAny(ForbiddenFrameworkNamespaces)
                .GetResult());
    }

    [Fact]
    public void ApplicationTypesDoNotDependOnInfrastructureFrameworks()
    {
        var applicationAssemblies = GetLayerAssemblies("Application");

        Assert.Equal(10, applicationAssemblies.Length);
        AssertRuleSucceeds(
            Types.InAssemblies(applicationAssemblies)
                .That()
                .ResideInNamespaceMatching(@"^FoodTraceability\.Modules\..*\.Application(?:\..*)?$")
                .ShouldNot()
                .HaveDependencyOnAny(ForbiddenFrameworkNamespaces)
                .GetResult());
    }

    [Fact]
    public void ModuleTypesDoNotDependOnOtherModules()
    {
        var moduleGroups = ModuleAssemblies
            .GroupBy(assembly => GetModuleNamespace(assembly.GetName().Name!))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(10, moduleGroups.Length);
        Assert.Equal(30, ModuleAssemblies.Length);

        foreach (var moduleGroup in moduleGroups)
        {
            var otherModuleNamespaces = moduleGroups
                .Where(otherGroup => !string.Equals(
                    otherGroup.Key,
                    moduleGroup.Key,
                    StringComparison.Ordinal))
                .Select(otherGroup => otherGroup.Key)
                .ToArray();

            AssertRuleSucceeds(
                Types.InAssemblies(moduleGroup)
                    .ShouldNot()
                    .HaveDependencyOnAny(otherModuleNamespaces)
                    .GetResult());
        }
    }

    private static Assembly[] GetLayerAssemblies(string layer)
    {
        return ModuleAssemblies
            .Where(assembly => assembly.GetName().Name!.EndsWith(
                $".{layer}",
                StringComparison.Ordinal))
            .ToArray();
    }

    private static string GetModuleNamespace(string assemblyName)
    {
        var lastSeparator = assemblyName.LastIndexOf('.');
        return assemblyName[..lastSeparator];
    }

    private static Assembly[] LoadModuleAssemblies()
    {
        return Directory
            .EnumerateFiles(
                AppContext.BaseDirectory,
                "FoodTraceability.Modules.*.dll",
                SearchOption.TopDirectoryOnly)
            .Select(Assembly.LoadFrom)
            .OrderBy(assembly => assembly.GetName().Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AssertRuleSucceeds(TestResult result)
    {
        if (result.IsSuccessful)
        {
            return;
        }

        var failingTypeNames = result.FailingTypes is null
            ? "details unavailable"
            : string.Join(", ", result.FailingTypes.Select(type => type.FullName ?? type.Name));

        Assert.True(
            result.IsSuccessful,
            $"Architecture rule violations: {failingTypeNames}");
    }
}
