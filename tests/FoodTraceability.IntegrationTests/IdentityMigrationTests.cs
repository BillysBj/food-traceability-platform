using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Identity.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class IdentityMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task IdentityMigrationAppliesToEmptyDatabase()
    {
        await using var context = database.CreateIdentityDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        await context.Database.MigrateAsync(timeout.Token);
        var appliedMigrations = await context.Database
            .GetAppliedMigrationsAsync(timeout.Token);

        var migration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialIdentity", migration, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdentitySchemaExists()
    {
        const string sql = """
            SELECT nspname
            FROM pg_catalog.pg_namespace
            WHERE nspname = 'identity';
            """;

        var schemas = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(IdentityDbContext.Schema, Assert.Single(schemas));
    }

    [Fact]
    public async Task UserTableExistsWithExpectedColumns()
    {
        const string sql = """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name = 'user'
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
                new DatabaseColumn("user_id", "NO", "uuid", null),
                new DatabaseColumn("email", "NO", "character varying", EmailAddress.MaximumLength),
                new DatabaseColumn("first_name", "NO", "character varying", User.MaximumNameLength),
                new DatabaseColumn("last_name", "NO", "character varying", User.MaximumNameLength),
                new DatabaseColumn("is_active", "NO", "boolean", null),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
                new DatabaseColumn("updated_at", "NO", "timestamp with time zone", null),
            ],
            columns);
    }

    [Fact]
    public async Task UserEmailHasUniqueConstraint()
    {
        const string sql = """
            SELECT DISTINCT index_definition.indisunique
            FROM pg_catalog.pg_class AS table_definition
            JOIN pg_catalog.pg_namespace AS schema_definition
              ON schema_definition.oid = table_definition.relnamespace
            JOIN pg_catalog.pg_index AS index_definition
              ON index_definition.indrelid = table_definition.oid
            JOIN pg_catalog.pg_attribute AS column_definition
              ON column_definition.attrelid = table_definition.oid
             AND column_definition.attnum = ANY(index_definition.indkey)
            WHERE schema_definition.nspname = 'identity'
              AND table_definition.relname = 'user'
              AND column_definition.attname = 'email';
            """;

        var uniqueFlags = await QueryAsync(sql, static reader => reader.GetBoolean(0));

        Assert.True(Assert.Single(uniqueFlags));
    }

    [Fact]
    public async Task IdentityMigrationsHistoryLivesInIdentitySchema()
    {
        const string sql = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_name = '__ef_migrations_history'
            ORDER BY table_schema;
            """;

        var schemas = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(IdentityDbContext.Schema, Assert.Single(schemas));
    }

    [Fact]
    public async Task IdentityMigrationCreatesNoOtherTables()
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'identity'
            ORDER BY table_name;
            """;

        var tables = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [PersistenceConventions.MigrationsHistoryTableName, "user"],
            tables);
    }

    [Fact]
    public async Task UserRoundTripsThroughDatabase()
    {
        var createdAt = new DateTimeOffset(2026, 9, 1, 8, 30, 0, TimeSpan.Zero)
            .AddMilliseconds(123);
        var updatedAt = createdAt.AddHours(2);
        var user = User.Create(
            Guid.NewGuid(),
            EmailAddress.Create($"roundtrip-{Guid.NewGuid():N}@example.com"),
            "Eleni",
            "Papadopoulou",
            createdAt);
        user.Deactivate(updatedAt);

        await using (var writeContext = database.CreateIdentityDbContext())
        {
            writeContext.Users.Add(user);
            await writeContext.SaveChangesAsync();
        }

        await using var readContext = database.CreateIdentityDbContext();
        var reloaded = await readContext.Users
            .AsNoTracking()
            .SingleAsync(candidate => candidate.Id == user.Id);

        Assert.Equal(user.Id, reloaded.Id);
        Assert.Equal(user.Email, reloaded.Email);
        Assert.Equal(user.FirstName, reloaded.FirstName);
        Assert.Equal(user.LastName, reloaded.LastName);
        Assert.Equal(user.IsActive, reloaded.IsActive);
        Assert.Equal(user.CreatedAt, reloaded.CreatedAt);
        Assert.Equal(user.UpdatedAt, reloaded.UpdatedAt);
    }

    [Fact]
    public async Task DuplicateEmailIsRejectedByDatabase()
    {
        var email = EmailAddress.Create($"duplicate-{Guid.NewGuid():N}@example.com");
        var createdAt = new DateTimeOffset(2026, 9, 1, 9, 0, 0, TimeSpan.Zero);

        await using var context = database.CreateIdentityDbContext();
        context.Users.Add(User.Create(Guid.NewGuid(), email, "Nikos", "Dimitriou", createdAt));
        await context.SaveChangesAsync();

        context.Users.Add(User.Create(Guid.NewGuid(), email, "Maria", "Georgiou", createdAt));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.IdentityConnectionString);
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
        int? MaximumLength);
}
