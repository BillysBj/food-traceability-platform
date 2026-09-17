using FoodTraceability.Api.Controllers;
using FoodTraceability.Api.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
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
    private const string QualitySampleListLotNotFoundTitle = "Lot not found.";
    private const string QualitySampleListLotNotFoundErrorCode = "QUALITY_SAMPLE_LIST_LOT_NOT_FOUND";
    private const string QualityResultListSampleNotFoundTitle = "Sample not found.";
    private const string QualityResultListSampleNotFoundErrorCode = "QUALITY_RESULT_LIST_SAMPLE_NOT_FOUND";
    private const string SampleConflictTitle = "The sample conflicts with existing data.";
    private const string SampleConflictErrorCode = "SAMPLE_CONFLICT";
    private const string SampleValidationTitle = "The sample request is invalid.";
    private const string SampleValidationErrorCode = "SAMPLE_VALIDATION_FAILED";
    private const string LabResultConflictTitle = "The lab result conflicts with existing data.";
    private const string LabResultConflictErrorCode = "LAB_RESULT_CONFLICT";
    private const string AmbiguousSpecificationTitle = "The quality specification configuration is ambiguous.";
    private const string AmbiguousSpecificationErrorCode = "QUALITY_SPECIFICATION_AMBIGUOUS";
    private const string LabResultSampleNotFoundTitle = "Sample not found.";
    private const string LabResultSampleNotFoundErrorCode = "LAB_RESULT_SAMPLE_NOT_FOUND";
    private const string LabResultValidationTitle = "The lab result request is invalid.";
    private const string LabResultValidationErrorCode = "LAB_RESULT_VALIDATION_FAILED";
    private const string LotBlockConflictTitle = "The lot already has an open block.";
    private const string LotBlockConflictErrorCode = "LOT_BLOCK_CONFLICT";
    private const string LotBlockNotFoundTitle = "Lot not found.";
    private const string LotBlockNotFoundErrorCode = "LOT_BLOCK_LOT_NOT_FOUND";
    private const string LotBlockValidationTitle = "The lot block request is invalid.";
    private const string LotBlockValidationErrorCode = "LOT_BLOCK_VALIDATION_FAILED";
    private const string LotBlockReleaseNotFoundTitle = "Lot block not found.";
    private const string LotBlockReleaseNotFoundErrorCode = "LOT_BLOCK_RELEASE_NOT_FOUND";
    private const string LotBlockReleaseConflictTitle = "The lot block has already been released.";
    private const string LotBlockReleaseConflictErrorCode = "LOT_BLOCK_RELEASE_CONFLICT";
    private const string MembershipConflictTitle =
        "The membership conflicts with existing data.";
    private const string MembershipConflictErrorCode = "MEMBERSHIP_CONFLICT";
    private const string MembershipNotFoundTitle = "Membership not found.";
    private const string MembershipNotFoundErrorCode = "MEMBERSHIP_NOT_FOUND";
    private const string MembershipValidationTitle = "The membership request is invalid.";
    private const string MembershipValidationErrorCode = "MEMBERSHIP_VALIDATION_FAILED";
    private const string OrganizationConflictTitle = "The organization conflicts with existing data.";
    private const string OrganizationConflictErrorCode = "ORGANIZATION_CONFLICT";
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
    private const string TraceabilityEventConflictTitle =
        "The traceability event conflicts with existing data.";
    private const string TraceabilityEventConflictErrorCode =
        "TRACEABILITY_EVENT_CONFLICT";
    private const string TraceabilityEventNotFoundTitle = "Traceability event not found.";
    private const string TraceabilityTraceNotFoundTitle = "Traceability trace not found.";
    private const string TraceabilityTraceNotFoundErrorCode = "TRACEABILITY_TRACE_NOT_FOUND";
    private const string TraceabilityTraceTooLargeTitle = "The trace exceeds the configured node limit.";
    private const string TraceabilityTraceTooLargeErrorCode = "TRACEABILITY_TRACE_TOO_LARGE";
    private const string TraceabilityEventNotFoundErrorCode =
        "TRACEABILITY_EVENT_NOT_FOUND";
    private const string TraceabilityEventValidationTitle =
        "The traceability event request is invalid.";
    private const string TraceabilityEventValidationErrorCode =
        "TRACEABILITY_EVENT_VALIDATION_FAILED";
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

    public ProblemDetails CreateOrganizationConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            OrganizationConflictTitle,
            OrganizationConflictErrorCode,
            detail);

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

    public ProblemDetails CreateTraceabilityEventConflict(
        HttpContext httpContext,
        string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            TraceabilityEventConflictTitle,
            TraceabilityEventConflictErrorCode,
            detail);

    public ProblemDetails CreateTraceabilityEventNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            TraceabilityEventNotFoundTitle,
            TraceabilityEventNotFoundErrorCode);

    public ProblemDetails CreateTraceabilityTraceNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status404NotFound,
            TraceabilityTraceNotFoundTitle,
            TraceabilityTraceNotFoundErrorCode);

    public ProblemDetails CreateTraceabilityTraceTooLarge(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            TraceabilityTraceTooLargeTitle,
            TraceabilityTraceTooLargeErrorCode,
            detail);

    public ValidationProblemDetails CreateTraceabilityEventValidationError(
        HttpContext httpContext,
        string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("TraceabilityEvent", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            TraceabilityEventValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] =
            TraceabilityEventValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateSampleConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status409Conflict,
            SampleConflictTitle,
            SampleConflictErrorCode,
            detail);

    public ProblemDetails CreateQualitySampleListLotNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status404NotFound,
            QualitySampleListLotNotFoundTitle, QualitySampleListLotNotFoundErrorCode);

    public ProblemDetails CreateQualityResultListSampleNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status404NotFound,
            QualityResultListSampleNotFoundTitle, QualityResultListSampleNotFoundErrorCode);

    public ValidationProblemDetails CreateSampleValidationError(HttpContext httpContext, string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Sample", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext,
            modelState,
            StatusCodes.Status400BadRequest,
            SampleValidationTitle,
            detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = SampleValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateLabResultConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status409Conflict,
            LabResultConflictTitle, LabResultConflictErrorCode, detail);

    public ProblemDetails CreateAmbiguousSpecification(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status409Conflict,
            AmbiguousSpecificationTitle, AmbiguousSpecificationErrorCode, detail);

    public ProblemDetails CreateLabResultSampleNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status404NotFound,
            LabResultSampleNotFoundTitle, LabResultSampleNotFoundErrorCode);

    public ValidationProblemDetails CreateLabResultValidationError(HttpContext httpContext, string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("LabResult", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext, modelState, StatusCodes.Status400BadRequest,
            LabResultValidationTitle, detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = LabResultValidationErrorCode;
        return problemDetails;
    }

    public ProblemDetails CreateLotBlockConflict(HttpContext httpContext, string detail) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status409Conflict,
            LotBlockConflictTitle, LotBlockConflictErrorCode, detail);

    public ProblemDetails CreateLotBlockReleaseNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status404NotFound,
            LotBlockReleaseNotFoundTitle, LotBlockReleaseNotFoundErrorCode);

    public ProblemDetails CreateLotBlockReleaseConflict(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status409Conflict,
            LotBlockReleaseConflictTitle, LotBlockReleaseConflictErrorCode);

    public ProblemDetails CreateLotBlockNotFound(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext, StatusCodes.Status404NotFound,
            LotBlockNotFoundTitle, LotBlockNotFoundErrorCode);

    public ValidationProblemDetails CreateLotBlockValidationError(HttpContext httpContext, string detail)
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("LotBlock", detail);
        var problemDetails = CreateValidationProblemDetails(
            httpContext, modelState, StatusCodes.Status400BadRequest,
            LotBlockValidationTitle, detail: detail);
        problemDetails.Extensions[ErrorCodeExtensionName] = LotBlockValidationErrorCode;
        return problemDetails;
    }

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

        // Include automatic model-binding failures (invalid JSON, decimal or timestamp)
        // in the new endpoint's error vocabulary without changing existing endpoints.
        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()
                ?.ControllerTypeInfo.AsType() == typeof(LabResultsController))
        {
            problemDetails.Title = LabResultValidationTitle;
            problemDetails.Extensions[ErrorCodeExtensionName] = LabResultValidationErrorCode;
        }
        else if (httpContext.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()
                     ?.ControllerTypeInfo.AsType() == typeof(LotBlocksController))
        {
            problemDetails.Title = LotBlockValidationTitle;
            problemDetails.Extensions[ErrorCodeExtensionName] = LotBlockValidationErrorCode;
        }

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
