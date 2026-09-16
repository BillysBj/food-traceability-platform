using FoodTraceability.Modules.Quality.Domain;

namespace FoodTraceability.UnitTests;

public sealed class LotBlockTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly Guid OrganizationId = Guid.NewGuid();
    private static readonly Guid LotId = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly DateTimeOffset BlockedAt =
        new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero).AddTicks(7);
    private static readonly DateTimeOffset CreatedAt = BlockedAt.AddTicks(13);

    [Fact]
    public void ValidOpenBlockPreservesAllFieldsAndTimestampPrecision()
    {
        var block = Create(reason: "  Investigation required  ");

        Assert.Equal(Id, block.Id);
        Assert.Equal(OrganizationId, block.OrganizationId);
        Assert.Equal(LotId, block.LotId);
        Assert.Equal("Investigation required", block.Reason);
        Assert.Equal(BlockedAt, block.BlockedAt);
        Assert.Equal(UserId, block.BlockedBy);
        Assert.Equal(CreatedAt, block.CreatedAt);
        Assert.Null(block.ReleasedAt);
        Assert.Null(block.ReleasedBy);
    }

    [Theory]
    [InlineData(nameof(LotBlock.Id))]
    [InlineData(nameof(LotBlock.OrganizationId))]
    [InlineData(nameof(LotBlock.LotId))]
    [InlineData(nameof(LotBlock.BlockedBy))]
    public void EmptyGuidIsRejected(string field)
    {
        Assert.Throws<QualityDomainException>(() => LotBlock.Create(
            field == nameof(LotBlock.Id) ? Guid.Empty : Id,
            field == nameof(LotBlock.OrganizationId) ? Guid.Empty : OrganizationId,
            field == nameof(LotBlock.LotId) ? Guid.Empty : LotId,
            "Investigation required", BlockedAt,
            field == nameof(LotBlock.BlockedBy) ? Guid.Empty : UserId,
            CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void MissingReasonIsRejected(string? reason)
    {
        Assert.Throws<QualityDomainException>(() => Create(reason));
    }

    [Fact]
    public void ReasonLimitIsAppliedAfterTrimming()
    {
        var reason = new string('A', LotBlock.MaximumReasonLength);
        Assert.Equal(reason, Create($"  {reason}  ").Reason);
        Assert.Throws<QualityDomainException>(() => Create($"  {reason}A  "));
    }

    [Fact]
    public void ReleaseSetsBothFieldsExactlyOnceWithoutChangingHistory()
    {
        var block = Create();
        var releasedAt = BlockedAt.AddHours(1).AddTicks(1);
        var releasedBy = Guid.NewGuid();

        block.Release(releasedAt, releasedBy);

        Assert.Equal(releasedAt, block.ReleasedAt);
        Assert.Equal(releasedBy, block.ReleasedBy);
        Assert.Equal(Id, block.Id);
        Assert.Equal(OrganizationId, block.OrganizationId);
        Assert.Equal(LotId, block.LotId);
        Assert.Equal("Investigation required", block.Reason);
        Assert.Equal(BlockedAt, block.BlockedAt);
        Assert.Equal(UserId, block.BlockedBy);
        Assert.Equal(CreatedAt, block.CreatedAt);

        Assert.Throws<QualityDomainException>(() =>
            block.Release(releasedAt.AddHours(1), Guid.NewGuid()));
        Assert.Equal(releasedAt, block.ReleasedAt);
        Assert.Equal(releasedBy, block.ReleasedBy);
    }

    [Fact]
    public void EmptyReleaseUserIsRejectedWithoutPartiallyReleasing()
    {
        var block = Create();
        Assert.Throws<QualityDomainException>(() => block.Release(BlockedAt.AddHours(1), Guid.Empty));
        Assert.Null(block.ReleasedAt);
        Assert.Null(block.ReleasedBy);
    }

    private static LotBlock Create(string? reason = "Investigation required") =>
        LotBlock.Create(Id, OrganizationId, LotId, reason, BlockedAt, UserId, CreatedAt);
}
