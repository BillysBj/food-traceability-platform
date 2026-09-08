namespace FoodTraceability.Api.Contracts.Users;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    DateTimeOffset CreatedAt);
