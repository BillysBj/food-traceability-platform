using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

public sealed class AuthorizationScopeGuardTests
{
    private const string OrganizationRoutePrefix =
        "/api/v1/organizations/{organizationId}";
    private const string OrganizationRequirementTypeName =
        "OrganizationPermissionRequirement";
    private const string PlatformRoutePrefix = "/api/v1/platform/";
    private const string PlatformRequirementTypeName = "PlatformPermissionRequirement";

    [Fact]
    public async Task PlatformRoutesUsePlatformRequirementsOnly()
    {
        var violations = await FindScopeViolationsAsync(
            PlatformRoutePrefix,
            PlatformRequirementTypeName);

        Assert.Empty(violations);
    }

    [Fact]
    public async Task OrganizationRoutesUseOrganizationRequirementsOnly()
    {
        var violations = await FindScopeViolationsAsync(
            OrganizationRoutePrefix,
            OrganizationRequirementTypeName);

        Assert.Empty(violations);
    }

    private static async Task<IReadOnlyList<string>> FindScopeViolationsAsync(
        string routePrefix,
        string expectedRequirementTypeName)
    {
        await using var factory = new ApiWebApplicationFactory();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var violations = new List<string>();
        var endpoints = EndpointAuthorizationDiscovery
            .Discover()
            .Where(endpoint => endpoint.Route.StartsWith(
                routePrefix,
                StringComparison.Ordinal));

        foreach (var endpoint in endpoints)
        {
            if (endpoint.PolicyName is null)
            {
                violations.Add($"{endpoint} has no authorization policy.");
                continue;
            }

            var policy = await provider.GetPolicyAsync(endpoint.PolicyName);
            if (policy is null)
            {
                violations.Add($"{endpoint} references an unknown policy.");
                continue;
            }

            var businessRequirementTypes = policy.Requirements
                .Where(static requirement =>
                    requirement is not DenyAnonymousAuthorizationRequirement)
                .Select(static requirement => requirement.GetType().Name)
                .ToArray();
            if (businessRequirementTypes.Length != 1
                || !string.Equals(
                    businessRequirementTypes[0],
                    expectedRequirementTypeName,
                    StringComparison.Ordinal))
            {
                violations.Add(
                    $"{endpoint} has requirements [{string.Join(", ", businessRequirementTypes)}] "
                    + $"instead of exactly one {expectedRequirementTypeName}.");
            }
        }

        return violations;
    }
}
