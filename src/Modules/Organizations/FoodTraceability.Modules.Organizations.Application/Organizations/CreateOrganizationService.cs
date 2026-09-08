using FoodTraceability.Modules.Organizations.Domain;

namespace FoodTraceability.Modules.Organizations.Application.Organizations;

public sealed class CreateOrganizationService(
    IOrganizationWriter writer,
    TimeProvider timeProvider)
{
    public async Task<OrganizationDetails> CreateAsync(
        CreateOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Organization organization;
        try
        {
            organization = Organization.Create(
                Guid.NewGuid(),
                command.Name,
                command.VatId,
                command.TaxNumber,
                command.Email,
                command.Phone,
                timeProvider.GetUtcNow());
        }
        catch (OrganizationsDomainException exception)
        {
            throw new OrganizationValidationException(exception.Message);
        }

        await writer.AddAsync(organization, cancellationToken);

        return new OrganizationDetails(
            organization.Id,
            organization.Name,
            organization.VatId,
            organization.TaxNumber,
            organization.Email,
            organization.Phone,
            organization.CreatedAt,
            organization.UpdatedAt);
    }
}
