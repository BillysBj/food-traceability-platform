using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Contracts.Organizations;

public sealed class LocationListRequest
{
    [Range(1, int.MaxValue)]
    [FromQuery(Name = "page")]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    [FromQuery(Name = "pageSize")]
    public int PageSize { get; init; } = 50;
}
