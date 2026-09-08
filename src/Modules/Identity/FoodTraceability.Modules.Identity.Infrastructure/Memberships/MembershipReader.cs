using FoodTraceability.Modules.Identity.Application.Memberships;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Identity.Infrastructure.Memberships;

internal sealed class MembershipReader(IdentityDbContext dbContext) : IMembershipReader
{
    public async Task<MembershipDetails?> FindAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var membership = await dbContext.OrganizationMemberships
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.OrganizationId == organizationId
                    && candidate.UserId == userId,
                cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var assignedRoles = await (
                from assignment in dbContext.OrganizationRoleAssignments.AsNoTracking()
                join role in dbContext.Roles.AsNoTracking()
                    on assignment.RoleId equals role.Id
                where assignment.OrganizationId == organizationId
                    && assignment.UserId == userId
                orderby assignment.CreatedAt, assignment.RoleId
                select new
                {
                    assignment.RoleId,
                    role.Code,
                    assignment.CreatedAt,
                })
            .ToArrayAsync(cancellationToken);

        return new MembershipDetails(
            membership.OrganizationId,
            membership.UserId,
            membership.CreatedAt,
            assignedRoles
                .Select(assignment => new AssignedRole(
                    assignment.RoleId,
                    assignment.Code.Value,
                    assignment.CreatedAt))
                .ToArray());
    }
}
