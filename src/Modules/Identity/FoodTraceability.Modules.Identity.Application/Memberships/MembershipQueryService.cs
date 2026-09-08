namespace FoodTraceability.Modules.Identity.Application.Memberships;

public sealed class MembershipQueryService(IMembershipReader reader)
{
    public Task<MembershipDetails?> FindAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (organizationId == Guid.Empty || userId == Guid.Empty)
        {
            return Task.FromResult<MembershipDetails?>(null);
        }

        return reader.FindAsync(organizationId, userId, cancellationToken);
    }
}
