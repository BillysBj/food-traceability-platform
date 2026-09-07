namespace FoodTraceability.Modules.Identity.Application.Bootstrap;

public sealed class BootstrapValidationException : Exception
{
    public BootstrapValidationException(string message)
        : base(message)
    {
    }
}

public sealed class PlatformAdministratorAlreadyExistsException : Exception
{
    public PlatformAdministratorAlreadyExistsException(string message)
        : base(message)
    {
    }
}
