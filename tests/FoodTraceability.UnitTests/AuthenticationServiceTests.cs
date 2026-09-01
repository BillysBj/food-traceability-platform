using FoodTraceability.Modules.Identity.Application.Authentication;

namespace FoodTraceability.UnitTests;

public sealed class AuthenticationServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SuccessfulLoginNormalizesEmailAndCreatesSession()
    {
        var account = new LoginAccount(Guid.NewGuid(), true, "stored-password-hash");
        var fixture = new AuthenticationFixture(account);

        var result = await fixture.Service.LoginAsync(
            new LoginRequest("  USER@Example.COM ", "valid-password"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("user@example.com", fixture.SessionStore.LastNormalizedEmail);
        Assert.Equal(account.UserId, fixture.SessionStore.CreatedUserId);
        Assert.NotEqual(Guid.Empty, fixture.SessionStore.CreatedSessionId);
        Assert.Equal(Now, fixture.SessionStore.CreatedRefreshToken?.IssuedAt);
        Assert.Equal(Now.AddDays(14), fixture.SessionStore.CreatedRefreshToken?.ExpiresAt);
        Assert.Equal("access-token", result.Tokens?.AccessToken);
        Assert.Equal(900, result.Tokens?.ExpiresIn);
        Assert.Equal("plain-refresh-token", result.Tokens?.RefreshToken);
        Assert.Equal("user@example.com", fixture.LoginAttemptLimiter.ResetEmail);
    }

    [Fact]
    public async Task UnknownAccountStillPerformsDummyHashVerificationAndRecordsEnteredEmail()
    {
        var fixture = new AuthenticationFixture(account: null);

        var result = await fixture.Service.LoginAsync(
            new LoginRequest(" Unknown@Example.com ", "submitted-password"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.True(fixture.PasswordVerifier.WasCalled);
        Assert.Null(fixture.PasswordVerifier.UserId);
        Assert.Null(fixture.PasswordVerifier.StoredHash);
        Assert.Equal("submitted-password", fixture.PasswordVerifier.ProvidedPassword);
        Assert.Equal("unknown@example.com", fixture.LoginAttemptLimiter.FailureEmail);
        Assert.Null(fixture.SessionStore.CreatedRefreshToken);
    }

    [Theory]
    [InlineData(false, "stored-password-hash", true)]
    [InlineData(true, null, false)]
    [InlineData(true, "stored-password-hash", false)]
    public async Task InvalidAccountStatesDoNotCreateSession(
        bool isActive,
        string? storedHash,
        bool passwordMatches)
    {
        var fixture = new AuthenticationFixture(
            new LoginAccount(Guid.NewGuid(), isActive, storedHash));
        fixture.PasswordVerifier.Result = passwordMatches;

        var result = await fixture.Service.LoginAsync(
            new LoginRequest("user@example.com", "password"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.True(fixture.PasswordVerifier.WasCalled);
        Assert.Null(fixture.SessionStore.CreatedRefreshToken);
    }

    [Fact]
    public async Task BlockedEnteredEmailCannotLoginEvenWithValidCredentials()
    {
        var fixture = new AuthenticationFixture(
            new LoginAccount(Guid.NewGuid(), true, "stored-password-hash"));
        fixture.LoginAttemptLimiter.Blocked = true;

        var result = await fixture.Service.LoginAsync(
            new LoginRequest("USER@example.com", "valid-password"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal("user@example.com", fixture.LoginAttemptLimiter.CheckedEmail);
        Assert.Equal("user@example.com", fixture.LoginAttemptLimiter.FailureEmail);
        Assert.Null(fixture.SessionStore.CreatedRefreshToken);
    }

    [Fact]
    public async Task SuccessfulRefreshUsesOnlyHashForLookupAndReturnsReplacement()
    {
        var userId = Guid.NewGuid();
        var fixture = new AuthenticationFixture(account: null);
        fixture.SessionStore.RotationResult = RefreshRotationResult.Succeeded(userId);

        var result = await fixture.Service.RefreshAsync(
            new RefreshRequest("presented-refresh-token"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal("hash:presented-refresh-token", fixture.SessionStore.RotatedTokenHash);
        Assert.Equal("generated-refresh-hash", fixture.SessionStore.ReplacementToken?.Hash);
        Assert.Equal(userId, fixture.AccessTokenIssuer.UserId);
        Assert.Equal("plain-refresh-token", result.Tokens?.RefreshToken);
    }

    [Fact]
    public async Task FailedRefreshDoesNotIssueAccessToken()
    {
        var fixture = new AuthenticationFixture(account: null);
        fixture.SessionStore.RotationResult = RefreshRotationResult.Failed(
            RefreshRotationStatus.Revoked);

        var result = await fixture.Service.RefreshAsync(
            new RefreshRequest("replayed-token"),
            CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Null(fixture.AccessTokenIssuer.UserId);
    }

    [Fact]
    public async Task LogoutHashesPresentedTokenBeforeStoreCall()
    {
        var fixture = new AuthenticationFixture(account: null);

        await fixture.Service.LogoutAsync(
            new LogoutRequest("logout-token"),
            CancellationToken.None);

        Assert.Equal("hash:logout-token", fixture.SessionStore.RevokedTokenHash);
        Assert.Equal(Now, fixture.SessionStore.RevokedAt);
    }

    private sealed class AuthenticationFixture
    {
        public AuthenticationFixture(LoginAccount? account)
        {
            SessionStore.Account = account;
            Service = new AuthenticationService(
                SessionStore,
                PasswordVerifier,
                RefreshTokenProtector,
                AccessTokenIssuer,
                LoginAttemptLimiter,
                new AuthenticationConfiguration(TimeSpan.FromDays(14)),
                new FixedTimeProvider(Now));
        }

        public FakeSessionStore SessionStore { get; } = new();

        public FakePasswordVerifier PasswordVerifier { get; } = new();

        public FakeRefreshTokenProtector RefreshTokenProtector { get; } = new();

        public FakeAccessTokenIssuer AccessTokenIssuer { get; } = new();

        public FakeLoginAttemptLimiter LoginAttemptLimiter { get; } = new();

        public AuthenticationService Service { get; }
    }

    private sealed class FakeSessionStore : IAuthenticationSessionStore
    {
        public LoginAccount? Account { get; set; }

        public string? LastNormalizedEmail { get; private set; }

        public Guid? CreatedUserId { get; private set; }

        public Guid? CreatedSessionId { get; private set; }

        public NewRefreshToken? CreatedRefreshToken { get; private set; }

        public string? RotatedTokenHash { get; private set; }

        public NewRefreshToken? ReplacementToken { get; private set; }

        public RefreshRotationResult RotationResult { get; set; } =
            RefreshRotationResult.Failed(RefreshRotationStatus.NotFound);

        public string? RevokedTokenHash { get; private set; }

        public DateTimeOffset? RevokedAt { get; private set; }

        public Task<LoginAccount?> FindLoginAccountAsync(
            string normalizedEmail,
            CancellationToken cancellationToken)
        {
            LastNormalizedEmail = normalizedEmail;
            return Task.FromResult(Account);
        }

        public Task CreateSessionAsync(
            Guid userId,
            Guid sessionId,
            NewRefreshToken refreshToken,
            CancellationToken cancellationToken)
        {
            CreatedUserId = userId;
            CreatedSessionId = sessionId;
            CreatedRefreshToken = refreshToken;
            return Task.CompletedTask;
        }

        public Task<RefreshRotationResult> RotateRefreshTokenAsync(
            string currentTokenHash,
            NewRefreshToken replacementToken,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            RotatedTokenHash = currentTokenHash;
            ReplacementToken = replacementToken;
            return Task.FromResult(RotationResult);
        }

        public Task RevokeSessionAsync(
            string tokenHash,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            RevokedTokenHash = tokenHash;
            RevokedAt = now;
            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordVerifier : IPasswordVerifier
    {
        public bool Result { get; set; } = true;

        public bool WasCalled { get; private set; }

        public string? StoredHash { get; private set; }

        public Guid? UserId { get; private set; }

        public string? ProvidedPassword { get; private set; }

        public bool Verify(
            Guid? userId,
            string? storedPasswordHash,
            string providedPassword)
        {
            WasCalled = true;
            UserId = userId;
            StoredHash = storedPasswordHash;
            ProvidedPassword = providedPassword;
            return Result;
        }
    }

    private sealed class FakeRefreshTokenProtector : IRefreshTokenProtector
    {
        public GeneratedRefreshToken Generate() =>
            new("plain-refresh-token", "generated-refresh-hash");

        public string Hash(string plainTextToken) => $"hash:{plainTextToken}";
    }

    private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public Guid? UserId { get; private set; }

        public IssuedAccessToken Issue(Guid userId, DateTimeOffset issuedAt)
        {
            UserId = userId;
            return new IssuedAccessToken("access-token", 900);
        }
    }

    private sealed class FakeLoginAttemptLimiter : ILoginAttemptLimiter
    {
        public bool Blocked { get; set; }

        public string? CheckedEmail { get; private set; }

        public string? FailureEmail { get; private set; }

        public string? ResetEmail { get; private set; }

        public bool IsBlocked(string normalizedEnteredEmail)
        {
            CheckedEmail = normalizedEnteredEmail;
            return Blocked;
        }

        public void RecordFailure(string normalizedEnteredEmail)
        {
            FailureEmail = normalizedEnteredEmail;
        }

        public void Reset(string normalizedEnteredEmail)
        {
            ResetEmail = normalizedEnteredEmail;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
