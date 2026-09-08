namespace FoodTraceability.Modules.Identity.Application.Users;

public sealed record CreateUserCommand(
    string? Email,
    string? FirstName,
    string? LastName,
    string Password)
{
    public override string ToString() =>
        $"{nameof(CreateUserCommand)} {{ Email = {Email}, FirstName = {FirstName}, LastName = {LastName}, Password = *** }}";
}

public sealed record UserDetails(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTimeOffset CreatedAt);
