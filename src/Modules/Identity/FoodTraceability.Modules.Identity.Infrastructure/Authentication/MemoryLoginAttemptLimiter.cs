using FoodTraceability.Modules.Identity.Application.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class MemoryLoginAttemptLimiter(
    IMemoryCache cache,
    TimeProvider timeProvider,
    IOptions<LoginAttemptLimitOptions> options) : ILoginAttemptLimiter
{
    private const string CacheKeyPrefix = "identity:login-attempts:";

    private readonly object _sync = new();
    private readonly LoginAttemptLimitOptions _options = options.Value;

    public bool IsBlocked(string normalizedEnteredEmail)
    {
        ArgumentNullException.ThrowIfNull(normalizedEnteredEmail);

        lock (_sync)
        {
            var state = GetCurrentState(normalizedEnteredEmail);
            return state is not null && state.FailureCount >= _options.PermitLimit;
        }
    }

    public void RecordFailure(string normalizedEnteredEmail)
    {
        ArgumentNullException.ThrowIfNull(normalizedEnteredEmail);

        lock (_sync)
        {
            var state = GetCurrentState(normalizedEnteredEmail);
            var now = timeProvider.GetUtcNow();
            var updatedState = state is null
                ? new LoginAttemptState(1, now)
                : state with { FailureCount = state.FailureCount + 1 };

            cache.Set(
                CreateCacheKey(normalizedEnteredEmail),
                updatedState,
                TimeSpan.FromSeconds(_options.WindowSeconds));
        }
    }

    public void Reset(string normalizedEnteredEmail)
    {
        ArgumentNullException.ThrowIfNull(normalizedEnteredEmail);

        lock (_sync)
        {
            cache.Remove(CreateCacheKey(normalizedEnteredEmail));
        }
    }

    private LoginAttemptState? GetCurrentState(string normalizedEnteredEmail)
    {
        var cacheKey = CreateCacheKey(normalizedEnteredEmail);
        if (!cache.TryGetValue<LoginAttemptState>(cacheKey, out var state) || state is null)
        {
            return null;
        }

        var window = TimeSpan.FromSeconds(_options.WindowSeconds);
        if (timeProvider.GetUtcNow() - state.WindowStartedAt < window)
        {
            return state;
        }

        cache.Remove(cacheKey);
        return null;
    }

    private static string CreateCacheKey(string normalizedEnteredEmail) =>
        CacheKeyPrefix + normalizedEnteredEmail;

    private sealed record LoginAttemptState(
        int FailureCount,
        DateTimeOffset WindowStartedAt);
}
