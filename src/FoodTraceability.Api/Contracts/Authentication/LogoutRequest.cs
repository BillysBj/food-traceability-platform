using System.ComponentModel.DataAnnotations;

namespace FoodTraceability.Api.Contracts.Authentication;

public sealed record LogoutRequest(
    [Required]
    string? RefreshToken);
