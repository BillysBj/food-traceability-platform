namespace FoodTraceability.Modules.Identity.Application.Authorization;

public interface IEffectiveAuthorizationStore
{
    Task<EffectiveAuthorization?> ResolveAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
