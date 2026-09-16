using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Quality.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LotBlockEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";
    private static readonly Guid KilogramId = Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");

    [Fact]
    public async Task QualityManagerCreatesExactlyOneOpenBlockAndBlockedLot()
    {
        var setup = await CreateSetupAsync();
        // Keep JWT timestamps current while exercising sub-microsecond precision.
        var now = TimestampPrecision.TruncateToMicroseconds(DateTimeOffset.UtcNow);
        await using var factory = CreateFactory(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now.AddTicks(7))));
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(BlockPath(setup),
            new BlockLotRequest("  Investigation required  "), factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var block = await ReadBlockAsync(response, factory.RequestCancellationToken);
        Assert.Equal($"{BlockPath(setup)}/{block.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(setup.Lot.Id, block.LotId);
        Assert.Equal("Investigation required", block.Reason);
        Assert.Equal(now, block.BlockedAt);
        Assert.Equal(setup.Account.UserId, block.BlockedBy);
        Assert.Equal("BLOCKED", block.QualityStatus);
        var persisted = Assert.Single(await ReadBlocksAsync(factory, setup.OrganizationId));
        Assert.Equal(block.Id, persisted.Id);
        Assert.Equal(block.LotId, persisted.LotId);
        Assert.Equal(block.Reason, persisted.Reason);
        Assert.Equal(block.BlockedAt, persisted.BlockedAt);
        Assert.Equal(now, persisted.CreatedAt);
        Assert.Equal(block.BlockedBy, persisted.BlockedBy);
        Assert.Null(persisted.ReleasedAt);
        Assert.Null(persisted.ReleasedBy);
        Assert.Equal(LotQualityStatus.Blocked, await ReadStatusAsync(factory, setup));
    }

    [Fact]
    public async Task UserWithoutMembershipGets403WithoutWrites()
    {
        var setup = await CreateSetupAsync();
        var outsider = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, outsider, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(BlockPath(setup),
            new BlockLotRequest("Investigation required"), factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        await AssertUntouchedAsync(factory, setup);
    }

    [Fact]
    public async Task LaboratoryWithReadAndResultCreateButWithoutBlockGets403()
    {
        var setup = await CreateSetupAsync(StandardRoleIds.Laboratory);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(BlockPath(setup),
            new BlockLotRequest("Investigation required"), factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        await AssertUntouchedAsync(factory, setup);
    }

    [Fact]
    public async Task ForeignAndUnknownLotsReturnIdentical404WithoutWrites()
    {
        var own = await CreateSetupAsync();
        var foreign = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, own.Account, factory.RequestCancellationToken);
        var request = new BlockLotRequest("Investigation required");

        using var foreignResponse = await client.PostAsJsonAsync(
            BlockPath(own.OrganizationId, foreign.Lot.Id), request, factory.RequestCancellationToken);
        using var unknownResponse = await client.PostAsJsonAsync(
            BlockPath(own.OrganizationId, Guid.NewGuid()), request, factory.RequestCancellationToken);

        var foreignProblem = await AssertProblemAsync(foreignResponse, HttpStatusCode.NotFound,
            "LOT_BLOCK_LOT_NOT_FOUND", factory.RequestCancellationToken);
        var unknownProblem = await AssertProblemAsync(unknownResponse, HttpStatusCode.NotFound,
            "LOT_BLOCK_LOT_NOT_FOUND", factory.RequestCancellationToken);
        // Only request-specific correlation/trace identifiers differ. Compare every other field.
        Assert.Equal(ComparableProblem(foreignProblem), ComparableProblem(unknownProblem));
        await AssertUntouchedAsync(factory, own);
        await AssertUntouchedAsync(factory, foreign);
    }

    [Fact]
    public async Task SecondBlockReturns409AfterStatusWriteAndKeepsExactlyOneOpenBlock()
    {
        var setup = await CreateSetupAsync();
        var observation = new WriteObservation();
        await using var factory = CreateFactory(services => ObserveWriter(services, observation));
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = new BlockLotRequest("Investigation required");
        using var first = await client.PostAsJsonAsync(BlockPath(setup), request, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var original = await ReadBlockAsync(first, factory.RequestCancellationToken);

        using var second = await client.PostAsJsonAsync(BlockPath(setup),
            request with { Reason = "A second decision" }, factory.RequestCancellationToken);

        await AssertProblemAsync(second, HttpStatusCode.Conflict, "LOT_BLOCK_CONFLICT", factory.RequestCancellationToken);
        Assert.Equal(new[] { LotQualityStatus.Blocked, LotQualityStatus.Blocked }, observation.StatusesBeforeInsert);
        Assert.Equal(1, observation.ConflictsFromRealWriter);
        var persisted = Assert.Single(await ReadBlocksAsync(factory, setup.OrganizationId));
        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal(original.Reason, persisted.Reason);
        Assert.Equal(original.BlockedAt, persisted.BlockedAt);
        Assert.Null(persisted.ReleasedAt);
        Assert.Equal(LotQualityStatus.Blocked, await ReadStatusAsync(factory, setup));
    }

    [Fact]
    public async Task RealUniqueConflictRollsBackObservablePendingToBlockedChange()
    {
        var setup = await CreateSetupAsync();
        var now = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
        var original = LotBlock.Create(Guid.NewGuid(), setup.OrganizationId, setup.Lot.Id,
            "Pre-existing block", now, setup.Account.UserId, now);
        // Deliberately inconsistent fixture, not a supported API workflow: an open block
        // with a PENDING lot makes a failed rollback observable (BLOCKED would hide it).
        await using (var quality = database.CreateQualityDbContext())
        {
            quality.LotBlocks.Add(original);
            await quality.SaveChangesAsync();
        }

        var observation = new WriteObservation();
        await using var factory = CreateFactory(services => ObserveWriter(services, observation));
        Assert.Equal(LotQualityStatus.Pending, await ReadStatusAsync(factory, setup));
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(BlockPath(setup),
            new BlockLotRequest("New decision must fail"), factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "LOT_BLOCK_CONFLICT", factory.RequestCancellationToken);
        // The decorator observes actual database state on the request's transaction,
        // then delegates to the unmodified production writer; no exception is injected.
        Assert.Equal(LotQualityStatus.Blocked, Assert.Single(observation.StatusesBeforeInsert));
        Assert.Equal(1, observation.ConflictsFromRealWriter);
        // Fresh scope: both the intermediate status and attempted new block were rolled back.
        Assert.Equal(LotQualityStatus.Pending, await ReadStatusAsync(factory, setup));
        var persisted = Assert.Single(await ReadBlocksAsync(factory, setup.OrganizationId));
        Assert.Equal(original.Id, persisted.Id);
        Assert.Equal(original.Reason, persisted.Reason);
        Assert.Equal(original.BlockedAt, persisted.BlockedAt);
        Assert.Equal(original.BlockedBy, persisted.BlockedBy);
        Assert.Null(persisted.ReleasedAt);
        Assert.Null(persisted.ReleasedBy);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("too-long")]
    public async Task InvalidReasonReturns400WithoutWrites(string? reason)
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(BlockPath(setup),
            new BlockLotRequest(reason == "too-long" ? new string('x', LotBlock.MaximumReasonLength + 1) : reason),
            factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "LOT_BLOCK_VALIDATION_FAILED", factory.RequestCancellationToken);
        await AssertUntouchedAsync(factory, setup);
    }

    [Fact]
    public async Task InvalidJsonUsesLotBlockValidationCodeWithoutWrites()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var content = new StringContent("{\"reason\":42}", Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(BlockPath(setup), content, factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "LOT_BLOCK_VALIDATION_FAILED", factory.RequestCancellationToken);
        await AssertUntouchedAsync(factory, setup);
    }

    private static void ObserveWriter(IServiceCollection services, WriteObservation observation)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(ILotBlockWriter));
        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException("Expected the production lot-block writer registration.");
        services.Remove(descriptor);
        services.AddScoped<ILotBlockWriter>(provider => new ObservingBlockWriter(
            (ILotBlockWriter)ActivatorUtilities.CreateInstance(provider, implementationType),
            provider.GetRequiredService<TraceabilityDbContext>(),
            provider.GetRequiredService<ScopedTransaction>(), observation));
    }

    private sealed class WriteObservation
    {
        public List<LotQualityStatus> StatusesBeforeInsert { get; } = [];
        public int ConflictsFromRealWriter { get; set; }
    }

    private sealed class ObservingBlockWriter(
        ILotBlockWriter inner, TraceabilityDbContext trace,
        ScopedTransaction transaction, WriteObservation observation) : ILotBlockWriter
    {
        public async Task AddAsync(LotBlock block, CancellationToken cancellationToken)
        {
            Assert.True(transaction.IsActive);
            Assert.NotNull(trace.Database.CurrentTransaction);
            observation.StatusesBeforeInsert.Add(await trace.Lots.AsNoTracking()
                .Where(lot => lot.OrganizationId == block.OrganizationId && lot.Id == block.LotId)
                .Select(lot => lot.QualityStatus).SingleAsync(cancellationToken));
            try
            {
                await inner.AddAsync(block, cancellationToken);
            }
            catch (LotBlockConflictException)
            {
                observation.ConflictsFromRealWriter++;
                throw;
            }
        }
    }

    private static async Task AssertUntouchedAsync(ApiWebApplicationFactory factory, Setup setup)
    {
        Assert.Empty(await ReadBlocksAsync(factory, setup.OrganizationId));
        Assert.Equal(LotQualityStatus.Pending, await ReadStatusAsync(factory, setup));
    }

    private static async Task<List<LotBlock>> ReadBlocksAsync(ApiWebApplicationFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<QualityDbContext>().LotBlocks.AsNoTracking()
            .Where(block => block.OrganizationId == organizationId).ToListAsync(factory.RequestCancellationToken);
    }

    private static async Task<LotQualityStatus> ReadStatusAsync(ApiWebApplicationFactory factory, Setup setup)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>().Lots.AsNoTracking()
            .Where(lot => lot.OrganizationId == setup.OrganizationId && lot.Id == setup.Lot.Id)
            .Select(lot => lot.QualityStatus).SingleAsync(factory.RequestCancellationToken);
    }

    private ApiWebApplicationFactory CreateFactory(Action<IServiceCollection>? configure = null) =>
        new(Environments.Development, new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.QualityConnectionString,
            ["RateLimiting:Authentication:PermitLimit"] = "100",
        }, configureTestServices: configure);

    private async Task<Setup> CreateSetupAsync(Guid? roleId = null)
    {
        var account = await CreateAccountAsync();
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        await using (var context = database.CreateQualityOrganizationsDbContext())
        {
            context.Organizations.Add(Organization.Create(organizationId, $"Block org {organizationId:N}",
                vatId: null, taxNumber: null, email: null, phone: null, now));
            await context.SaveChangesAsync();
        }

        var product = Product.Create(Guid.NewGuid(), $"BLOCK-PRODUCT-{Guid.NewGuid():N}", "Block product", now);
        var article = Article.Create(Guid.NewGuid(), organizationId, product.Id,
            $"BLOCK-SKU-{Guid.NewGuid():N}", gtin: null, now);
        await using (var context = database.CreateQualityCatalogDbContext())
        {
            context.Products.Add(product);
            context.Articles.Add(article);
            await context.SaveChangesAsync();
        }

        var lot = Lot.Create(Guid.NewGuid(), organizationId, article.Id,
            $"BLOCK-LOT-{Guid.NewGuid():N}", 10m, KilogramId, now);
        await using (var context = database.CreateQualityTraceabilityDbContext())
        {
            context.Lots.Add(lot);
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateQualityIdentityDbContext())
        {
            context.OrganizationMemberships.Add(OrganizationMembership.Create(account.UserId, organizationId, now));
            context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
                Guid.NewGuid(), account.UserId, organizationId, roleId ?? StandardRoleIds.QualityManager,
                locationId: null, now));
            await context.SaveChangesAsync();
        }

        return new Setup(account, organizationId, lot);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"block-api-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Block", "Test", now);
        var credential = UserCredential.Create(userId, "temporary-hash", now, now);
        credential.ChangePasswordHash(new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword), now);
        await using var context = database.CreateQualityIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        return new TestAccount(userId, email);
    }

    private static async Task AuthenticateAsync(HttpClient client, TestAccount account, CancellationToken cancellationToken)
    {
        using var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new { account.Email, Password = ValidPassword }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("The authentication response body was empty.");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private static async Task<LotBlockResponse> ReadBlockAsync(HttpResponseMessage response, CancellationToken token) =>
        await response.Content.ReadFromJsonAsync<LotBlockResponse>(token)
        ?? throw new InvalidOperationException("The lot block response body was empty.");

    private static async Task<JsonElement> AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string errorCode, CancellationToken token)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
        Assert.Equal(errorCode, document.RootElement.GetProperty("errorCode").GetString());
        return document.RootElement.Clone();
    }

    private static string[] ComparableProblem(JsonElement problem) => problem.EnumerateObject()
        .Where(property => property.Name is not ("correlationId" or "traceId"))
        .OrderBy(property => property.Name, StringComparer.Ordinal)
        .Select(property => $"{property.Name}={property.Value.GetRawText()}").ToArray();

    private static string BlockPath(Setup setup) => BlockPath(setup.OrganizationId, setup.Lot.Id);
    private static string BlockPath(Guid organizationId, Guid lotId) =>
        $"/api/v1/organizations/{organizationId}/lots/{lotId}/blocks";

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed record TestAccount(Guid UserId, string Email);
    private sealed record Setup(TestAccount Account, Guid OrganizationId, Lot Lot);
    private sealed record TokenResponse(string AccessToken);
}
