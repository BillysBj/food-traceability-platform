namespace FoodTraceability.Modules.Identity.Domain;

public sealed class PlatformRoleAssignment
{
    private PlatformRoleAssignment(Guid userId, Guid roleId, DateTimeOffset createdAt)
    {
        UserId = userId;
        RoleId = roleId;
        AssignmentScope = RoleAssignmentScope.Platform;
        CreatedAt = createdAt;
    }

    public Guid UserId { get; }

    public Guid RoleId { get; }

    public RoleAssignmentScope AssignmentScope { get; }

    public DateTimeOffset CreatedAt { get; }

    public static PlatformRoleAssignment Create(
        Guid userId,
        Guid roleId,
        DateTimeOffset createdAt)
    {
        if (userId == Guid.Empty)
        {
            throw new IdentityDomainException("Platform role assignment user id must not be empty.");
        }

        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("Platform role assignment role id must not be empty.");
        }

        return new PlatformRoleAssignment(userId, roleId, createdAt);
    }
}
