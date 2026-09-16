using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Contracts.Transactions;

namespace FoodTraceability.UnitTests;

public sealed class ReleaseLotBlockServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CommitsOnlyAfterStatusAndReleaseWritesAndPropagatesScopeActorTimeAndCancellation()
    {
        var scenario = new Scenario();
        using var cancellation = new CancellationTokenSource();

        var result = await scenario.Service.ReleaseAsync(scenario.Command, cancellation.Token);

        Assert.Equal(new[] { "begin", "read", "status", "save", "commit", "dispose" }, scenario.Calls);
        Assert.Equal((scenario.Command.OrganizationId, scenario.Command.LotId, scenario.Command.BlockId), scenario.Writer.Request);
        Assert.Equal((scenario.Command.OrganizationId, scenario.Command.LotId, LotQualityStatus.Released), scenario.Status.Request);
        Assert.Same(scenario.Block, scenario.Writer.Saved);
        Assert.Equal(Now, scenario.Block.ReleasedAt);
        Assert.Equal(scenario.Command.ReleasedBy, scenario.Block.ReleasedBy);
        Assert.Equal(new ReleasedLotBlockDetails(scenario.Block.Id, scenario.Block.LotId,
            scenario.Block.Reason, scenario.Block.BlockedAt, scenario.Block.BlockedBy,
            Now, scenario.Command.ReleasedBy, LotQualityStatus.Released), result);
        Assert.Equal(cancellation.Token, scenario.Transaction.BeginToken);
        Assert.Equal(cancellation.Token, scenario.Transaction.CommitToken);
        Assert.Equal(cancellation.Token, scenario.Writer.ReadToken);
        Assert.Equal(cancellation.Token, scenario.Writer.SaveToken);
        Assert.Equal(cancellation.Token, scenario.Status.Token);
    }

    [Fact]
    public async Task MissingBlockDoesNotWriteOrCommit()
    {
        var scenario = new Scenario();
        scenario.Writer.Found = null;

        await Assert.ThrowsAsync<ReleaseLotBlockNotFoundException>(() =>
            scenario.Service.ReleaseAsync(scenario.Command, CancellationToken.None));

        Assert.Equal(new[] { "begin", "read", "dispose" }, scenario.Calls);
    }

    [Fact]
    public async Task AlreadyReleasedBlockDoesNotChangeFieldsWriteStatusOrCommit()
    {
        var scenario = new Scenario();
        var originalTime = Now.AddHours(-1);
        var originalActor = Guid.NewGuid();
        scenario.Block.Release(originalTime, originalActor);

        await Assert.ThrowsAsync<LotBlockAlreadyReleasedException>(() =>
            scenario.Service.ReleaseAsync(scenario.Command, CancellationToken.None));

        Assert.Equal(new[] { "begin", "read", "dispose" }, scenario.Calls);
        Assert.Equal(originalTime, scenario.Block.ReleasedAt);
        Assert.Equal(originalActor, scenario.Block.ReleasedBy);
    }

    [Fact]
    public async Task MissingLotDoesNotSaveReleaseOrCommit()
    {
        var scenario = new Scenario();
        scenario.Status.Found = false;

        await Assert.ThrowsAsync<ReleaseLotBlockNotFoundException>(() =>
            scenario.Service.ReleaseAsync(scenario.Command, CancellationToken.None));

        Assert.Equal(new[] { "begin", "read", "status", "dispose" }, scenario.Calls);
    }

    [Theory]
    [InlineData("conflict")]
    [InlineData("failure")]
    [InlineData("cancellation")]
    public async Task ReleaseWriteFailureDisposesWithoutCommit(string failureKind)
    {
        var scenario = new Scenario();
        Exception failure = failureKind switch
        {
            "conflict" => new LotBlockAlreadyReleasedException(),
            "cancellation" => new OperationCanceledException(),
            _ => new InvalidOperationException("write failed"),
        };
        scenario.Writer.Failure = failure;

        var exception = await Record.ExceptionAsync(() =>
            scenario.Service.ReleaseAsync(scenario.Command, CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.Equal(new[] { "begin", "read", "status", "save", "dispose" }, scenario.Calls);
    }

    [Fact]
    public async Task StatusWriteFailureDoesNotSaveReleaseOrCommit()
    {
        var scenario = new Scenario();
        var failure = new InvalidOperationException("status failed");
        scenario.Status.Failure = failure;

        var exception = await Record.ExceptionAsync(() =>
            scenario.Service.ReleaseAsync(scenario.Command, CancellationToken.None));

        Assert.Same(failure, exception);
        Assert.Equal(new[] { "begin", "read", "status", "dispose" }, scenario.Calls);
    }

    [Fact]
    public async Task EmptyReleaseActorDoesNotWriteOrCommit()
    {
        var scenario = new Scenario();

        await Assert.ThrowsAsync<QualityDomainException>(() => scenario.Service.ReleaseAsync(
            scenario.Command with { ReleasedBy = Guid.Empty }, CancellationToken.None));

        Assert.Equal(new[] { "begin", "read", "dispose" }, scenario.Calls);
        Assert.Null(scenario.Block.ReleasedAt);
        Assert.Null(scenario.Block.ReleasedBy);
    }

    private sealed class Scenario
    {
        public List<string> Calls { get; } = [];
        public ReleaseLotBlockCommand Command { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        public LotBlock Block { get; }
        public StubTransaction Transaction { get; }
        public StubStatusWriter Status { get; }
        public StubBlockWriter Writer { get; }
        public ReleaseLotBlockService Service { get; }

        public Scenario()
        {
            Block = LotBlock.Create(Command.BlockId, Command.OrganizationId, Command.LotId,
                "Investigation required", Now.AddDays(-1), Guid.NewGuid(), Now.AddDays(-1));
            Transaction = new StubTransaction(Calls);
            Status = new StubStatusWriter(Calls);
            Writer = new StubBlockWriter(Calls) { Found = Block };
            Service = new ReleaseLotBlockService(Transaction, Status, Writer, new FixedTimeProvider(Now.AddTicks(7)));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubTransaction(List<string> calls) : IApplicationTransaction, IApplicationTransactionHandle
    {
        public CancellationToken BeginToken { get; private set; }
        public CancellationToken CommitToken { get; private set; }

        public Task<IApplicationTransactionHandle> BeginAsync(CancellationToken cancellationToken)
        {
            calls.Add("begin");
            BeginToken = cancellationToken;
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
        public bool Found { get; set; } = true;
        public Exception? Failure { get; set; }
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
        public LotBlock? Found { get; set; }
        public LotBlock? Saved { get; private set; }
        public Exception? Failure { get; set; }
        public (Guid OrganizationId, Guid LotId, Guid BlockId) Request { get; private set; }
        public CancellationToken ReadToken { get; private set; }
        public CancellationToken SaveToken { get; private set; }

        public Task AddAsync(LotBlock block, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LotBlock?> FindAsync(Guid organizationId, Guid lotId, Guid blockId, CancellationToken cancellationToken)
        {
            calls.Add("read");
            Request = (organizationId, lotId, blockId);
            ReadToken = cancellationToken;
            return Task.FromResult(Found);
        }

        public Task SaveReleaseAsync(LotBlock block, CancellationToken cancellationToken)
        {
            calls.Add("save");
            Saved = block;
            SaveToken = cancellationToken;
            if (Failure is not null)
            {
                throw Failure;
            }
            return Task.CompletedTask;
        }
    }
}
