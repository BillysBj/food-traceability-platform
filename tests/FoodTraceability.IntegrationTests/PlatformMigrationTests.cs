using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class PlatformMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task MigrationAppliesToEmptyDatabase()
    {
        await using var context = database.CreateDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        var appliedMigrations = await context.Database
            .GetAppliedMigrationsAsync(timeout.Token);

        Assert.Equal(["20260831193936_InitialPlatformCollations"], appliedMigrations);
    }

    [Fact]
    public async Task MigrationsHistoryTableExistsInPublicSchema()
    {
        const string sql = """
            SELECT table_schema, table_name
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name = '__ef_migrations_history';
            """;

        var tables = await QueryAsync(
            sql,
            static reader => (Schema: reader.GetString(0), Name: reader.GetString(1)));

        var table = Assert.Single(tables);
        Assert.Equal(PlatformDbContext.MigrationsHistorySchema, table.Schema);
        Assert.Equal(PersistenceConventions.MigrationsHistoryTableName, table.Name);
    }

    [Fact]
    public async Task OnlyTheMigrationsHistoryTableExists()
    {
        const string sql = """
            SELECT schemaname, tablename
            FROM pg_catalog.pg_tables
            WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
              AND schemaname NOT LIKE 'pg_toast%'
            ORDER BY schemaname, tablename;
            """;

        var tables = await QueryAsync(
            sql,
            static reader => (Schema: reader.GetString(0), Name: reader.GetString(1)));

        var table = Assert.Single(tables);
        Assert.Equal(PlatformDbContext.MigrationsHistorySchema, table.Schema);
        Assert.Equal(PersistenceConventions.MigrationsHistoryTableName, table.Name);
    }

    [Fact]
    public async Task IcuCollationsExistWithIcuProvider()
    {
        const string sql = """
            SELECT collname, collprovider::text
            FROM pg_catalog.pg_collation
            WHERE collnamespace = 'public'::regnamespace
              AND collname IN ('en', 'el')
            ORDER BY collname;
            """;

        var collations = await QueryAsync(
            sql,
            static reader => (Name: reader.GetString(0), Provider: reader.GetString(1)));

        Assert.Equal(["el", "en"], collations.Select(collation => collation.Name));
        Assert.All(collations, collation => Assert.Equal("i", collation.Provider));
    }

    [Fact]
    public async Task GreekCollationOrdersDifferentlyThanC()
    {
        const string sql = """
            SELECT
                string_agg(word, ',' ORDER BY word COLLATE "el"),
                string_agg(word, ',' ORDER BY word COLLATE "C")
            FROM (VALUES ('αετός'), ('άλογο')) AS greek_words(word);
            """;

        var orderings = await QueryAsync(
            sql,
            static reader => (Greek: reader.GetString(0), Ordinal: reader.GetString(1)));

        var ordering = Assert.Single(orderings);
        Assert.NotEqual(ordering.Ordinal, ordering.Greek);
    }

    [Fact]
    public async Task NoBusinessSchemaExists()
    {
        const string sql = """
            SELECT nspname
            FROM pg_catalog.pg_namespace
            WHERE nspname = ANY (ARRAY[
                'identity', 'org', 'catalog', 'trace', 'production', 'quality',
                'logistics', 'asset', 'docs', 'certification', 'recall',
                'publictrace', 'integration', 'audit', 'ai', 'olive', 'dairy',
                'livestock', 'meat', 'seafood', 'produce'
            ])
            ORDER BY nspname;
            """;

        var schemas = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Empty(schemas);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
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
}
