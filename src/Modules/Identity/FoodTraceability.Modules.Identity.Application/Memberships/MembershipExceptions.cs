namespace FoodTraceability.Modules.Identity.Application.Memberships;

public sealed class MembershipValidationException : Exception
{
    public MembershipValidationException(string message)
        : base(message)
    {
    }
}

public sealed class MembershipConflictException : Exception
{
    public MembershipConflictException(string message)
        : base(message)
    {
    }
}

public sealed class MembershipNotFoundException : Exception
{
    public MembershipNotFoundException(string message)
        : base(message)
    {
    }
}
