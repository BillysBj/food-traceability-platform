using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Modules.Identity.Application.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog.Events;

namespace FoodTraceability.IntegrationTests;

public sealed class AuthenticationApiFoundationTests
{
    private static readonly object UnknownLogin = new
    {
        email = "unknown@example.com",
        password = "not-the-password"
    };

    [Fact]
    public async Task SwaggerDocumentsAllAuthenticationEndpointsAndResponses()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var paths = document.RootElement.GetProperty("paths");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertResponses(paths, "/api/v1/auth/login", "200", "400", "401", "429");
        AssertResponses(paths, "/api/v1/auth/refresh", "200", "400", "401", "429");
        AssertResponses(paths, "/api/v1/auth/logout", "204", "400", "429");
    }

    [Fact]
    public async Task SuccessfulLoginReturnsExpectedTokenShapeWithoutLoggingRefreshToken()
    {
        var userId = Guid.NewGuid();
        await using var factory = new ApiWebApplicationFactory(
            Environments.Development,
            configuration: null,
            configureTestServices: services =>
            {
                services.RemoveAll<IAuthenticationSessionStore>();
                services.RemoveAll<IPasswordVerifier>();
                services.AddScoped<IAuthenticationSessionStore>(_ =>
                    new ActiveAccountSessionStore(userId));
                services.AddSingleton<IPasswordVerifier, AcceptingPasswordVerifier>();
            });
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "user@example.com", password = "valid-password" },
            factory.RequestCancellationToken);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(
            factory.RequestCancellationToken);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens?.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(tokens);
        Assert.Equal(900, tokens.ExpiresIn);
        Assert.Equal(43, tokens.RefreshToken.Length);
        Assert.All(tokens.RefreshToken, character => Assert.True(
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
        Assert.Equal(
            userId.ToString(),
            Assert.Single(jwt.Claims, claim => claim.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.All(
            new[]
            {
                JwtRegisteredClaimNames.Jti,
                JwtRegisteredClaimNames.Iat,
                JwtRegisteredClaimNames.Nbf,
                JwtRegisteredClaimNames.Exp
            },
            claimType => Assert.Single(jwt.Claims, claim => claim.Type == claimType));
        Assert.DoesNotContain(jwt.Claims, claim =>
            claim.Type.Contains("role", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || claim.Type.Contains("organization", StringComparison.OrdinalIgnoreCase));

        var renderedLogs = string.Join(
            Environment.NewLine,
            factory.LogSink.Events.Select(logEvent => logEvent.ToString()));
        Assert.DoesNotContain(tokens.RefreshToken, renderedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingSigningKeyPreventsApplicationStart()
    {
        using var factory = new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = null
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:SigningKey", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SigningKeyShorterThan256BitsPreventsApplicationStart()
    {
        const string shortKey = "short-signing-key";
        using var factory = new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["Jwt:SigningKey"] = shortKey
            });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("at least 256 bits", exception.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(shortKey, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticationPolicyAppliesInAdditionToGlobalLimiter()
    {
        await using var authenticationLimitedFactory = CreateFactory(
            globalPermitLimit: 100,
            authenticationPermitLimit: 1);
        using var authenticationLimitedClient = authenticationLimitedFactory.CreateClient();

        using var firstAuthenticationResponse = await authenticationLimitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            UnknownLogin,
            authenticationLimitedFactory.RequestCancellationToken);
        using var authenticationRejectedResponse = await authenticationLimitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            UnknownLogin,
            authenticationLimitedFactory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, firstAuthenticationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, authenticationRejectedResponse.StatusCode);

        await using var globallyLimitedFactory = CreateFactory(
            globalPermitLimit: 1,
            authenticationPermitLimit: 100);
        using var globallyLimitedClient = globallyLimitedFactory.CreateClient();

        using var firstGlobalResponse = await globallyLimitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            UnknownLogin,
            globallyLimitedFactory.RequestCancellationToken);
        using var globalRejectedResponse = await globallyLimitedClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            UnknownLogin,
            globallyLimitedFactory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, firstGlobalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, globalRejectedResponse.StatusCode);
    }

    [Fact]
    public async Task LoginAttemptLimitIsKeyedByNormalizedEnteredEmailNotResolvedUser()
    {
        var userId = Guid.NewGuid();
        var passwordVerifier = new FailFiveThenSucceedPasswordVerifier();
        await using var factory = new ApiWebApplicationFactory(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["RateLimiting:Authentication:PermitLimit"] = "100"
            },
            configureTestServices: services =>
            {
                services.RemoveAll<IAuthenticationSessionStore>();
                services.RemoveAll<IPasswordVerifier>();
                services.AddScoped<IAuthenticationSessionStore>(_ =>
                    new ActiveAccountSessionStore(userId));
                services.AddSingleton<IPasswordVerifier>(passwordVerifier);
            });
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var failedResponse = await client.PostAsJsonAsync(
                "/api/v1/auth/login",
                new { email = "  FIRST@Example.com ", password = "password" },
                factory.RequestCancellationToken);
            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        using var blockedResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "first@example.COM", password = "password" },
            factory.RequestCancellationToken);
        using var differentEnteredEmailResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email = "second@example.com", password = "password" },
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, blockedResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, differentEnteredEmailResponse.StatusCode);
    }

    [Fact]
    public async Task UnusableStoredPasswordHashReturnsGenericFailureAndIsLoggedWithoutSecrets()
    {
        const string storedPasswordHash = "NOT-A-VALID-HASH";
        const string email = "invalid-hash@example.com";
        const string password = "submitted-password";
        var userId = Guid.NewGuid();
        await using var invalidHashFactory = new ApiWebApplicationFactory(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["RateLimiting:Authentication:PermitLimit"] = "100"
            },
            configureTestServices: services =>
            {
                services.RemoveAll<IAuthenticationSessionStore>();
                services.AddScoped<IAuthenticationSessionStore>(_ =>
                    new UnusableHashSessionStore(userId, storedPasswordHash));
            });
        using var invalidHashClient = invalidHashFactory.CreateClient();

        using var invalidHashResponse = await invalidHashClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            invalidHashFactory.RequestCancellationToken);
        var invalidHashBody = await invalidHashResponse.Content.ReadAsStringAsync(
            invalidHashFactory.RequestCancellationToken);

        await using var unknownAccountFactory = CreateFactory(
            globalPermitLimit: 100,
            authenticationPermitLimit: 100);
        using var unknownAccountClient = unknownAccountFactory.CreateClient();
        using var unknownAccountResponse = await unknownAccountClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            unknownAccountFactory.RequestCancellationToken);
        var unknownAccountBody = await unknownAccountResponse.Content.ReadAsStringAsync(
            unknownAccountFactory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, invalidHashResponse.StatusCode);
        Assert.Equal(unknownAccountResponse.StatusCode, invalidHashResponse.StatusCode);
        Assert.Equal(unknownAccountBody, invalidHashBody);

        var errorEvent = Assert.Single(
            invalidHashFactory.LogSink.Events,
            logEvent => logEvent.Level == LogEventLevel.Error
                && logEvent.MessageTemplate.Text.Contains(
                    "stored password hash",
                    StringComparison.Ordinal));
        Assert.IsType<FormatException>(errorEvent.Exception);
        var loggedUserId = Assert.IsType<ScalarValue>(errorEvent.Properties["UserId"]);
        Assert.Equal(userId, loggedUserId.Value);

        var renderedError = errorEvent.ToString();
        Assert.DoesNotContain(storedPasswordHash, renderedError, StringComparison.Ordinal);
        Assert.DoesNotContain(password, renderedError, StringComparison.Ordinal);
        Assert.DoesNotContain(email, renderedError, StringComparison.Ordinal);
    }

    private static ApiWebApplicationFactory CreateFactory(
        int globalPermitLimit,
        int authenticationPermitLimit)
    {
        return new ApiWebApplicationFactory(
            Environments.Development,
            new Dictionary<string, string?>
            {
                ["RateLimiting:PermitLimit"] = globalPermitLimit.ToString(),
                ["RateLimiting:Authentication:PermitLimit"] =
                    authenticationPermitLimit.ToString()
            },
            configureTestServices: static services =>
            {
                services.RemoveAll<IAuthenticationSessionStore>();
                services.AddScoped<IAuthenticationSessionStore, MissingAccountSessionStore>();
            });
    }

    private static void AssertResponses(
        JsonElement paths,
        string path,
        params string[] expectedStatusCodes)
    {
        var operation = paths.GetProperty(path).GetProperty("post");
        var responses = operation.GetProperty("responses");

        foreach (var statusCode in expectedStatusCodes)
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Swagger operation {path} does not document HTTP {statusCode}.");
        }
    }

    private sealed class MissingAccountSessionStore : IAuthenticationSessionStore
    {
        public Task<LoginAccount?> FindLoginAccountAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<LoginAccount?>(null);

        public Task CreateSessionAsync(
            Guid userId,
            Guid sessionId,
            NewRefreshToken refreshToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No test account can create a session.");

        public Task<RefreshRotationResult> RotateRefreshTokenAsync(
            string currentTokenHash,
            NewRefreshToken replacementToken,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(RefreshRotationResult.Failed(RefreshRotationStatus.NotFound));

        public Task RevokeSessionAsync(
            string tokenHash,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class ActiveAccountSessionStore(Guid userId) : IAuthenticationSessionStore
    {
        public Task<LoginAccount?> FindLoginAccountAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<LoginAccount?>(new LoginAccount(userId, true, "test-hash"));

        public Task CreateSessionAsync(
            Guid createdUserId,
            Guid sessionId,
            NewRefreshToken refreshToken,
            CancellationToken cancellationToken)
        {
            Assert.Equal(userId, createdUserId);
            Assert.NotEqual(Guid.Empty, sessionId);
            Assert.Equal(64, refreshToken.Hash.Length);
            Assert.All(refreshToken.Hash, character => Assert.True(char.IsAsciiHexDigit(character)));
            return Task.CompletedTask;
        }

        public Task<RefreshRotationResult> RotateRefreshTokenAsync(
            string currentTokenHash,
            NewRefreshToken replacementToken,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(RefreshRotationResult.Failed(RefreshRotationStatus.NotFound));

        public Task RevokeSessionAsync(
            string tokenHash,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class UnusableHashSessionStore(
        Guid userId,
        string storedPasswordHash) : IAuthenticationSessionStore
    {
        public Task<LoginAccount?> FindLoginAccountAsync(
            string normalizedEmail,
            CancellationToken cancellationToken) =>
            Task.FromResult<LoginAccount?>(new LoginAccount(userId, true, storedPasswordHash));

        public Task CreateSessionAsync(
            Guid createdUserId,
            Guid sessionId,
            NewRefreshToken refreshToken,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An unusable hash cannot create a session.");

        public Task<RefreshRotationResult> RotateRefreshTokenAsync(
            string currentTokenHash,
            NewRefreshToken replacementToken,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(RefreshRotationResult.Failed(RefreshRotationStatus.NotFound));

        public Task RevokeSessionAsync(
            string tokenHash,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class AcceptingPasswordVerifier : IPasswordVerifier
    {
        public bool Verify(
            Guid? userId,
            string? storedPasswordHash,
            string providedPassword) => true;
    }

    private sealed class FailFiveThenSucceedPasswordVerifier : IPasswordVerifier
    {
        private int _verificationCount;

        public bool Verify(
            Guid? userId,
            string? storedPasswordHash,
            string providedPassword)
        {
            return Interlocked.Increment(ref _verificationCount) > 5;
        }
    }

    private sealed record TokenResponse(
        string AccessToken,
        int ExpiresIn,
        string RefreshToken);
}
