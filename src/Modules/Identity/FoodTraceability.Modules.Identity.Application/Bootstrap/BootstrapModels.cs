namespace FoodTraceability.Modules.Identity.Application.Bootstrap;

public sealed record BootstrapPlatformAdministratorCommand(
    string? Email,
    string? FirstName,
    string? LastName,
    string Password)
{
    public override string ToString() =>
        $"{nameof(BootstrapPlatformAdministratorCommand)} {{ Email = {Email}, FirstName = {FirstName}, LastName = {LastName}, Password = *** }}";
}

public sealed record BootstrapResult(Guid UserId, string Email);
