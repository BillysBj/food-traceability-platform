using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Api.Contracts.Samples;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Quality.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class SampleEndpointTests(PostgreSqlContainerFixture database)
{
    private const string ValidPassword = "Valid-test-password-42!";
    private static readonly Guid KilogramId = Guid.Parse("4ba563a7-f314-57d8-b3d7-ee5c12ff1085");

    // T-01 through T-03: real authentication and the unmodified role-permission matrix.
    [Fact]
    public async Task QualityManagerCreatesPendingSampleAndExactlyOneSampleEventInput()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = ValidRequest(setup);

        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), request, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sample = await ReadSampleAsync(response, factory.RequestCancellationToken);
        Assert.Equal($"{SamplePath(setup.Organization.Id)}/{sample.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(request.SampleNumber, sample.SampleNumber);
        Assert.Equal("PENDING", sample.Status);
        Assert.Equal(request.TakenAt, sample.TakenAt);
        Assert.Equal(request.LotId, sample.LotId);
        Assert.Equal(request.LocationId, sample.LocationId);

        await using var scope = factory.Services.CreateAsyncScope();
        var quality = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
        var persisted = Assert.Single(await quality.Samples.AsNoTracking()
            .Where(item => item.OrganizationId == setup.Organization.Id)
            .ToListAsync(factory.RequestCancellationToken));
        Assert.Equal(sample.Id, persisted.Id);
        Assert.Equal(sample.TraceabilityEventId, persisted.TraceabilityEventId);
        Assert.Equal(sample.SampleNumber, persisted.SampleNumber);
        Assert.Equal(SampleStatus.Pending, persisted.Status);
        Assert.Equal(sample.TakenAt, persisted.TakenAt);
        Assert.Equal(sample.LotId, persisted.LotId);
        Assert.Equal(sample.LocationId, persisted.LocationId);

        var trace = scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        var traceabilityEvent = Assert.Single(await trace.TraceabilityEvents.AsNoTracking()
            .Include(item => item.Inputs).Include(item => item.Outputs).AsSplitQuery()
            .Where(item => item.OrganizationId == setup.Organization.Id)
            .ToListAsync(factory.RequestCancellationToken));
        var eventType = await trace.EventTypes.AsNoTracking()
            .SingleAsync(item => item.Id == traceabilityEvent.EventTypeId, factory.RequestCancellationToken);
        Assert.Equal("SAMPLE", eventType.Code.Value);
        Assert.Equal(sample.TraceabilityEventId, traceabilityEvent.Id);
        Assert.Equal(sample.TakenAt, traceabilityEvent.OccurredAt);
        Assert.Equal(setup.Account.UserId, traceabilityEvent.CreatedBy);
        Assert.Equal(setup.Organization.LocationId, traceabilityEvent.LocationId);
        Assert.Null(traceabilityEvent.ExternalReference);
        Assert.Null(traceabilityEvent.Description);
        var input = Assert.Single(traceabilityEvent.Inputs);
        Assert.Equal(setup.Lot.Id, input.LotId);
        Assert.Equal(request.Quantity, input.Quantity);
        Assert.Equal(KilogramId, input.UnitId);
        Assert.Empty(traceabilityEvent.Outputs);
    }

    [Fact]
    public async Task UserWithoutMembershipCannotCreateSample()
    {
        var setup = await CreateSetupAsync();
        var outsider = await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, outsider, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, setup.Organization.Id));
    }

    [Fact]
    public async Task MemberWithoutSamplePermissionCannotCreateSample()
    {
        var setup = await CreateSetupAsync(StandardRoleIds.Processor);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, setup.Organization.Id));
    }

    [Fact]
    public async Task LaboratoryMemberWithQualityReadButWithoutSampleCreateCannotCreateSample()
    {
        // D-44: quality.read must not authorize the quantity-booking sample operation.
        var setup = await CreateSetupAsync(StandardRoleIds.Laboratory);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, setup.Organization.Id));
    }

    // T-04: permission in A never grants permission in B.
    [Fact]
    public async Task QualityManagerInOrganizationACannotCreateSampleInOrganizationB()
    {
        var setupA = await CreateSetupAsync();
        var setupB = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setupA.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setupB.Organization.Id), ValidRequest(setupB), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, setupB.Organization.Id));
    }

    // T-05 and the corresponding location boundary.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ForeignLotOrLocationIsRejectedWithoutSampleOrEvent(bool foreignLot)
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        var request = foreignLot
            ? ValidRequest(setup) with { LotId = other.Lot.Id }
            : ValidRequest(setup) with { LocationId = other.Organization.LocationId };
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), request, factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "SAMPLE_VALIDATION_FAILED", factory.RequestCancellationToken);
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, setup.Organization.Id));
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, other.Organization.Id));
    }

    // T-06, T-07, T-10: the duplicate is detected by SampleWriter AFTER the event was saved.
    [Theory]
    [InlineData("SAMPLE-DUPLICATE")]
    [InlineData("sample-duplicate")]
    public async Task DuplicateSampleNumberRollsBackAlreadyWrittenEventAndItsInput(string duplicateNumber)
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var request = ValidRequest(setup) with { SampleNumber = "SAMPLE-DUPLICATE", Quantity = 2m };
        using var first = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), request, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var before = await ReadCountsAsync(factory, setup.Organization.Id);
        Assert.Equal(new PersistedCounts(1, 1, 1), before);

        using var duplicate = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), request with { SampleNumber = duplicateNumber },
            factory.RequestCancellationToken);
        var detail = await AssertProblemAsync(
            duplicate, HttpStatusCode.Conflict, "SAMPLE_CONFLICT", factory.RequestCancellationToken);
        Assert.Equal("A sample with this sample number already exists in this organization.", detail);
        // A fresh DI scope observes committed database state, never the failed request's tracker.
        Assert.Equal(before, await ReadCountsAsync(factory, setup.Organization.Id));

        // The failed input must not consume quantity either: all remaining stock is available.
        using var remaining = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id),
            request with { SampleNumber = "SAMPLE-REMAINDER", Quantity = 8m },
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, remaining.StatusCode);
    }

    // T-08: uniqueness is scoped to the organization.
    [Fact]
    public async Task SameSampleNumberInDifferentOrganizationsIsAllowed()
    {
        var setupA = await CreateSetupAsync();
        var setupB = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        foreach (var setup in new[] { setupA, setupB })
        {
            await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
            using var response = await client.PostAsJsonAsync(
                SamplePath(setup.Organization.Id), ValidRequest(setup) with { SampleNumber = "SAME-NUMBER" },
                factory.RequestCancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal(new PersistedCounts(1, 1, 1), await ReadCountsAsync(factory, setup.Organization.Id));
        }
    }

    // T-09: both initial and remaining availability are enforced by the existing event path.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverconsumptionReturnsConflictWithoutCreatingSampleOrEvent(bool consumeFirst)
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        if (consumeFirst)
        {
            using var first = await client.PostAsJsonAsync(
                SamplePath(setup.Organization.Id), ValidRequest(setup) with { Quantity = 8m },
                factory.RequestCancellationToken);
            Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        }

        var before = await ReadCountsAsync(factory, setup.Organization.Id);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id),
            ValidRequest(setup) with { Quantity = consumeFirst ? 3m : 11m },
            factory.RequestCancellationToken);
        var detail = await AssertProblemAsync(
            response, HttpStatusCode.Conflict, "SAMPLE_CONFLICT", factory.RequestCancellationToken);
        Assert.Equal($"Lot '{setup.Lot.Id}' does not have sufficient available quantity.", detail);
        Assert.Equal(before, await ReadCountsAsync(factory, setup.Organization.Id));
    }

    // T-11: exact truncation, not a tolerance or a post-create read used to repair the response.
    [Fact]
    public async Task TakenAtIsTruncatedToMicrosecondsInResponseSampleAndEvent()
    {
        var setup = await CreateSetupAsync();
        var expected = new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero).AddTicks(1234560);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), ValidRequest(setup) with { TakenAt = expected.AddTicks(7) },
            factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var sample = await ReadSampleAsync(response, factory.RequestCancellationToken);
        Assert.Equal(expected, sample.TakenAt);
        await using var scope = factory.Services.CreateAsyncScope();
        var persisted = await scope.ServiceProvider.GetRequiredService<QualityDbContext>()
            .Samples.AsNoTracking().SingleAsync(item => item.Id == sample.Id, factory.RequestCancellationToken);
        var traceabilityEvent = await scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>()
            .TraceabilityEvents.AsNoTracking()
            .SingleAsync(item => item.Id == sample.TraceabilityEventId, factory.RequestCancellationToken);
        Assert.Equal(expected.UtcTicks, persisted.TakenAt.UtcTicks);
        Assert.Equal(expected.UtcTicks, traceabilityEvent.OccurredAt.UtcTicks);
    }

    [Theory]
    [InlineData("number")]
    [InlineData("quantity")]
    [InlineData("takenAt")]
    public async Task InvalidRequestReturnsSampleValidationErrorWithoutPersistingData(string invalidField)
    {
        var setup = await CreateSetupAsync();
        var request = invalidField switch
        {
            "number" => ValidRequest(setup) with { SampleNumber = " " },
            "quantity" => ValidRequest(setup) with { Quantity = 0m },
            "takenAt" => ValidRequest(setup) with { TakenAt = null },
            _ => throw new InvalidOperationException(),
        };
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(
            SamplePath(setup.Organization.Id), request, factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "SAMPLE_VALIDATION_FAILED", factory.RequestCancellationToken);
        Assert.Equal(new PersistedCounts(0, 0, 0), await ReadCountsAsync(factory, setup.Organization.Id));
    }

    private static async Task<PersistedCounts> ReadCountsAsync(ApiWebApplicationFactory factory, Guid organizationId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var quality = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
        var trace = scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        var sampleCount = await quality.Samples.CountAsync(
            item => item.OrganizationId == organizationId, factory.RequestCancellationToken);
        var sampleCode = EventTypeCode.Create("SAMPLE");
        var sampleTypeId = await trace.EventTypes.Where(item => item.Code == sampleCode)
            .Select(item => item.Id).SingleAsync(factory.RequestCancellationToken);
        var eventCount = await trace.TraceabilityEvents.CountAsync(
            item => item.OrganizationId == organizationId && item.EventTypeId == sampleTypeId,
            factory.RequestCancellationToken);
        var inputCount = await trace.Set<EventInput>().CountAsync(
            item => EF.Property<Guid>(item, "OrganizationId") == organizationId,
            factory.RequestCancellationToken);
        return new PersistedCounts(sampleCount, eventCount, inputCount);
    }

    private ApiWebApplicationFactory CreateFactory() => new(new Dictionary<string, string?>
    {
        ["ConnectionStrings:FoodTraceability"] = database.QualityConnectionString,
        ["RateLimiting:Authentication:PermitLimit"] = "100",
    });

    private async Task<Setup> CreateSetupAsync(Guid? roleId = null)
    {
        var account = await CreateAccountAsync();
        var now = DateTimeOffset.UtcNow;
        var organization = new TestOrganization(Guid.NewGuid(), Guid.NewGuid());
        await using (var context = database.CreateQualityOrganizationsDbContext())
        {
            context.Organizations.Add(Organization.Create(organization.Id, $"Sample org {organization.Id:N}",
                vatId: null, taxNumber: null, email: null, phone: null, now));
            context.Locations.Add(Location.Create(organization.LocationId, organization.Id, "Sample location",
                city: null, region: null, countryCode: null, latitude: null, longitude: null, now));
            await context.SaveChangesAsync();
        }

        var product = Product.Create(Guid.NewGuid(), $"SAMPLE-PRODUCT-{Guid.NewGuid():N}", "Sample product", now);
        var article = Article.Create(Guid.NewGuid(), organization.Id, product.Id,
            $"SAMPLE-SKU-{Guid.NewGuid():N}", gtin: null, now);
        await using (var context = database.CreateQualityCatalogDbContext())
        {
            context.Products.Add(product);
            context.Articles.Add(article);
            await context.SaveChangesAsync();
        }

        var lot = Lot.Create(Guid.NewGuid(), organization.Id, article.Id,
            $"SAMPLE-LOT-{Guid.NewGuid():N}", 10m, KilogramId, now);
        await using (var context = database.CreateQualityTraceabilityDbContext())
        {
            context.Lots.Add(lot);
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateQualityIdentityDbContext())
        {
            context.OrganizationMemberships.Add(OrganizationMembership.Create(account.UserId, organization.Id, now));
            context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
                Guid.NewGuid(), account.UserId, organization.Id, roleId ?? StandardRoleIds.QualityManager,
                locationId: null, now));
            await context.SaveChangesAsync();
        }

        return new Setup(account, organization, lot);
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = Guid.NewGuid();
        var email = $"sample-api-{userId:N}@example.com";
        var user = User.Create(userId, EmailAddress.Create(email), "Sample", "Test", now);
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

    private static async Task<SampleResponse> ReadSampleAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<SampleResponse>(cancellationToken)
        ?? throw new InvalidOperationException("The sample response body was empty.");

    private static async Task<string?> AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string errorCode, CancellationToken cancellationToken)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(errorCode, document.RootElement.GetProperty("errorCode").GetString());
        return document.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
    }

    private static CreateSampleRequest ValidRequest(Setup setup) => new(
        $"SAMPLE-{Guid.NewGuid():N}", setup.Lot.Id, setup.Organization.LocationId, 1.25m,
        new DateTimeOffset(2026, 9, 13, 10, 0, 0, TimeSpan.Zero));

    private static string SamplePath(Guid organizationId) => $"/api/v1/organizations/{organizationId}/samples";
    private sealed record PersistedCounts(int Samples, int SampleEvents, int Inputs);
    private sealed record TestAccount(Guid UserId, string Email);
    private sealed record TestOrganization(Guid Id, Guid LocationId);
    private sealed record Setup(TestAccount Account, TestOrganization Organization, Lot Lot);
    private sealed record TokenResponse(string AccessToken);
}
