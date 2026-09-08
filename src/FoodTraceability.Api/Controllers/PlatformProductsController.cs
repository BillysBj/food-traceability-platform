using FoodTraceability.Api.Contracts.Products;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Catalog.Application.Products;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/platform/products")]
public sealed class PlatformProductsController(
    CreateProductService createService,
    ProductQueryService queryService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates a product in the global platform catalog.</summary>
    /// <remarks>
    /// Product codes are globally unique without regard to casing, while the originally
    /// supplied casing is preserved. These platform endpoints are authorized exclusively
    /// through platform permissions. An organization-scoped assignment of
    /// <c>product.create</c> or <c>product.read</c> does not authorize access under D-27.
    /// Organization-scoped catalog reads require a separate future endpoint.
    /// </remarks>
    /// <param name="request">The global product data.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created product.</returns>
    /// <response code="201">Returns the newly created product.</response>
    /// <response code="400">The product request is invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide product.create permission.</response>
    /// <response code="409">A product with the same product code already exists.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.PlatformProductCreate)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductResponse>> Create(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        ProductDetails product;
        try
        {
            product = await createService.CreateAsync(
                new CreateProductCommand(request.ProductCode, request.Name),
                cancellationToken);
        }
        catch (ProductValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateProductValidationError(
                    HttpContext,
                    exception.Message));
        }
        catch (ProductConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateProductConflict(
                    HttpContext,
                    exception.Message));
        }

        return Created(
            $"/api/v1/platform/products/{product.Id}",
            MapResponse(product));
    }

    /// <summary>Returns a product from the global platform catalog.</summary>
    /// <remarks>
    /// Product codes are globally unique without regard to casing, while the originally
    /// supplied casing is preserved. These platform endpoints are authorized exclusively
    /// through platform permissions. An organization-scoped assignment of
    /// <c>product.read</c> or <c>product.create</c> does not authorize access under D-27.
    /// Organization-scoped catalog reads require a separate future endpoint.
    /// </remarks>
    /// <param name="productId">The global product identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested product.</returns>
    /// <response code="200">Returns the requested product.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks platform-wide product.read permission.</response>
    /// <response code="404">No product exists with the supplied identifier.</response>
    [HttpGet("{productId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.PlatformProductRead)]
    [ProducesResponseType<ProductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductResponse>> GetById(
        Guid productId,
        CancellationToken cancellationToken)
    {
        var product = await queryService.FindByIdAsync(productId, cancellationToken);
        if (product is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateProductNotFound(HttpContext));
        }

        return Ok(MapResponse(product));
    }

    private static ProductResponse MapResponse(ProductDetails product) =>
        new(
            product.Id,
            product.ProductCode,
            product.Name,
            product.CreatedAt);
}
