using FoodTraceability.Modules.Traceability.Application.EventTypes;

namespace FoodTraceability.UnitTests;

public sealed class EventTypeQueryServiceTests
{
    private static readonly Guid PressId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");

    [Fact]
    public async Task LowercaseKnownCodeReturnsExpectedId()
    {
        var service = new EventTypeQueryService(new StubEventTypeReader());

        var result = await service.FindIdByCodeAsync("press", CancellationToken.None);

        Assert.Equal(PressId, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!")]
    [InlineData("UNKNOWN")]
    public async Task UnknownOrInvalidCodeReturnsNull(string? code)
    {
        var service = new EventTypeQueryService(new StubEventTypeReader());

        var result = await service.FindIdByCodeAsync(code, CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task KnownIdReturnsCode()
    {
        var service = new EventTypeQueryService(new StubEventTypeReader());

        var result = await service.FindCodeByIdAsync(PressId, CancellationToken.None);

        Assert.Equal("PRESS", result);
    }

    [Fact]
    public async Task EmptyIdReturnsNullWithoutCallingReader()
    {
        var reader = new StubEventTypeReader();
        var service = new EventTypeQueryService(reader);

        var result = await service.FindCodeByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(0, reader.FindCodeByIdCallCount);
    }

    private sealed class StubEventTypeReader : IEventTypeReader
    {
        public int FindCodeByIdCallCount { get; private set; }

        public Task<Guid?> FindIdByCodeAsync(
            string code,
            CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(code == "PRESS" ? PressId : null);

        public Task<string?> FindCodeByIdAsync(
            Guid eventTypeId,
            CancellationToken cancellationToken)
        {
            FindCodeByIdCallCount++;
            return Task.FromResult(eventTypeId == PressId ? "PRESS" : null);
        }
    }
}
