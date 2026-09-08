using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class PlatformUserEndpointTests(PostgreSqlContainerFixture database)
{
    private const string AdministratorPassword = "Valid-administrator-password-42!";
    private const string InitialPassword = "Valid-initial-password-42!";

    [Fact]
    public async Task PlatformAdminCreatesUserWithLocationHeaderAndCredential()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest();

        using var response = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            request,
            factory.RequestCancellationToken);
        var body = await ReadUserAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            $"{PlatformUserCollectionPath}/{body.Id}",
            response.Headers.Location?.OriginalString);
        Assert.Equal(request.Email, body.Email);
        Assert.Equal(request.FirstName, body.FirstName);
        Assert.Equal(request.LastName, body.LastName);
        Assert.True(body.IsActive);

        await using var context = database.CreateIdentityDbContext();
        var persistedUser = await context.Users
            .AsNoTracking()
            .SingleAsync(user => user.Id == body.Id);
        var persistedCredential = await context.UserCredentials
            .AsNoTracking()
            .SingleAsync(credential => credential.UserId == body.Id);
        Assert.Equal(body.Email, persistedUser.Email.Value);
        Assert.NotEqual(request.Password, persistedCredential.PasswordHash);
        Assert.Empty(await context.OrganizationMemberships
            .Where(membership => membership.UserId == body.Id)
            .ToArrayAsync());
        Assert.Empty(await context.OrganizationRoleAssignments
            .Where(assignment => assignment.UserId == body.Id)
            .ToArrayAsync());
        Assert.Empty(await context.PlatformRoleAssignments
            .Where(assignment => assignment.UserId == body.Id)
            .ToArrayAsync());
    }

    [Fact]
    public async Task LocationHeaderReturnsTheCreatedUser()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);
        var created = await ReadUserAsync(createResponse, factory.RequestCancellationToken);
        var location = Assert.IsType<Uri>(createResponse.Headers.Location);

        using var getResponse = await client.GetAsync(
            location,
            factory.RequestCancellationToken);
        var read = await ReadUserAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created, read);
    }

    [Fact]
    public async Task UnauthenticatedPostReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnauthenticatedGetReturns401()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            PlatformUserPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenReturns401()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        await DeactivateUserAsync(account.UserId);

        using var response = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationScopedUserManageDoesNotAuthorizePost()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.Id,
            StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationScopedUserReadDoesNotAuthorizeGet()
    {
        var account = await CreateAccountAsync();
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            account.UserId,
            organization.Id,
            StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            PlatformUserPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutPlatformPermissionsReturns403FromBothEndpoints()
    {
        var account = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var postResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            PlatformUserPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            postResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            getResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DuplicateEmailReturns409WithoutCreatingAnotherUserOrCredential()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest();

        using var firstResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            request,
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            request,
            factory.RequestCancellationToken);
        var problemBody = await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "USER_CONFLICT",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.DoesNotContain(request.Password, problemBody, StringComparison.Ordinal);
        AssertSecretIsNotLogged(factory, request.Password);
        var email = EmailAddress.Create(request.Email);
        await using var context = database.CreateIdentityDbContext();
        var users = await context.Users
            .AsNoTracking()
            .Where(user => user.Email == email)
            .Select(user => user.Id)
            .ToArrayAsync();
        var userId = Assert.Single(users);
        Assert.Equal(
            1,
            await context.UserCredentials.CountAsync(
                credential => credential.UserId == userId));
    }

    [Fact]
    public async Task DuplicateEmailWithDifferentCasingReturns409()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var suffix = Guid.NewGuid().ToString("N");
        var firstRequest = ValidRequest($"Mixed.Case-{suffix}@Example.COM");
        var secondRequest = ValidRequest($"mixed.case-{suffix}@example.com");

        using var firstResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            firstRequest,
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            secondRequest,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var problemBody = await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "USER_CONFLICT",
            factory.RequestCancellationToken);
        Assert.DoesNotContain(secondRequest.Password, problemBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ShortPasswordReturns400WithoutCreatingUser()
    {
        const string shortPassword = "short-password";
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest(password: shortPassword);

        using var response = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            request,
            factory.RequestCancellationToken);
        var problemBody = await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "USER_VALIDATION_FAILED",
            factory.RequestCancellationToken);

        Assert.DoesNotContain(shortPassword, problemBody, StringComparison.Ordinal);
        AssertSecretIsNotLogged(factory, shortPassword);
        var email = EmailAddress.Create(request.Email);
        await using var context = database.CreateIdentityDbContext();
        Assert.False(await context.Users.AnyAsync(user => user.Email == email));
    }

    [Fact]
    public async Task UnknownUserReturns404WithUserNotFoundCode()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            PlatformUserPath(Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "USER_NOT_FOUND",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserResponseAndLogsContainNeitherPasswordNorHash()
    {
        const string password = "recognizable-user-secret-583901!";
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(password: password),
            factory.RequestCancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(responseBody);
        var propertyNames = document.RootElement
            .EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            ["createdAt", "email", "firstName", "id", "isActive", "lastName"],
            propertyNames);
        Assert.DoesNotContain(password, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain(
            propertyNames,
            name => name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || name.Contains("hash", StringComparison.OrdinalIgnoreCase));
        AssertSecretIsNotLogged(factory, password);
    }

    [Fact]
    public async Task CreatedUserCanLogInWithInitialPassword()
    {
        const string password = "login-compatible-initial-password-42!";
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);
        var request = ValidRequest(password: password);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            request,
            factory.RequestCancellationToken);
        using var loginClient = factory.CreateClient();
        using var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { request.Email, request.Password },
            factory.RequestCancellationToken);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenPairResponse>(
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.NotNull(tokens);
        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
    }

    [Fact]
    public async Task PostAndFollowingGetReturnExactlyTheSameCreatedAt()
    {
        var account = await CreateAccountAsync();
        await AddPlatformRoleAsync(account.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, account, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            PlatformUserCollectionPath,
            ValidRequest(),
            factory.RequestCancellationToken);
        var created = await ReadUserAsync(createResponse, factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            Assert.IsType<Uri>(createResponse.Headers.Location),
            factory.RequestCancellationToken);
        var read = await ReadUserAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(created.CreatedAt, read.CreatedAt);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.IdentityConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"platform-user-admin-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Platform",
            "Administrator",
            now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(
            new PasswordHasher<UserCredential>().HashPassword(
                credential,
                AdministratorPassword),
            now);

        await using var context = database.CreateIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        return new TestAccount(userId, email);
    }

    private async Task<Organization> CreateOrganizationAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Platform User Authorization {Guid.NewGuid():N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateIdentityOrganizationsDbContext();
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
    }

    private async Task AddPlatformRoleAsync(Guid userId, Guid roleId)
    {
        await using var context = database.CreateIdentityDbContext();
        context.PlatformRoleAssignments.Add(PlatformRoleAssignment.Create(
            userId,
            roleId,
            DateTimeOffset.UtcNow));
        await context.SaveChangesAsync();
    }

    private async Task AddMembershipAndOrganizationRoleAsync(
        Guid userId,
        Guid organizationId,
        Guid roleId)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateIdentityDbContext();
        context.OrganizationMemberships.Add(OrganizationMembership.Create(
            userId,
            organizationId,
            now));
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(),
            userId,
            organizationId,
            roleId,
            locationId: null,
            now));
        await context.SaveChangesAsync();
    }

    private async Task DeactivateUserAsync(Guid userId)
    {
        await using var context = database.CreateIdentityDbContext();
        var user = await context.Users.SingleAsync(candidate => candidate.Id == userId);
        user.Deactivate(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();
    }

    private static async Task AuthenticateAsync(
        HttpClient client,
        TestAccount account,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new { account.Email, Password = AdministratorPassword },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenPairResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication response body was empty.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);
    }

    private static async Task<UserTestResponse> ReadUserAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<UserTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The user response body was empty.");
    }

    private static async Task<string> AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedErrorCode,
        CancellationToken cancellationToken)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            expectedErrorCode,
            document.RootElement.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("correlationId").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(
            document.RootElement.GetProperty("traceId").GetString()));
        return body;
    }

    private static void AssertSecretIsNotLogged(
        ApiWebApplicationFactory factory,
        string secret)
    {
        var renderedLogs = string.Join(
            Environment.NewLine,
            factory.LogSink.Events.Select(logEvent => logEvent.ToString()));
        Assert.DoesNotContain(secret, renderedLogs, StringComparison.Ordinal);
    }

    private static CreateUserTestRequest ValidRequest(
        string? email = null,
        string password = InitialPassword)
    {
        return new CreateUserTestRequest(
            email ?? $"created-user-{Guid.NewGuid():N}@example.com",
            "Created",
            "User",
            password);
    }

    private static string PlatformUserCollectionPath => "/api/v1/platform/users";

    private static string PlatformUserPath(Guid userId) =>
        $"{PlatformUserCollectionPath}/{userId}";

    private sealed record TestAccount(Guid UserId, string Email);

    private sealed record TokenPairResponse(
        string AccessToken,
        int ExpiresIn,
        string RefreshToken);

    private sealed record CreateUserTestRequest(
        string? Email,
        string? FirstName,
        string? LastName,
        string Password);

    private sealed record UserTestResponse(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        bool IsActive,
        DateTimeOffset CreatedAt);
}

