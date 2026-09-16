using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.UnitTests;

public sealed class BlockLotServiceTests
{
    [Fact]
    public async Task SetsStatusBeforeWritingBlockAndCommitsOnlyAfterBothWrites()
    {
        var calls = new List<string>();
        var transaction = new StubTransaction(calls);
        var status = new StubStatusWriter(calls);
        var writer = new StubBlockWriter(calls);
        var now = new DateTimeOffset(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);
        var service = new BlockLotService(transaction, status, writer, new FixedTimeProvider(now.AddTicks(7)));
        var command = ValidCommand();
        using var cancellation = new CancellationTokenSource();

        var result = await service.BlockAsync(command, cancellation.Token);

        Assert.Equal(new[] { "begin", "status", "block", "commit", "dispose" }, calls);
        Assert.Equal((command.OrganizationId, command.LotId, LotQualityStatus.Blocked), status.Request);
        var block = Assert.IsType<LotBlock>(writer.Block);
        Assert.NotEqual(Guid.Empty, block.Id);
        Assert.Equal(command.OrganizationId, block.OrganizationId);
        Assert.Equal(command.LotId, block.LotId);
        Assert.Equal("Investigation required", block.Reason);
        Assert.Equal(command.BlockedBy, block.BlockedBy);
        Assert.Equal(now, block.BlockedAt);
        Assert.Equal(now, block.CreatedAt);
        Assert.Null(block.ReleasedAt);
        Assert.Null(block.ReleasedBy);
        Assert.Equal(new LotBlockDetails(block.Id, command.LotId, block.Reason,
            now, command.BlockedBy, LotQualityStatus.Blocked), result);
        Assert.Equal(cancellation.Token, transaction.Token);
        Assert.Equal(cancellation.Token, status.Token);
        Assert.Equal(cancellation.Token, writer.Token);
        Assert.Equal(cancellation.Token, transaction.CommitToken);
    }

    [Fact]
    public async Task MissingLotDisposesWithoutBlockWriteOrCommit()
    {
        var calls = new List<string>();
        var service = new BlockLotService(new StubTransaction(calls),
            new StubStatusWriter(calls) { Found = false }, new StubBlockWriter(calls), TimeProvider.System);

        await Assert.ThrowsAsync<LotBlockNotFoundException>(() => service.BlockAsync(ValidCommand(), CancellationToken.None));

        Assert.Equal(new[] { "begin", "status", "dispose" }, calls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("too-long")]
    public async Task InvalidReasonDisposesBeforeAnyWrite(string? reason)
    {
        var calls = new List<string>();
        var service = new BlockLotService(new StubTransaction(calls), new StubStatusWriter(calls),
            new StubBlockWriter(calls), TimeProvider.System);
        var command = ValidCommand() with
        {
            Reason = reason == "too-long" ? new string('x', LotBlock.MaximumReasonLength + 1) : reason,
        };

        await Assert.ThrowsAsync<LotBlockValidationException>(() => service.BlockAsync(command, CancellationToken.None));

        Assert.Equal(new[] { "begin", "dispose" }, calls);
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task BlockWriterExceptionDisposesWithoutCommit(string failureKind)
    {
        var calls = new List<string>();
        Exception failure = failureKind switch
        {
            "conflict" => new LotBlockConflictException("already blocked"),
            "cancellation" => new OperationCanceledException(),
            _ => new InvalidOperationException("write failed"),
        };
        var service = new BlockLotService(new StubTransaction(calls), new StubStatusWriter(calls),
            new StubBlockWriter(calls) { Failure = failure }, TimeProvider.System);

        var exception = await Record.ExceptionAsync(() => service.BlockAsync(ValidCommand(), CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.Equal(new[] { "begin", "status", "block", "dispose" }, calls);
    }

    [Fact]
    public async Task StatusWriterExceptionDisposesWithoutBlockWriteOrCommit()
    {
        var calls = new List<string>();
        var failure = new InvalidOperationException("status write failed");
        var service = new BlockLotService(new StubTransaction(calls),
            new StubStatusWriter(calls) { Failure = failure }, new StubBlockWriter(calls), TimeProvider.System);

        var exception = await Record.ExceptionAsync(() => service.BlockAsync(ValidCommand(), CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.Equal(new[] { "begin", "status", "dispose" }, calls);
    }

    private static BlockLotCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "  Investigation required  ", Guid.NewGuid());

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

    private sealed class StubStatusWriter(List<string> calls) : ILotQualityStatusWriter
    {
        public bool Found { get; init; } = true;
        public Exception? Failure { get; init; }
        public (Guid OrganizationId, Guid LotId, LotQualityStatus Status) Request { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<bool> SetAsync(Guid organizationId, Guid lotId, LotQualityStatus qualityStatus, CancellationToken cancellationToken)
        {
            calls.Add("status");
            Request = (organizationId, lotId, qualityStatus);
            Token = cancellationToken;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.FromResult(Found);
        }
    }

    private sealed class StubBlockWriter(List<string> calls) : ILotBlockWriter
    {
        public LotBlock? Block { get; private set; }
        public Exception? Failure { get; init; }
        public CancellationToken Token { get; private set; }

        public Task AddAsync(LotBlock block, CancellationToken cancellationToken)
        {
            calls.Add("block");
            Block = block;
            Token = cancellationToken;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.CompletedTask;
        }
    }
}
