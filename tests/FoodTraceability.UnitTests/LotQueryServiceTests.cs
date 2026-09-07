using FoodTraceability.Modules.Traceability.Application.Lots;

namespace FoodTraceability.UnitTests;

public sealed class LotQueryServiceTests
{
    [Fact]
    public async Task EmptyOrganizationReturnsEmptyPageWithoutCallingReader()
    {
        var reader = new CapturingLotReader();
        var service = new LotQueryService(reader);

        var result = await service.ListAsync(
            new ListLotsQuery(Guid.Empty, 3, 25, Guid.NewGuid(), "LOT-001"),
            CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(3, result.Page);
        Assert.Equal(25, result.PageSize);
        Assert.Equal(0, result.TotalCount);
        Assert.Null(reader.ListQuery);
    }

    [Fact]
    public async Task LotNumberIsTrimmedBeforeItReachesReader()
    {
        var reader = new CapturingLotReader();
        var service = new LotQueryService(reader);

        await service.ListAsync(
            new ListLotsQuery(Guid.NewGuid(), 1, 50, null, "  MiXeD-001  "),
            CancellationToken.None);

        Assert.Equal("MiXeD-001", reader.ListQuery?.LotNumber);
    }

    [Fact]
    public async Task WhitespaceOnlyLotNumberReachesReaderAsUnset()
    {
        var reader = new CapturingLotReader();
        var service = new LotQueryService(reader);

        await service.ListAsync(
            new ListLotsQuery(Guid.NewGuid(), 1, 50, null, "   "),
            CancellationToken.None);

        Assert.NotNull(reader.ListQuery);
        Assert.Null(reader.ListQuery.LotNumber);
    }

    [Fact]
    public async Task PageAndPageSizeReachReaderUnchanged()
    {
        var reader = new CapturingLotReader();
        var service = new LotQueryService(reader);

        await service.ListAsync(
            new ListLotsQuery(Guid.NewGuid(), 7, 83, null, null),
            CancellationToken.None);

        Assert.Equal(7, reader.ListQuery?.Page);
        Assert.Equal(83, reader.ListQuery?.PageSize);
    }

    private sealed class CapturingLotReader : ILotReader
    {
        public ListLotsQuery? ListQuery { get; private set; }

        public Task<LotDetails?> FindByIdAsync(
            Guid organizationId,
            Guid lotId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<LotDetails?>(null);
        }

        public Task<LotPage> ListAsync(
            ListLotsQuery query,
            CancellationToken cancellationToken)
        {
            ListQuery = query;
            return Task.FromResult(new LotPage([], query.Page, query.PageSize, 0));
        }
    }
}
