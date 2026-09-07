using FoodTraceability.Modules.Identity.Application.Authentication;
using Microsoft.AspNetCore.Identity;

namespace FoodTraceability.Modules.Identity.Infrastructure.Authentication;

internal sealed class AspNetCorePasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<PasswordHashSubject> _passwordHasher = new();
    private readonly PasswordHashSubject _subject = new();

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);
        return _passwordHasher.HashPassword(_subject, password);
    }
}
