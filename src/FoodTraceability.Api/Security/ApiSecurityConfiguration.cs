using System.Globalization;
using System.Threading.RateLimiting;
using FoodTraceability.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FoodTraceability.Api.Security;

public static class ApiSecurityConfiguration
{
    public const string CorsPolicyName = "ConfiguredOrigins";

    private const string AllowedOriginsConfigurationKey = "Cors:AllowedOrigins";
    private const string PermitLimitConfigurationKey = "RateLimiting:PermitLimit";
    private const string WindowSecondsConfigurationKey = "RateLimiting:WindowSeconds";
    private const string UnknownClientPartition = "unknown";
    private const string RateLimitErrorCode = "RATE_LIMIT_EXCEEDED";
    private const int DefaultPermitLimit = 100;
    private const int DefaultWindowSeconds = 60;

    public static IServiceCollection AddApiSecurity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            var allowedOrigins = configuration
                .GetSection(AllowedOriginsConfigurationKey)
                .Get<string[]>()?
                .Where(static origin => !string.IsNullOrWhiteSpace(origin))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray() ?? [];

            options.AddPolicy(
                CorsPolicyName,
                policy =>
                {
                    if (allowedOrigins.Length > 0)
                    {
                        policy
                            .WithOrigins(allowedOrigins)
                            .AllowAnyHeader()
                            .AllowAnyMethod();
                    }
                });
        });

        services.AddRateLimiter(options =>
        {
            var permitLimit = configuration.GetValue<int?>(PermitLimitConfigurationKey)
                ?? DefaultPermitLimit;
            var windowSeconds = configuration.GetValue<int?>(WindowSecondsConfigurationKey)
                ?? DefaultWindowSeconds;

            if (permitLimit <= 0)
            {
                throw new InvalidOperationException(
                    $"Configuration value '{PermitLimitConfigurationKey}' must be greater than zero.");
            }

            if (windowSeconds <= 0)
            {
                throw new InvalidOperationException(
                    $"Configuration value '{WindowSecondsConfigurationKey}' must be greater than zero.");
            }

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? UnknownClientPartition,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        AutoReplenishment = true,
                        PermitLimit = permitLimit,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        Window = TimeSpan.FromSeconds(windowSeconds)
                    }));
            options.OnRejected = WriteRateLimitProblemDetailsAsync;
        });

        return services;
    }

    private static async ValueTask WriteRateLimitProblemDetailsAsync(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds)
                .ToString(CultureInfo.InvariantCulture);
        }

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "The request rate limit has been exceeded. Retry after the current window."
        };
        problemDetails.Extensions["errorCode"] = RateLimitErrorCode;

        var problemDetailsService = httpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails
        });
    }
}
