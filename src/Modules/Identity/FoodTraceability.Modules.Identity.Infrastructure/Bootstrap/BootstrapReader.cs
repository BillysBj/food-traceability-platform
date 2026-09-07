using FoodTraceability.Modules.Identity.Application.Bootstrap;
using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.Modules.Identity.Infrastructure.Bootstrap;

internal sealed class BootstrapReader(IdentityDbContext dbContext) : IBootstrapReader
{
    public Task<bool> PlatformAdministratorExistsAsync(CancellationToken cancellationToken)
    {
        return dbContext.PlatformRoleAssignments
            .AsNoTracking()
            .AnyAsync(
                assignment => assignment.RoleId == StandardRoleIds.PlatformAdmin,
                cancellationToken);
    }

    public Task<bool> UserExistsAsync(
        string normalizedEmail,
        CancellationToken cancellationToken)
    {
        var email = EmailAddress.Create(normalizedEmail);
        return dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == email, cancellationToken);
    }
}
