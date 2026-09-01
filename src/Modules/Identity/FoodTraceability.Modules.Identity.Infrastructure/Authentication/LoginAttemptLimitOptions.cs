namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class LoginAttemptLimitOptions
{
    public const string SectionName = "Authentication:LoginAttempts";

    public int PermitLimit { get; init; }

    public int WindowSeconds { get; init; }
}
