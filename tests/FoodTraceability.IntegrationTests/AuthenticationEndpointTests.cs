using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FoodTraceability.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class AuthenticationEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task SuccessfulLoginReturnsTokensWithMinimalJwtClaimsAndDoesNotLogRefreshToken()
    {
        var account = await CreateUserAsync(isActive: true, hasCredential: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await LoginAsync(
            client,
            account.Email,
            ValidPassword,
            factory.RequestCancellationToken);
        var tokens = await ReadTokensAsync(response, factory.RequestCancellationToken);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(900, tokens.ExpiresIn);
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal(account.UserId.ToString(), GetSingleClaim(jwt, JwtRegisteredClaimNames.Sub));
        Assert.False(string.IsNullOrWhiteSpace(GetSingleClaim(jwt, JwtRegisteredClaimNames.Jti)));
        Assert.False(string.IsNullOrWhiteSpace(GetSingleClaim(jwt, JwtRegisteredClaimNames.Iat)));
        Assert.False(string.IsNullOrWhiteSpace(GetSingleClaim(jwt, JwtRegisteredClaimNames.Nbf)));
        Assert.False(string.IsNullOrWhiteSpace(GetSingleClaim(jwt, JwtRegisteredClaimNames.Exp)));
        Assert.Equal("FoodTraceability.Api", jwt.Issuer);
        Assert.Equal("FoodTraceability.Client", Assert.Single(jwt.Audiences));
        Assert.DoesNotContain(jwt.Claims, claim => IsAuthorizationClaim(claim.Type));

        var renderedLogs = string.Join(
            Environment.NewLine,
            factory.LogSink.Events.Select(logEvent => logEvent.ToString()));
        Assert.DoesNotContain(tokens.RefreshToken, renderedLogs, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AllLoginCredentialFailuresReturnIdenticalStatusAndBody()
    {
        var activeAccount = await CreateUserAsync(isActive: true, hasCredential: true);
        var disabledAccount = await CreateUserAsync(isActive: false, hasCredential: true);
        var accountWithoutCredential = await CreateUserAsync(isActive: true, hasCredential: false);
        var accountWithUnusableHash = await CreateUserAsync(
            isActive: true,
            hasCredential: true,
            storedPasswordHash: "NOT-A-VALID-HASH");
        var attempts = new[]
        {
            new LoginAttempt(activeAccount.Email, "wrong-password"),
            new LoginAttempt($"unknown-{Guid.NewGuid():N}@example.com", "wrong-password"),
            new LoginAttempt(disabledAccount.Email, ValidPassword),
            new LoginAttempt(accountWithoutCredential.Email, ValidPassword),
            new LoginAttempt(accountWithUnusableHash.Email, ValidPassword)
        };
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var responses = new List<LoginFailure>();

        foreach (var attempt in attempts)
        {
            using var response = await LoginAsync(
                client,
                attempt.Email,
                attempt.Password,
                factory.RequestCancellationToken);
            responses.Add(new LoginFailure(
                response.StatusCode,
                await response.Content.ReadAsStringAsync(factory.RequestCancellationToken)));
        }

        var expected = responses[0];
        Assert.Equal(HttpStatusCode.Unauthorized, expected.StatusCode);
        Assert.All(responses, failure =>
        {
            Assert.Equal(expected.StatusCode, failure.StatusCode);
            Assert.Equal(expected.Body, failure.Body);
        });
        Assert.Contains("\"errorCode\":\"AUTHENTICATION_FAILED\"", expected.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshRotatesTokenAndReplacementCanBeUsed()
    {
        var account = await CreateUserAsync(isActive: true, hasCredential: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var loginResponse = await LoginAsync(
            client,
            account.Email,
            ValidPassword,
            factory.RequestCancellationToken);
        var loginTokens = await ReadTokensAsync(loginResponse, factory.RequestCancellationToken);

        using var firstRefreshResponse = await RefreshAsync(
            client,
            loginTokens.RefreshToken,
            factory.RequestCancellationToken);
        var firstRefreshTokens = await ReadTokensAsync(
            firstRefreshResponse,
            factory.RequestCancellationToken);
        using var secondRefreshResponse = await RefreshAsync(
            client,
            firstRefreshTokens.RefreshToken,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, firstRefreshResponse.StatusCode);
        Assert.NotEqual(loginTokens.RefreshToken, firstRefreshTokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, secondRefreshResponse.StatusCode);
    }

    [Fact]
    public async Task ReplayingRotatedTokenRevokesEntireSessionChain()
    {
        var account = await CreateUserAsync(isActive: true, hasCredential: true);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var loginResponse = await LoginAsync(
            client,
            account.Email,
            ValidPassword,
            factory.RequestCancellationToken);
        var loginTokens = await ReadTokensAsync(loginResponse, factory.RequestCancellationToken);
        using var refreshResponse = await RefreshAsync(
            client,
            loginTokens.RefreshToken,
            factory.RequestCancellationToken);
        var rotatedTokens = await ReadTokensAsync(refreshResponse, factory.RequestCancellationToken);

        using var replayResponse = await RefreshAsync(
            client,
            loginTokens.RefreshToken,
            factory.RequestCancellationToken);
        using var latestTokenResponse = await RefreshAsync(
            client,
            rotatedTokens.RefreshToken,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, latestTokenResponse.StatusCode);
    }

    [Fact]
    public async Task ExpiredRefreshTokenIsRejectedWithoutRotation()
    {
        var account = await CreateUserAsync(isActive: true, hasCredential: false);
        var sessionId = Guid.NewGuid();
        var plainTextToken = CreatePlainTextToken();
        var now = DateTimeOffset.UtcNow;
        await using (var context = database.CreateIdentityDbContext())
        {
            context.RefreshTokens.Add(RefreshToken.Create(
                Guid.NewGuid(),
                account.UserId,
                sessionId,
                HashRefreshToken(plainTextToken),
                now.AddDays(-15),
                now.AddDays(-1)));
            await context.SaveChangesAsync();
        }

        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await RefreshAsync(
            client,
            plainTextToken,
            factory.RequestCancellationToken);

        await using var verificationContext = database.CreateIdentityDbContext();
        var persistedTokens = await verificationContext.RefreshTokens
            .Where(token => token.SessionId == sessionId)
            .ToArrayAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(persistedTokens);
        Assert.Null(persistedTokens[0].RevokedAt);
    }

    [Fact]
    public async Task LogoutRevokesWholeSessionAndIsIdempotentForAllTokenStates()
    {
        var account = await CreateUserAsync(isActive: true, hasCredential: true);
        var expiredToken = await CreateExpiredTokenAsync(account.UserId);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var loginResponse = await LoginAsync(
            client,
            account.Email,
            ValidPassword,
            factory.RequestCancellationToken);
        var loginTokens = await ReadTokensAsync(loginResponse, factory.RequestCancellationToken);
        using var refreshResponse = await RefreshAsync(
            client,
            loginTokens.RefreshToken,
            factory.RequestCancellationToken);
        var rotatedTokens = await ReadTokensAsync(refreshResponse, factory.RequestCancellationToken);

        using var logoutResponse = await LogoutAsync(
            client,
            loginTokens.RefreshToken,
            factory.RequestCancellationToken);
        using var repeatedLogoutResponse = await LogoutAsync(
            client,
            loginTokens.RefreshToken,
            factory.RequestCancellationToken);
        using var unknownLogoutResponse = await LogoutAsync(
            client,
            CreatePlainTextToken(),
            factory.RequestCancellationToken);
        using var expiredLogoutResponse = await LogoutAsync(
            client,
            expiredToken,
            factory.RequestCancellationToken);
        using var refreshAfterLogoutResponse = await RefreshAsync(
            client,
            rotatedTokens.RefreshToken,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, repeatedLogoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknownLogoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, expiredLogoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogoutResponse.StatusCode);
    }

    private ApiWebApplicationFactory CreateFactory()
    {
        return new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.IdentityConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100"
            });
    }

    private async Task<TestAccount> CreateUserAsync(
        bool isActive,
        bool hasCredential,
        string? storedPasswordHash = null)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"auth-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Authentication",
            "Test",
            now);
        if (!isActive)
        {
            user.Deactivate(now);
        }

        await using var context = database.CreateIdentityDbContext();
        context.Users.Add(user);
        if (hasCredential)
        {
            var credential = UserCredential.Create(userId, "temporary-hash", now, now);
            var passwordHash = storedPasswordHash
                ?? new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword);
            credential.ChangePasswordHash(passwordHash, now);
            context.UserCredentials.Add(credential);
        }

        await context.SaveChangesAsync();
        return new TestAccount(userId, email);
    }

    private async Task<string> CreateExpiredTokenAsync(Guid userId)
    {
        var plainTextToken = CreatePlainTextToken();
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateIdentityDbContext();
        context.RefreshTokens.Add(RefreshToken.Create(
            Guid.NewGuid(),
            userId,
            Guid.NewGuid(),
            HashRefreshToken(plainTextToken),
            now.AddDays(-15),
            now.AddDays(-1)));
        await context.SaveChangesAsync();
        return plainTextToken;
    }

    private static Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { email, password },
            cancellationToken);
    }

    private static Task<HttpResponseMessage> RefreshAsync(
        HttpClient client,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/refresh",
            new { refreshToken },
            cancellationToken);
    }

    private static Task<HttpResponseMessage> LogoutAsync(
        HttpClient client,
        string refreshToken,
        CancellationToken cancellationToken)
    {
        return client.PostAsJsonAsync(
            "/api/v1/auth/logout",
            new { refreshToken },
            cancellationToken);
    }

    private static async Task<TokenResponse> ReadTokensAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication response body was empty.");
    }

    private static string GetSingleClaim(JwtSecurityToken jwt, string claimType)
    {
        return Assert.Single(jwt.Claims, claim => claim.Type == claimType).Value;
    }

    private static bool IsAuthorizationClaim(string claimType)
    {
        return claimType.Contains("role", StringComparison.OrdinalIgnoreCase)
            || claimType.Contains("permission", StringComparison.OrdinalIgnoreCase)
            || claimType.Contains("organization", StringComparison.OrdinalIgnoreCase);
    }

    private static string CreatePlainTextToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashRefreshToken(string plainTextToken)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plainTextToken)));
    }

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record LoginAttempt(string Email, string Password);

    private sealed record LoginFailure(HttpStatusCode StatusCode, string Body);

    private sealed record TokenResponse(
        string AccessToken,
        int ExpiresIn,
        string RefreshToken);
}
