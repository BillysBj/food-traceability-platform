using FoodTraceability.Modules.Traceability.Application.EventTypes;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.UnitTests;

public sealed class EventTypeQueryServiceTests
{
    private static readonly Guid PressId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");

    [Fact]
    public async Task LowercaseKnownCodeReturnsExpectedId()
    {
        var service = new EventTypeQueryService(new StubEventTypeReader());

        var result = await service.FindByCodeAsync("press", CancellationToken.None);

        Assert.Equal(PressId, result?.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!")]
    [InlineData("UNKNOWN")]
    public async Task UnknownOrInvalidCodeReturnsNull(string? code)
    {
        var service = new EventTypeQueryService(new StubEventTypeReader());

        var result = await service.FindByCodeAsync(code, CancellationToken.None);

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

        public Task<EventTypeLookup?> FindByCodeAsync(
            string code,
            CancellationToken cancellationToken) =>
            Task.FromResult<EventTypeLookup?>(code == "PRESS"
                ? new EventTypeLookup(PressId, EventTypeClassification.Traceability)
                : null);

        public Task<string?> FindCodeByIdAsync(
            Guid eventTypeId,
            CancellationToken cancellationToken)
        {
            FindCodeByIdCallCount++;
            return Task.FromResult(eventTypeId == PressId ? "PRESS" : null);
        }
    }
}
