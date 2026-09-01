using FoodTraceability.Modules.Identity.Application.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class AspNetCorePasswordVerifier : IPasswordVerifier
{
    private readonly ILogger<AspNetCorePasswordVerifier> _logger;
    private readonly PasswordHashSubject _subject = new();
    private readonly IPasswordHasher<PasswordHashSubject> _passwordHasher;
    private readonly string _dummyPasswordHash;

    public AspNetCorePasswordVerifier(ILogger<AspNetCorePasswordVerifier> logger)
    {
        _logger = logger;
        _passwordHasher = new PasswordHasher<PasswordHashSubject>();
        _dummyPasswordHash = _passwordHasher.HashPassword(
            _subject,
            "food-traceability-dummy-password-verification");
    }

    public bool Verify(Guid? userId, string? storedPasswordHash, string providedPassword)
    {
        ArgumentNullException.ThrowIfNull(providedPassword);

        var passwordHash = string.IsNullOrWhiteSpace(storedPasswordHash)
            ? _dummyPasswordHash
            : storedPasswordHash;
        PasswordVerificationResult result;
        try
        {
            result = _passwordHasher.VerifyHashedPassword(
                _subject,
                passwordHash,
                providedPassword);
        }
        catch (FormatException exception)
        {
            VerifyDummyHash(providedPassword);
            LogUnusableStoredHash(userId, exception);
            return false;
        }
        catch (ArgumentException exception)
        {
            VerifyDummyHash(providedPassword);
            LogUnusableStoredHash(userId, exception);
            return false;
        }

        return result is PasswordVerificationResult.Success
            or PasswordVerificationResult.SuccessRehashNeeded;
    }

    private void VerifyDummyHash(string providedPassword)
    {
        _passwordHasher.VerifyHashedPassword(
            _subject,
            _dummyPasswordHash,
            providedPassword);
    }

    private void LogUnusableStoredHash(Guid? userId, Exception exception)
    {
        _logger.LogError(
            exception,
            "The stored password hash for user {UserId} is unusable; authentication was rejected.",
            userId);
    }

    private sealed class PasswordHashSubject;
}
