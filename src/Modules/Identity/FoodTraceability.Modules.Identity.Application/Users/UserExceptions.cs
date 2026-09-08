namespace FoodTraceability.Modules.Identity.Application.Users;

public sealed class UserValidationException : Exception
{
    public UserValidationException(string message)
        : base(message)
    {
    }
}

public sealed class UserConflictException : Exception
{
    public UserConflictException(string message)
        : base(message)
    {
    }
}
