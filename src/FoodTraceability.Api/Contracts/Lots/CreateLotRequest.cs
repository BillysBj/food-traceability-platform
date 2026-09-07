using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Traceability.Domain;

namespace FoodTraceability.Api.Contracts.Lots;

public sealed record CreateLotRequest(
    Guid ArticleId,
    [Required, StringLength(Lot.MaximumLotNumberLength)] string? LotNumber,
    decimal Quantity,
    [Required, StringLength(UnitCode.MaximumLength)] string? UnitCode);
