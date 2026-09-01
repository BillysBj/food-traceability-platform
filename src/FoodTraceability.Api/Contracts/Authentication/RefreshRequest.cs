using System.ComponentModel.DataAnnotations;

namespace FoodTraceability.Api.Contracts.Authentication;

public sealed record RefreshRequest(
    [Required]
    string? RefreshToken);
