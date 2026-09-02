using FoodTraceability.Api.Errors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace FoodTraceability.Api.Security;

internal sealed class ProblemDetailsAuthorizationResultHandler(
    IProblemDetailsService problemDetailsService,
    ApiProblemDetailsFactory problemDetailsFactory)
    : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        var invalidPrincipal = authorizeResult.AuthorizationFailure?.FailureReasons.Any(reason =>
            string.Equals(
                reason.Message,
                DatabaseAuthorizationHandler.InvalidPrincipalFailureReason,
                StringComparison.Ordinal)) == true;

        var authenticationRequired = authorizeResult.Challenged || invalidPrincipal;
        if (authenticationRequired)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var problemDetails = authenticationRequired
            ? problemDetailsFactory.CreateAuthenticationRequired(context)
            : problemDetailsFactory.CreateAuthorizationDenied(context);
        context.Response.StatusCode = problemDetails.Status
            ?? throw new InvalidOperationException("Problem details status is required.");
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = problemDetails
        });
    }
}
