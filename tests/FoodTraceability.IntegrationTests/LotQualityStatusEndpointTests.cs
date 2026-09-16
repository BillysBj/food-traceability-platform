using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.Api.Contracts.Lots;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LotQualityStatusEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";

    [Fact]
    public async Task CreateAndGetReturnPendingWithoutQualityReadPermission()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var token = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Producer, token);

        var created = await CreateLotAsync(client, setup, token);
        var retrieved = await GetLotAsync(client, setup, created.Id, token);

        Assert.Equal("PENDING", created.QualityStatus);
        Assert.Equal(created.Id, retrieved.Id);
        Assert.Equal("PENDING", retrieved.QualityStatus);
    }

    [Fact]
    public async Task GetReturnsPersistedBlockedAndReleasedStatusWithoutQualityReadPermission()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var reader = factory.CreateClient();
        using var qualityManager = factory.CreateClient();
        var token = factory.RequestCancellationToken;
        await AuthenticateAsync(reader, setup.Producer, token);
        await AuthenticateAsync(qualityManager, setup.QualityManager, token);
        var lot = await CreateLotAsync(reader, setup, token);

        var block = await BlockLotAsync(qualityManager, setup, lot.Id, token);
        var blockedLot = await GetLotAsync(reader, setup, lot.Id, token);

        Assert.Equal("BLOCKED", block.QualityStatus);
        Assert.Equal(lot.Id, blockedLot.Id);
        Assert.Equal(block.QualityStatus, blockedLot.QualityStatus);

        using var releaseResponse = await qualityManager.PostAsync(
            $"{CollectionPath(setup)}/{lot.Id}/blocks/{block.Id}/release", null, token);
        Assert.Equal(HttpStatusCode.OK, releaseResponse.StatusCode);
        var released = await releaseResponse.Content.ReadFromJsonAsync<ReleasedLotBlockResponse>(token);
        Assert.NotNull(released);
        var releasedLot = await GetLotAsync(reader, setup, lot.Id, token);

        Assert.Equal("RELEASED", released.QualityStatus);
        Assert.Equal(lot.Id, releasedLot.Id);
        Assert.Equal(released.QualityStatus, releasedLot.QualityStatus);
    }

    [Fact]
    public async Task ListReturnsEachLotsOwnStatusWithoutQualityReadPermission()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var reader = factory.CreateClient();
        using var qualityManager = factory.CreateClient();
        var token = factory.RequestCancellationToken;
        await AuthenticateAsync(reader, setup.Producer, token);
        await AuthenticateAsync(qualityManager, setup.QualityManager, token);
        var blocked = await CreateLotAsync(reader, setup, token);
        var pending = await CreateLotAsync(reader, setup, token);
        await BlockLotAsync(qualityManager, setup, blocked.Id, token);

        using var response = await reader.GetAsync($"{CollectionPath(setup)}?page=1&pageSize=10", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<LotListResponse>(token);
        Assert.NotNull(page);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.All(page.Items, lot => Assert.Equal(setup.OrganizationId, lot.OrganizationId));
        Assert.Equal("BLOCKED", Assert.Single(page.Items, lot => lot.Id == blocked.Id).QualityStatus);
        Assert.Equal("PENDING", Assert.Single(page.Items, lot => lot.Id == pending.Id).QualityStatus);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(Environments.Development, new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.QualityConnectionString,
            ["RateLimiting:Authentication:PermitLimit"] = "100",
        });

    private async Task<Setup> CreateSetupAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        await using (var context = database.CreateQualityOrganizationsDbContext())
        {
            context.Organizations.Add(Organization.Create(organizationId, $"Lot status org {organizationId:N}",
                vatId: null, taxNumber: null, email: null, phone: null, now));
            await context.SaveChangesAsync();
        }

        var product = Product.Create(Guid.NewGuid(), $"STATUS-PRODUCT-{Guid.NewGuid():N}", "Lot status product", now);
        var article = Article.Create(Guid.NewGuid(), organizationId, product.Id,
            $"STATUS-SKU-{Guid.NewGuid():N}", gtin: null, now);
        await using (var context = database.CreateQualityCatalogDbContext())
        {
            context.Products.Add(product);
            context.Articles.Add(article);
            await context.SaveChangesAsync();
        }

        // Producer has lot.create and lot.read, but no quality.read permission.
        var producer = await CreateMemberAsync(organizationId, StandardRoleIds.Producer);
        var qualityManager = await CreateMemberAsync(organizationId, StandardRoleIds.QualityManager);
        return new Setup(organizationId, article.Id, producer, qualityManager);
    }

    private async Task<TestAccount> CreateMemberAsync(Guid organizationId, Guid roleId)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"lot-status-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Lot", "Status", now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword), now);
        await using var context = database.CreateQualityIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        context.OrganizationMemberships.Add(OrganizationMembership.Create(userId, organizationId, now));
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(), userId, organizationId, roleId, locationId: null, now));
        await context.SaveChangesAsync();
        return new TestAccount(email);
    }

    private static async Task AuthenticateAsync(HttpClient client, TestAccount account, CancellationToken token)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { account.Email, Password = ValidPassword }, token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(token);
        Assert.NotNull(tokens);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private static async Task<LotResponse> CreateLotAsync(HttpClient client, Setup setup, CancellationToken token)
    {
        using var response = await client.PostAsJsonAsync(CollectionPath(setup),
            new CreateLotRequest(setup.ArticleId, $"STATUS-LOT-{Guid.NewGuid():N}", 10m, "KG"), token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var lot = await response.Content.ReadFromJsonAsync<LotResponse>(token);
        Assert.NotNull(lot);
        return lot;
    }

    private static async Task<LotResponse> GetLotAsync(
        HttpClient client, Setup setup, Guid lotId, CancellationToken token)
    {
        using var response = await client.GetAsync($"{CollectionPath(setup)}/{lotId}", token);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lot = await response.Content.ReadFromJsonAsync<LotResponse>(token);
        Assert.NotNull(lot);
        return lot;
    }

    private static async Task<LotBlockResponse> BlockLotAsync(
        HttpClient client, Setup setup, Guid lotId, CancellationToken token)
    {
        using var response = await client.PostAsJsonAsync($"{CollectionPath(setup)}/{lotId}/blocks",
            new BlockLotRequest("Investigation required"), token);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var block = await response.Content.ReadFromJsonAsync<LotBlockResponse>(token);
        Assert.NotNull(block);
        return block;
    }

    private static string CollectionPath(Setup setup) =>
        $"/api/v1/organizations/{setup.OrganizationId}/lots";

    private sealed record Setup(Guid OrganizationId, Guid ArticleId, TestAccount Producer, TestAccount QualityManager);
    private sealed record TestAccount(string Email);
    private sealed record TokenResponse(string AccessToken);
}
