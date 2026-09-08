using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Catalog.Domain;

namespace FoodTraceability.Api.Contracts.Products;

public sealed record CreateProductRequest(
    [Required, StringLength(Product.MaximumProductCodeLength)] string? ProductCode,
    [Required, StringLength(Product.MaximumNameLength)] string? Name);
