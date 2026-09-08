using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Memberships;

public sealed class AddMemberService(
    IMembershipWriter writer,
    TimeProvider timeProvider)
{
    public async Task<MembershipDetails> AddAsync(
        AddMemberCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        OrganizationMembership membership;
        try
        {
            membership = OrganizationMembership.Create(
                command.UserId,
                command.OrganizationId,
                timeProvider.GetUtcNow());
        }
        catch (IdentityDomainException exception)
        {
            throw new MembershipValidationException(exception.Message);
        }

        await writer.AddMemberAsync(membership, cancellationToken);

        return new MembershipDetails(
            membership.OrganizationId,
            membership.UserId,
            membership.CreatedAt,
            []);
    }
}
