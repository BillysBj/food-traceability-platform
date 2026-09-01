using FoodTraceability.Api.Contracts.Authentication;
using FoodTraceability.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using AuthenticationService = FoodTraceability.Modules.Identity.Application.Authentication.AuthenticationService;
using ApplicationLoginRequest = FoodTraceability.Modules.Identity.Application.Authentication.LoginRequest;
using ApplicationLogoutRequest = FoodTraceability.Modules.Identity.Application.Authentication.LogoutRequest;
using ApplicationRefreshRequest = FoodTraceability.Modules.Identity.Application.Authentication.RefreshRequest;

namespace FoodTraceability.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting(ApiSecurityConfiguration.AuthenticationRateLimitPolicyName)]
public sealed class AuthController(AuthenticationService authenticationService) : ControllerBase
{
    private const string AuthenticationFailedErrorCode = "AUTHENTICATION_FAILED";

    /// <summary>Authenticates a local user and creates a refresh-token session.</summary>
    /// <param name="request">The submitted e-mail address and password.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>Short-lived access and rotating refresh tokens.</returns>
    [HttpPost("login")]
    [ProducesResponseType<AuthenticationTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticationTokenResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.LoginAsync(
            new ApplicationLoginRequest(request.Email, request.Password),
            cancellationToken);

        return result.IsSuccessful
            ? Ok(ToResponse(result.Tokens!))
            : AuthenticationFailed();
    }

    /// <summary>Rotates a valid refresh token and issues a new token pair.</summary>
    /// <param name="request">The refresh token received in an earlier response body.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    /// <returns>A new access token and a replacement refresh token.</returns>
    [HttpPost("refresh")]
    [ProducesResponseType<AuthenticationTokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthenticationTokenResponse>> Refresh(
        RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authenticationService.RefreshAsync(
            new ApplicationRefreshRequest(request.RefreshToken),
            cancellationToken);

        return result.IsSuccessful
            ? Ok(ToResponse(result.Tokens!))
            : AuthenticationFailed();
    }

    /// <summary>Revokes the complete refresh-token session.</summary>
    /// <remarks>
    /// Logout is idempotent. Because access tokens are stateless JWTs, access tokens already
    /// issued for this session remain valid until their normal expiration time.
    /// </remarks>
    /// <param name="request">Any refresh token belonging to the session to revoke.</param>
    /// <param name="cancellationToken">Cancels request processing.</param>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authenticationService.LogoutAsync(
            new ApplicationLogoutRequest(request.RefreshToken),
            cancellationToken);

        return NoContent();
    }

    private static AuthenticationTokenResponse ToResponse(
        FoodTraceability.Modules.Identity.Application.Authentication.AuthenticationTokenResponse tokens)
    {
        return new AuthenticationTokenResponse(
            tokens.AccessToken,
            tokens.ExpiresIn,
            tokens.RefreshToken);
    }

    private static ObjectResult AuthenticationFailed()
    {
        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Authentication failed."
        };
        problemDetails.Extensions["errorCode"] = AuthenticationFailedErrorCode;

        var result = new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status401Unauthorized
        };
        result.ContentTypes.Add("application/problem+json");
        return result;
    }
}
