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

        var migrations = appliedMigrations.ToArray();
        Assert.Equal(3, migrations.Length);
        Assert.EndsWith("_InitialIdentity", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_AddRoles", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddPermissions", migrations[2], StringComparison.Ordinal);
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
            [PersistenceConventions.MigrationsHistoryTableName, "permission", "role", "user"],
            tables);
    }

    [Fact]
    public async Task RoleTableExistsWithExpectedColumns()
    {
        const string sql = """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name = 'role'
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
                new DatabaseColumn("role_id", "NO", "uuid", null),
                new DatabaseColumn("code", "NO", "character varying", RoleCode.MaximumLength),
                new DatabaseColumn("name", "NO", "character varying", Role.MaximumNameLength),
                new DatabaseColumn(
                    "description",
                    "YES",
                    "character varying",
                    Role.MaximumDescriptionLength),
            ],
            columns);
    }

    [Fact]
    public async Task RoleCodeAndNameHaveUniqueConstraints()
    {
        const string sql = """
            SELECT column_definition.attname, index_definition.indisunique
            FROM pg_catalog.pg_class AS table_definition
            JOIN pg_catalog.pg_namespace AS schema_definition
              ON schema_definition.oid = table_definition.relnamespace
            JOIN pg_catalog.pg_index AS index_definition
              ON index_definition.indrelid = table_definition.oid
            JOIN pg_catalog.pg_attribute AS column_definition
              ON column_definition.attrelid = table_definition.oid
             AND column_definition.attnum = ANY(index_definition.indkey)
            WHERE schema_definition.nspname = 'identity'
              AND table_definition.relname = 'role'
              AND column_definition.attname IN ('code', 'name')
            ORDER BY column_definition.attname;
            """;

        var indexes = await QueryAsync(
            sql,
            static reader => new UniqueIndex(reader.GetString(0), reader.GetBoolean(1)));

        Assert.Equal(
            [
                new UniqueIndex("code", true),
                new UniqueIndex("name", true),
            ],
            indexes);
    }

    [Fact]
    public async Task TenDefaultRolesAreSeededWithExpectedCodes()
    {
        const string sql = """
            SELECT code
            FROM identity.role
            ORDER BY code;
            """;

        var codes = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [
                "AUDITOR",
                "BOTTLER",
                "LABORATORY",
                "LOGISTICS",
                "ORGANIZATION_ADMIN",
                "PLATFORM_ADMIN",
                "PROCESSOR",
                "PRODUCER",
                "QUALITY_MANAGER",
                "RETAILER",
            ],
            codes);
    }

    [Fact]
    public async Task SeededRoleIdsAreStable()
    {
        const string sql = """
            SELECT code, role_id
            FROM identity.role
            ORDER BY code;
            """;

        var roles = await QueryAsync(
            sql,
            static reader => new SeededRole(reader.GetString(0), reader.GetGuid(1)));

        Assert.Equal(
            [
                new SeededRole("AUDITOR", StandardRoleIds.Auditor),
                new SeededRole("BOTTLER", StandardRoleIds.Bottler),
                new SeededRole("LABORATORY", StandardRoleIds.Laboratory),
                new SeededRole("LOGISTICS", StandardRoleIds.Logistics),
                new SeededRole("ORGANIZATION_ADMIN", StandardRoleIds.OrganizationAdmin),
                new SeededRole("PLATFORM_ADMIN", StandardRoleIds.PlatformAdmin),
                new SeededRole("PROCESSOR", StandardRoleIds.Processor),
                new SeededRole("PRODUCER", StandardRoleIds.Producer),
                new SeededRole("QUALITY_MANAGER", StandardRoleIds.QualityManager),
                new SeededRole("RETAILER", StandardRoleIds.Retailer),
            ],
            roles);
    }

    [Fact]
    public async Task DuplicateRoleCodeIsRejectedByDatabase()
    {
        await using var context = database.CreateIdentityDbContext();
        context.Roles.Add(Role.Create(
            Guid.Parse("b51e253e-fbb0-4a33-a8f4-791d0ebc50f1"),
            RoleCode.Create("PRODUCER"),
            "DuplicateProducer"));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
    }

    [Fact]
    public async Task PermissionTableExistsWithExpectedColumns()
    {
        const string sql = """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'identity'
              AND table_name = 'permission'
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
                new DatabaseColumn("permission_id", "NO", "uuid", null),
                new DatabaseColumn("code", "NO", "character varying", PermissionCode.MaximumLength),
                new DatabaseColumn(
                    "description",
                    "YES",
                    "character varying",
                    Permission.MaximumDescriptionLength),
            ],
            columns);
    }

    [Fact]
    public async Task PermissionCodeHasUniqueConstraint()
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
              AND table_definition.relname = 'permission'
              AND column_definition.attname = 'code';
            """;

        var uniqueFlags = await QueryAsync(sql, static reader => reader.GetBoolean(0));

        Assert.True(Assert.Single(uniqueFlags));
    }

    [Fact]
    public async Task AllTwentySixPermissionsAreSeededWithExpectedCodes()
    {
        const string sql = """
            SELECT code
            FROM identity.permission
            ORDER BY code;
            """;

        var codes = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [
                "audit.read",
                "delivery.create",
                "delivery.read",
                "document.read",
                "document.upload",
                "lot.create",
                "lot.read",
                "lot.update",
                "organization.manage",
                "organization.read",
                "permission.read",
                "product.create",
                "product.read",
                "product.update",
                "quality.block",
                "quality.read",
                "quality.release",
                "quality.result.create",
                "quality.sample.create",
                "role.read",
                "trace.event.create",
                "trace.read",
                "transport.create",
                "transport.read",
                "user.manage",
                "user.read",
            ],
            codes);
        Assert.All(codes, code => Assert.Equal(code.ToLowerInvariant(), code));
    }

    [Fact]
    public async Task SeededPermissionIdsAreStable()
    {
        const string sql = """
            SELECT code, permission_id
            FROM identity.permission
            ORDER BY code;
            """;

        var permissions = await QueryAsync(
            sql,
            static reader => new SeededPermission(reader.GetString(0), reader.GetGuid(1)));

        Assert.Equal(
            [
                new SeededPermission("audit.read", StandardPermissionIds.AuditRead),
                new SeededPermission("delivery.create", StandardPermissionIds.DeliveryCreate),
                new SeededPermission("delivery.read", StandardPermissionIds.DeliveryRead),
                new SeededPermission("document.read", StandardPermissionIds.DocumentRead),
                new SeededPermission("document.upload", StandardPermissionIds.DocumentUpload),
                new SeededPermission("lot.create", StandardPermissionIds.LotCreate),
                new SeededPermission("lot.read", StandardPermissionIds.LotRead),
                new SeededPermission("lot.update", StandardPermissionIds.LotUpdate),
                new SeededPermission("organization.manage", StandardPermissionIds.OrganizationManage),
                new SeededPermission("organization.read", StandardPermissionIds.OrganizationRead),
                new SeededPermission("permission.read", StandardPermissionIds.PermissionRead),
                new SeededPermission("product.create", StandardPermissionIds.ProductCreate),
                new SeededPermission("product.read", StandardPermissionIds.ProductRead),
                new SeededPermission("product.update", StandardPermissionIds.ProductUpdate),
                new SeededPermission("quality.block", StandardPermissionIds.QualityBlock),
                new SeededPermission("quality.read", StandardPermissionIds.QualityRead),
                new SeededPermission("quality.release", StandardPermissionIds.QualityRelease),
                new SeededPermission("quality.result.create", StandardPermissionIds.QualityResultCreate),
                new SeededPermission("quality.sample.create", StandardPermissionIds.QualitySampleCreate),
                new SeededPermission("role.read", StandardPermissionIds.RoleRead),
                new SeededPermission("trace.event.create", StandardPermissionIds.TraceEventCreate),
                new SeededPermission("trace.read", StandardPermissionIds.TraceRead),
                new SeededPermission("transport.create", StandardPermissionIds.TransportCreate),
                new SeededPermission("transport.read", StandardPermissionIds.TransportRead),
                new SeededPermission("user.manage", StandardPermissionIds.UserManage),
                new SeededPermission("user.read", StandardPermissionIds.UserRead),
            ],
            permissions);
    }

    [Fact]
    public async Task DuplicatePermissionCodeIsRejectedByDatabase()
    {
        await using var context = database.CreateIdentityDbContext();
        context.Permissions.Add(Permission.Create(
            Guid.Parse("a2410543-a52c-4a4f-a0b3-c462aba3094f"),
            PermissionCode.Create("lot.read")));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync());

        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgresException.SqlState);
    }

    [Fact]
    public async Task IdentityMigrationsCreateNoUnexpectedTables()
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'identity'
            ORDER BY table_name;
            """;

        var tables = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [PersistenceConventions.MigrationsHistoryTableName, "permission", "role", "user"],
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

    private sealed record UniqueIndex(string ColumnName, bool IsUnique);

    private sealed record SeededRole(string Code, Guid Id);

    private sealed record SeededPermission(string Code, Guid Id);
}
