using FoodTraceability.Modules.Identity.Application.Authentication;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Users;

public sealed class CreateUserService(
    IUserWriter writer,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<UserDetails> CreateAsync(
        CreateUserCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidatePassword(command.Password);
        var email = CreateEmailAddress(command.Email);
        var now = timeProvider.GetUtcNow();
        User user;
        try
        {
            user = User.Create(
                Guid.NewGuid(),
                email,
                command.FirstName,
                command.LastName,
                now);
        }
        catch (IdentityDomainException exception)
        {
            throw new UserValidationException(exception.Message);
        }

        var passwordHash = passwordHasher.Hash(command.Password);
        var credential = UserCredential.Create(user.Id, passwordHash, now, now);

        await writer.AddAsync(user, credential, cancellationToken);

        return new UserDetails(
            user.Id,
            user.Email.Value,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.CreatedAt);
    }

    private static EmailAddress CreateEmailAddress(string? email)
    {
        try
        {
            return EmailAddress.Create(email);
        }
        catch (IdentityDomainException exception)
        {
            throw new UserValidationException(exception.Message);
        }
    }

    private static void ValidatePassword(string? password)
    {
        try
        {
            PasswordPolicy.Validate(password);
        }
        catch (IdentityDomainException exception)
        {
            throw new UserValidationException(exception.Message);
        }
    }
}
