using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace FoodTraceability.Api.Security;

internal sealed class ProblemDetailsAuthorizationResultHandler
    : IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    private const string AuthenticationRequiredErrorCode = "AUTHENTICATION_REQUIRED";
    private const string AuthorizationDeniedErrorCode = "AUTHORIZATION_DENIED";

    public Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            return next(context);
        }

        var invalidPrincipal = authorizeResult.AuthorizationFailure?.FailureReasons.Any(reason =>
            string.Equals(
                reason.Message,
                DatabaseAuthorizationHandler.InvalidPrincipalFailureReason,
                StringComparison.Ordinal)) == true;

        return authorizeResult.Challenged || invalidPrincipal
            ? WriteProblemDetailsAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Authentication is required.",
                AuthenticationRequiredErrorCode,
                includeBearerChallenge: true)
            : WriteProblemDetailsAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Access is forbidden.",
                AuthorizationDeniedErrorCode,
                includeBearerChallenge: false);
    }

    private static Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string errorCode,
        bool includeBearerChallenge)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        if (includeBearerChallenge)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
        };
        problemDetails.Extensions["errorCode"] = errorCode;

        return JsonSerializer.SerializeAsync(
            context.Response.Body,
            problemDetails,
            SerializerOptions,
            context.RequestAborted);
    }
}
