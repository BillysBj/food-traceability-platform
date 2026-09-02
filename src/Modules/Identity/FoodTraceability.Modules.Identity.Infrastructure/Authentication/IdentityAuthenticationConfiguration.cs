using System.Text;
using FoodTraceability.Modules.Identity.Application.Authentication;
using FoodTraceability.Modules.Identity.Application.Authorization;
using FoodTraceability.Modules.Identity.Infrastructure.Authorization;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

public static class IdentityAuthenticationConfiguration
{
    private const int MinimumSigningKeySizeInBytes = 32;

    public static IServiceCollection AddIdentityAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddDbContext<IdentityDbContext>((serviceProvider, options) =>
        {
            var applicationConfiguration = serviceProvider.GetRequiredService<IConfiguration>();
            var connectionString = applicationConfiguration
                .GetConnectionString("FoodTraceability")
                ?? throw new InvalidOperationException(
                    "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

            options.UseFoodTraceabilityPostgres(connectionString, IdentityDbContext.Schema);
        });

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Issuer),
                "Jwt:Issuer must be configured.")
            .Validate(
                static options => !string.IsNullOrWhiteSpace(options.Audience),
                "Jwt:Audience must be configured.")
            .Validate(
                static options => options.AccessTokenMinutes > 0,
                "Jwt:AccessTokenMinutes must be greater than zero.")
            .Validate(
                static options => options.RefreshTokenDays > 0,
                "Jwt:RefreshTokenDays must be greater than zero.")
            .Validate(
                static options => options.SigningKey is not null
                    && Encoding.UTF8.GetByteCount(options.SigningKey) >= MinimumSigningKeySizeInBytes,
                "Jwt:SigningKey must contain at least 256 bits (32 bytes).")
            .ValidateOnStart();

        services.AddOptions<LoginAttemptLimitOptions>()
            .Bind(configuration.GetSection(LoginAttemptLimitOptions.SectionName))
            .Validate(
                static options => options.PermitLimit > 0,
                "Authentication:LoginAttempts:PermitLimit must be greater than zero.")
            .Validate(
                static options => options.WindowSeconds > 0,
                "Authentication:LoginAttempts:WindowSeconds must be greater than zero.")
            .ValidateOnStart();

        services.AddMemoryCache();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordVerifier, AspNetCorePasswordVerifier>();
        services.AddSingleton<IRefreshTokenProtector, CryptographicRefreshTokenProtector>();
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
        services.AddSingleton<ILoginAttemptLimiter, MemoryLoginAttemptLimiter>();
        services.AddScoped<IAuthenticationSessionStore, AuthenticationSessionStore>();
        services.AddScoped<AuthenticationService>();
        services.AddScoped<IEffectiveAuthorizationStore, EffectiveAuthorizationStore>();
        services.AddScoped<EffectiveAuthorizationService>();
        services.AddSingleton(serviceProvider =>
        {
            var jwtOptions = serviceProvider.GetRequiredService<IOptions<JwtOptions>>().Value;
            return new AuthenticationConfiguration(
                TimeSpan.FromDays(jwtOptions.RefreshTokenDays));
        });

        return services;
    }
}
