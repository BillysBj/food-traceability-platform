using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
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

        var migration = Assert.Single(appliedMigrations);
        Assert.EndsWith("_InitialCatalog", migration, StringComparison.Ordinal);
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
                "product",
            ],
            tables);
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

    private async Task<Guid> CreateProductAsync(string productCode)
    {
        var product = Product.Create(
            Guid.NewGuid(),
            productCode,
            $"Catalog Test Product {Guid.NewGuid():N}",
            CreatedAt);

        await using var context = database.CreateCatalogDbContext();
        context.Products.Add(product);
        await context.SaveChangesAsync();

        return product.Id;
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
}
