using FoodTraceability.Modules.Identity.Application.Users;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Identity.Infrastructure.Users;

internal sealed class UserReader(IdentityDbContext dbContext) : IUserReader
{
    public async Task<UserDetails?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);

        return user is null
            ? null
            : new UserDetails(
                user.Id,
                user.Email.Value,
                user.FirstName,
                user.LastName,
                user.IsActive,
                user.CreatedAt);
    }
}
