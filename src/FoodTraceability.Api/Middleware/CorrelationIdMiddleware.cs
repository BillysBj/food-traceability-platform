using System.Diagnostics;
using Serilog.Context;

namespace FoodTraceability.Api.Middleware;

public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string LogPropertyName = "CorrelationId";

    private const int MaximumLength = 128;
    private static readonly object CorrelationIdItemKey = new();

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetIncomingCorrelationId(context) ?? CreateCorrelationId();
        context.Items[CorrelationIdItemKey] = correlationId;
        Activity.Current?.SetTag("correlation.id", correlationId);
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty(LogPropertyName, correlationId))
        {
            await next(context);
        }
    }

    public static string GetCorrelationId(HttpContext context)
    {
        return context.Items.TryGetValue(CorrelationIdItemKey, out var value)
            && value is string correlationId
            ? correlationId
            : CreateCorrelationId();
    }

    public static string GetTraceId(HttpContext context)
    {
        var traceId = Activity.Current?.TraceId.ToString();
        return string.IsNullOrEmpty(traceId) ? context.TraceIdentifier : traceId;
    }

    private static string? GetIncomingCorrelationId(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values) || values.Count != 1)
        {
            return null;
        }

        var value = values[0];
        return IsSafe(value) ? value : null;
    }

    private static bool IsSafe(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaximumLength)
        {
            return false;
        }

        return value.All(static character =>
            char.IsAsciiLetterOrDigit(character)
            || character is '-' or '_' or '.' or ':');
    }

    private static string CreateCorrelationId()
    {
        var traceId = Activity.Current?.TraceId ?? default;
        return traceId != default
            ? traceId.ToString()
            : ActivityTraceId.CreateRandom().ToString();
    }
}
