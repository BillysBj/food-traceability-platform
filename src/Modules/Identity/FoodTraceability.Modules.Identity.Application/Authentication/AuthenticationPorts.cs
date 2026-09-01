namespace FoodTraceability.Modules.Identity.Application.Authentication;

public interface IAuthenticationSessionStore
{
    Task<LoginAccount?> FindLoginAccountAsync(
        string normalizedEmail,
        CancellationToken cancellationToken);

    Task CreateSessionAsync(
        Guid userId,
        Guid sessionId,
        NewRefreshToken refreshToken,
        CancellationToken cancellationToken);

    Task<RefreshRotationResult> RotateRefreshTokenAsync(
        string currentTokenHash,
        NewRefreshToken replacementToken,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task RevokeSessionAsync(
        string tokenHash,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public interface IPasswordVerifier
{
    bool Verify(Guid? userId, string? storedPasswordHash, string providedPassword);
}

public interface IRefreshTokenProtector
{
    GeneratedRefreshToken Generate();

    string Hash(string plainTextToken);
}

public interface IAccessTokenIssuer
{
    IssuedAccessToken Issue(Guid userId, DateTimeOffset issuedAt);
}

public interface ILoginAttemptLimiter
{
    bool IsBlocked(string normalizedEnteredEmail);

    void RecordFailure(string normalizedEnteredEmail);

    void Reset(string normalizedEnteredEmail);
}
