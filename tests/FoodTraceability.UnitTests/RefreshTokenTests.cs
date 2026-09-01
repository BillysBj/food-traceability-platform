using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.UnitTests;

public sealed class RefreshTokenTests
{
    private static readonly Guid TokenId = Guid.Parse("ecfc60a9-5d37-4d26-ae75-24ed0e1ac3f4");
    private static readonly Guid UserId = Guid.Parse("cd9f3d2e-5411-48a6-8a29-d45f4e4bf12b");
    private static readonly Guid SessionId = Guid.Parse("5a57560f-eefc-4228-9008-458f9f4947ad");
    private static readonly DateTimeOffset IssuedAt =
        new(2026, 9, 1, 16, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ExpiresAt = IssuedAt.AddDays(7);

    public static IEnumerable<object?[]> EmptyRequiredIds()
    {
        yield return [Guid.Empty, UserId, SessionId];
        yield return [TokenId, Guid.Empty, SessionId];
        yield return [TokenId, UserId, Guid.Empty];
    }

    public static IEnumerable<object?[]> InvalidTokenHashes()
    {
        yield return [null];
        yield return [string.Empty];
        yield return ["   "];
    }

    [Fact]
    public void ValidRefreshTokenIsCreatedWithExpectedValues()
    {
        var token = CreateToken();

        Assert.Equal(TokenId, token.Id);
        Assert.Equal(UserId, token.UserId);
        Assert.Equal(SessionId, token.SessionId);
        Assert.Equal("token-hash", token.TokenHash);
        Assert.Equal(IssuedAt, token.IssuedAt);
        Assert.Equal(ExpiresAt, token.ExpiresAt);
        Assert.Null(token.RevokedAt);
    }

    [Theory]
    [MemberData(nameof(EmptyRequiredIds))]
    public void EmptyRequiredIdIsRejected(Guid id, Guid userId, Guid sessionId)
    {
        Assert.Throws<IdentityDomainException>(() => RefreshToken.Create(
            id,
            userId,
            sessionId,
            "token-hash",
            IssuedAt,
            ExpiresAt));
    }

    [Theory]
    [MemberData(nameof(InvalidTokenHashes))]
    public void InvalidTokenHashIsRejected(string? tokenHash)
    {
        Assert.Throws<IdentityDomainException>(() => RefreshToken.Create(
            TokenId,
            UserId,
            SessionId,
            tokenHash,
            IssuedAt,
            ExpiresAt));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ExpirationNotAfterIssueTimeIsRejected(int offsetTicks)
    {
        Assert.Throws<IdentityDomainException>(() => RefreshToken.Create(
            TokenId,
            UserId,
            SessionId,
            "token-hash",
            IssuedAt,
            IssuedAt.AddTicks(offsetTicks)));
    }

    [Fact]
    public void RevokeStoresOccurrenceTime()
    {
        var token = CreateToken();
        var revokedAt = IssuedAt.AddHours(1);

        token.Revoke(revokedAt);

        Assert.Equal(revokedAt, token.RevokedAt);
    }

    [Fact]
    public void RevokeBeforeIssueTimeIsRejected()
    {
        var token = CreateToken();

        Assert.Throws<IdentityDomainException>(() => token.Revoke(IssuedAt.AddTicks(-1)));
    }

    [Fact]
    public void RevokeIsIdempotent()
    {
        var token = CreateToken();
        var firstRevocation = IssuedAt.AddHours(1);
        token.Revoke(firstRevocation);

        token.Revoke(IssuedAt.AddTicks(-1));

        Assert.Equal(firstRevocation, token.RevokedAt);
    }

    [Fact]
    public void IsActiveIsTrueBetweenIssueAndExpiration()
    {
        var token = CreateToken();

        Assert.True(token.IsActive(IssuedAt));
        Assert.True(token.IsActive(ExpiresAt.AddTicks(-1)));
    }

    [Fact]
    public void IsActiveIsFalseAfterRevocation()
    {
        var token = CreateToken();
        token.Revoke(IssuedAt.AddHours(1));

        Assert.False(token.IsActive(IssuedAt.AddHours(2)));
    }

    [Fact]
    public void IsActiveIsFalseAtAndAfterExpiration()
    {
        var token = CreateToken();

        Assert.False(token.IsActive(ExpiresAt));
        Assert.False(token.IsActive(ExpiresAt.AddTicks(1)));
    }

    [Fact]
    public void IsActiveIsFalseBeforeIssueTime()
    {
        var token = CreateToken();

        Assert.False(token.IsActive(IssuedAt.AddTicks(-1)));
    }

    private static RefreshToken CreateToken()
    {
        return RefreshToken.Create(
            TokenId,
            UserId,
            SessionId,
            "token-hash",
            IssuedAt,
            ExpiresAt);
    }
}
