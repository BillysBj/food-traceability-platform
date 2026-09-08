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
public sealed class PlatformOrganizationMemberEndpointTests(
    PostgreSqlContainerFixture database)
{
    private const string AdministratorPassword = "Valid-administrator-password-42!";
    private const string MemberPassword = "Valid-member-password-42!";

    [Fact]
    public async Task PlatformAdminAddsMemberWithLocationHeader()
    {
        var administrator = await CreateAccountAsync("membership-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("membership-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { member.UserId },
            factory.RequestCancellationToken);
        var body = await ReadMembershipAsync(response, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(
            MemberPath(organization.Id, member.UserId),
            response.Headers.Location?.OriginalString);
        Assert.Equal(organization.Id, body.OrganizationId);
        Assert.Equal(member.UserId, body.UserId);
        Assert.Empty(body.Roles);
    }

    [Fact]
    public async Task LocationHeaderReturnsCreatedMembership()
    {
        var administrator = await CreateAccountAsync("membership-location-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("membership-location-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var createResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { member.UserId },
            factory.RequestCancellationToken);
        var created = await ReadMembershipAsync(
            createResponse,
            factory.RequestCancellationToken);
        var location = Assert.IsType<Uri>(createResponse.Headers.Location);

        using var getResponse = await client.GetAsync(
            location,
            factory.RequestCancellationToken);
        var read = await ReadMembershipAsync(getResponse, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        // Record equality compares the Roles list member by reference, not by its elements.
        Assert.Equal(created.OrganizationId, read.OrganizationId);
        Assert.Equal(created.UserId, read.UserId);
        Assert.Equal(created.CreatedAt, read.CreatedAt);
        Assert.Equal(created.Roles.Count, read.Roles.Count);
        for (var index = 0; index < created.Roles.Count; index++)
        {
            Assert.Equal(created.Roles[index].RoleId, read.Roles[index].RoleId);
            Assert.Equal(created.Roles[index].RoleCode, read.Roles[index].RoleCode);
            Assert.Equal(created.Roles[index].CreatedAt, read.Roles[index].CreatedAt);
        }
    }

    [Fact]
    public async Task AssignedOrganizationRoleAppearsInFollowingGetWithRoleCode()
    {
        var administrator = await CreateAccountAsync("membership-role-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("membership-role-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);
        await AddMemberThroughApiAsync(
            client,
            organization.Id,
            member.UserId,
            factory.RequestCancellationToken);

        using var assignResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.UserId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);
        var assignedMembership = await ReadMembershipAsync(
            assignResponse,
            factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            MemberPath(organization.Id, member.UserId),
            factory.RequestCancellationToken);
        var membership = await ReadMembershipAsync(
            getResponse,
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, assignResponse.StatusCode);
        Assert.Equal(
            MemberPath(organization.Id, member.UserId),
            assignResponse.Headers.Location?.OriginalString);
        var assignedRole = Assert.Single(assignedMembership.Roles);
        Assert.Equal(StandardRoleIds.Producer, assignedRole.RoleId);
        Assert.Equal("PRODUCER", assignedRole.RoleCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var role = Assert.Single(membership.Roles);
        Assert.Equal(StandardRoleIds.Producer, role.RoleId);
        Assert.Equal("PRODUCER", role.RoleCode);
    }

    [Fact]
    public async Task UnauthenticatedCallerReceives401FromAllThreeEndpoints()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        using var addResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organizationId),
            new { UserId = userId },
            factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            MemberPath(organizationId, userId),
            factory.RequestCancellationToken);
        using var roleResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organizationId, userId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            addResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            getResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            roleResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DeactivatedUserWithValidTokenReceives401FromAllThreeEndpoints()
    {
        var administrator = await CreateAccountAsync("membership-disabled-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);
        await DeactivateUserAsync(administrator.UserId);
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var addResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organizationId),
            new { UserId = userId },
            factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            MemberPath(organizationId, userId),
            factory.RequestCancellationToken);
        using var roleResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organizationId, userId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            addResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            getResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            roleResponse,
            HttpStatusCode.Unauthorized,
            "AUTHENTICATION_REQUIRED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationScopedUserManageDoesNotAuthorizeEitherPost()
    {
        var caller = await CreateAccountAsync("membership-org-manage-caller");
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            caller.UserId,
            organization.Id,
            StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, caller, factory.RequestCancellationToken);
        var targetUserId = Guid.NewGuid();

        using var addResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { UserId = targetUserId },
            factory.RequestCancellationToken);
        using var roleResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, targetUserId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            addResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            roleResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task OrganizationScopedUserReadDoesNotAuthorizeGet()
    {
        var caller = await CreateAccountAsync("membership-org-read-caller");
        var organization = await CreateOrganizationAsync();
        await AddMembershipAndOrganizationRoleAsync(
            caller.UserId,
            organization.Id,
            StandardRoleIds.OrganizationAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, caller, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            MemberPath(organization.Id, Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UserWithoutPermissionReceives403FromAllThreeEndpoints()
    {
        var caller = await CreateAccountAsync("membership-no-permission-caller");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, caller, factory.RequestCancellationToken);
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        using var addResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organizationId),
            new { UserId = userId },
            factory.RequestCancellationToken);
        using var getResponse = await client.GetAsync(
            MemberPath(organizationId, userId),
            factory.RequestCancellationToken);
        using var roleResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organizationId, userId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            addResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            getResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
        await AssertProblemAsync(
            roleResponse,
            HttpStatusCode.Forbidden,
            "AUTHORIZATION_DENIED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DuplicateMembershipReturns409()
    {
        var administrator = await CreateAccountAsync("membership-duplicate-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("membership-duplicate-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { member.UserId },
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { member.UserId },
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "MEMBERSHIP_CONFLICT",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task DuplicateRoleAssignmentReturns409()
    {
        var administrator = await CreateAccountAsync("role-duplicate-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("role-duplicate-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);
        await AddMemberThroughApiAsync(
            client,
            organization.Id,
            member.UserId,
            factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.UserId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);
        using var secondResponse = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.UserId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        await AssertProblemAsync(
            secondResponse,
            HttpStatusCode.Conflict,
            "MEMBERSHIP_CONFLICT",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownUserReferenceReturns400()
    {
        var administrator = await CreateAccountAsync("unknown-user-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { UserId = Guid.NewGuid() },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "MEMBERSHIP_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownOrganizationReferenceReturns400()
    {
        var administrator = await CreateAccountAsync("unknown-organization-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var member = await CreateAccountAsync("unknown-organization-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            MemberCollectionPath(Guid.NewGuid()),
            new { member.UserId },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "MEMBERSHIP_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task RoleAssignmentWithoutMembershipReturns404()
    {
        var administrator = await CreateAccountAsync("missing-membership-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var user = await CreateAccountAsync("missing-membership-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, user.UserId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "MEMBERSHIP_NOT_FOUND",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task PlatformRoleCannotBeAssignedWithinOrganization()
    {
        var administrator = await CreateAccountAsync("platform-role-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("platform-role-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);
        await AddMemberThroughApiAsync(
            client,
            organization.Id,
            member.UserId,
            factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.UserId),
            new { RoleId = StandardRoleIds.PlatformAdmin },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "MEMBERSHIP_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownRoleReferenceReturns400()
    {
        var administrator = await CreateAccountAsync("unknown-role-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("unknown-role-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);
        await AddMemberThroughApiAsync(
            client,
            organization.Id,
            member.UserId,
            factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.UserId),
            new { RoleId = Guid.NewGuid() },
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.BadRequest,
            "MEMBERSHIP_VALIDATION_FAILED",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task UnknownMembershipGetReturns404()
    {
        var administrator = await CreateAccountAsync("unknown-membership-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);

        using var response = await client.GetAsync(
            MemberPath(Guid.NewGuid(), Guid.NewGuid()),
            factory.RequestCancellationToken);

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            "MEMBERSHIP_NOT_FOUND",
            factory.RequestCancellationToken);
    }

    [Fact]
    public async Task MembershipResponseDoesNotContainLocationId()
    {
        var administrator = await CreateAccountAsync("response-shape-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        var organization = await CreateOrganizationAsync();
        var member = await CreateAccountAsync("response-shape-target");
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, administrator, factory.RequestCancellationToken);
        await AddMemberThroughApiAsync(
            client,
            organization.Id,
            member.UserId,
            factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.UserId),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);
        var body = await response.Content.ReadAsStringAsync(factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.DoesNotContain("locationId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiOnlySetupPathGrantsCreatedProducerOrganizationLotRead()
    {
        var administrator = await CreateAccountAsync("setup-path-admin");
        await AddPlatformRoleAsync(administrator.UserId, StandardRoleIds.PlatformAdmin);
        await using var factory = CreateFactory();
        using var administratorClient = factory.CreateClient();
        await AuthenticateAsync(
            administratorClient,
            administrator,
            factory.RequestCancellationToken);

        using var organizationResponse = await administratorClient.PostAsJsonAsync(
            "/api/v1/platform/organizations",
            new
            {
                Name = $"API Setup Organization {Guid.NewGuid():N}",
                VatId = (string?)null,
                TaxNumber = (string?)null,
                Email = (string?)null,
                Phone = (string?)null,
            },
            factory.RequestCancellationToken);
        organizationResponse.EnsureSuccessStatusCode();
        var organization = await organizationResponse.Content
            .ReadFromJsonAsync<OrganizationTestResponse>(factory.RequestCancellationToken)
            ?? throw new InvalidOperationException("The organization response body was empty.");

        var memberEmail = $"api-setup-member-{Guid.NewGuid():N}@example.com";
        using var userResponse = await administratorClient.PostAsJsonAsync(
            "/api/v1/platform/users",
            new
            {
                Email = memberEmail,
                FirstName = "API",
                LastName = "Producer",
                Password = MemberPassword,
            },
            factory.RequestCancellationToken);
        userResponse.EnsureSuccessStatusCode();
        var member = await userResponse.Content.ReadFromJsonAsync<UserTestResponse>(
            factory.RequestCancellationToken)
            ?? throw new InvalidOperationException("The user response body was empty.");

        using var membershipResponse = await administratorClient.PostAsJsonAsync(
            MemberCollectionPath(organization.Id),
            new { UserId = member.Id },
            factory.RequestCancellationToken);
        membershipResponse.EnsureSuccessStatusCode();
        using var roleResponse = await administratorClient.PostAsJsonAsync(
            RoleCollectionPath(organization.Id, member.Id),
            new { RoleId = StandardRoleIds.Producer },
            factory.RequestCancellationToken);
        roleResponse.EnsureSuccessStatusCode();

        using var memberClient = factory.CreateClient();
        await AuthenticateAsync(
            memberClient,
            new TestAccount(member.Id, memberEmail, MemberPassword),
            factory.RequestCancellationToken);
        using var lotsResponse = await memberClient.GetAsync(
            $"/api/v1/organizations/{organization.Id}/lots",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, organizationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, userResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, membershipResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, roleResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, lotsResponse.StatusCode);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = database.LotApiConnectionString,
                ["RateLimiting:Authentication:PermitLimit"] = "100",
            });

    private async Task<TestAccount> CreateAccountAsync(string prefix)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"{prefix}-{userId:N}@example.com";
        var user = User.Create(
            userId,
            EmailAddress.Create(email),
            "Test",
            "Account",
            now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(
            new PasswordHasher<UserCredential>().HashPassword(
                credential,
                AdministratorPassword),
            now);

        await using var context = database.CreateLotApiIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        return new TestAccount(userId, email, AdministratorPassword);
    }

    private async Task<Organization> CreateOrganizationAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Membership Organization {Guid.NewGuid():N}",
            vatId: null,
            taxNumber: null,
            email: null,
            phone: null,
            DateTimeOffset.UtcNow);
        await using var context = database.CreateLotApiOrganizationsDbContext();
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        return organization;
    }

    private async Task AddPlatformRoleAsync(Guid userId, Guid roleId)
    {
        await using var context = database.CreateLotApiIdentityDbContext();
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
        await using var context = database.CreateLotApiIdentityDbContext();
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
        await using var context = database.CreateLotApiIdentityDbContext();
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
            new { account.Email, account.Password },
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenPairResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication response body was empty.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            tokens.AccessToken);
    }

    private static async Task AddMemberThroughApiAsync(
        HttpClient client,
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync(
            MemberCollectionPath(organizationId),
            new { UserId = userId },
            cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<MembershipTestResponse> ReadMembershipAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        return await response.Content.ReadFromJsonAsync<MembershipTestResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The membership response body was empty.");
    }

    private static async Task AssertProblemAsync(
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
    }

    private static string MemberCollectionPath(Guid organizationId) =>
        $"/api/v1/platform/organizations/{organizationId}/members";

    private static string MemberPath(Guid organizationId, Guid userId) =>
        $"{MemberCollectionPath(organizationId)}/{userId}";

    private static string RoleCollectionPath(Guid organizationId, Guid userId) =>
        $"{MemberPath(organizationId, userId)}/roles";

    private sealed record TestAccount(Guid UserId, string Email, string Password);

    private sealed record TokenPairResponse(
        string AccessToken,
        int ExpiresIn,
        string RefreshToken);

    private sealed record OrganizationTestResponse(Guid Id);

    private sealed record UserTestResponse(Guid Id);

    private sealed record MembershipTestResponse(
        Guid OrganizationId,
        Guid UserId,
        DateTimeOffset CreatedAt,
        IReadOnlyList<AssignedRoleTestResponse> Roles);

    private sealed record AssignedRoleTestResponse(
        Guid RoleId,
        string RoleCode,
        DateTimeOffset CreatedAt);
}
