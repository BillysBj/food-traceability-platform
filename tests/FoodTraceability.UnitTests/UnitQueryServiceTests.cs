using FoodTraceability.Modules.Catalog.Application.Units;

namespace FoodTraceability.UnitTests;

public sealed class UnitQueryServiceTests
{
    private static readonly Guid KilogramId =
        Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");

    [Fact]
    public async Task UppercaseKnownCodeReturnsExpectedId()
    {
        var service = new UnitQueryService(new StubUnitReader());

        var result = await service.FindIdByCodeAsync("KG", CancellationToken.None);

        Assert.Equal(KilogramId, result);
    }

    [Fact]
    public async Task LowercaseKnownCodeReturnsSameId()
    {
        var service = new UnitQueryService(new StubUnitReader());

        var result = await service.FindIdByCodeAsync("kg", CancellationToken.None);

        Assert.Equal(KilogramId, result);
    }

    [Fact]
    public async Task UnknownCodeReturnsNull()
    {
        var service = new UnitQueryService(new StubUnitReader());

        var result = await service.FindIdByCodeAsync("XX", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task InvalidCodeReturnsNullInsteadOfThrowing()
    {
        var service = new UnitQueryService(new StubUnitReader());

        var result = await service.FindIdByCodeAsync("!!", CancellationToken.None);

        Assert.Null(result);
    }

    private sealed class StubUnitReader : IUnitReader
    {
        public Task<Guid?> FindIdByCodeAsync(
            string code,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<Guid?>(code == "KG" ? KilogramId : null);
        }

        public Task<string?> FindCodeByIdAsync(
            Guid unitId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(unitId == KilogramId ? "KG" : null);
        }
    }
}
