using FoodTraceability.Modules.Identity.Application.Authentication;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Modules.Identity.Application.Bootstrap;

public sealed class BootstrapPlatformAdministratorService(
    IBootstrapReader reader,
    IBootstrapWriter writer,
    IPasswordHasher passwordHasher,
    TimeProvider timeProvider)
{
    public async Task<BootstrapResult> BootstrapAsync(
        BootstrapPlatformAdministratorCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        ValidatePassword(command.Password);
        var email = CreateEmailAddress(command.Email);

        if (await reader.PlatformAdministratorExistsAsync(cancellationToken))
        {
            throw new PlatformAdministratorAlreadyExistsException(
                "A platform administrator already exists.");
        }

        if (await reader.UserExistsAsync(email.Value, cancellationToken))
        {
            throw new BootstrapValidationException(
                "A user with the supplied email address already exists.");
        }

        var now = timeProvider.GetUtcNow();
        User user;
        try
        {
            user = User.Create(Guid.NewGuid(), email, command.FirstName, command.LastName, now);
        }
        catch (IdentityDomainException exception)
        {
            throw new BootstrapValidationException(exception.Message);
        }

        var passwordHash = passwordHasher.Hash(command.Password);
        var credential = UserCredential.Create(user.Id, passwordHash, now, now);
        var assignment = PlatformRoleAssignment.Create(
            user.Id,
            StandardRoleIds.PlatformAdmin,
            now);

        await writer.AddAsync(user, credential, assignment, cancellationToken);

        return new BootstrapResult(user.Id, email.Value);
    }

    private static EmailAddress CreateEmailAddress(string? email)
    {
        try
        {
            return EmailAddress.Create(email);
        }
        catch (IdentityDomainException exception)
        {
            throw new BootstrapValidationException(exception.Message);
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
            throw new BootstrapValidationException(exception.Message);
        }
    }
}