public sealed class PlatformUserApiFoundationTests
{
    [Fact]
    public async Task SwaggerDocumentsPlatformUserContractsResponsesAndBearerSecurity()
    {
        await using var factory = new ApiWebApplicationFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/swagger/v1/swagger.json",
            factory.RequestCancellationToken);
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        var root = document.RootElement;
        var collectionPath = root
            .GetProperty("paths")
            .GetProperty("/api/v1/platform/users");
        var itemPath = root
            .GetProperty("paths")
            .GetProperty("/api/v1/platform/users/{userId}");
        var post = collectionPath.GetProperty("post");
        var get = itemPath.GetProperty("get");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertOperation(post, ["201", "400", "401", "403", "409"]);
        AssertOperation(get, ["200", "401", "403", "404"]);
        AssertSchemaReference(post.GetProperty("responses"), "201", "UserResponse");
        AssertSchemaReference(post.GetProperty("responses"), "400", "ValidationProblemDetails");
        AssertSchemaReference(post.GetProperty("responses"), "401", "ProblemDetails");
        AssertSchemaReference(post.GetProperty("responses"), "403", "ProblemDetails");
        AssertSchemaReference(post.GetProperty("responses"), "409", "ProblemDetails");
        AssertSchemaReference(get.GetProperty("responses"), "200", "UserResponse");
        AssertSchemaReference(get.GetProperty("responses"), "401", "ProblemDetails");
        AssertSchemaReference(get.GetProperty("responses"), "403", "ProblemDetails");
        AssertSchemaReference(get.GetProperty("responses"), "404", "ProblemDetails");

