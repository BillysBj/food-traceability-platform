namespace FoodTraceability.Modules.Identity.Application.Users;

public sealed class UserQueryService(IUserReader reader)
{
    public Task<UserDetails?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
        {
            return Task.FromResult<UserDetails?>(null);
        }

        return reader.FindByIdAsync(userId, cancellationToken);
    }
}
