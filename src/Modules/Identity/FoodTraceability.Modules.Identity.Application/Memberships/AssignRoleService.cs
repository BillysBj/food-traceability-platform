using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Memberships;

public sealed class AssignRoleService(
    IMembershipWriter writer,
    TimeProvider timeProvider)
{
    public async Task AssignAsync(
        AssignRoleCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        OrganizationRoleAssignment assignment;
        try
        {
            assignment = OrganizationRoleAssignment.Create(
                Guid.NewGuid(),
                command.UserId,
                command.OrganizationId,
                command.RoleId,
                locationId: null,
                timeProvider.GetUtcNow());
        }
        catch (IdentityDomainException exception)
        {
            throw new MembershipValidationException(exception.Message);
        }

        await writer.AssignRoleAsync(assignment, cancellationToken);
    }
}
