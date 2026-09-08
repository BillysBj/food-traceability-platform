using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Memberships;

public interface IMembershipReader
{
    Task<MembershipDetails?> FindAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken);
}

public interface IMembershipWriter
{
    Task AddMemberAsync(
        OrganizationMembership membership,
        CancellationToken cancellationToken);

    Task AssignRoleAsync(
        OrganizationRoleAssignment assignment,
        CancellationToken cancellationToken);
}
