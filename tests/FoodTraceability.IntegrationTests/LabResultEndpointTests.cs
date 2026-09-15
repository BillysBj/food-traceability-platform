using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Api.Contracts.LabResults;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Quality.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed partial class LabResultEndpointTests(PostgreSqlContainerFixture database) : IAsyncLifetime
{
    private const string ValidPassword = "Valid-test-password-42!";
    private static readonly DateTimeOffset MeasuredAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
    private readonly List<Guid> _sampleIds = [];
    private readonly List<Guid> _parameterIds = [];
    private readonly List<Guid> _specificationIds = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // The shared fixture also verifies the empty reference catalog. Remove only
        // this test's results, specifications and parameters, including after a failed assertion.
        await using var context = database.CreateQualityDbContext();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await context.LabResults.Where(item => _sampleIds.Contains(item.SampleId)).ExecuteDeleteAsync(timeout.Token);
        await context.SpecificationParameters.Where(item => _specificationIds.Contains(item.SpecificationId)).ExecuteDeleteAsync(timeout.Token);
        await context.Specifications.Where(item => _specificationIds.Contains(item.Id)).ExecuteDeleteAsync(timeout.Token);
        await context.Parameters.Where(item => _parameterIds.Contains(item.Id)).ExecuteDeleteAsync(timeout.Token);
    }

    // T-01, T-02, T-10: real JWT authentication and the existing role-permission matrix.
    [Theory]
    [InlineData("PASS", "PENDING", SampleStatus.Pending)]
    [InlineData("FAIL", "FAIL", SampleStatus.Fail)]
    public async Task LaboratoryCreatesResultWithExpectedSampleStatusAndMicrosecondPrecision(
        string assessment, string expectedStatus, SampleStatus persistedStatus)
    {
        var setup = await CreateSetupAsync();
        var saveProbe = new SaveChangesProbe();
        await using var factory = CreateFactory(saveProbe);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        var expectedTime = MeasuredAt.AddTicks(1234560);
        var request = ValidRequest(setup) with { Assessment = assessment, MeasuredAt = expectedTime.AddTicks(7) };
        using var response = await client.PostAsJsonAsync(ResultPath(setup), request, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await ReadResultAsync(response, factory.RequestCancellationToken);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(setup.Sample.Id, result.SampleId);
        Assert.Equal(setup.Parameter.Id, result.ParameterId);
        Assert.Equal(request.Value, result.Value);
        Assert.Equal(assessment, result.Assessment);
        Assert.Equal("ISO 660", result.Method);
        Assert.Equal(expectedTime, result.MeasuredAt);
        Assert.Equal(expectedStatus, result.SampleStatus);
        Assert.Equal($"{ResultPath(setup)}/{result.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(1, saveProbe.SaveCount);
        Assert.Equal(1, saveProbe.AddedResultCount);
        Assert.Equal(assessment == "FAIL" ? new[] { nameof(Sample.Status) } : [], saveProbe.ModifiedSampleProperties);

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
        var saved = Assert.Single(await context.LabResults.AsNoTracking()
            .Where(item => item.SampleId == setup.Sample.Id).ToListAsync(factory.RequestCancellationToken));
        Assert.Equal(result.Id, saved.Id);
        Assert.Equal(result.ParameterId, saved.ParameterId);
        Assert.Equal(result.Value, saved.Value);
        Assert.Equal(assessment == "FAIL" ? LabResultAssessment.Fail : LabResultAssessment.Pass, saved.Assessment);
        Assert.Equal(result.Method, saved.Method);
        Assert.Equal(expectedTime.UtcTicks, saved.MeasuredAt.UtcTicks);
        await AssertSampleStatusAsync(factory, setup, persistedStatus);
    }

    // T-03 and T-08: a second parameter is allowed, but PASS never reverses FAIL.
    [Theory]
    [InlineData("FAIL", "FAIL", SampleStatus.Fail)]
    [InlineData("PASS", "PENDING", SampleStatus.Pending)]
    public async Task PassingAnotherParameterLeavesSampleStatusUnchanged(
        string firstAssessment, string expectedStatus, SampleStatus persistedStatus)
    {
        var setup = await CreateSetupAsync();
        var secondParameter = await CreateParameterAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var first = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { Assessment = firstAssessment }, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var second = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { ParameterId = secondParameter.Id }, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var result = await ReadResultAsync(second, factory.RequestCancellationToken);
        Assert.Equal("PASS", result.Assessment);
        Assert.Equal(expectedStatus, result.SampleStatus);
        await AssertSampleStatusAsync(factory, setup, persistedStatus);
        await AssertResultCountAsync(factory, setup, 2);
    }

    // T-04: a Laboratory assignment in B does not grant membership or permission in A.
    [Fact]
    public async Task LaboratoryWithoutRouteOrganizationMembershipGetsForbidden()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, other.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        await AssertResultCountAsync(factory, setup, 0);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
    }

    // T-05: quality.sample.create and quality.read do not authorize result creation.
    [Fact]
    public async Task QualityManagerGetsForbidden()
    {
        var setup = await CreateSetupAsync(StandardRoleIds.QualityManager);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        await AssertResultCountAsync(factory, setup, 0);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
    }

    // T-06: compare every semantic problem field, excluding request-specific trace identifiers.
    [Fact]
    public async Task ForeignAndMissingSamplesHaveIdenticalNotFoundResponses()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var foreign = await client.PostAsJsonAsync(
            ResultPath(setup.Sample.OrganizationId, other.Sample.Id), ValidRequest(setup), factory.RequestCancellationToken);
        using var missing = await client.PostAsJsonAsync(
            ResultPath(setup.Sample.OrganizationId, Guid.NewGuid()), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(foreign, HttpStatusCode.NotFound, "LAB_RESULT_SAMPLE_NOT_FOUND", factory.RequestCancellationToken);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "LAB_RESULT_SAMPLE_NOT_FOUND", factory.RequestCancellationToken);
        Assert.Equal(await ReadProblemFieldsAsync(foreign, factory.RequestCancellationToken),
            await ReadProblemFieldsAsync(missing, factory.RequestCancellationToken));
        await AssertResultCountAsync(factory, setup, 0);
        await AssertResultCountAsync(factory, other, 0);
        await AssertSampleStatusAsync(factory, other, SampleStatus.Pending);
    }

    // T-07 and atomic rollback: the rejected FAIL must not change the PENDING sample.
    [Fact]
    public async Task DuplicateParameterReturnsConflictAndRollsBackSampleChange()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var first = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { Assessment = "FAIL" }, factory.RequestCancellationToken);
        await AssertProblemAsync(duplicate, HttpStatusCode.Conflict, "LAB_RESULT_CONFLICT", factory.RequestCancellationToken);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
        await AssertResultCountAsync(factory, setup, 1);
    }

    // T-09: an unknown parameter is an invalid request reference (400), not a missing routed sample.
    [Fact]
    public async Task UnknownParameterReturnsValidationErrorAndRollsBackSampleChange()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { ParameterId = Guid.NewGuid(), Assessment = "FAIL" }, factory.RequestCancellationToken);
        var detail = await AssertProblemAsync(response, HttpStatusCode.BadRequest,
            "LAB_RESULT_VALIDATION_FAILED", factory.RequestCancellationToken);
        Assert.Equal("The referenced parameter does not exist.", detail);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
        await AssertResultCountAsync(factory, setup, 0);
    }

    [Theory]
    [InlineData("value")]
    [InlineData("precision")]
    [InlineData("assessment")]
    [InlineData("method")]
    [InlineData("measuredAt")]
    [InlineData("parameterId")]
    public async Task InvalidRequestUsesLabResultValidationCodeWithoutWritingData(string field)
    {
        var setup = await CreateSetupAsync();
        var request = field switch
        {
            "value" => ValidRequest(setup) with { Value = null },
            "precision" => ValidRequest(setup) with { Value = 1.1234567m },
            "assessment" => ValidRequest(setup) with { Assessment = "PENDING" },
            "method" => ValidRequest(setup) with { Method = " " },
            "measuredAt" => ValidRequest(setup) with { MeasuredAt = null },
            "parameterId" => ValidRequest(setup) with { ParameterId = Guid.Empty },
            _ => throw new InvalidOperationException(),
        };
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(ResultPath(setup), request, factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "LAB_RESULT_VALIDATION_FAILED", factory.RequestCancellationToken);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
        await AssertResultCountAsync(factory, setup, 0);
    }

    [Fact]
    public async Task MalformedTimestampUsesLabResultValidationCode()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(ResultPath(setup), new
        {
            ParameterId = setup.Parameter.Id, Value = 0.5m, Assessment = "PASS",
            Method = "ISO 660", MeasuredAt = "not-a-timestamp",
        }, factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "LAB_RESULT_VALIDATION_FAILED", factory.RequestCancellationToken);
        await AssertResultCountAsync(factory, setup, 0);
    }

    private ApiWebApplicationFactory CreateFactory(
        SaveChangesProbe? saveProbe = null, DbCommandInterceptor? commandProbe = null) => new(
        Environments.Development,
        new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.QualityConnectionString,
            ["RateLimiting:Authentication:PermitLimit"] = "100",
        },
        configureTestServices: services =>
        {
            if (saveProbe is not null)
            {
                services.AddDbContext<QualityDbContext>((_, options) => options.AddInterceptors(saveProbe));
            }
            if (commandProbe is not null)
            {
                services.AddDbContext<QualityDbContext>((_, options) => options.AddInterceptors(commandProbe));
            }
        });

    private async Task<Setup> CreateSetupAsync(Guid? roleId = null)
    {
        var account = await CreateAccountAsync();
        var now = DateTimeOffset.UtcNow;
        var organizationId = Guid.NewGuid();
        var location = Location.Create(Guid.NewGuid(), organizationId, "Result location",
            null, null, null, null, null, now);
        await using (var context = database.CreateQualityOrganizationsDbContext())
        {
            context.Organizations.Add(Organization.Create(organizationId, $"Result org {organizationId:N}",
                null, null, null, null, now));
            context.Locations.Add(location);
            await context.SaveChangesAsync();
        }

        var product = Product.Create(Guid.NewGuid(), $"RESULT-PRODUCT-{Guid.NewGuid():N}", "Result product", now);
        var article = Article.Create(Guid.NewGuid(), organizationId, product.Id, $"RESULT-SKU-{Guid.NewGuid():N}", null, now);
        var unit = Unit.Create(Guid.NewGuid(), UnitCode.Create($"U{Guid.NewGuid():N}"[..UnitCode.MaximumLength]),
            "u", UnitDimension.Mass, now);
        await using (var context = database.CreateQualityCatalogDbContext())
        {
            context.Products.Add(product);
            context.Articles.Add(article);
            context.Units.Add(unit);
            await context.SaveChangesAsync();
        }

        await using (var context = database.CreateQualityIdentityDbContext())
        {
            context.OrganizationMemberships.Add(OrganizationMembership.Create(account.UserId, organizationId, now));
            context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
                Guid.NewGuid(), account.UserId, organizationId, roleId ?? StandardRoleIds.Laboratory, null, now));
            await context.SaveChangesAsync();
        }

        var lot = Lot.Create(Guid.NewGuid(), organizationId, article.Id, $"RESULT-LOT-{Guid.NewGuid():N}", 10m, unit.Id, now);
        Guid eventId;
        await using (var context = database.CreateQualityTraceabilityDbContext())
        {
            context.Lots.Add(lot);
            await context.SaveChangesAsync();
            var sampleCode = EventTypeCode.Create("SAMPLE");
            var sampleTypeId = await context.EventTypes.Where(item => item.Code == sampleCode)
                .Select(item => item.Id).SingleAsync();
            var sampleEvent = TraceabilityEvent.Create(Guid.NewGuid(), sampleTypeId, organizationId, location.Id,
                MeasuredAt, null, null, account.UserId, now,
                [EventInput.Create(Guid.NewGuid(), lot.Id, 1m, unit.Id)], []);
            context.TraceabilityEvents.Add(sampleEvent);
            await context.SaveChangesAsync();
            eventId = sampleEvent.Id;
        }

        var sample = Sample.Create(Guid.NewGuid(), organizationId, lot.Id, location.Id, eventId,
            $"RESULT-SAMPLE-{Guid.NewGuid():N}", MeasuredAt, now);
        _sampleIds.Add(sample.Id);
        await using (var context = database.CreateQualityDbContext())
        {
            context.Samples.Add(sample);
            await context.SaveChangesAsync();
        }

        return new Setup(account, sample, await CreateParameterAsync(), article.Id);
    }

    private async Task<Parameter> CreateParameterAsync()
    {
        var parameter = Parameter.Create(Guid.NewGuid(), ParameterCode.Create($"RESULT_{Guid.NewGuid():N}"), null, "ISO 660");
        _parameterIds.Add(parameter.Id);
        await using var context = database.CreateQualityDbContext();
        context.Parameters.Add(parameter);
        await context.SaveChangesAsync();
        return parameter;
    }

    private async Task<TestAccount> CreateAccountAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        var email = $"result-api-{id:N}@example.com";
        var user = User.Create(id, EmailAddress.Create(email), "Result", "Test", now);
        var credential = UserCredential.Create(id, "temporary-hash", now, now);
        credential.ChangePasswordHash(new PasswordHasher<UserCredential>().HashPassword(credential, ValidPassword), now);
        await using var context = database.CreateQualityIdentityDbContext();
        context.Users.Add(user);
        context.UserCredentials.Add(credential);
        await context.SaveChangesAsync();
        return new TestAccount(id, email);
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

    private static async Task AssertSampleStatusAsync(ApiWebApplicationFactory factory, Setup setup, SampleStatus status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var sample = await scope.ServiceProvider.GetRequiredService<QualityDbContext>()
            .Samples.AsNoTracking().SingleAsync(item => item.Id == setup.Sample.Id, factory.RequestCancellationToken);
        Assert.Equal(status, sample.Status);
    }

    private static async Task AssertResultCountAsync(ApiWebApplicationFactory factory, Setup setup, int expected)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var count = await scope.ServiceProvider.GetRequiredService<QualityDbContext>()
            .LabResults.CountAsync(item => item.SampleId == setup.Sample.Id, factory.RequestCancellationToken);
        Assert.Equal(expected, count);
    }

    private static async Task<LabResultResponse> ReadResultAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<LabResultResponse>(cancellationToken)
        ?? throw new InvalidOperationException("The lab result response body was empty.");

    private static async Task<string?> AssertProblemAsync(
        HttpResponseMessage response, HttpStatusCode status, string errorCode, CancellationToken cancellationToken)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        Assert.Equal(errorCode, document.RootElement.GetProperty("errorCode").GetString());
        return document.RootElement.TryGetProperty("detail", out var detail) ? detail.GetString() : null;
    }

    private static async Task<string[]> ReadProblemFieldsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return document.RootElement.EnumerateObject()
            .Where(property => property.Name is not ("correlationId" or "traceId"))
            .OrderBy(property => property.Name, StringComparer.Ordinal)
            .Select(property => $"{property.Name}={property.Value.GetRawText()}").ToArray();
    }

    private static CreateLabResultRequest ValidRequest(Setup setup) => new(
        setup.Parameter.Id, 0.5m, "PASS", "  ISO 660  ", MeasuredAt);

    private static string ResultPath(Setup setup) => ResultPath(setup.Sample.OrganizationId, setup.Sample.Id);
    private static string ResultPath(Guid organizationId, Guid sampleId) =>
        $"/api/v1/organizations/{organizationId}/samples/{sampleId}/results";

    private sealed class SaveChangesProbe : SaveChangesInterceptor
    {
        public int SaveCount { get; private set; }
        public int AddedResultCount { get; private set; }
        public string[] ModifiedSampleProperties { get; private set; } = [];

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            var context = Assert.IsType<QualityDbContext>(eventData.Context);
            context.ChangeTracker.DetectChanges();
            SaveCount++;
            AddedResultCount = context.ChangeTracker.Entries<LabResult>().Count(entry => entry.State == EntityState.Added);
            ModifiedSampleProperties = context.ChangeTracker.Entries<Sample>()
                .SelectMany(entry => entry.Properties.Where(property => property.IsModified))
                .Select(property => property.Metadata.Name).ToArray();
            return ValueTask.FromResult(result);
        }
    }

    private sealed record TestAccount(Guid UserId, string Email);
    private sealed record Setup(TestAccount Account, Sample Sample, Parameter Parameter, Guid ArticleId);
    private sealed record TokenResponse(string AccessToken);
}
