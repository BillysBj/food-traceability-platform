using System.ComponentModel.DataAnnotations;

namespace FoodTraceability.Api.Contracts.Authentication;

public sealed record LoginRequest(
    [Required]
    [StringLength(254)]
    string? Email,
    [Required]
    string? Password);
