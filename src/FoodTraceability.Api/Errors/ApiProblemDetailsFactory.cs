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
    private const string AuthorizationDeniedTitle = "Access is forbidden.";
    private const string AuthorizationDeniedErrorCode = "AUTHORIZATION_DENIED";
    private const string RateLimitExceededTitle = "Too many requests.";
    private const string RateLimitExceededDetail =
        "The request rate limit has been exceeded. Retry after the current window.";
    private const string RateLimitExceededErrorCode = "RATE_LIMIT_EXCEEDED";
    private const string UnhandledErrorTitle = "An unexpected error occurred.";
    private const string UnhandledErrorCode = "UNHANDLED_ERROR";

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

    public ProblemDetails CreateAuthorizationDenied(HttpContext httpContext) =>
        CreateApiProblemDetails(
            httpContext,
            StatusCodes.Status403Forbidden,
            AuthorizationDeniedTitle,
            AuthorizationDeniedErrorCode);

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
