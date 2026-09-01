namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string? Issuer { get; init; }

    public string? Audience { get; init; }

    public int AccessTokenMinutes { get; init; }

    public int RefreshTokenDays { get; init; }

    public string? SigningKey { get; init; }
}
