using System.ComponentModel.DataAnnotations;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Contracts.Lots;

public sealed class LotListRequest
{
    [Range(1, int.MaxValue)]
    [FromQuery(Name = "page")]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = 50;

    [FromQuery(Name = "articleId")]
    public Guid? ArticleId { get; init; }

    [StringLength(Lot.MaximumLotNumberLength)]
    [FromQuery(Name = "lotNumber")]
    public string? LotNumber { get; init; }
}
