namespace FoodTraceability.Modules.Identity.Domain;

public sealed class RolePermission
{
    private RolePermission(Guid roleId, Guid permissionId)
    {
        RoleId = roleId;
        PermissionId = permissionId;
    }

    public Guid RoleId { get; }

    public Guid PermissionId { get; }

    public static RolePermission Create(Guid roleId, Guid permissionId)
    {
        if (roleId == Guid.Empty)
        {
            throw new IdentityDomainException("Role id must not be empty.");
        }

        if (permissionId == Guid.Empty)
        {
            throw new IdentityDomainException("Permission id must not be empty.");
        }

        return new RolePermission(roleId, permissionId);
    }
}
