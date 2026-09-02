using FoodTraceability.Modules.Identity.Application.Authorization;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authorization;

internal sealed class EffectiveAuthorizationStore(IdentityDbContext dbContext)
    : IEffectiveAuthorizationStore
{
    public async Task<EffectiveAuthorization?> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        if (!user.IsActive)
        {
            return new EffectiveAuthorization(
                user.Id,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                false,
                [],
                [],
                []);
        }

        var membershipOrganizationIds = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .Where(membership => membership.UserId == userId)
            .Select(membership => membership.OrganizationId)
            .OrderBy(organizationId => organizationId)
            .ToArrayAsync(cancellationToken);

        var platformPermissionCodes = await (
            from assignment in dbContext.PlatformRoleAssignments.AsNoTracking()
            join rolePermission in dbContext.RolePermissions.AsNoTracking()
                on assignment.RoleId equals rolePermission.RoleId
            join permission in dbContext.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where assignment.UserId == userId
            select permission.Code)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        var organizationPermissionRows = await (
            from assignment in dbContext.OrganizationRoleAssignments.AsNoTracking()
            join rolePermission in dbContext.RolePermissions.AsNoTracking()
                on assignment.RoleId equals rolePermission.RoleId
            join permission in dbContext.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where assignment.UserId == userId && assignment.LocationId == null
            select new
            {
                assignment.OrganizationId,
                PermissionCode = permission.Code,
            })
            .ToArrayAsync(cancellationToken);

        var locationPermissionRows = await (
            from assignment in dbContext.OrganizationRoleAssignments.AsNoTracking()
            join rolePermission in dbContext.RolePermissions.AsNoTracking()
                on assignment.RoleId equals rolePermission.RoleId
            join permission in dbContext.Permissions.AsNoTracking()
                on rolePermission.PermissionId equals permission.Id
            where assignment.UserId == userId && assignment.LocationId != null
            select new
            {
                assignment.OrganizationId,
                LocationId = assignment.LocationId!.Value,
                PermissionCode = permission.Code,
            })
            .ToArrayAsync(cancellationToken);

        var organizationPermissions = membershipOrganizationIds
            .Select(organizationId => new OrganizationPermissionSet(
                organizationId,
                organizationPermissionRows
                    .Where(row => row.OrganizationId == organizationId)
                    .Select(row => row.PermissionCode.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();

        var locationPermissions = locationPermissionRows
            .GroupBy(row => new { row.OrganizationId, row.LocationId })
            .OrderBy(group => group.Key.OrganizationId)
            .ThenBy(group => group.Key.LocationId)
            .Select(group => new LocationPermissionSet(
                group.Key.OrganizationId,
                group.Key.LocationId,
                group.Select(row => row.PermissionCode.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();

        return new EffectiveAuthorization(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.IsActive,
            platformPermissionCodes
                .Select(code => code.Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            organizationPermissions,
            locationPermissions);
    }
}
