using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Identity.Domain;

namespace FoodTraceability.Api.Contracts.Users;

public sealed record CreateUserRequest(
    [Required, StringLength(EmailAddress.MaximumLength)] string? Email,
    [Required, StringLength(User.MaximumNameLength)] string? FirstName,
    [Required, StringLength(User.MaximumNameLength)] string? LastName,
    [Required] string? Password);
