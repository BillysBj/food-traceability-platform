namespace FoodTraceability.Modules.Identity.Domain;

public sealed class OrganizationRoleAssignment
{
    private OrganizationRoleAssignment(
        Guid id,
        Guid userId,
        Guid organizationId,
        Guid roleId,
        Guid? locationId,
        DateTimeOffset createdAt)
    {
        Id = id;
        UserId = userId;
        OrganizationId = organizationId;
        RoleId = roleId;
        LocationId = locationId;
        AssignmentScope = RoleAssignmentScope.Organization;
        CreatedAt = createdAt;
    }

    public Guid Id { get; }

    public Guid UserId { get; }

    public Guid OrganizationId { get; }

    public Guid RoleId { get; }

    public Guid? LocationId { get; }

    public RoleAssignmentScope AssignmentScope { get; }

    public DateTimeOffset CreatedAt { get; }

    public static OrganizationRoleAssignment Create(
        Guid id,
        Guid userId,
        Guid organizationId,
        Guid roleId,
        Guid? locationId,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new IdentityDomainException("Organization role assignment id must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("Organization role assignment user id must not be empty.");
        }

        if (organizationId == Guid.Empty)
        {
            throw new IdentityDomainException("Organization role assignment organization id must not be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("Organization role assignment role id must not be empty.");
        }

        if (locationId == Guid.Empty)
        {
            throw new IdentityDomainException("Organization role assignment location id must not be empty.");
        }

        return new OrganizationRoleAssignment(
            id,
            userId,
            organizationId,
            roleId,
            locationId,
            createdAt);
    }
}
