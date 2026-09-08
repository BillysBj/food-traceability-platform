using FoodTraceability.Modules.Identity.Application.Authorization;

namespace FoodTraceability.UnitTests;

public sealed class EffectiveAuthorizationServiceTests
{
    [Fact]
    public async Task EmptyUserIdDoesNotReachStore()
    {
        var store = new StubEffectiveAuthorizationStore(null);
        var service = new EffectiveAuthorizationService(store);

        var result = await service.ResolveAsync(Guid.Empty, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task ResolutionIsDelegatedForEveryCallWithoutCaching()
    {
        var userId = Guid.NewGuid();
        var authorization = new EffectiveAuthorization(
            userId,
            "user@example.com",
            "Test",
            "User",
            true,
            [],
            [],
            []);
        var store = new StubEffectiveAuthorizationStore(authorization);
        var service = new EffectiveAuthorizationService(store);

        var first = await service.ResolveAsync(userId, CancellationToken.None);
        var second = await service.ResolveAsync(userId, CancellationToken.None);

        Assert.Same(authorization, first);
        Assert.Same(authorization, second);
        Assert.Equal(2, store.CallCount);
    }

    [Fact]
    public void OrganizationPermissionDoesNotUsePlatformOrLocationPermissions()
    {
        var organizationId = Guid.NewGuid();
        var authorization = new EffectiveAuthorization(
            Guid.NewGuid(),
            "user@example.com",
            "Test",
            "User",
            true,
            ["organization.read"],
            [new OrganizationPermissionSet(organizationId, [])],
            [new LocationPermissionSet(
                organizationId,
                Guid.NewGuid(),
                ["organization.read"])]);

        var result = authorization.HasOrganizationPermission(
            organizationId,
            "organization.read");

        Assert.False(result);
        Assert.True(authorization.HasOrganizationMembership(organizationId));
    }

    private sealed class StubEffectiveAuthorizationStore(EffectiveAuthorization? result)
        : IEffectiveAuthorizationStore
    {
        public int CallCount { get; private set; }

        public Task<EffectiveAuthorization?> ResolveAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }
}

public sealed class EffectiveAuthorizationTests
{
    [Fact]
    public void HasPlatformPermissionFindsPlatformPermission()
    {
        var authorization = CreateAuthorization(
            ["organization.manage"],
            []);

        Assert.True(authorization.HasPlatformPermission("organization.manage"));
    }

    [Fact]
    public void HasPlatformPermissionDoesNotUseOrganizationPermissions()
    {
        var organizationId = Guid.NewGuid();
        var authorization = CreateAuthorization(
            [],
            [new OrganizationPermissionSet(
                organizationId,
                ["organization.manage"])]);

        Assert.False(authorization.HasPlatformPermission("organization.manage"));
        Assert.True(authorization.HasOrganizationPermission(
            organizationId,
            "organization.manage"));
    }

    [Fact]
    public void HasPlatformPermissionUsesCaseSensitiveComparison()
    {
        var authorization = CreateAuthorization(
            ["organization.manage"],
            []);

        Assert.False(authorization.HasPlatformPermission("ORGANIZATION.MANAGE"));
    }

    [Fact]
    public void HasOrganizationPermissionDoesNotUseAnotherOrganization()
    {
        var assignedOrganizationId = Guid.NewGuid();
        var requestedOrganizationId = Guid.NewGuid();
        var authorization = CreateAuthorization(
            [],
            [new OrganizationPermissionSet(
                assignedOrganizationId,
                ["organization.read"])]);

        Assert.False(authorization.HasOrganizationPermission(
            requestedOrganizationId,
            "organization.read"));
        Assert.True(authorization.HasOrganizationPermission(
            assignedOrganizationId,
            "organization.read"));
    }

    private static EffectiveAuthorization CreateAuthorization(
        IReadOnlyList<string> platformPermissions,
        IReadOnlyList<OrganizationPermissionSet> organizationPermissions) =>
        new(
            Guid.NewGuid(),
            "platform-authorization@example.com",
            "Platform",
            "Authorization",
            true,
            platformPermissions,
            organizationPermissions,
            []);
}
