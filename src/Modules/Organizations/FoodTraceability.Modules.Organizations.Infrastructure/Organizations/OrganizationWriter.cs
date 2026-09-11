using FoodTraceability.Modules.Organizations.Application.Organizations;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Organizations.Infrastructure.Organizations;

internal sealed class OrganizationWriter(OrganizationsDbContext dbContext) : IOrganizationWriter
{
    private const string VatIdUniqueIndex = "ux_organization_vat_id";

    public async Task AddAsync(
        Organization organization,
        CancellationToken cancellationToken)
    {
        dbContext.Organizations.Add(organization);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException postgresException
                && postgresException.SqlState == PostgresErrorCodes.UniqueViolation
                && postgresException.ConstraintName == VatIdUniqueIndex)
        {
            throw new OrganizationConflictException(
                "An organization with the same VAT id already exists.");
        }
    }
}
