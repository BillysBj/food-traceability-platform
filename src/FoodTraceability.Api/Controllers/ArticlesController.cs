using FoodTraceability.Api.Contracts.Articles;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Security;
using FoodTraceability.Modules.Catalog.Application.Articles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/organizations/{organizationId:guid}/articles")]
public sealed class ArticlesController(
    ArticleQueryService queryService,
    CreateArticleService createService,
    ApiProblemDetailsFactory problemDetailsFactory) : ControllerBase
{
    /// <summary>Creates an article in the organization selected by the route.</summary>
    /// <remarks>
    /// The request body cannot select the organization. A <c>productId</c> that does not
    /// identify an existing product produces HTTP 400 Bad Request, not HTTP 404 Not Found:
    /// <c>productId</c> is a referenced input of the create command and is not the primary
    /// resource addressed by the route. A missing product reference is therefore a validation
    /// error. An article number or GTIN already assigned in this organization produces HTTP
    /// 409 Conflict because it conflicts with the existing state.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="request">The article data, including its referenced global product.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The newly created article.</returns>
    /// <response code="201">Returns the newly created article.</response>
    /// <response code="400">The request or referenced product is invalid.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide article.create permission.</response>
    /// <response code="409">The article number or GTIN is already assigned in the organization.</response>
    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ArticleCreate)]
    [ProducesResponseType<ArticleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ArticleResponse>> Create(
        Guid organizationId,
        CreateArticleRequest request,
        CancellationToken cancellationToken)
    {
        ArticleDetails article;
        try
        {
            article = await createService.CreateAsync(
                new CreateArticleCommand(
                    organizationId,
                    request.ProductId,
                    request.ArticleNumber,
                    request.Gtin),
                cancellationToken);
        }
        catch (ArticleValidationException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateArticleValidationError(
                    HttpContext,
                    exception.Message));
        }
        catch (ArticleConflictException exception)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateArticleConflict(HttpContext, exception.Message));
        }

        var response = MapResponse(article);
        return Created(
            $"/api/v1/organizations/{organizationId}/articles/{article.Id}",
            response);
    }

    /// <summary>Returns an article owned by the organization selected by the route.</summary>
    /// <remarks>
    /// An article owned by another organization and an article that does not exist both produce
    /// the same HTTP 404 Not Found response. This prevents disclosure of cross-tenant article IDs.
    /// </remarks>
    /// <param name="organizationId">The organization identifier from the tenant-scoped route.</param>
    /// <param name="articleId">The article identifier.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>The requested article.</returns>
    /// <response code="200">Returns the requested article.</response>
    /// <response code="401">Authentication is required or the authenticated user is inactive.</response>
    /// <response code="403">The caller lacks organization-wide article.read permission.</response>
    /// <response code="404">No article exists in this organization with the supplied identifier.</response>
    [HttpGet("{articleId:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ArticleRead)]
    [ProducesResponseType<ArticleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ArticleResponse>> GetById(
        Guid organizationId,
        Guid articleId,
        CancellationToken cancellationToken)
    {
        var article = await queryService.FindByIdAsync(
            organizationId,
            articleId,
            cancellationToken);
        if (article is null)
        {
            return problemDetailsFactory.CreateResult(
                problemDetailsFactory.CreateArticleNotFound(HttpContext));
        }

        return Ok(MapResponse(article));
    }

    private static ArticleResponse MapResponse(ArticleDetails article) =>
        new(
            article.Id,
            article.OrganizationId,
            article.ProductId,
            article.ArticleNumber,
            article.Gtin,
            article.CreatedAt);
}
