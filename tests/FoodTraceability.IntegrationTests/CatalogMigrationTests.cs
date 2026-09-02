using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class CatalogMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 2, 14, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CatalogMigrationAppliesToEmptyDatabase()
    {
        await using var context = database.CreateCatalogDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);

        await context.Database.MigrateAsync(timeout.Token);
        var appliedMigrations = await context.Database.GetAppliedMigrationsAsync(timeout.Token);

        Assert.Collection(
            appliedMigrations,
            migration => Assert.EndsWith(
                "_InitialCatalog",
                migration,
                StringComparison.Ordinal),
            migration => Assert.EndsWith(
                "_AddArticle",
                migration,
                StringComparison.Ordinal),
            migration => Assert.EndsWith(
                "_AddUnit",
                migration,
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task CatalogSchemaExistsWithExpectedTables()
    {
        const string sql = """
            SELECT table_name
            FROM information_schema.tables
            WHERE table_schema = 'catalog'
            ORDER BY table_name;
            """;

        var tables = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(
            [
                PersistenceConventions.MigrationsHistoryTableName,
                "article",
                "product",
                "unit",
            ],
            tables);
    }

    [Fact]
    public async Task UnitTableHasExactlyTheExpectedColumns()
    {
        const string sql = """
            SELECT column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'catalog'
              AND table_name = 'unit'
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
                new DatabaseColumn("unit_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "code",
                    "NO",
                    "character varying",
                    UnitCode.MaximumLength),
                new DatabaseColumn(
                    "symbol",
                    "NO",
                    "character varying",
                    Unit.MaximumSymbolLength),
                new DatabaseColumn(
                    "dimension",
                    "NO",
                    "character varying",
                    UnitDimensionCodes.MaximumLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            columns);

        Assert.DoesNotContain(columns, column => column.Name == "name");
        Assert.DoesNotContain(columns, column => column.Name == "updated_at");
        Assert.DoesNotContain(columns, column => column.Name == "organization_id");
    }

    [Fact]
    public async Task ExactlyTheFiveStandardUnitsAreSeeded()
    {
        const string sql = """
            SELECT unit_id, code, symbol, dimension
            FROM catalog.unit
            ORDER BY code;
            """;

        var units = await QueryAsync(
            sql,
            static reader => new SeededUnit(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));

        Assert.Equal(
            [
                new SeededUnit(
                    Guid.Parse("5e726b86-c672-5ed0-9601-904328038341"),
                    "G",
                    "g",
                    "MASS"),
                new SeededUnit(
                    Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085"),
                    "KG",
                    "kg",
                    "MASS"),
                new SeededUnit(
                    Guid.Parse("8d8ed466-8384-5e44-8430-eee76f15a180"),
                    "L",
                    "l",
                    "VOLUME"),
                new SeededUnit(
                    Guid.Parse("dd541026-8821-53a3-97de-f0a974327970"),
                    "ML",
                    "ml",
                    "VOLUME"),
                new SeededUnit(
                    Guid.Parse("d227d884-ef6c-5667-9587-1d9fdee6836e"),
                    "PCS",
                    "pcs",
                    "COUNT"),
            ],
            units);
    }

    [Fact]
    public async Task DuplicateUnitCodeIsRejected()
    {
        await using var context = database.CreateCatalogDbContext();
        context.Units.Add(Unit.Create(
            Guid.NewGuid(),
            UnitCode.Create("kg"),
            "duplicate",
            UnitDimension.Mass,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task InvalidUnitDimensionIsRejectedByDatabase()
    {
        await AssertUnitInsertErrorAsync(
            "LENGTH_UNIT",
            "length",
            "LENGTH",
            PostgresErrorCodes.CheckViolation);
    }

    [Fact]
    public async Task UnitCodeLongerThanMaximumIsRejectedByDatabase()
    {
        await AssertUnitInsertErrorAsync(
            new string('A', UnitCode.MaximumLength + 1),
            "oversized",
            UnitDimensionCodes.Count,
            PostgresErrorCodes.StringDataRightTruncation);
    }

    [Fact]
    public async Task ProductTableHasExactlyTheExpectedColumns()
    {
        const string sql = """
            SELECT column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'catalog'
              AND table_name = 'product'
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
                new DatabaseColumn("product_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "product_code",
                    "NO",
                    "character varying",
                    Product.MaximumProductCodeLength),
                new DatabaseColumn(
                    "name",
                    "NO",
                    "character varying",
                    Product.MaximumNameLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            columns);

        Assert.DoesNotContain(columns, column => column.Name == "organization_id");
        Assert.DoesNotContain(columns, column => column.Name == "status");
        Assert.DoesNotContain(columns, column => column.Name == "updated_at");
        Assert.DoesNotContain(columns, column => column.Name == "category_id");
    }

    [Fact]
    public async Task ArticleTableHasExactlyTheExpectedColumns()
    {
        const string sql = """
            SELECT column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'catalog'
              AND table_name = 'article'
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
                new DatabaseColumn("article_id", "NO", "uuid", null),
                new DatabaseColumn("organization_id", "NO", "uuid", null),
                new DatabaseColumn("product_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "article_number",
                    "NO",
                    "character varying",
                    Article.MaximumArticleNumberLength),
                new DatabaseColumn(
                    "gtin",
                    "YES",
                    "character varying",
                    Article.MaximumGtinLength),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
            ],
            columns);

        Assert.DoesNotContain(columns, column => column.Name == "unit_id");
        Assert.DoesNotContain(columns, column => column.Name == "net_quantity");
        Assert.DoesNotContain(columns, column => column.Name == "updated_at");
    }

    [Fact]
    public async Task ArticleHasRequiredCompositeAlternateKey()
    {
        const string sql = """
            SELECT constraint_name
            FROM information_schema.table_constraints
            WHERE table_schema = 'catalog'
              AND table_name = 'article'
              AND constraint_type = 'UNIQUE';
            """;

        var constraints = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Contains("ak_article_article_id_organization_id", constraints);
    }

    [Fact]
    public async Task DuplicateArticleNumberInSameOrganizationIsRejected()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();
        await CreateArticleAsync(organizationId, productId, "DUPLICATE-ARTICLE", null);

        await using var context = database.CreateCatalogDbContext();
        context.Articles.Add(Article.Create(
            Guid.NewGuid(),
            organizationId,
            productId,
            "DUPLICATE-ARTICLE",
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ArticleNumberIsCaseInsensitivelyUniqueWithinOrganization()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();
        await CreateArticleAsync(organizationId, productId, "ART-1", null);

        await using var context = database.CreateCatalogDbContext();
        context.Articles.Add(Article.Create(
            Guid.NewGuid(),
            organizationId,
            productId,
            "art-1",
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameArticleNumberInDifferentOrganizationsIsAccepted()
    {
        var firstOrganizationId = await CreateOrganizationAsync();
        var secondOrganizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();

        await using var context = database.CreateCatalogDbContext();
        context.Articles.AddRange(
            Article.Create(
                Guid.NewGuid(),
                firstOrganizationId,
                productId,
                "SHARED-ARTICLE",
                null,
                CreatedAt),
            Article.Create(
                Guid.NewGuid(),
                secondOrganizationId,
                productId,
                "SHARED-ARTICLE",
                null,
                CreatedAt));

        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(2, affectedRows);
    }

    [Fact]
    public async Task StoredArticleNumberPreservesOriginalCasing()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();
        var articleId = await CreateArticleAsync(
            organizationId,
            productId,
            "ART-1",
            null);

        const string sql = """
            SELECT article_number
            FROM catalog.article
            WHERE article_id = @article_id;
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("article_id", articleId);

        var storedArticleNumber = await command.ExecuteScalarAsync(timeout.Token);

        Assert.Equal("ART-1", Assert.IsType<string>(storedArticleNumber));
    }

    [Fact]
    public async Task DuplicateGtinInSameOrganizationIsRejected()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();
        await CreateArticleAsync(
            organizationId,
            productId,
            "GTIN-FIRST",
            "1234567890123");

        await using var context = database.CreateCatalogDbContext();
        context.Articles.Add(Article.Create(
            Guid.NewGuid(),
            organizationId,
            productId,
            "GTIN-SECOND",
            "1234567890123",
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task SameGtinInDifferentOrganizationsIsAccepted()
    {
        var firstOrganizationId = await CreateOrganizationAsync();
        var secondOrganizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();

        await using var context = database.CreateCatalogDbContext();
        context.Articles.AddRange(
            Article.Create(
                Guid.NewGuid(),
                firstOrganizationId,
                productId,
                "FIRST-GTIN-ARTICLE",
                "1234567890123",
                CreatedAt),
            Article.Create(
                Guid.NewGuid(),
                secondOrganizationId,
                productId,
                "SECOND-GTIN-ARTICLE",
                "1234567890123",
                CreatedAt));

        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(2, affectedRows);
    }

    [Fact]
    public async Task MultipleNullGtinsInSameOrganizationAreAccepted()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();

        await using var context = database.CreateCatalogDbContext();
        context.Articles.AddRange(
            Article.Create(
                Guid.NewGuid(),
                organizationId,
                productId,
                "NULL-GTIN-FIRST",
                null,
                CreatedAt),
            Article.Create(
                Guid.NewGuid(),
                organizationId,
                productId,
                "NULL-GTIN-SECOND",
                null,
                CreatedAt));

        var affectedRows = await context.SaveChangesAsync();

        Assert.Equal(2, affectedRows);
    }

    [Theory]
    [InlineData("1234A678", PostgresErrorCodes.CheckViolation)]
    [InlineData("1234567", PostgresErrorCodes.CheckViolation)]
    [InlineData("123456789", PostgresErrorCodes.CheckViolation)]
    [InlineData("12345678901", PostgresErrorCodes.CheckViolation)]
    [InlineData("123456789012345", PostgresErrorCodes.StringDataRightTruncation)]
    public async Task InvalidGtinFormatsAreRejectedByDatabase(
        string gtin,
        string expectedSqlState)
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();

        await AssertArticleInsertErrorAsync(
            organizationId,
            productId,
            $"INVALID-GTIN-{Guid.NewGuid():N}",
            gtin,
            expectedSqlState);
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("123456789012")]
    [InlineData("1234567890123")]
    [InlineData("12345678901234")]
    public async Task ValidGtinLengthsAreAcceptedByDatabase(string gtin)
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();

        var articleId = await InsertArticleAsync(
            organizationId,
            productId,
            $"VALID-GTIN-{Guid.NewGuid():N}",
            gtin);

        Assert.NotEqual(Guid.Empty, articleId);
    }

    [Fact]
    public async Task ArticleWithUnknownOrganizationIsRejected()
    {
        var productId = await CreateProductAsync();

        await using var context = database.CreateCatalogDbContext();
        context.Articles.Add(Article.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            productId,
            "UNKNOWN-ORGANIZATION",
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task ArticleWithUnknownProductIsRejected()
    {
        var organizationId = await CreateOrganizationAsync();

        await using var context = database.CreateCatalogDbContext();
        context.Articles.Add(Article.Create(
            Guid.NewGuid(),
            organizationId,
            Guid.NewGuid(),
            "UNKNOWN-PRODUCT",
            null,
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation);
    }

    [Fact]
    public async Task OrganizationWithArticleCannotBeDeleted()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();
        var articleId = await CreateArticleAsync(
            organizationId,
            productId,
            "ORGANIZATION-RESTRICT",
            null);

        await using (var context = database.CreateCatalogOrganizationsDbContext())
        {
            var organization = await context.Organizations.SingleAsync(
                candidate => candidate.Id == organizationId);
            context.Organizations.Remove(organization);

            await AssertDatabaseErrorAsync(
                () => context.SaveChangesAsync(),
                PostgresErrorCodes.ForeignKeyViolation);
        }

        await using var verificationContext = database.CreateCatalogDbContext();
        Assert.True(await verificationContext.Articles.AnyAsync(
            article => article.Id == articleId));
    }

    [Fact]
    public async Task ProductWithArticleCannotBeDeleted()
    {
        var organizationId = await CreateOrganizationAsync();
        var productId = await CreateProductAsync();
        var articleId = await CreateArticleAsync(
            organizationId,
            productId,
            "PRODUCT-RESTRICT",
            null);

        await using (var context = database.CreateCatalogDbContext())
        {
            var product = await context.Products.SingleAsync(
                candidate => candidate.Id == productId);
            context.Products.Remove(product);

            await AssertDatabaseErrorAsync(
                () => context.SaveChangesAsync(),
                PostgresErrorCodes.ForeignKeyViolation);
        }

        await using var verificationContext = database.CreateCatalogDbContext();
        Assert.True(await verificationContext.Articles.AnyAsync(
            article => article.Id == articleId));
    }

    [Fact]
    public async Task DuplicateProductCodeIsRejected()
    {
        await CreateProductAsync("DUPLICATE-PRODUCT");

        await using var context = database.CreateCatalogDbContext();
        context.Products.Add(Product.Create(
            Guid.NewGuid(),
            "DUPLICATE-PRODUCT",
            "Duplicate Olive Oil",
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task ProductCodeIsGloballyCaseInsensitivelyUnique()
    {
        await DeleteProductByCodeAsync("OLIVE-OIL-EV");
        await CreateProductAsync("OLIVE-OIL-EV");

        await using var context = database.CreateCatalogDbContext();
        context.Products.Add(Product.Create(
            Guid.NewGuid(),
            "olive-oil-ev",
            "Lowercase Duplicate Olive Oil",
            CreatedAt));

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation);
    }

    [Fact]
    public async Task StoredProductCodePreservesOriginalCasing()
    {
        await DeleteProductByCodeAsync("OLIVE-OIL-EV");
        var productId = await CreateProductAsync("OLIVE-OIL-EV");

        const string sql = """
            SELECT product_code
            FROM catalog.product
            WHERE product_id = @product_id;
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("product_id", productId);

        var storedProductCode = await command.ExecuteScalarAsync(timeout.Token);

        Assert.Equal("OLIVE-OIL-EV", Assert.IsType<string>(storedProductCode));
    }

    [Fact]
    public async Task ProductCodeLongerThanMaximumIsRejectedByDatabase()
    {
        await AssertOversizedValueIsRejectedAsync(
            new string('A', Product.MaximumProductCodeLength + 1),
            "Olive Oil");
    }

    [Fact]
    public async Task NameLongerThanMaximumIsRejectedByDatabase()
    {
        await AssertOversizedValueIsRejectedAsync(
            "OLIVE-OIL-EV",
            new string('A', Product.MaximumNameLength + 1));
    }

    [Fact]
    public async Task CatalogMigrationsHistoryLivesInCatalogSchema()
    {
        const string sql = """
            SELECT table_schema
            FROM information_schema.tables
            WHERE table_schema = 'catalog'
              AND table_name = '__ef_migrations_history';
            """;

        var schemas = await QueryAsync(sql, static reader => reader.GetString(0));

        Assert.Equal(CatalogDbContext.Schema, Assert.Single(schemas));
    }

    private async Task<Guid> CreateOrganizationAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Catalog Test Organization {Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            CreatedAt);

        await using var context = database.CreateCatalogOrganizationsDbContext();
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        return organization.Id;
    }

    private async Task<Guid> CreateProductAsync(string? productCode = null)
    {
        var product = Product.Create(
            Guid.NewGuid(),
            productCode ?? $"PRODUCT-{Guid.NewGuid():N}",
            $"Catalog Test Product {Guid.NewGuid():N}",
            CreatedAt);

        await using var context = database.CreateCatalogDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product.Id;
    }

    private async Task<Guid> CreateArticleAsync(
        Guid organizationId,
        Guid productId,
        string articleNumber,
        string? gtin)
    {
        var article = Article.Create(
            Guid.NewGuid(),
            organizationId,
            productId,
            articleNumber,
            gtin,
            CreatedAt);

        await using var context = database.CreateCatalogDbContext();
        context.Articles.Add(article);
        await context.SaveChangesAsync();

        return article.Id;
    }

    private async Task<Guid> InsertArticleAsync(
        Guid organizationId,
        Guid productId,
        string articleNumber,
        string? gtin)
    {
        const string sql = """
            INSERT INTO catalog.article (
                article_id,
                organization_id,
                product_id,
                article_number,
                gtin,
                created_at)
            VALUES (
                @article_id,
                @organization_id,
                @product_id,
                @article_number,
                @gtin,
                @created_at);
            """;

        var articleId = Guid.NewGuid();
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("article_id", articleId);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("article_number", articleNumber);
        command.Parameters.AddWithValue("gtin", gtin is null ? DBNull.Value : gtin);
        command.Parameters.AddWithValue("created_at", CreatedAt);
        await command.ExecuteNonQueryAsync(timeout.Token);

        return articleId;
    }

    private async Task AssertArticleInsertErrorAsync(
        Guid organizationId,
        Guid productId,
        string articleNumber,
        string gtin,
        string expectedSqlState)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertArticleAsync(
                organizationId,
                productId,
                articleNumber,
                gtin));

        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private async Task AssertUnitInsertErrorAsync(
        string code,
        string symbol,
        string dimension,
        string expectedSqlState)
    {
        const string sql = """
            INSERT INTO catalog.unit (unit_id, code, symbol, dimension, created_at)
            VALUES (@unit_id, @code, @symbol, @dimension, @created_at);
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("unit_id", Guid.NewGuid());
        command.Parameters.AddWithValue("code", code);
        command.Parameters.AddWithValue("symbol", symbol);
        command.Parameters.AddWithValue("dimension", dimension);
        command.Parameters.AddWithValue("created_at", CreatedAt);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(timeout.Token));

        Assert.Equal(expectedSqlState, exception.SqlState);
    }

    private async Task DeleteProductByCodeAsync(string productCode)
    {
        const string sql = """
            DELETE FROM catalog.product
            WHERE upper(product_code) = upper(@product_code);
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("product_code", productCode);
        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    private async Task AssertOversizedValueIsRejectedAsync(
        string productCode,
        string name)
    {
        const string sql = """
            INSERT INTO catalog.product (product_id, product_code, name, created_at)
            VALUES (@product_id, @product_code, @name, @created_at);
            """;

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("product_id", Guid.NewGuid());
        command.Parameters.AddWithValue("product_code", productCode);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("created_at", CreatedAt);

        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(timeout.Token));

        Assert.Equal(PostgresErrorCodes.StringDataRightTruncation, exception.SqlState);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.CatalogConnectionString);
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

    private sealed record SeededUnit(
        Guid Id,
        string Code,
        string Symbol,
        string Dimension);
}
