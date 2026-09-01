using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class RoleAssignmentTests
{
    private static readonly Guid UserId = Guid.Parse("b07a8411-43f1-4c82-8ec0-b7675d7e16f3");
    private static readonly Guid OrganizationId = Guid.Parse("78b350df-4665-40c4-a970-21110a2529fe");
    private static readonly Guid RoleId = Guid.Parse("89893c4e-dc9f-4f85-997f-19a5c65aef2b");
    private static readonly Guid LocationId = Guid.Parse("05126dd6-eed8-4894-b028-cf35f64ac7fb");
    private static readonly Guid AssignmentId = Guid.Parse("d477595b-bd01-4aa4-835f-5bd40e90613b");
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 1, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ValidOrganizationMembershipIsCreated()
    {
        var membership = OrganizationMembership.Create(UserId, OrganizationId, CreatedAt);

        Assert.Equal(UserId, membership.UserId);
        Assert.Equal(OrganizationId, membership.OrganizationId);
        Assert.Equal(CreatedAt, membership.CreatedAt);
    }

    [Fact]
    public void EmptyIdsOnMembershipAreRejected()
    {
        Assert.Throws<IdentityDomainException>(() => OrganizationMembership.Create(
            Guid.Empty,
            OrganizationId,
            CreatedAt));
        Assert.Throws<IdentityDomainException>(() => OrganizationMembership.Create(
            UserId,
            Guid.Empty,
            CreatedAt));
    }

    [Fact]
    public void ValidOrganizationRoleAssignmentIsCreated()
    {
        var organizationWide = OrganizationRoleAssignment.Create(
            AssignmentId,
            UserId,
            OrganizationId,
            RoleId,
            null,
            CreatedAt);
        var locationSpecific = OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            UserId,
            OrganizationId,
            RoleId,
            LocationId,
            CreatedAt);

        Assert.Equal(AssignmentId, organizationWide.Id);
        Assert.Equal(UserId, organizationWide.UserId);
        Assert.Equal(OrganizationId, organizationWide.OrganizationId);
        Assert.Equal(RoleId, organizationWide.RoleId);
        Assert.Null(organizationWide.LocationId);
        Assert.Equal(RoleAssignmentScope.Organization, organizationWide.AssignmentScope);
        Assert.Equal(CreatedAt, organizationWide.CreatedAt);
        Assert.Equal(LocationId, locationSpecific.LocationId);
        Assert.Equal(RoleAssignmentScope.Organization, locationSpecific.AssignmentScope);
    }

    [Fact]
    public void EmptyLocationIdOnAssignmentIsRejected()
    {
        Assert.Throws<IdentityDomainException>(() => OrganizationRoleAssignment.Create(
            AssignmentId,
            UserId,
            OrganizationId,
            RoleId,
            Guid.Empty,
            CreatedAt));
    }

    [Fact]
    public void ValidPlatformRoleAssignmentIsCreated()
    {
        var assignment = PlatformRoleAssignment.Create(UserId, RoleId, CreatedAt);

        Assert.Equal(UserId, assignment.UserId);
        Assert.Equal(RoleId, assignment.RoleId);
        Assert.Equal(RoleAssignmentScope.Platform, assignment.AssignmentScope);
        Assert.Equal(CreatedAt, assignment.CreatedAt);
    }

    [Fact]
    public void EmptyRequiredIdsOnRoleAssignmentsAreRejected()
    {
        Assert.Throws<IdentityDomainException>(() => OrganizationRoleAssignment.Create(
            Guid.Empty,
            UserId,
            OrganizationId,
            RoleId,
            null,
            CreatedAt));
        Assert.Throws<IdentityDomainException>(() => OrganizationRoleAssignment.Create(
            AssignmentId,
            Guid.Empty,
            OrganizationId,
            RoleId,
            null,
            CreatedAt));
        Assert.Throws<IdentityDomainException>(() => OrganizationRoleAssignment.Create(
            AssignmentId,
            UserId,
            Guid.Empty,
            RoleId,
            null,
            CreatedAt));
        Assert.Throws<IdentityDomainException>(() => OrganizationRoleAssignment.Create(
            AssignmentId,
            UserId,
            OrganizationId,
            Guid.Empty,
            null,
            CreatedAt));
        Assert.Throws<IdentityDomainException>(() => PlatformRoleAssignment.Create(
            Guid.Empty,
            RoleId,
            CreatedAt));
        Assert.Throws<IdentityDomainException>(() => PlatformRoleAssignment.Create(
            UserId,
            Guid.Empty,
            CreatedAt));
    }
}
