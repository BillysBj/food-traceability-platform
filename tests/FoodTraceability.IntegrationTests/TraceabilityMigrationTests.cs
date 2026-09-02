using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class TraceabilityMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TraceabilityMigrationAppliesToEmptyDatabase()
    {
        await using var context = database.CreateTraceabilityDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        await context.Database.MigrateAsync(timeout.Token);
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(timeout.Token);

        var migration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialTraceability", migration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TraceSchemaExistsWithExpectedTables()
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'trace'
            ORDER BY table_name;
            """;

        var tables = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [
                PersistenceConventions.MigrationsHistoryTableName,
                "lot",
            ],
            tables);
    }

    [Fact]
    public async Task LotTableHasExactlyTheExpectedColumns()
    {
        const string sql = """
            SELECT column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'trace'
              AND table_name = 'lot'
            ORDER BY ordinal_position;
            """;

        var columns = await QueryAsync(
            sql,
            static reader => new DatabaseColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));

        Assert.Equal(
            [
                new DatabaseColumn("lot_id", "NO", "uuid", null),
                new DatabaseColumn("organization_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "lot_number",
                    "NO",
                    "character varying",
                    Lot.MaximumLotNumberLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            columns);
    }

    [Fact]
    public async Task DuplicateLotNumberInSameOrganizationIsRejected()
    {
        var organizationId = await CreateOrganizationAsync();
        await CreateLotAsync(organizationId, "DUPLICATE-LOT");

        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.Add(Lot.Create(
            Guid.NewGuid(),
            organizationId,
            "DUPLICATE-LOT",
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task LotNumberIsCaseInsensitivelyUniqueWithinOrganization()
    {
        var organizationId = await CreateOrganizationAsync();
        await CreateLotAsync(organizationId, "ABC-123");

        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.Add(Lot.Create(
            Guid.NewGuid(),
            organizationId,
            "abc-123",
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameLotNumberInDifferentOrganizationsIsAccepted()
    {
        var firstOrganizationId = await CreateOrganizationAsync();
        var secondOrganizationId = await CreateOrganizationAsync();

        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.AddRange(
            Lot.Create(Guid.NewGuid(), firstOrganizationId, "SHARED-LOT", CreatedAt),
            Lot.Create(Guid.NewGuid(), secondOrganizationId, "SHARED-LOT", CreatedAt));

        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(2, affectedRows);
    }

    [Fact]
    public async Task StoredLotNumberPreservesOriginalCasing()
    {
        var organizationId = await CreateOrganizationAsync();
        var lotId = await CreateLotAsync(organizationId, "ABC-123");

        const string sql = """
            SELECT lot_number
            FROM trace.lot
            WHERE lot_id = @lot_id;
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("lot_id", lotId);

        var storedLotNumber = await command.ExecuteScalarAsync(timeout.Token);

        Assert.Equal("ABC-123", Assert.IsType<string>(storedLotNumber));
    }

    [Fact]
    public async Task LotWithUnknownOrganizationIsRejected()
    {
        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.Add(Lot.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "UNKNOWN-ORGANIZATION",
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task LotHasForeignKeyToOrganizationWithRestrict()
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
            WHERE source_schema.nspname = 'trace'
              AND source_table.relname = 'lot'
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
                "org",
                "organization",
                "organization_id",
                "RESTRICT"),
            Assert.Single(foreignKeys));
    }

    [Fact]
    public async Task OrganizationWithLotCannotBeDeleted()
    {
        var organizationId = await CreateOrganizationAsync();
        var lotId = await CreateLotAsync(organizationId, "RESTRICT-DELETE");

        await using (var context = database.CreateTraceabilityOrganizationsDbContext())
        {
            var organization = await context.Organizations.SingleAsync(
                candidate => candidate.Id == organizationId);
            context.Organizations.Remove(organization);

            await AssertDatabaseErrorAsync(
                () => context.SaveChangesAsync(),
                PostgresErrorCodes.ForeignKeyViolation);
        }

        await using var verificationContext = database.CreateTraceabilityDbContext();
        Assert.True(await verificationContext.Lots.AnyAsync(lot => lot.Id == lotId));
    }

    [Fact]
    public async Task TraceabilityMigrationsHistoryLivesInTraceSchema()
    {
        const string sql = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_schema = 'trace'
              AND table_name = '__ef_migrations_history';
            """;

        var schemas = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(TraceabilityDbContext.Schema, Assert.Single(schemas));
    }

    private async Task<Guid> CreateOrganizationAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Traceability Test Organization {Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            CreatedAt);

        await using var context = database.CreateTraceabilityOrganizationsDbContext();
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        return organization.Id;
    }

    private async Task<Guid> CreateLotAsync(Guid organizationId, string lotNumber)
    {
        var lot = Lot.Create(Guid.NewGuid(), organizationId, lotNumber, CreatedAt);

        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.Add(lot);
        await context.SaveChangesAsync();

        return lot.Id;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
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

    private static async Task AssertDatabaseErrorAsync(
        Func<Task> action,
        string expectedSqlState)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(expectedSqlState, postgresException.SqlState);
    }

    private sealed record DatabaseColumn(
        string Name,
        string IsNullable,
        string DataType,
        int? MaximumLength);

    private sealed record ForeignKey(
        string SourceColumn,
        string TargetSchema,
        string TargetTable,
        string TargetColumn,
        string DeleteRule);
}
