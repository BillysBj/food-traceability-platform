namespace FoodTraceability.Modules.Identity.Application.Authentication;

public sealed class AuthenticationService(
    IAuthenticationSessionStore sessionStore,
    IPasswordVerifier passwordVerifier,
    IRefreshTokenProtector refreshTokenProtector,
    IAccessTokenIssuer accessTokenIssuer,
    ILoginAttemptLimiter loginAttemptLimiter,
    AuthenticationConfiguration configuration,
    TimeProvider timeProvider)
{
    public async Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var normalizedEmail = NormalizeEnteredEmail(request.Email);
        var providedPassword = request.Password ?? string.Empty;
        var account = await sessionStore.FindLoginAccountAsync(
            normalizedEmail,
            cancellationToken);

        // The verifier deliberately performs a real dummy-hash verification when no
        // credential exists. It must run before any failure branch.
        var passwordIsValid = passwordVerifier.Verify(
            account?.UserId,
            account?.PasswordHash,
            providedPassword);
        var loginIsAllowed = account is { IsActive: true }
            && passwordIsValid
            && !loginAttemptLimiter.IsBlocked(normalizedEmail);

        if (!loginIsAllowed)
        {
            loginAttemptLimiter.RecordFailure(normalizedEmail);
            return AuthenticationResult.Failure;
        }

        loginAttemptLimiter.Reset(normalizedEmail);
        return await CreateSessionAsync(account!.UserId, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthenticationResult> RefreshAsync(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var presentedToken = request.RefreshToken ?? string.Empty;
        var currentTokenHash = refreshTokenProtector.Hash(presentedToken);
        var now = timeProvider.GetUtcNow();
        var replacement = CreateNewRefreshToken(now);

        var rotationResult = await sessionStore.RotateRefreshTokenAsync(
            currentTokenHash,
            replacement.PersistedToken,
            now,
            cancellationToken);

        if (rotationResult is not { Status: RefreshRotationStatus.Succeeded, UserId: not null })
        {
            return AuthenticationResult.Failure;
        }

        var accessToken = accessTokenIssuer.Issue(rotationResult.UserId.Value, now);
        return AuthenticationResult.Success(new AuthenticationTokenResponse(
            accessToken.Value,
            accessToken.ExpiresInSeconds,
            replacement.GeneratedToken.PlainText));
    }

    public Task LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var presentedToken = request.RefreshToken ?? string.Empty;
        var tokenHash = refreshTokenProtector.Hash(presentedToken);
        return sessionStore.RevokeSessionAsync(
            tokenHash,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private async Task<AuthenticationResult> CreateSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var refreshToken = CreateNewRefreshToken(now);

        await sessionStore.CreateSessionAsync(
            userId,
            sessionId,
            refreshToken.PersistedToken,
            cancellationToken);

        var accessToken = accessTokenIssuer.Issue(userId, now);
        return AuthenticationResult.Success(new AuthenticationTokenResponse(
            accessToken.Value,
            accessToken.ExpiresInSeconds,
            refreshToken.GeneratedToken.PlainText));
    }

    private RefreshTokenPair CreateNewRefreshToken(DateTimeOffset now)
    {
        var generatedToken = refreshTokenProtector.Generate();
        var persistedToken = new NewRefreshToken(
            Guid.NewGuid(),
            generatedToken.Hash,
            now,
            now.Add(configuration.RefreshTokenLifetime));

        return new RefreshTokenPair(generatedToken, persistedToken);
    }

    private static string NormalizeEnteredEmail(string? email)
    {
        return (email ?? string.Empty).Trim().ToLowerInvariant();
    }

    private sealed record RefreshTokenPair(
        GeneratedRefreshToken GeneratedToken,
        NewRefreshToken PersistedToken);
}
