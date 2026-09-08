using System.Reflection;
using FoodTraceability.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

public sealed class AuthorizationPolicyGuardTests
{
    private static readonly IReadOnlyDictionary<string, ExpectedPolicy> ExpectedPolicies =
        new Dictionary<string, ExpectedPolicy>(StringComparer.Ordinal)
        {
            ["ActiveUser"] = new(
                "ActiveUser",
                "ActiveUserRequirement",
                null),
            ["OrganizationRead"] = new(
                "OrganizationRead",
                "OrganizationPermissionRequirement",
                "organization.read"),
            ["OrganizationManage"] = new(
                "OrganizationManage",
                "OrganizationPermissionRequirement",
                "organization.manage"),
            ["ArticleRead"] = new(
                "ArticleRead",
                "OrganizationPermissionRequirement",
                "article.read"),
            ["ArticleCreate"] = new(
                "ArticleCreate",
                "OrganizationPermissionRequirement",
                "article.create"),
            ["LotRead"] = new(
                "LotRead",
                "OrganizationPermissionRequirement",
                "lot.read"),
            ["LotCreate"] = new(
                "LotCreate",
                "OrganizationPermissionRequirement",
                "lot.create"),
            ["PlatformOrganizationManage"] = new(
                "PlatformOrganizationManage",
                "PlatformPermissionRequirement",
                "organization.manage"),
            ["PlatformUserRead"] = new(
                "PlatformUserRead",
                "PlatformPermissionRequirement",
                "user.read"),
            ["PlatformUserManage"] = new(
                "PlatformUserManage",
                "PlatformPermissionRequirement",
                "user.manage"),
            ["PlatformProductRead"] = new(
                "PlatformProductRead",
                "PlatformPermissionRequirement",
                "product.read"),
            ["PlatformProductCreate"] = new(
                "PlatformProductCreate",
                "PlatformPermissionRequirement",
                "product.create")
        };

    [Fact]
    public async Task EveryPolicyRequiresAnAuthenticatedUser()
    {
        await using var factory = new ApiWebApplicationFactory();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var expected in ExpectedPolicies.Values)
        {
            var policy = await GetRequiredPolicyAsync(provider, expected.PolicyName);

            Assert.Contains(
                policy.Requirements,
                static requirement => requirement is DenyAnonymousAuthorizationRequirement);
        }
    }

    [Fact]
    public async Task EveryPolicyHasTheExpectedRequirementTypeAndPermissionCode()
    {
        await using var factory = new ApiWebApplicationFactory();
        var provider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        foreach (var expected in ExpectedPolicies.Values)
        {
            var policy = await GetRequiredPolicyAsync(provider, expected.PolicyName);
            var businessRequirements = policy.Requirements
                .Where(static requirement =>
                    requirement is not DenyAnonymousAuthorizationRequirement)
                .ToArray();
            var requirement = Assert.Single(businessRequirements);

            Assert.Equal(expected.RequirementTypeName, requirement.GetType().Name);

            var permissionProperty = requirement.GetType().GetProperty(
                "PermissionCode",
                BindingFlags.Instance | BindingFlags.Public);
            var permissionCode = permissionProperty?.GetValue(requirement) as string;
            Assert.Equal(expected.PermissionCode, permissionCode);
        }
    }

    [Fact]
    public void EveryDeclaredPolicyConstantIsCovered()
    {
        var expectedConstants = ExpectedPolicies
            .Select(static pair => new PolicyConstant(pair.Key, pair.Value.PolicyName));
        var actualConstants = typeof(AuthorizationPolicies)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static field =>
                field.IsLiteral
                && !field.IsInitOnly
                && field.FieldType == typeof(string))
            .Select(static field => new PolicyConstant(
                field.Name,
                (string)field.GetRawConstantValue()!));

        PolicyConstantSetAssert.Equal(expectedConstants, actualConstants);
    }

    private static async Task<AuthorizationPolicy> GetRequiredPolicyAsync(
        IAuthorizationPolicyProvider provider,
        string policyName)
    {
        var policy = await provider.GetPolicyAsync(policyName);
        return Assert.IsType<AuthorizationPolicy>(policy);
    }

    private sealed record ExpectedPolicy(
        string PolicyName,
        string RequirementTypeName,
        string? PermissionCode);

    private sealed record PolicyConstant(string Name, string Value)
    {
        public override string ToString() => $"{Name} = {Value}";
    }

    private static class PolicyConstantSetAssert
    {
        public static void Equal(
            IEnumerable<PolicyConstant> expected,
            IEnumerable<PolicyConstant> actual)
        {
            var expectedSet = expected.ToHashSet();
            var actualSet = actual.ToHashSet();
            var missing = expectedSet
                .Except(actualSet)
                .OrderBy(static policy => policy.Name, StringComparer.Ordinal)
                .ToArray();
            var unexpected = actualSet
                .Except(expectedSet)
                .OrderBy(static policy => policy.Name, StringComparer.Ordinal)
                .ToArray();

            Assert.True(
                missing.Length == 0 && unexpected.Length == 0,
                $"Missing policy constants:{Environment.NewLine}{Format(missing)}{Environment.NewLine}"
                + $"Unexpected policy constants:{Environment.NewLine}{Format(unexpected)}");
        }

        private static string Format(IReadOnlyCollection<PolicyConstant> policies) =>
            policies.Count == 0
                ? "  <none>"
                : string.Join(Environment.NewLine, policies.Select(static policy => $"  {policy}"));
    }
}
