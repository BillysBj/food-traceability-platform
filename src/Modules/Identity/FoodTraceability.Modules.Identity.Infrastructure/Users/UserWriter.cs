using FoodTraceability.Modules.Identity.Application.Users;
using FoodTraceability.Modules.Identity.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.Modules.Identity.Infrastructure.Users;

internal sealed class UserWriter(IdentityDbContext dbContext) : IUserWriter
{
    private const string UserEmailUniqueIndex = "ix_user_email";

    public async Task AddAsync(
        User user,
        UserCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(credential);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            cancellationToken);
        dbContext.Users.Add(user);
        dbContext.UserCredentials.Add(credential);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (IsEmailConflict(exception))
        {
            throw new UserConflictException(
                "A user with the same email address already exists.");
        }
    }

    private static bool IsEmailConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: UserEmailUniqueIndex
        };
    }
}
