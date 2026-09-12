using System.Globalization;
using FoodTraceability.Modules.Catalog.Domain;
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
    private const decimal DefaultQuantity = 1000m;

    [Fact]
    public async Task TraceabilityMigrationAppliesToEmptyDatabase()
    {
        await using var context = database.CreateTraceabilityDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        await context.Database.MigrateAsync(timeout.Token);
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(timeout.Token);

        var migrations = appliedMigrations.ToArray();
        Assert.Equal(7, migrations.Length);
        Assert.EndsWith("_InitialTraceability", migrations[0], StringComparison.Ordinal);
        Assert.EndsWith("_AddLotArticleAndQuantity", migrations[1], StringComparison.Ordinal);
        Assert.EndsWith("_AddEventType", migrations[2], StringComparison.Ordinal);
        Assert.EndsWith("_AddTraceabilityEvent", migrations[3], StringComparison.Ordinal);
        Assert.EndsWith("_AddEventTypeClassification", migrations[4], StringComparison.Ordinal);
        Assert.EndsWith("_AddTraceabilityEventIndexes", migrations[5], StringComparison.Ordinal);
        Assert.EndsWith("_AddLotOrganizationAlternateKey", migrations[6], StringComparison.Ordinal);
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
                "event_input",
                "event_output",
                "event_type",
                "lot",
                "traceability_event",
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
            ORDER BY column_name;
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
                new DatabaseColumn("article_id", "NO", "uuid", null),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
                new DatabaseColumn("lot_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "lot_number",
                    "NO",
                    "character varying",
                    Lot.MaximumLotNumberLength),
                new DatabaseColumn("organization_id", "NO", "uuid", null),
                new DatabaseColumn("quantity", "NO", "numeric", null),
                new DatabaseColumn("unit_id", "NO", "uuid", null),
            ],
            columns);
    }

    [Fact]
    public async Task QuantityUsesTheDecidedPrecisionAndScale()
    {
        const string sql = """
            SELECT numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'trace'
              AND table_name = 'lot'
              AND column_name = 'quantity';
            """;

        var precisions = await QueryAsync(
            sql,
            static reader => (Precision: reader.GetInt32(0), Scale: reader.GetInt32(1)));

        Assert.Equal((18, 6), Assert.Single(precisions));
    }

    [Fact]
    public async Task DuplicateLotNumberInSameOrganizationIsRejected()
    {
        var context = await CreateLotContextAsync();
        await CreateLotAsync(context, "DUPLICATE-LOT");

        await AssertLotIsRejectedAsync(
            context.NewLot("DUPLICATE-LOT"),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task LotNumberIsCaseInsensitivelyUniqueWithinOrganization()
    {
        var context = await CreateLotContextAsync();
        await CreateLotAsync(context, "ABC-123");

        await AssertLotIsRejectedAsync(
            context.NewLot("abc-123"),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameLotNumberInDifferentOrganizationsIsAccepted()
    {
        var first = await CreateLotContextAsync();
        var second = await CreateLotContextAsync();

        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.AddRange(first.NewLot("SHARED-LOT"), second.NewLot("SHARED-LOT"));

        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(2, affectedRows);
    }

    [Fact]
    public async Task StoredLotNumberPreservesOriginalCasing()
    {
        var context = await CreateLotContextAsync();
        var lotId = await CreateLotAsync(context, "ABC-123");

        var storedLotNumber = await ScalarAsync(
            "SELECT lot_number FROM trace.lot WHERE lot_id = @lot_id;",
            lotId);

        Assert.Equal("ABC-123", Assert.IsType<string>(storedLotNumber));
    }

    [Fact]
    public async Task LotWithUnknownOrganizationIsRejected()
    {
        var context = await CreateLotContextAsync();

        await AssertLotIsRejectedAsync(
            context.WithOrganization(Guid.NewGuid()).NewLot("UNKNOWN-ORGANIZATION"),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task LotWithUnknownArticleIsRejected()
    {
        var context = await CreateLotContextAsync();

        await AssertLotIsRejectedAsync(
            context.WithArticle(Guid.NewGuid()).NewLot("UNKNOWN-ARTICLE"),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task LotWithUnknownUnitIsRejected()
    {
        var context = await CreateLotContextAsync();

        await AssertLotIsRejectedAsync(
            context.WithUnit(Guid.NewGuid()).NewLot("UNKNOWN-UNIT"),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task LotCannotReferenceAnArticleOfAnotherOrganization()
    {
        var own = await CreateLotContextAsync();
        var foreign = await CreateLotContextAsync();

        // Everything is valid on its own: the organization exists, the article exists, and the
        // unit exists. Only the combination is wrong - the article belongs to another
        // organization. The composite foreign key is what rejects it.
        await AssertLotIsRejectedAsync(
            own.WithArticle(foreign.ArticleId).NewLot("CROSS-TENANT-ARTICLE"),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.000001")]
    public async Task NonPositiveQuantityIsRejectedByTheDatabase(string quantity)
    {
        // The domain already rejects these values, so a Lot instance cannot carry them. The
        // check constraint is a database guarantee and is therefore verified with raw SQL,
        // independently of the domain.
        var context = await CreateLotContextAsync();

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertLotDirectlyAsync(context, "INVALID-QUANTITY", quantity));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_lot_quantity_positive", exception.ConstraintName);
    }

    [Fact]
    public async Task SmallestRepresentableQuantityIsStoredExactly()
    {
        var context = await CreateLotContextAsync();
        var lotId = await CreateLotAsync(context, "TINY-QUANTITY", quantity: 0.000001m);

        var storedQuantity = await ScalarAsync(
            "SELECT quantity FROM trace.lot WHERE lot_id = @lot_id;",
            lotId);

        Assert.Equal(0.000001m, Assert.IsType<decimal>(storedQuantity));
    }

    [Fact]
    public async Task LotHasTheExpectedForeignKeys()
    {
        const string sql = """
            SELECT foreign_key.conname,
                   target_schema.nspname,
                   target_table.relname,
                   CASE foreign_key.confdeltype
                       WHEN 'a' THEN 'NO ACTION'
                       WHEN 'r' THEN 'RESTRICT'
                       WHEN 'c' THEN 'CASCADE'
                       WHEN 'n' THEN 'SET NULL'
                       WHEN 'd' THEN 'SET DEFAULT'
                   END,
                   (
                       SELECT string_agg(source_column.attname, ',' ORDER BY key_pair.ordinal_position)
                       FROM unnest(foreign_key.conkey) WITH ORDINALITY
                            AS key_pair(source_attnum, ordinal_position)
                       JOIN pg_catalog.pg_attribute AS source_column
                         ON source_column.attrelid = source_table.oid
                        AND source_column.attnum = key_pair.source_attnum
                   )
            FROM pg_catalog.pg_constraint AS foreign_key
            JOIN pg_catalog.pg_class AS source_table
              ON source_table.oid = foreign_key.conrelid
            JOIN pg_catalog.pg_namespace AS source_schema
              ON source_schema.oid = source_table.relnamespace
            JOIN pg_catalog.pg_class AS target_table
              ON target_table.oid = foreign_key.confrelid
            JOIN pg_catalog.pg_namespace AS target_schema
              ON target_schema.oid = target_table.relnamespace
            WHERE source_schema.nspname = 'trace'
              AND source_table.relname = 'lot'
              AND foreign_key.contype = 'f'
            ORDER BY foreign_key.conname;
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
            [
                new ForeignKey(
                    "fk_lot_catalog_article",
                    "catalog",
                    "article",
                    "RESTRICT",
                    "article_id,organization_id"),
                new ForeignKey(
                    "fk_lot_catalog_unit",
                    "catalog",
                    "unit",
                    "RESTRICT",
                    "unit_id"),
                new ForeignKey(
                    "fk_lot_org_organization",
                    "org",
                    "organization",
                    "RESTRICT",
                    "organization_id"),
            ],
            foreignKeys);
    }

    [Fact]
    public async Task OrganizationWithLotCannotBeDeleted()
    {
        var context = await CreateLotContextAsync();
        var lotId = await CreateLotAsync(context, "RESTRICT-ORGANIZATION");

        await using (var organizations = database.CreateTraceabilityOrganizationsDbContext())
        {
            var organization = await organizations.Organizations.SingleAsync(
                candidate => candidate.Id == context.OrganizationId);
            organizations.Organizations.Remove(organization);

            await AssertDatabaseErrorAsync(
                () => organizations.SaveChangesAsync(),
                PostgresErrorCodes.ForeignKeyViolation);
        }

        await AssertLotStillExistsAsync(lotId);
    }

    [Fact]
    public async Task ArticleWithLotCannotBeDeleted()
    {
        var context = await CreateLotContextAsync();
        var lotId = await CreateLotAsync(context, "RESTRICT-ARTICLE");

        await using (var catalog = database.CreateTraceabilityCatalogDbContext())
        {
            var article = await catalog.Articles.SingleAsync(
                candidate => candidate.Id == context.ArticleId);
            catalog.Articles.Remove(article);

            await AssertDatabaseErrorAsync(
                () => catalog.SaveChangesAsync(),
                PostgresErrorCodes.ForeignKeyViolation);
        }

        await AssertLotStillExistsAsync(lotId);
    }

    [Fact]
    public async Task UnitWithLotCannotBeDeleted()
    {
        var context = await CreateLotContextAsync();
        var lotId = await CreateLotAsync(context, "RESTRICT-UNIT");

        await using (var catalog = database.CreateTraceabilityCatalogDbContext())
        {
            var unit = await catalog.Units.SingleAsync(
                candidate => candidate.Id == context.UnitId);
            catalog.Units.Remove(unit);

            await AssertDatabaseErrorAsync(
                () => catalog.SaveChangesAsync(),
                PostgresErrorCodes.ForeignKeyViolation);
        }

        await AssertLotStillExistsAsync(lotId);
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

    private async Task<LotContext> CreateLotContextAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Traceability Test Organization {Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            CreatedAt);

        await using (var organizations = database.CreateTraceabilityOrganizationsDbContext())
        {
            organizations.Organizations.Add(organization);
            await organizations.SaveChangesAsync();
        }

        var product = Product.Create(
            Guid.NewGuid(),
            $"PRODUCT-{Guid.NewGuid():N}",
            "Traceability Test Product",
            CreatedAt);
        var article = Article.Create(
            Guid.NewGuid(),
            organization.Id,
            product.Id,
            $"ART-{Guid.NewGuid():N}",
            null,
            CreatedAt);
        var unit = Unit.Create(
            Guid.NewGuid(),
            UnitCode.Create($"U{Guid.NewGuid():N}"[..8]),
            "u",
            UnitDimension.Mass,
            CreatedAt);

        await using (var catalog = database.CreateTraceabilityCatalogDbContext())
        {
            catalog.Products.Add(product);
            catalog.Articles.Add(article);
            catalog.Units.Add(unit);
            await catalog.SaveChangesAsync();
        }

        return new LotContext(organization.Id, article.Id, unit.Id);
    }

    private async Task<Guid> CreateLotAsync(
        LotContext context,
        string lotNumber,
        decimal quantity = DefaultQuantity)
    {
        var lot = context.NewLot(lotNumber, quantity);

        await using var traceability = database.CreateTraceabilityDbContext();
        traceability.Lots.Add(lot);
        await traceability.SaveChangesAsync();

        return lot.Id;
    }

    private async Task AssertLotIsRejectedAsync(Lot lot, string expectedSqlState)
    {
        await using var context = database.CreateTraceabilityDbContext();
        context.Lots.Add(lot);

        await AssertDatabaseErrorAsync(() => context.SaveChangesAsync(), expectedSqlState);
    }

    private async Task AssertLotStillExistsAsync(Guid lotId)
    {
        await using var context = database.CreateTraceabilityDbContext();

        Assert.True(await context.Lots.AnyAsync(lot => lot.Id == lotId));
    }

    private async Task InsertLotDirectlyAsync(
        LotContext context,
        string lotNumber,
        string quantity)
    {
        const string sql = """
            INSERT INTO trace.lot
                (lot_id, organization_id, article_id, lot_number, quantity, unit_id, created_at)
            VALUES
                (@lot_id, @organization_id, @article_id, @lot_number, @quantity, @unit_id, @created_at);
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("lot_id", Guid.NewGuid());
        command.Parameters.AddWithValue("organization_id", context.OrganizationId);
        command.Parameters.AddWithValue("article_id", context.ArticleId);
        command.Parameters.AddWithValue("lot_number", lotNumber);
        command.Parameters.AddWithValue(
            "quantity",
            decimal.Parse(quantity, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("unit_id", context.UnitId);
        command.Parameters.AddWithValue("created_at", CreatedAt);

        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    private async Task<object?> ScalarAsync(string sql, Guid lotId)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("lot_id", lotId);

        return await command.ExecuteScalarAsync(timeout.Token);
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

    private sealed record LotContext(Guid OrganizationId, Guid ArticleId, Guid UnitId)
    {
        public LotContext WithOrganization(Guid organizationId) =>
            this with { OrganizationId = organizationId };

        public LotContext WithArticle(Guid articleId) => this with { ArticleId = articleId };

        public LotContext WithUnit(Guid unitId) => this with { UnitId = unitId };

        public Lot NewLot(string lotNumber, decimal quantity = DefaultQuantity) =>
            Lot.Create(
                Guid.NewGuid(),
                OrganizationId,
                ArticleId,
                lotNumber,
                quantity,
                UnitId,
                CreatedAt);
    }

    private sealed record DatabaseColumn(
        string Name,
        string IsNullable,
        string DataType,
        int? MaximumLength);

    private sealed record ForeignKey(
        string Name,
        string TargetSchema,
        string TargetTable,
        string DeleteRule,
        string SourceColumns);
}
