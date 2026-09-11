using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Organizations.Application.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class OrganizationVatIdUniquenessTests(PostgreSqlContainerFixture database)
{
    [Fact]
    public async Task VatIdIndexIsUniqueAndFiltered()
    {
        await using var context = database.CreateOrganizationsDbContext();
        await context.Database.MigrateAsync();
        await using var connection = new NpgsqlConnection(database.OrganizationsConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT i.indisunique, pg_get_expr(i.indpred, i.indrelid),
                   pg_get_indexdef(i.indexrelid, 1, true)
            FROM pg_index i
            JOIN pg_class index_table ON index_table.oid = i.indexrelid
            JOIN pg_class source_table ON source_table.oid = i.indrelid
            JOIN pg_namespace schema ON schema.oid = source_table.relnamespace
            WHERE schema.nspname = 'org'
              AND source_table.relname = 'organization'
              AND index_table.relname = 'ux_organization_vat_id';
            """, connection);
        await using var reader = await command.ExecuteReaderAsync();

        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetBoolean(0));
        Assert.False(reader.IsDBNull(1));
        Assert.Equal("(vat_id IS NOT NULL)", reader.GetString(1));
        Assert.Equal("vat_id", reader.GetString(2));
        Assert.False(await reader.ReadAsync());
    }

    [Fact]
    public Task IdenticalVatIdsAreRejectedByNamedUniqueIndex() =>
        AssertDuplicateVatIdAsync(differentSpelling: false);

    [Fact]
    public Task DifferentlySpelledVatIdsAreRejectedByNamedUniqueIndex() =>
        AssertDuplicateVatIdAsync(differentSpelling: true);

    private async Task AssertDuplicateVatIdAsync(bool differentSpelling)
    {
        var vatId = $"DE{Guid.NewGuid():N}".ToUpperInvariant();
        var first = CreateOrganization(vatId: vatId);
        var second = CreateOrganization(
            vatId: differentSpelling ? $" {vatId.ToLowerInvariant()}- . " : vatId);
        await using var context = database.CreateOrganizationsDbContext();
        await context.Database.MigrateAsync();
        context.Organizations.Add(first);
        await context.SaveChangesAsync();
        context.Organizations.Add(second);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("ux_organization_vat_id", postgresException.ConstraintName);
    }

    [Fact]
    public async Task MultipleOrganizationsWithoutVatIdAreAllowedByD42()
    {
        await AssertBothPersistAsync(CreateOrganization(), CreateOrganization());
    }

    [Fact]
    public async Task WriterDoesNotTranslateOtherUniqueViolationsToVatConflict()
    {
        var organization = CreateOrganization();
        await using (var context = database.CreateOrganizationsDbContext())
        {
            await context.Database.MigrateAsync();
            context.Organizations.Add(organization);
            await context.SaveChangesAsync();
        }

        await using var factory = new ApiWebApplicationFactory(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.OrganizationsConnectionString
        });
        await using var scope = factory.Services.CreateAsyncScope();
        var writer = scope.ServiceProvider.GetRequiredService<IOrganizationWriter>();

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => writer.AddAsync(organization, factory.RequestCancellationToken));
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
        Assert.Equal("pk_organization", postgresException.ConstraintName);
    }

    [Fact]
    public async Task SameTaxNumberIsAllowed()
    {
        var taxNumber = $"tax-{Guid.NewGuid():N}";
        await AssertBothPersistAsync(
            CreateOrganization(vatId: Guid.NewGuid().ToString("N"), taxNumber: taxNumber),
            CreateOrganization(vatId: Guid.NewGuid().ToString("N"), taxNumber: taxNumber));
    }

    [Fact]
    public async Task SameNameIsAllowed()
    {
        var name = $"Same Name {Guid.NewGuid():N}";
        await AssertBothPersistAsync(
            CreateOrganization(name: name, vatId: Guid.NewGuid().ToString("N")),
            CreateOrganization(name: name, vatId: Guid.NewGuid().ToString("N")));
    }

    private async Task AssertBothPersistAsync(Organization first, Organization second)
    {
        await using var context = database.CreateOrganizationsDbContext();
        await context.Database.MigrateAsync();
        context.Organizations.AddRange(first, second);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var organizations = await context.Organizations.AsNoTracking()
            .Where(organization => organization.Id == first.Id || organization.Id == second.Id)
            .ToListAsync();
        Assert.Equal(2, organizations.Count);
        Assert.Contains(organizations, organization => organization.Id == first.Id
            && organization.VatId == first.VatId && organization.Name == first.Name
            && organization.TaxNumber == first.TaxNumber);
        Assert.Contains(organizations, organization => organization.Id == second.Id
            && organization.VatId == second.VatId && organization.Name == second.Name
            && organization.TaxNumber == second.TaxNumber);
    }

    private static Organization CreateOrganization(
        string? name = null, string? vatId = null, string? taxNumber = null) =>
        Organization.Create(
            Guid.NewGuid(), name ?? $"VAT Test {Guid.NewGuid():N}", vatId, taxNumber,
            null, null, new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero));
}
