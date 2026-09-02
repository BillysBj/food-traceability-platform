namespace FoodTraceability.Modules.Identity.Application.Authorization;

public sealed class EffectiveAuthorizationService(IEffectiveAuthorizationStore store)
{
    public Task<EffectiveAuthorization?> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult<EffectiveAuthorization?>(null);
        }

        return store.ResolveAsync(userId, cancellationToken);
    }
}
