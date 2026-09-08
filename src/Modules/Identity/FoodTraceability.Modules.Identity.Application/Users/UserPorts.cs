using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Users;

public interface IUserReader
{
    Task<UserDetails?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);
}

public interface IUserWriter
{
    Task AddAsync(
        User user,
        UserCredential credential,
        CancellationToken cancellationToken);
}