        var requestSchemaReference = post
            .GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.EndsWith("/CreateUserRequest", requestSchemaReference, StringComparison.Ordinal);
        var schemas = root.GetProperty("components").GetProperty("schemas");
        var requestSchema = schemas.GetProperty("CreateUserRequest");
        var requestProperties = requestSchema.GetProperty("properties");
        Assert.True(requestProperties.TryGetProperty("email", out _));
        Assert.True(requestProperties.TryGetProperty("firstName", out _));
        Assert.True(requestProperties.TryGetProperty("lastName", out _));
        var passwordProperty = requestProperties.GetProperty("password");
        Assert.False(passwordProperty.TryGetProperty("maxLength", out _));

        var responseProperties = schemas
            .GetProperty("UserResponse")
            .GetProperty("properties");
        Assert.True(responseProperties.TryGetProperty("id", out _));
        Assert.True(responseProperties.TryGetProperty("email", out _));
        Assert.True(responseProperties.TryGetProperty("firstName", out _));
        Assert.True(responseProperties.TryGetProperty("lastName", out _));
        Assert.True(responseProperties.TryGetProperty("isActive", out _));
        Assert.True(responseProperties.TryGetProperty("createdAt", out _));
        Assert.DoesNotContain(
            responseProperties.EnumerateObject(),
            property => property.Name.Contains("password", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("hash", StringComparison.OrdinalIgnoreCase));
        Assert.True(schemas.TryGetProperty("ProblemDetails", out _));
        Assert.True(schemas.TryGetProperty("ValidationProblemDetails", out _));
    }

    private static void AssertOperation(
        JsonElement operation,
        IReadOnlyList<string> expectedStatusCodes)
    {
        var responses = operation.GetProperty("responses");
        foreach (var statusCode in expectedStatusCodes)
        {
            Assert.True(
                responses.TryGetProperty(statusCode, out _),
                $"Platform user operation does not document HTTP {statusCode}.");
        }

        var security = Assert.Single(operation.GetProperty("security").EnumerateArray());
        Assert.True(security.TryGetProperty("Bearer", out _));
    }

    private static void AssertSchemaReference(
        JsonElement responses,
        string statusCode,
        string expectedSchemaName)
    {
        var content = responses.GetProperty(statusCode).GetProperty("content");
        var schemaReferences = content
            .EnumerateObject()
            .Select(mediaType => mediaType.Value.GetProperty("schema").GetProperty("$ref").GetString())
            .ToArray();
        Assert.Contains(
            schemaReferences,
            reference => reference?.EndsWith(
                $"/{expectedSchemaName}",
                StringComparison.Ordinal) == true);
    }
}
