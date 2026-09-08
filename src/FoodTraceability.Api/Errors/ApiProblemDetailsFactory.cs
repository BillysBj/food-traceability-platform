using FoodTraceability.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace FoodTraceability.Api.Errors;

public sealed class ApiProblemDetailsFactory(IOptions<ApiBehaviorOptions> apiBehaviorOptions)
    : ProblemDetailsFactory
{
    private const string ErrorCodeExtensionName = "errorCode";
    private const string CorrelationIdExtensionName = "correlationId";
    private const string TraceIdExtensionName = "traceId";

    private const string AuthenticationFailedTitle = "Authentication failed.";
    private const string AuthenticationFailedErrorCode = "AUTHENTICATION_FAILED";
    private const string AuthenticationRequiredTitle = "Authentication is required.";
    private const string AuthenticationRequiredErrorCode = "AUTHENTICATION_REQUIRED";
    private const string ArticleConflictTitle = "The article conflicts with existing data.";
    private const string ArticleConflictErrorCode = "ARTICLE_CONFLICT";
    private const string ArticleNotFoundTitle = "Article not found.";
    private const string ArticleNotFoundErrorCode = "ARTICLE_NOT_FOUND";
    private const string ArticleValidationTitle = "The article request is invalid.";
    private const string ArticleValidationErrorCode = "ARTICLE_VALIDATION_FAILED";
    private const string AuthorizationDeniedTitle = "Access is forbidden.";
    private const string AuthorizationDeniedErrorCode = "AUTHORIZATION_DENIED";
    private const string LocationNotFoundTitle = "Location not found.";
    private const string LocationNotFoundErrorCode = "LOCATION_NOT_FOUND";
    private const string LotConflictTitle = "The lot conflicts with existing data.";
    private const string LotConflictErrorCode = "LOT_CONFLICT";
    private const string LotNotFoundTitle = "Lot not found.";
    private const string LotNotFoundErrorCode = "LOT_NOT_FOUND";
    private const string LotValidationTitle = "The lot request is invalid.";
    private const string LotValidationErrorCode = "LOT_VALIDATION_FAILED";
    private const string MembershipConflictTitle =
        "The membership conflicts with existing data.";
    private const string MembershipConflictErrorCode = "MEMBERSHIP_CONFLICT";
    private const string MembershipNotFoundTitle = "Membership not found.";
    private const string MembershipNotFoundErrorCode = "MEMBERSHIP_NOT_FOUND";
    private const string MembershipValidationTitle = "The membership request is invalid.";
    private const string MembershipValidationErrorCode = "MEMBERSHIP_VALIDATION_FAILED";
    private const string OrganizationNotFoundTitle = "Organization not found.";
    private const string OrganizationNotFoundErrorCode = "ORGANIZATION_NOT_FOUND";
    private const string OrganizationValidationTitle = "The organization request is invalid.";
    private const string OrganizationValidationErrorCode = "ORGANIZATION_VALIDATION_FAILED";
    private const string ProductConflictTitle = "The product conflicts with existing data.";
    private const string ProductConflictErrorCode = "PRODUCT_CONFLICT";
    private const string ProductNotFoundTitle = "Product not found.";
    private const string ProductNotFoundErrorCode = "PRODUCT_NOT_FOUND";
    private const string ProductValidationTitle = "The product request is invalid.";
    private const string ProductValidationErrorCode = "PRODUCT_VALIDATION_FAILED";
    private const string RateLimitExceededTitle = "Too many requests.";
    private const string RateLimitExceededDetail =
        "The request rate limit has been exceeded. Retry after the current window.";
    private const string RateLimitExceededErrorCode = "RATE_LIMIT_EXCEEDED";
    private const string UnhandledErrorTitle = "An unexpected error occurred.";
    private const string UnhandledErrorCode = "UNHANDLED_ERROR";
    private const string UserConflictTitle = "The user conflicts with existing data.";
    private const string UserConflictErrorCode = "USER_CONFLICT";
    private const string UserNotFoundTitle = "User not found.";
    private const string UserNotFoundErrorCode = "USER_NOT_FOUND";
    private const string UserValidationTitle = "The user request is invalid.";
    private const string UserValidationErrorCode = "USER_VALIDATION_FAILED";

    private readonly ApiBehaviorOptions _apiBehaviorOptions =
        apiBehaviorOptions?.Value ?? throw new ArgumentNullException(nameof(apiBehaviorOptions));

    public ProblemDetails CreateAuthenticationFailed(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status401Unauthorized,
            AuthenticationFailedTitle,
            AuthenticationFailedErrorCode);

    public ProblemDetails CreateAuthenticationRequired(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status401Unauthorized,
            AuthenticationRequiredTitle,
            AuthenticationRequiredErrorCode);

    public ProblemDetails CreateArticleConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            ArticleConflictTitle,
            ArticleConflictErrorCode,
            detail);

    public ProblemDetails CreateArticleNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            ArticleNotFoundTitle,
            ArticleNotFoundErrorCode);

    public ValidationProblemDetails CreateArticleValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Article", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            ArticleValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = ArticleValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateAuthorizationDenied(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status403Forbidden,
            AuthorizationDeniedTitle,
            AuthorizationDeniedErrorCode);

    public ProblemDetails CreateLocationNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            LocationNotFoundTitle,
            LocationNotFoundErrorCode);

    public ProblemDetails CreateLotConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            LotConflictTitle,
            LotConflictErrorCode,
            detail);

    public ProblemDetails CreateLotNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            LotNotFoundTitle,
            LotNotFoundErrorCode);

    public ValidationProblemDetails CreateLotValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Lot", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            LotValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = LotValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateMembershipConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            MembershipConflictTitle,
            MembershipConflictErrorCode,
            detail);

    public ProblemDetails CreateMembershipNotFound(
        HttpContext httpContext,
        string? detail = null) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            MembershipNotFoundTitle,
            MembershipNotFoundErrorCode,
            detail);

    public ValidationProblemDetails CreateMembershipValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Membership", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            MembershipValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = MembershipValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateOrganizationNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            OrganizationNotFoundTitle,
            OrganizationNotFoundErrorCode);

    public ValidationProblemDetails CreateOrganizationValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Organization", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            OrganizationValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = OrganizationValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateProductConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            ProductConflictTitle,
            ProductConflictErrorCode,
            detail);

    public ProblemDetails CreateProductNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            ProductNotFoundTitle,
            ProductNotFoundErrorCode);

    public ValidationProblemDetails CreateProductValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Product", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            ProductValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = ProductValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateRateLimitExceeded(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status429TooManyRequests,
            RateLimitExceededTitle,
            RateLimitExceededErrorCode,
            RateLimitExceededDetail);

    public ProblemDetails CreateUnhandledError(HttpContext httpContext, string? detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status500InternalServerError,
            UnhandledErrorTitle,
            UnhandledErrorCode,
            detail);

    public ProblemDetails CreateUserConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            UserConflictTitle,
            UserConflictErrorCode,
            detail);

    public ProblemDetails CreateUserNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            UserNotFoundTitle,
            UserNotFoundErrorCode);

    public ValidationProblemDetails CreateUserValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("User", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            UserValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = UserValidationErrorCode;
        return problemDetails;
    }

    public ObjectResult CreateResult(ProblemDetails problemDetails)
    {
        ArgumentNullException.ThrowIfNull(problemDetails);

        var result = new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }

    public override ProblemDetails CreateProblemDetails(
        HttpContext httpContext,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode ?? StatusCodes.Status500InternalServerError,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };

        ApplyDefaults(httpContext, problemDetails);
        return problemDetails;
    }

    public override ValidationProblemDetails CreateValidationProblemDetails(
        HttpContext httpContext,
        ModelStateDictionary modelStateDictionary,
        int? statusCode = null,
        string? title = null,
        string? type = null,
        string? detail = null,
        string? instance = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(modelStateDictionary);

        var problemDetails = new ValidationProblemDetails(modelStateDictionary)
        {
            Status = statusCode ?? StatusCodes.Status400BadRequest,
            Title = title,
            Type = type,
            Detail = detail,
            Instance = instance
        };

        ApplyDefaults(httpContext, problemDetails);
        return problemDetails;
    }

    internal static void AddCorrelationIdentifiers(
        ProblemDetails problemDetails,
        HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(problemDetails);
        ArgumentNullException.ThrowIfNull(httpContext);

        problemDetails.Extensions.TryAdd(
            CorrelationIdExtensionName,
            CorrelationIdMiddleware.GetCorrelationId(httpContext));
        problemDetails.Extensions.TryAdd(
            TraceIdExtensionName,
            CorrelationIdMiddleware.GetTraceId(httpContext));
    }

    private ProblemDetails CreateApiProblemDetails(
        HttpContext httpContext,
        int statusCode,
        string title,
        string errorCode,
        string? detail = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };
        problemDetails.Extensions[ErrorCodeExtensionName] = errorCode;
        AddCorrelationIdentifiers(problemDetails, httpContext);
        return problemDetails;
    }

    private void ApplyDefaults(HttpContext httpContext, ProblemDetails problemDetails)
    {
        if (problemDetails.Status is int statusCode
            && _apiBehaviorOptions.ClientErrorMapping.TryGetValue(
                statusCode,
                out var clientErrorData))
        {
            problemDetails.Title ??= clientErrorData.Title;
            problemDetails.Type ??= clientErrorData.Link;
        }

        AddCorrelationIdentifiers(problemDetails, httpContext);
    }
}
