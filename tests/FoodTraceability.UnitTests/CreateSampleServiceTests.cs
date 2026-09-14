using FoodTraceability.Modules.Quality.Application.Samples;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.UnitTests;

public sealed class CreateSampleServiceTests
{
    [Fact]
    public async Task SampleUsesReturnedEventIdAndTimeProviderAndCommitsAfterBothWrites()
    {
        var calls = new List<string>();
        var transaction = new StubTransaction(calls);
        var creator = new StubEventCreator(calls);
        var writer = new StubSampleWriter(calls);
        var now = new DateTimeOffset(2026, 9, 13, 12, 0, 0, TimeSpan.Zero);
        var service = new CreateSampleService(transaction, creator, writer, new FixedTimeProvider(now));
        var command = ValidCommand() with { TakenAt = now.AddTicks(1234567) };
        using var cancellation = new CancellationTokenSource();

        var result = await service.CreateAsync(command, cancellation.Token);

        Assert.Equal(new[] { "begin", "event", "sample", "commit", "dispose" }, calls);
        Assert.NotNull(creator.Request);
        Assert.Equal(command.OrganizationId, creator.Request.OrganizationId);
        Assert.Equal(command.LocationId, creator.Request.LocationId);
        Assert.Equal(command.CreatedBy, creator.Request.CreatedBy);
        Assert.Equal("SAMPLE", creator.Request.EventTypeCode);
        Assert.Equal(now.AddTicks(1234560), creator.Request.OccurredAt);
        Assert.Null(creator.Request.ExternalReference);
        Assert.Null(creator.Request.Description);
        Assert.NotNull(creator.Request.Inputs);
        Assert.Equal(new TraceabilityEventLot(command.LotId, command.Quantity), Assert.Single(creator.Request.Inputs));
        Assert.NotNull(creator.Request.Outputs);
        Assert.Empty(creator.Request.Outputs);
        Assert.NotNull(writer.Sample);
        Assert.Equal(now, writer.Sample.CreatedAt);
        Assert.Equal(SampleStatus.Pending, writer.Sample.Status);
        Assert.Equal(creator.EventId, writer.Sample.TraceabilityEventId);
        Assert.Equal(creator.EventId, result.TraceabilityEventId);
        Assert.Equal(creator.Request.OccurredAt, result.TakenAt);
        Assert.Equal(cancellation.Token, transaction.Token);
        Assert.Equal(cancellation.Token, creator.Token);
        Assert.Equal(cancellation.Token, writer.Token);
        Assert.Equal(cancellation.Token, transaction.CommitToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContractFailureKeepsMessageAndDisposesWithoutCommit(bool conflict)
    {
        var calls = new List<string>();
        var creator = new StubEventCreator(calls)
        {
            Failure = conflict
                ? new TraceabilityEventConflictException("contract conflict")
                : new TraceabilityEventValidationException("contract validation"),
        };
        var service = new CreateSampleService(new StubTransaction(calls), creator,
            new StubSampleWriter(calls), TimeProvider.System);
        var exception = await Record.ExceptionAsync(() => service.CreateAsync(ValidCommand(), CancellationToken.None));
        if (conflict)
        {
            Assert.IsType<SampleConflictException>(exception);
        }
        else
        {
            Assert.IsType<SampleValidationException>(exception);
        }
        Assert.NotNull(exception);
        Assert.NotNull(creator.Failure);
        Assert.Equal(creator.Failure.Message, exception.Message);
        Assert.Equal(new[] { "begin", "event", "dispose" }, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WriterFailureOrCancellationNeverCommits(bool cancelled)
    {
        var calls = new List<string>();
        Exception failure = cancelled ? new OperationCanceledException() : new InvalidOperationException("write failed");
        var writer = new StubSampleWriter(calls) { Failure = failure };
        var service = new CreateSampleService(new StubTransaction(calls), new StubEventCreator(calls), writer, TimeProvider.System);
        var exception = await Record.ExceptionAsync(() => service.CreateAsync(ValidCommand(), CancellationToken.None));
        Assert.Same(failure, exception);
        Assert.Equal(new[] { "begin", "event", "sample", "dispose" }, calls);
    }

    private static CreateSampleCommand ValidCommand() => new(
        Guid.NewGuid(), "SAMPLE-1", Guid.NewGuid(), Guid.NewGuid(), 1m,
        new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero), Guid.NewGuid());

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubTransaction(List<string> calls) : IApplicationTransaction, IApplicationTransactionHandle
    {
        public CancellationToken Token { get; private set; }
        public CancellationToken CommitToken { get; private set; }
        public Task<IApplicationTransactionHandle> BeginAsync(CancellationToken cancellationToken)
        {
            calls.Add("begin");
            Token = cancellationToken;
            return Task.FromResult<IApplicationTransactionHandle>(this);
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            calls.Add("commit");
            CommitToken = cancellationToken;
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

        public ValueTask DisposeAsync()
        {
            calls.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubEventCreator(List<string> calls) : ITraceabilityEventCreator
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public CreateTraceabilityEventRequest? Request { get; private set; }
        public Exception? Failure { get; init; }
        public CancellationToken Token { get; private set; }

        public Task<CreateTraceabilityEventResult> CreateAsync(CreateTraceabilityEventRequest request, CancellationToken cancellationToken)
        {
            calls.Add("event");
            Request = request;
            Token = cancellationToken;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.FromResult(new CreateTraceabilityEventResult(EventId, request.OccurredAt));
        }
    }

    private sealed class StubSampleWriter(List<string> calls) : ISampleWriter
    {
        public Sample? Sample { get; private set; }
        public Exception? Failure { get; init; }
        public CancellationToken Token { get; private set; }

        public Task AddAsync(Sample sample, CancellationToken cancellationToken)
        {
            calls.Add("sample");
            Sample = sample;
            Token = cancellationToken;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.CompletedTask;
        }
    }
}
