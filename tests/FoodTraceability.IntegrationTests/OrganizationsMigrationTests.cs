using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Organizations.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class OrganizationsMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task OrganizationsMigrationAppliesToEmptyDatabase()
    {
        await using var context = database.CreateOrganizationsDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        await context.Database.MigrateAsync(timeout.Token);
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(timeout.Token);

        var migration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialOrganizations", migration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrgSchemaExistsWithExpectedTables()
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'org'
            ORDER BY table_name;
            """;

        var tables = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [
                PersistenceConventions.MigrationsHistoryTableName,
                "location",
                "organization",
            ],
            tables);
    }

    [Fact]
    public async Task OrganizationTableExistsWithExpectedColumns()
    {
        var columns = await QueryColumnsAsync("organization");

        Assert.Equal(
            [
                new DatabaseColumn("organization_id", "NO", "uuid", null, null, null),
                new DatabaseColumn(
                    "name",
                    "NO",
                    "character varying",
                    Organization.MaximumNameLength,
                    null,
                    null),
                new DatabaseColumn(
                    "vat_id",
                    "YES",
                    "character varying",
                    Organization.MaximumVatIdLength,
                    null,
                    null),
                new DatabaseColumn(
                    "tax_number",
                    "YES",
                    "character varying",
                    Organization.MaximumTaxNumberLength,
                    null,
                    null),
                new DatabaseColumn(
                    "email",
                    "YES",
                    "character varying",
                    Organization.MaximumEmailLength,
                    null,
                    null),
                new DatabaseColumn(
                    "phone",
                    "YES",
                    "character varying",
                    Organization.MaximumPhoneLength,
                    null,
                    null),
                new DatabaseColumn(
                    "created_at",
                    "NO",
                    "timestamp with time zone",
                    null,
                    null,
                    null),
                new DatabaseColumn(
                    "updated_at",
                    "NO",
                    "timestamp with time zone",
                    null,
                    null,
                    null),
            ],
            columns);
    }

    [Fact]
    public async Task LocationTableExistsWithExpectedColumns()
    {
        var columns = await QueryColumnsAsync("location");

        Assert.Equal(
            [
                new DatabaseColumn("location_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("organization_id", "NO", "uuid", null, null, null),
                new DatabaseColumn(
                    "name",
                    "NO",
                    "character varying",
                    Location.MaximumNameLength,
                    null,
                    null),
                new DatabaseColumn(
                    "city",
                    "YES",
                    "character varying",
                    Location.MaximumCityLength,
                    null,
                    null),
                new DatabaseColumn(
                    "region",
                    "YES",
                    "character varying",
                    Location.MaximumRegionLength,
                    null,
                    null),
                new DatabaseColumn(
                    "country_code",
                    "YES",
                    "character",
                    CountryCode.Length,
                    null,
                    null),
                new DatabaseColumn(
                    "latitude",
                    "YES",
                    "numeric",
                    null,
                    9,
                    6),
                new DatabaseColumn(
                    "longitude",
                    "YES",
                    "numeric",
                    null,
                    9,
                    6),
                new DatabaseColumn(
                    "created_at",
                    "NO",
                    "timestamp with time zone",
                    null,
                    null,
                    null),
            ],
            columns);

        Assert.DoesNotContain(columns, column => column.Name is "status" or "type");
    }

    [Fact]
    public async Task LocationHasForeignKeyToOrganizationWithRestrict()
    {
        const string sql = """
            SELECT source_column.attname,
                   target_schema.nspname,
                   target_table.relname,
                   target_column.attname,
                   CASE foreign_key.confdeltype
                       WHEN 'a' THEN 'NO ACTION'
                       WHEN 'r' THEN 'RESTRICT'
                       WHEN 'c' THEN 'CASCADE'
                       WHEN 'n' THEN 'SET NULL'
                       WHEN 'd' THEN 'SET DEFAULT'
                   END
            FROM pg_catalog.pg_constraint AS foreign_key
            JOIN pg_catalog.pg_class AS source_table
              ON source_table.oid = foreign_key.conrelid
            JOIN pg_catalog.pg_namespace AS source_schema
              ON source_schema.oid = source_table.relnamespace
            JOIN pg_catalog.pg_class AS target_table
              ON target_table.oid = foreign_key.confrelid
            JOIN pg_catalog.pg_namespace AS target_schema
              ON target_schema.oid = target_table.relnamespace
            CROSS JOIN LATERAL unnest(foreign_key.conkey, foreign_key.confkey)
              WITH ORDINALITY AS key_pair(source_attnum, target_attnum, ordinal_position)
            JOIN pg_catalog.pg_attribute AS source_column
              ON source_column.attrelid = source_table.oid
             AND source_column.attnum = key_pair.source_attnum
            JOIN pg_catalog.pg_attribute AS target_column
              ON target_column.attrelid = target_table.oid
             AND target_column.attnum = key_pair.target_attnum
            WHERE source_schema.nspname = 'org'
              AND source_table.relname = 'location'
              AND foreign_key.contype = 'f';
            """;

        var foreignKeys = await QueryAsync(
            sql,
            static reader => new ForeignKey(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4)));

        Assert.Equal(
            new ForeignKey(
                "organization_id",
                OrganizationsDbContext.Schema,
                "organization",
                "organization_id",
                "RESTRICT"),
            Assert.Single(foreignKeys));
    }

    [Fact]
    public async Task LocationHasUniqueConstraintOnIdAndOrganizationId()
    {
        const string sql = """
            SELECT column_definition.attname
            FROM pg_catalog.pg_constraint AS constraint_definition
            JOIN pg_catalog.pg_class AS table_definition
              ON table_definition.oid = constraint_definition.conrelid
            JOIN pg_catalog.pg_namespace AS schema_definition
              ON schema_definition.oid = table_definition.relnamespace
            CROSS JOIN LATERAL unnest(constraint_definition.conkey)
              WITH ORDINALITY AS key_column(attnum, ordinal_position)
            JOIN pg_catalog.pg_attribute AS column_definition
              ON column_definition.attrelid = table_definition.oid
             AND column_definition.attnum = key_column.attnum
            WHERE schema_definition.nspname = 'org'
              AND table_definition.relname = 'location'
              AND constraint_definition.contype = 'u'
            ORDER BY key_column.ordinal_position;
            """;

        var columns = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(["location_id", "organization_id"], columns);
    }

    [Fact]
    public async Task OrganizationsMigrationsHistoryLivesInOrgSchema()
    {
        const string sql = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_name = '__ef_migrations_history'
            ORDER BY table_schema;
            """;

        var schemas = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(OrganizationsDbContext.Schema, Assert.Single(schemas));
    }

    [Fact]
    public async Task OrganizationAndLocationRoundTripThroughDatabase()
    {
        var createdAt = new DateTimeOffset(2026, 9, 1, 12, 30, 0, TimeSpan.Zero)
            .AddMilliseconds(123);
        var organization = Organization.Create(
            Guid.NewGuid(),
            "Aegean Foods",
            "EL123456789",
            "TAX-42",
            "contact@example.com",
            "+30 210 123 4567",
            createdAt);
        var location = Location.Create(
            Guid.NewGuid(),
            organization.Id,
            "Athens Warehouse",
            "Athens",
            "Attica",
            CountryCode.Create("gr"),
            37.983810m,
            23.727539m,
            createdAt);

        await using (var writeContext = database.CreateOrganizationsDbContext())
        {
            writeContext.Organizations.Add(organization);
            writeContext.Locations.Add(location);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = database.CreateOrganizationsDbContext();
        var reloadedOrganization = await readContext.Organizations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == organization.Id);
        var reloadedLocation = await readContext.Locations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == location.Id);

        Assert.Equal(organization.Id, reloadedOrganization.Id);
        Assert.Equal(organization.Name, reloadedOrganization.Name);
        Assert.Equal(organization.VatId, reloadedOrganization.VatId);
        Assert.Equal(organization.TaxNumber, reloadedOrganization.TaxNumber);
        Assert.Equal(organization.Email, reloadedOrganization.Email);
        Assert.Equal(organization.Phone, reloadedOrganization.Phone);
        Assert.Equal(organization.CreatedAt, reloadedOrganization.CreatedAt);
        Assert.Equal(organization.UpdatedAt, reloadedOrganization.UpdatedAt);
        Assert.Equal(location.Id, reloadedLocation.Id);
        Assert.Equal(location.OrganizationId, reloadedLocation.OrganizationId);
        Assert.Equal(location.Name, reloadedLocation.Name);
        Assert.Equal(location.City, reloadedLocation.City);
        Assert.Equal(location.Region, reloadedLocation.Region);
        Assert.Equal(location.CountryCode, reloadedLocation.CountryCode);
        Assert.Equal(location.Latitude, reloadedLocation.Latitude);
        Assert.Equal(location.Longitude, reloadedLocation.Longitude);
        Assert.Equal(location.CreatedAt, reloadedLocation.CreatedAt);
    }

    [Fact]
    public async Task LocationWithUnknownOrganizationIsRejectedByDatabase()
    {
        var location = Location.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Unknown Organization Warehouse",
            null,
            null,
            null,
            null,
            null,
            new DateTimeOffset(2026, 9, 1, 13, 0, 0, TimeSpan.Zero));

        await using var context = database.CreateOrganizationsDbContext();
        context.Locations.Add(location);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, postgresException.SqlState);
    }

    private Task<IReadOnlyList<DatabaseColumn>> QueryColumnsAsync(string tableName)
    {
        var sql = $"""
            SELECT column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length,
                   numeric_precision,
                   numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'org'
              AND table_name = '{tableName}'
            ORDER BY ordinal_position;
            """;

        return QueryAsync(
            sql,
            static reader => new DatabaseColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5)));
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.OrganizationsConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(timeout.Token);
        var rows = new List<T>();

        while (await reader.ReadAsync(timeout.Token))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private sealed record DatabaseColumn(
        string Name,
        string IsNullable,
        string DataType,
        int? MaximumLength,
        int? NumericPrecision,
        int? NumericScale);

    private sealed record ForeignKey(
        string SourceColumn,
        string TargetSchema,
        string TargetTable,
        string TargetColumn,
        string DeleteRule);
}
