using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class OrganizationMembershipMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 1, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task RoleHasAssignmentScopeColumnWithCheckConstraint()
    {
        var columns = await QueryColumnsAsync("role");
        var scopeColumn = Assert.Single(columns, column => column.Name == "assignment_scope");

        Assert.Equal("NO", scopeColumn.IsNullable);
        Assert.Equal("character varying", scopeColumn.DataType);
        Assert.Equal(RoleAssignmentScopeCodes.MaximumLength, scopeColumn.MaximumLength);

        var checks = await QueryCheckConstraintsAsync("role");
        var check = Assert.Single(checks);
        Assert.Equal("ck_role_assignment_scope", check.Name);
        Assert.Contains(RoleAssignmentScopeCodes.Platform, check.Definition, StringComparison.Ordinal);
        Assert.Contains(RoleAssignmentScopeCodes.Organization, check.Definition, StringComparison.Ordinal);

        var uniqueConstraints = await QueryConstraintColumnsAsync("role", "u");
        Assert.Contains(
            uniqueConstraints,
            constraint => constraint.Columns.SequenceEqual(
                ["role_id", "assignment_scope"],
                StringComparer.Ordinal));
    }

    [Fact]
    public async Task OnlyPlatformAdminHasPlatformScope()
    {
        const string sql = """
            SELECT code, assignment_scope
            FROM identity.role
            ORDER BY code;
            """;

        var roles = await QueryAsync(
            sql,
            static reader => new RoleScope(reader.GetString(0), reader.GetString(1)));

        Assert.Equal(10, roles.Count);
        Assert.Equal(
            ["PLATFORM_ADMIN"],
            roles.Where(role => role.Scope == RoleAssignmentScopeCodes.Platform)
                .Select(role => role.Code));
        Assert.Equal(
            9,
            roles.Count(role => role.Scope == RoleAssignmentScopeCodes.Organization));
        Assert.DoesNotContain(
            roles,
            role => role.Scope is not RoleAssignmentScopeCodes.Platform
                and not RoleAssignmentScopeCodes.Organization);
    }

    [Fact]
    public async Task NewTablesExistWithExpectedColumns()
    {
        Assert.Equal(
            [
                new DatabaseColumn("user_id", "NO", "uuid", null),
                new DatabaseColumn("organization_id", "NO", "uuid", null),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            await QueryColumnsAsync("organization_membership"));

        Assert.Equal(
            [
                new DatabaseColumn("organization_role_assignment_id", "NO", "uuid", null),
                new DatabaseColumn("user_id", "NO", "uuid", null),
                new DatabaseColumn("organization_id", "NO", "uuid", null),
                new DatabaseColumn("role_id", "NO", "uuid", null),
                new DatabaseColumn("location_id", "YES", "uuid", null),
                new DatabaseColumn(
                    "assignment_scope",
                    "NO",
                    "character varying",
                    RoleAssignmentScopeCodes.MaximumLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            await QueryColumnsAsync("organization_role_assignment"));

        var platformColumns = await QueryColumnsAsync("platform_role_assignment");
        Assert.Equal(
            [
                new DatabaseColumn("user_id", "NO", "uuid", null),
                new DatabaseColumn("role_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "assignment_scope",
                    "NO",
                    "character varying",
                    RoleAssignmentScopeCodes.MaximumLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            platformColumns);
        Assert.DoesNotContain(platformColumns, column => column.Name is "organization_id" or "location_id");

        var organizationChecks = await QueryCheckConstraintsAsync("organization_role_assignment");
        var organizationCheck = Assert.Single(organizationChecks);
        Assert.Equal("ck_organization_role_assignment_assignment_scope", organizationCheck.Name);
        Assert.Contains(RoleAssignmentScopeCodes.Organization, organizationCheck.Definition, StringComparison.Ordinal);

        var platformChecks = await QueryCheckConstraintsAsync("platform_role_assignment");
        var platformCheck = Assert.Single(platformChecks);
        Assert.Equal("ck_platform_role_assignment_assignment_scope", platformCheck.Name);
        Assert.Contains(RoleAssignmentScopeCodes.Platform, platformCheck.Definition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MembershipHasCompositePrimaryKeyAndForeignKeys()
    {
        var primaryKeys = await QueryConstraintColumnsAsync("organization_membership", "p");
        Assert.Equal(
            ["user_id", "organization_id"],
            Assert.Single(primaryKeys).Columns);

        var foreignKeys = await QueryForeignKeysAsync("organization_membership");
        Assert.Equal(2, foreignKeys.Count);
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.SourceColumns.SequenceEqual(["user_id"], StringComparer.Ordinal)
                && foreignKey.TargetSchema == "identity"
                && foreignKey.TargetTable == "user"
                && foreignKey.TargetColumns.SequenceEqual(["user_id"], StringComparer.Ordinal)
                && foreignKey.DeleteRule == "RESTRICT");
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.SourceColumns.SequenceEqual(["organization_id"], StringComparer.Ordinal)
                && foreignKey.TargetSchema == "org"
                && foreignKey.TargetTable == "organization"
                && foreignKey.TargetColumns.SequenceEqual(["organization_id"], StringComparer.Ordinal)
                && foreignKey.DeleteRule == "RESTRICT");
    }

    [Fact]
    public async Task OrganizationRoleAssignmentHasRequiredForeignKeys()
    {
        var foreignKeys = await QueryForeignKeysAsync("organization_role_assignment");

        Assert.Equal(3, foreignKeys.Count);
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.SourceColumns.SequenceEqual(
                    ["user_id", "organization_id"],
                    StringComparer.Ordinal)
                && foreignKey.TargetSchema == "identity"
                && foreignKey.TargetTable == "organization_membership"
                && foreignKey.TargetColumns.SequenceEqual(
                    ["user_id", "organization_id"],
                    StringComparer.Ordinal)
                && foreignKey.DeleteRule == "RESTRICT");
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.SourceColumns.SequenceEqual(
                    ["role_id", "assignment_scope"],
                    StringComparer.Ordinal)
                && foreignKey.TargetSchema == "identity"
                && foreignKey.TargetTable == "role"
                && foreignKey.TargetColumns.SequenceEqual(
                    ["role_id", "assignment_scope"],
                    StringComparer.Ordinal)
                && foreignKey.DeleteRule == "RESTRICT");
        Assert.Contains(
            foreignKeys,
            foreignKey => foreignKey.SourceColumns.SequenceEqual(
                    ["location_id", "organization_id"],
                    StringComparer.Ordinal)
                && foreignKey.TargetSchema == "org"
                && foreignKey.TargetTable == "location"
                && foreignKey.TargetColumns.SequenceEqual(
                    ["location_id", "organization_id"],
                    StringComparer.Ordinal)
                && foreignKey.DeleteRule == "RESTRICT");
    }

    [Fact]
    public async Task AssignmentWithoutMembershipIsRejected()
    {
        var testData = await CreateTestDataAsync(createMembership: false);
        await using var context = database.CreateIdentityDbContext();
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            testData.UserId,
            testData.OrganizationId,
            StandardRoleIds.Producer,
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task PlatformRoleCannotBeAssignedToOrganization()
    {
        var testData = await CreateTestDataAsync(createMembership: true);
        await using var context = database.CreateIdentityDbContext();
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            testData.UserId,
            testData.OrganizationId,
            StandardRoleIds.PlatformAdmin,
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task OrganizationRoleCannotBeAssignedPlatformWide()
    {
        var testData = await CreateTestDataAsync(createMembership: false);
        await using var context = database.CreateIdentityDbContext();
        context.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(
            testData.UserId,
            StandardRoleIds.Producer,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task LocationFromDifferentOrganizationIsRejected()
    {
        var testData = await CreateTestDataAsync(createMembership: true);
        var otherOrganizationId = Guid.NewGuid();
        var otherLocationId = Guid.NewGuid();

        await using (var organizationsContext = database.CreateIdentityOrganizationsDbContext())
        {
            organizationsContext.Organizations.Add(CreateOrganization(otherOrganizationId));
            organizationsContext.Locations.Add(CreateLocation(otherLocationId, otherOrganizationId, "Other location"));
            await organizationsContext.SaveChangesAsync();
        }

        await using var context = database.CreateIdentityDbContext();
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            testData.UserId,
            testData.OrganizationId,
            StandardRoleIds.Producer,
            otherLocationId,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task DuplicateOrganizationWideAssignmentIsRejected()
    {
        var testData = await CreateTestDataAsync(createMembership: true);

        await using (var firstContext = database.CreateIdentityDbContext())
        {
            firstContext.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
                Guid.NewGuid(),
                testData.UserId,
                testData.OrganizationId,
                StandardRoleIds.Producer,
                null,
                CreatedAt));
            await firstContext.SaveChangesAsync();
        }

        await using var duplicateContext = database.CreateIdentityDbContext();
        duplicateContext.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            testData.UserId,
            testData.OrganizationId,
            StandardRoleIds.Producer,
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => duplicateContext.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameRoleAtDifferentLocationsIsAllowed()
    {
        var firstLocationId = Guid.NewGuid();
        var secondLocationId = Guid.NewGuid();
        var testData = await CreateTestDataAsync(
            createMembership: true,
            firstLocationId,
            secondLocationId);

        await using var context = database.CreateIdentityDbContext();
        context.OrganizationRoleAssignments.AddRange(
            OrganizationRoleAssignment.Create(
                Guid.NewGuid(),
                testData.UserId,
                testData.OrganizationId,
                StandardRoleIds.Producer,
                firstLocationId,
                CreatedAt),
            OrganizationRoleAssignment.Create(
                Guid.NewGuid(),
                testData.UserId,
                testData.OrganizationId,
                StandardRoleIds.Producer,
                secondLocationId,
                CreatedAt));

        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(2, affectedRows);
    }

    [Fact]
    public async Task CrossSchemaForeignKeysExist()
    {
        var membershipForeignKeys = await QueryForeignKeysAsync("organization_membership");
        var assignmentForeignKeys = await QueryForeignKeysAsync("organization_role_assignment");
        var crossSchemaForeignKeys = membershipForeignKeys
            .Concat(assignmentForeignKeys)
            .Where(foreignKey => foreignKey.TargetSchema == "org")
            .ToArray();

        Assert.Equal(2, crossSchemaForeignKeys.Length);
        Assert.Contains(
            crossSchemaForeignKeys,
            foreignKey => foreignKey.TargetTable == "organization"
                && foreignKey.SourceColumns.SequenceEqual(["organization_id"], StringComparer.Ordinal));
        Assert.Contains(
            crossSchemaForeignKeys,
            foreignKey => foreignKey.TargetTable == "location"
                && foreignKey.SourceColumns.SequenceEqual(
                    ["location_id", "organization_id"],
                    StringComparer.Ordinal));
        Assert.All(crossSchemaForeignKeys, foreignKey => Assert.Equal("RESTRICT", foreignKey.DeleteRule));
    }

    private async Task<TestData> CreateTestDataAsync(
        bool createMembership,
        params Guid[] locationIds)
    {
        var userId = Guid.NewGuid();
        var organizationId = Guid.NewGuid();

        await using (var identityContext = database.CreateIdentityDbContext())
        {
            identityContext.Users.Add(User.Create(
                userId,
                EmailAddress.Create($"membership-{userId:N}@example.com"),
                "Test",
                "User",
                CreatedAt));
            await identityContext.SaveChangesAsync();
        }

        await using (var organizationsContext = database.CreateIdentityOrganizationsDbContext())
        {
            organizationsContext.Organizations.Add(CreateOrganization(organizationId));
            foreach (var locationId in locationIds)
            {
                organizationsContext.Locations.Add(CreateLocation(
                    locationId,
                    organizationId,
                    $"Location {locationId:N}"));
            }

            await organizationsContext.SaveChangesAsync();
        }

        if (createMembership)
        {
            await using var identityContext = database.CreateIdentityDbContext();
            identityContext.OrganizationMemberships.Add(OrganizationMembership.Create(
                userId,
                organizationId,
                CreatedAt));
            await identityContext.SaveChangesAsync();
        }

        return new TestData(userId, organizationId);
    }

    private static Organization CreateOrganization(Guid organizationId)
    {
        return Organization.Create(
            organizationId,
            $"Organization {organizationId:N}",
            null,
            null,
            null,
            null,
            CreatedAt);
    }

    private static Location CreateLocation(Guid locationId, Guid organizationId, string name)
    {
        return Location.Create(
            locationId,
            organizationId,
            name,
            null,
            null,
            null,
            null,
            null,
            CreatedAt);
    }

    private Task<IReadOnlyList<DatabaseColumn>> QueryColumnsAsync(string tableName)
    {
        var sql = $"""
            SELECT column_name, is_nullable, data_type, character_maximum_length
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
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));
    }

    private Task<IReadOnlyList<CheckConstraint>> QueryCheckConstraintsAsync(string tableName)
    {
        var sql = $"""
            SELECT constraint_definition.conname,
                   pg_get_constraintdef(constraint_definition.oid)
            FROM pg_catalog.pg_constraint AS constraint_definition
            JOIN pg_catalog.pg_class AS table_definition
              ON table_definition.oid = constraint_definition.conrelid
            JOIN pg_catalog.pg_namespace AS schema_definition
              ON schema_definition.oid = table_definition.relnamespace
            WHERE schema_definition.nspname = 'identity'
              AND table_definition.relname = '{tableName}'
              AND constraint_definition.contype = 'c'
            ORDER BY constraint_definition.conname;
            """;

        return QueryAsync(
            sql,
            static reader => new CheckConstraint(reader.GetString(0), reader.GetString(1)));
    }

    private async Task<IReadOnlyList<ConstraintColumns>> QueryConstraintColumnsAsync(
        string tableName,
        string constraintType)
    {
        var sql = $"""
            SELECT constraint_definition.conname,
                   column_definition.attname,
                   key_column.ordinal_position
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
            WHERE schema_definition.nspname = 'identity'
              AND table_definition.relname = '{tableName}'
              AND constraint_definition.contype = '{constraintType}'
            ORDER BY constraint_definition.conname, key_column.ordinal_position;
            """;

        var rows = await QueryAsync(
            sql,
            static reader => new ConstraintColumnRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt64(2)));

        return rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .Select(group => new ConstraintColumns(
                group.Key,
                group.OrderBy(row => row.OrdinalPosition)
                    .Select(row => row.Column)
                    .ToArray()))
            .ToArray();
    }

    private async Task<IReadOnlyList<ForeignKey>> QueryForeignKeysAsync(string tableName)
    {
        var sql = $"""
            SELECT foreign_key.conname,
                   source_column.attname,
                   target_schema.nspname,
                   target_table.relname,
                   target_column.attname,
                   key_pair.ordinal_position,
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
            WHERE source_schema.nspname = 'identity'
              AND source_table.relname = '{tableName}'
              AND foreign_key.contype = 'f'
            ORDER BY foreign_key.conname, key_pair.ordinal_position;
            """;

        var rows = await QueryAsync(
            sql,
            static reader => new ForeignKeyRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt64(5),
                reader.GetString(6)));

        return rows
            .GroupBy(row => row.Name, StringComparer.Ordinal)
            .Select(group =>
            {
                var orderedRows = group.OrderBy(row => row.OrdinalPosition).ToArray();
                var first = orderedRows[0];
                return new ForeignKey(
                    group.Key,
                    orderedRows.Select(row => row.SourceColumn).ToArray(),
                    first.TargetSchema,
                    first.TargetTable,
                    orderedRows.Select(row => row.TargetColumn).ToArray(),
                    first.DeleteRule);
            })
            .ToArray();
    }

    private async Task AssertDatabaseErrorAsync(Func<Task> action, string expectedSqlState)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);

        Assert.Equal(expectedSqlState, postgresException.SqlState);
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

    private sealed record RoleScope(string Code, string Scope);

    private sealed record CheckConstraint(string Name, string Definition);

    private sealed record ConstraintColumnRow(string Name, string Column, long OrdinalPosition);

    private sealed record ConstraintColumns(string Name, string[] Columns);

    private sealed record ForeignKeyRow(
        string Name,
        string SourceColumn,
        string TargetSchema,
        string TargetTable,
        string TargetColumn,
        long OrdinalPosition,
        string DeleteRule);

    private sealed record ForeignKey(
        string Name,
        string[] SourceColumns,
        string TargetSchema,
        string TargetTable,
        string[] TargetColumns,
        string DeleteRule);

    private sealed record TestData(Guid UserId, Guid OrganizationId);
}
