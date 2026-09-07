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

    [Fact]
    public async Task EmptyIdCollectionReturnsEmptyDictionaryWithoutCallingReader()
    {
        var reader = new StubUnitReader();
        var service = new UnitQueryService(reader);

        var result = await service.FindCodesByIdsAsync([], CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, reader.FindCodesByIdsCallCount);
    }

    [Fact]
    public async Task BatchLookupReturnsOnlyExistingUnits()
    {
        var missingUnitId = Guid.NewGuid();
        var reader = new StubUnitReader();
        var service = new UnitQueryService(reader);

        var result = await service.FindCodesByIdsAsync(
            [KilogramId, missingUnitId],
            CancellationToken.None);

        var unit = Assert.Single(result);
        Assert.Equal(KilogramId, unit.Key);
        Assert.Equal("KG", unit.Value);
        Assert.Equal(1, reader.FindCodesByIdsCallCount);
    }

    private sealed class StubUnitReader : IUnitReader
    {
        public int FindCodesByIdsCallCount { get; private set; }

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

        public Task<IReadOnlyDictionary<Guid, string>> FindCodesByIdsAsync(
            IReadOnlyCollection<Guid> unitIds,
            CancellationToken cancellationToken)
        {
            FindCodesByIdsCallCount++;
            IReadOnlyDictionary<Guid, string> result = unitIds.Contains(KilogramId)
                ? new Dictionary<Guid, string> { [KilogramId] = "KG" }
                : new Dictionary<Guid, string>();
            return Task.FromResult(result);
        }
    }
}
