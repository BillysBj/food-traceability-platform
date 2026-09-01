using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class CredentialPersistenceMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 9, 1, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TablesHaveExpectedColumns()
    {
        Assert.Equal(
            [
                new DatabaseColumn("user_id", "NO", "uuid"),
                new DatabaseColumn("password_hash", "NO", "text"),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone"),
                new DatabaseColumn("updated_at", "NO", "timestamp with time zone"),
            ],
            await QueryColumnsAsync("user_credential"));

        Assert.Equal(
            [
                new DatabaseColumn("id", "NO", "uuid"),
                new DatabaseColumn("user_id", "NO", "uuid"),
                new DatabaseColumn("session_id", "NO", "uuid"),
                new DatabaseColumn("token_hash", "NO", "text"),
                new DatabaseColumn("issued_at", "NO", "timestamp with time zone"),
                new DatabaseColumn("expires_at", "NO", "timestamp with time zone"),
                new DatabaseColumn("revoked_at", "YES", "timestamp with time zone"),
            ],
            await QueryColumnsAsync("refresh_token"));
    }

    [Fact]
    public async Task SecondCredentialForSameUserIsRejected()
    {
        var userId = await CreateUserAsync();

        await using (var firstContext = database.CreateIdentityDbContext())
        {
            firstContext.UserCredentials.Add(UserCredential.Create(
                userId,
                "first-password-hash",
                IssuedAt,
                IssuedAt));
            await firstContext.SaveChangesAsync();
        }

        await using var duplicateContext = database.CreateIdentityDbContext();
        duplicateContext.UserCredentials.Add(UserCredential.Create(
            userId,
            "second-password-hash",
            IssuedAt,
            IssuedAt));

        await AssertDatabaseErrorAsync(
            () => duplicateContext.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task DuplicateTokenHashIsRejected()
    {
        var userId = await CreateUserAsync();
        var tokenHash = $"duplicate-token-hash-{Guid.NewGuid():N}";

        await using (var firstContext = database.CreateIdentityDbContext())
        {
            firstContext.RefreshTokens.Add(CreateRefreshToken(userId, tokenHash));
            await firstContext.SaveChangesAsync();
        }

        await using var duplicateContext = database.CreateIdentityDbContext();
        duplicateContext.RefreshTokens.Add(CreateRefreshToken(userId, tokenHash));

        await AssertDatabaseErrorAsync(
            () => duplicateContext.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ExpirationAtOrBeforeIssueTimeIsRejected()
    {
        var userId = await CreateUserAsync();

        foreach (var expiresAt in new[] { IssuedAt, IssuedAt.AddTicks(-1) })
        {
            var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
                """
                INSERT INTO identity.refresh_token
                    (id, user_id, session_id, token_hash, issued_at, expires_at, revoked_at)
                VALUES
                    (@id, @user_id, @session_id, @token_hash, @issued_at, @expires_at, NULL);
                """,
                new NpgsqlParameter("id", Guid.NewGuid()),
                new NpgsqlParameter("user_id", userId),
                new NpgsqlParameter("session_id", Guid.NewGuid()),
                new NpgsqlParameter("token_hash", $"invalid-expiration-{Guid.NewGuid():N}"),
                new NpgsqlParameter("issued_at", IssuedAt),
                new NpgsqlParameter("expires_at", expiresAt)));

            Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
            Assert.Equal("ck_refresh_token_expires_after_issued", exception.ConstraintName);
        }
    }

    [Fact]
    public async Task RevocationBeforeIssueTimeIsRejected()
    {
        var userId = await CreateUserAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(
            """
            INSERT INTO identity.refresh_token
                (id, user_id, session_id, token_hash, issued_at, expires_at, revoked_at)
            VALUES
                (@id, @user_id, @session_id, @token_hash, @issued_at, @expires_at, @revoked_at);
            """,
            new NpgsqlParameter("id", Guid.NewGuid()),
            new NpgsqlParameter("user_id", userId),
            new NpgsqlParameter("session_id", Guid.NewGuid()),
            new NpgsqlParameter("token_hash", $"invalid-revocation-{Guid.NewGuid():N}"),
            new NpgsqlParameter("issued_at", IssuedAt),
            new NpgsqlParameter("expires_at", IssuedAt.AddDays(7)),
            new NpgsqlParameter("revoked_at", IssuedAt.AddTicks(-1))));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_refresh_token_revoked_not_before_issued", exception.ConstraintName);
    }

    [Fact]
    public async Task DeletingUserCascadesToCredentialAndRefreshTokens()
    {
        var userId = await CreateUserAsync();

        await using (var writeContext = database.CreateIdentityDbContext())
        {
            writeContext.UserCredentials.Add(UserCredential.Create(
                userId,
                "password-hash",
                IssuedAt,
                IssuedAt));
            writeContext.RefreshTokens.AddRange(
                CreateRefreshToken(userId, $"token-hash-{Guid.NewGuid():N}"),
                CreateRefreshToken(userId, $"token-hash-{Guid.NewGuid():N}"));
            await writeContext.SaveChangesAsync();
        }

        await using (var deleteContext = database.CreateIdentityDbContext())
        {
            var user = await deleteContext.Users.SingleAsync(candidate => candidate.Id == userId);
            deleteContext.Users.Remove(user);
            await deleteContext.SaveChangesAsync();
        }

        var counts = await QueryAsync(
            """
            SELECT
                (SELECT COUNT(*) FROM identity.user_credential WHERE user_id = @user_id),
                (SELECT COUNT(*) FROM identity.refresh_token WHERE user_id = @user_id);
            """,
            static reader => new DependentCounts(reader.GetInt64(0), reader.GetInt64(1)),
            new NpgsqlParameter("user_id", userId));
        var dependentCounts = Assert.Single(counts);

        Assert.Equal(0, dependentCounts.Credentials);
        Assert.Equal(0, dependentCounts.RefreshTokens);
    }

    [Fact]
    public async Task AuthenticationTablesHaveNoOrganizationIdColumn()
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name IN ('user_credential', 'refresh_token')
              AND column_name = 'organization_id'
            ORDER BY table_name;
            """;

        var tables = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Empty(tables);
    }

    [Fact]
    public async Task RefreshTokenHasExpectedIndexes()
    {
        const string sql = """
            SELECT indexname, indexdef
            FROM pg_catalog.pg_indexes
            WHERE schemaname = 'identity'
              AND tablename = 'refresh_token'
            ORDER BY indexname;
            """;

        var indexes = await QueryAsync(
            sql,
            static reader => new DatabaseIndex(reader.GetString(0), reader.GetString(1)));

        Assert.Contains(indexes, index => index.Name == "ix_refresh_token_user_id");
        Assert.Contains(indexes, index => index.Name == "ix_refresh_token_session_id");
        Assert.Contains(
            indexes,
            index => index.Name == "ix_refresh_token_token_hash"
                && index.Definition.Contains("UNIQUE", StringComparison.Ordinal));
    }

    private async Task<Guid> CreateUserAsync()
    {
        var userId = Guid.NewGuid();
        await using var context = database.CreateIdentityDbContext();
        context.Users.Add(User.Create(
            userId,
            EmailAddress.Create($"credential-{userId:N}@example.com"),
            "Test",
            "User",
            IssuedAt));
        await context.SaveChangesAsync();

        return userId;
    }

    private static RefreshToken CreateRefreshToken(Guid userId, string tokenHash)
    {
        return RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            tokenHash,
            IssuedAt,
            IssuedAt.AddDays(7));
    }

    private Task<IReadOnlyList<DatabaseColumn>> QueryColumnsAsync(string tableName)
    {
        var sql = $"""
            SELECT column_name, is_nullable, data_type
            FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name = '{tableName}'
            ORDER BY ordinal_position;
            """;

        return QueryAsync(
            sql,
            static reader => new DatabaseColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2)));
    }

    private async Task AssertDatabaseErrorAsync(Func<Task> action, string expectedSqlState)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(expectedSqlState, postgresException.SqlState);
    }

    private async Task ExecuteAsync(string sql, params NpgsqlParameter[] parameters)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.IdentityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map,
        params NpgsqlParameter[] parameters)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.IdentityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(timeout.Token);
        var rows = new List<T>();

        while (await reader.ReadAsync(timeout.Token))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private sealed record DatabaseColumn(string Name, string IsNullable, string DataType);

    private sealed record DatabaseIndex(string Name, string Definition);

    private sealed record DependentCounts(long Credentials, long RefreshTokens);
}
