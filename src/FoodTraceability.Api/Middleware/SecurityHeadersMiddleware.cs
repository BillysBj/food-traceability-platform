namespace FoodTraceability.Api.Middleware;

public sealed class SecurityHeadersMiddleware(
    RequestDelegate next,
    IHostEnvironment environment)
{
    public const string ContentSecurityPolicyHeaderName = "Content-Security-Policy";
    public const string RestrictiveContentSecurityPolicy =
        "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'";
    public const string SwaggerContentSecurityPolicy =
        "default-src 'self'; frame-ancestors 'none'; base-uri 'self'; object-src 'none'; "
        + "script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; "
        + "img-src 'self' data:; font-src 'self' data:";

    private const string ServerHeaderName = "Server";

    public async Task InvokeAsync(HttpContext context)
    {
        var useSwaggerPolicy = environment.IsDevelopment()
            && context.Request.Path.StartsWithSegments("/swagger");

        context.Response.Headers.Remove(ServerHeaderName);
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.Remove(ServerHeaderName);
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["X-Permitted-Cross-Domain-Policies"] = "none";

            // Swashbuckle's development-only UI uses inline scripts and styles. This narrowly
            // scoped policy keeps that UI functional without weakening CSP on any other path.
            headers[ContentSecurityPolicyHeaderName] = useSwaggerPolicy
                ? SwaggerContentSecurityPolicy
                : RestrictiveContentSecurityPolicy;

            return Task.CompletedTask;
        });

        await next(context);
    }
}
