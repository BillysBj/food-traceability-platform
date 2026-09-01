namespace FoodTraceability.Modules.Identity.Application.Authentication;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record RefreshRequest(string? RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record AuthenticationTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);

public sealed record AuthenticationResult(
    bool IsSuccessful,
    AuthenticationTokenResponse? Tokens)
{
    public static AuthenticationResult Success(AuthenticationTokenResponse tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        return new AuthenticationResult(true, tokens);
    }

    public static AuthenticationResult Failure { get; } = new(false, null);
}

public sealed record LoginAccount(
    Guid UserId,
    bool IsActive,
    string? PasswordHash);

public sealed record GeneratedRefreshToken(
    string PlainText,
    string Hash);

public sealed record NewRefreshToken(
    Guid Id,
    string Hash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);

public sealed record IssuedAccessToken(
    string Value,
    int ExpiresInSeconds);

public enum RefreshRotationStatus
{
    Succeeded,
    NotFound,
    Revoked,
    Expired,
    UserInactive
}

public sealed record RefreshRotationResult(
    RefreshRotationStatus Status,
    Guid? UserId)
{
    public static RefreshRotationResult Succeeded(Guid userId) =>
        new(RefreshRotationStatus.Succeeded, userId);

    public static RefreshRotationResult Failed(RefreshRotationStatus status) =>
        new(status, null);
}
