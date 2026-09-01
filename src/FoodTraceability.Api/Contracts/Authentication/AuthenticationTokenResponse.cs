namespace FoodTraceability.Api.Contracts.Authentication;

public sealed record AuthenticationTokenResponse(
    string AccessToken,
    int ExpiresIn,
    string RefreshToken);
