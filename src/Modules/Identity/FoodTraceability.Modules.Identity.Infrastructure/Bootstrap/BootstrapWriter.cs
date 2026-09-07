using FoodTraceability.Modules.Identity.Application.Bootstrap;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Infrastructure.Bootstrap;

internal sealed class BootstrapWriter(IdentityDbContext dbContext) : IBootstrapWriter
{
    public async Task AddAsync(
        User user,
        UserCredential credential,
        PlatformRoleAssignment assignment,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        dbContext.Users.Add(user);
        dbContext.UserCredentials.Add(credential);
        dbContext.PlatformRoleAssignments.Add(assignment);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
