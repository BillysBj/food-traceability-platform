using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class QualityMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task QualityMigrationAppliesToEmptyDatabase()
    {
        // The fixture creates a fresh database and migrates Organizations, Catalog, then Quality.
        await using var context = database.CreateQualityDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        await context.Database.MigrateAsync(timeout.Token);
        var migrations = await context.Database.GetAppliedMigrationsAsync(timeout.Token);

        Assert.EndsWith("_InitialQuality", Assert.Single(migrations), StringComparison.Ordinal);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task QualitySchemaExistsWithOnlyParameterAndMigrationHistory()
    {
        var tables = await QueryAsync(
            """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'quality'
            ORDER BY table_name;
            """,
            static reader => reader.GetString(0));

        Assert.Equal([PersistenceConventions.MigrationsHistoryTableName, "parameter"], tables);
    }

    [Fact]
    public async Task ParameterHasExactlyFourExpectedColumns()
    {
        var columns = await QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'quality' AND table_name = 'parameter'
            ORDER BY column_name;
            """,
            static reader => new DatabaseColumn(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));

        Assert.Equal(
            [
                new DatabaseColumn("code", "NO", "character varying", ParameterCode.MaximumLength),
                new DatabaseColumn("parameter_id", "NO", "uuid", null),
                new DatabaseColumn("standard_method", "NO", "character varying", Parameter.MaximumStandardMethodLength),
                new DatabaseColumn("unit_id", "YES", "uuid", null),
            ],
            columns);
    }

    [Fact]
    public async Task QualityMigrationHistoryIsStoredOnlyInQualitySchema()
    {
        var histories = await QueryAsync(
            """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_name = '__ef_migrations_history'
            ORDER BY table_schema;
            """,
            static reader => reader.GetString(0));
        Assert.Equal(["catalog", "org", "quality"], histories);

        var migrationCounts = await QueryAsync(
            "SELECT COUNT(*) FROM quality.__ef_migrations_history;",
            static reader => reader.GetInt64(0));
        Assert.Equal(1L, Assert.Single(migrationCounts));
    }

    [Fact]
    public async Task NoParametersAreSeeded()
    {
        await using var context = database.CreateQualityDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        Assert.Empty(await context.Parameters.AsNoTracking().ToListAsync(timeout.Token));
    }

    [Fact]
    public async Task CodeHasANormalUniqueIndex()
    {
        var definitions = await QueryAsync(
            """
            SELECT indexdef FROM pg_indexes
            WHERE schemaname = 'quality' AND tablename = 'parameter'
              AND indexname = 'ix_parameter_code';
            """,
            static reader => reader.GetString(0));

        var definition = Assert.Single(definitions);
        Assert.Contains("CREATE UNIQUE INDEX", definition, StringComparison.Ordinal);
        Assert.Contains("(code)", definition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateCodeIsRejectedByExpectedIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.Parameters.Add(CreateParameter("INDICATOR_01"));
        await context.SaveChangesAsync(timeout.Token);
        context.Parameters.Add(CreateParameter("  indicator_01  "));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, "ix_parameter_code");
    }

    [Fact]
    public async Task ParameterWithUnknownUnitIsRejected()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.Parameters.Add(CreateParameter("UNKNOWN_UNIT", Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, "fk_parameter_catalog_unit");
    }

    [Fact]
    public async Task ParameterWithoutUnitIsAcceptedAndRoundTrips()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = CreateParameter("  indicator_01  ");
        context.Parameters.Add(parameter);

        Assert.Equal(1, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();
        var stored = await context.Parameters.SingleAsync(p => p.Id == parameter.Id, timeout.Token);

        Assert.Null(stored.UnitId);
        Assert.Equal(ParameterCode.Create("INDICATOR_01"), stored.Code);
        Assert.Equal("ISO 660", stored.StandardMethod);
    }

    [Fact]
    public async Task ReferencedCatalogUnitCannotBeDeleted()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var catalogContext = database.CreateQualityCatalogDbContext();
        var unitId = await catalogContext.Units.Select(unit => unit.Id).FirstAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = CreateParameter("REFERENCED_UNIT", unitId);
        context.Parameters.Add(parameter);
        Assert.Equal(1, await context.SaveChangesAsync(timeout.Token));

        // Direct SQL is limited to this integrity test, using the same rollback transaction.
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM catalog.unit WHERE unit_id = {unitId}", timeout.Token));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Equal("fk_parameter_catalog_unit", exception.ConstraintName);
    }

    [Fact]
    public void QualityModelContainsOnlyParameterAndNoCrossModuleForeignKey()
    {
        using var context = database.CreateQualityDbContext();

        var entityType = Assert.Single(context.Model.GetEntityTypes());
        Assert.Equal(typeof(Parameter), entityType.ClrType);
        Assert.Empty(entityType.GetForeignKeys());
        Assert.Empty(entityType.GetNavigations());
    }

    private static Parameter CreateParameter(string code, Guid? unitId = null)
    {
        return Parameter.Create(Guid.NewGuid(), ParameterCode.Create(code), unitId, "ISO 660");
    }

    private static void AssertConstraint(DbUpdateException exception, string sqlState, string name)
    {
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgresException.SqlState);
        Assert.Equal(name, postgresException.ConstraintName);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.QualityConnectionString);
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

    private sealed record DatabaseColumn(string Name, string IsNullable, string DataType, int? MaximumLength);
}
