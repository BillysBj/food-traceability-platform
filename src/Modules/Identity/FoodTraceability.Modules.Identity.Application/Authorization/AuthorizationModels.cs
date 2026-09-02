namespace FoodTraceability.Modules.Identity.Application.Authorization;

public sealed record EffectiveAuthorization(
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyList<string> PlatformPermissions,
    IReadOnlyList<OrganizationPermissionSet> OrganizationPermissions,
    IReadOnlyList<LocationPermissionSet> LocationPermissions)
{
    public bool HasOrganizationPermission(Guid organizationId, string permissionCode)
    {
        return OrganizationPermissions.Any(permissionSet =>
            permissionSet.OrganizationId == organizationId
            && permissionSet.Permissions.Contains(permissionCode, StringComparer.Ordinal));
    }

    public bool HasOrganizationMembership(Guid organizationId)
    {
        return OrganizationPermissions.Any(permissionSet =>
            permissionSet.OrganizationId == organizationId);
    }
}

public sealed record OrganizationPermissionSet(
    Guid OrganizationId,
    IReadOnlyList<string> Permissions);

public sealed record LocationPermissionSet(
    Guid OrganizationId,
    Guid LocationId,
    IReadOnlyList<string> Permissions);
