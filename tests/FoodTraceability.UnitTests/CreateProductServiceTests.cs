using FoodTraceability.Modules.Catalog.Application.Products;
using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.UnitTests;

public sealed class CreateProductServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 8, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task ValidCommandReturnsNormalizedProductDetails()
    {
        var writer = new CapturingProductWriter();
        var service = new CreateProductService(writer, new FixedTimeProvider(Now));

        var result = await service.CreateAsync(
            new CreateProductCommand("  Olive-Oil-EV  ", "  Extra Virgin Olive Oil  "),
            CancellationToken.None);

        var persisted = Assert.IsType<Product>(writer.Product);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(result.Id, persisted.Id);
        Assert.Equal("Olive-Oil-EV", result.ProductCode);
        Assert.Equal("Extra Virgin Olive Oil", result.Name);
        Assert.Equal(Now, result.CreatedAt);
        Assert.Equal(result.ProductCode, persisted.ProductCode);
        Assert.Equal(result.Name, persisted.Name);
    }

    [Fact]
    public async Task MissingProductCodeIsRejectedAsValidationError()
    {
        var writer = new CapturingProductWriter();
        var service = new CreateProductService(writer, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.CreateAsync(
                new CreateProductCommand(null, "Olive Oil"),
                CancellationToken.None));

        Assert.Contains("Product code", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingNameIsRejectedAsValidationError()
    {
        var writer = new CapturingProductWriter();
        var service = new CreateProductService(writer, TimeProvider.System);

        var exception = await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.CreateAsync(
                new CreateProductCommand("OLIVE-OIL", null),
                CancellationToken.None));

        Assert.Contains("Product name", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InvalidInputDoesNotCallWriter()
    {
        var writer = new CapturingProductWriter();
        var service = new CreateProductService(writer, TimeProvider.System);

        await Assert.ThrowsAsync<ProductValidationException>(() =>
            service.CreateAsync(
                new CreateProductCommand("   ", "Olive Oil"),
                CancellationToken.None));

        Assert.Equal(0, writer.CallCount);
        Assert.Null(writer.Product);
    }

    private sealed class CapturingProductWriter : IProductWriter
    {
        public int CallCount { get; private set; }

        public Product? Product { get; private set; }

        public Task AddAsync(Product product, CancellationToken cancellationToken)
        {
            CallCount++;
            Product = product;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

public sealed class ProductQueryServiceTests
{
    [Fact]
    public async Task EmptyProductIdReturnsNullWithoutCallingReader()
    {
        var reader = new StubProductReader();
        var service = new ProductQueryService(reader);

        var result = await service.FindByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, reader.CallCount);
    }

    private sealed class StubProductReader : IProductReader
    {
        public int CallCount { get; private set; }

        public Task<ProductDetails?> FindByIdAsync(
            Guid productId,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult<ProductDetails?>(null);
        }
    }
}
