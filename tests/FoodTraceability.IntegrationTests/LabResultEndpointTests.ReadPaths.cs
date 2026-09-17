using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Api.Contracts.LabResults;
using FoodTraceability.Api.Contracts.Samples;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

public sealed partial class LabResultEndpointTests
{
    // QLT-009b T-01: real authentication with each existing quality.read role.
    [Theory]
    [InlineData("QualityManager")]
    [InlineData("Laboratory")]
    [InlineData("Auditor")]
    public async Task QualityReadersCanReadSamplesAndResults(string role)
    {
        var roleId = role switch
        {
            "QualityManager" => StandardRoleIds.QualityManager,
            "Laboratory" => StandardRoleIds.Laboratory,
            "Auditor" => StandardRoleIds.Auditor,
            _ => throw new InvalidOperationException(),
        };
        var setup = await CreateSetupAsync(roleId);
        var result = await SeedReadResultAsync(setup.Sample, setup.Parameter, MeasuredAt);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);

        var samples = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal(1, samples.Page);
        Assert.Equal(50, samples.PageSize);
        Assert.Equal(1L, samples.TotalCount);
        var sample = Assert.Single(samples.Items);
        Assert.Equal(setup.Sample.Id, sample.Id);
        Assert.Equal(setup.Sample.SampleNumber, sample.SampleNumber);
        Assert.Equal("PENDING", sample.Status);
        Assert.Equal(setup.Sample.TakenAt, sample.TakenAt);
        Assert.Equal(setup.Sample.LotId, sample.LotId);
        Assert.Equal(setup.Sample.LocationId, sample.LocationId);
        Assert.Equal(setup.Sample.TraceabilityEventId, sample.TraceabilityEventId);
        Assert.NotEqual(default, sample.CreatedAt);

        var results = await ReadPageAsync<LabResultListResponse>(client, ResultPath(setup), ct);
        Assert.Equal(1, results.Page);
        Assert.Equal(50, results.PageSize);
        Assert.Equal(1L, results.TotalCount);
        var item = Assert.Single(results.Items);
        Assert.Equal(result.Id, item.Id);
        Assert.Equal(result.SampleId, item.SampleId);
        Assert.Equal(result.ParameterId, item.ParameterId);
        Assert.Equal(result.Value, item.Value);
        Assert.Equal("PASS", item.Assessment);
        Assert.Equal(result.Method, item.Method);
        Assert.Equal(result.MeasuredAt, item.MeasuredAt);
        Assert.Equal(result.CreatedAt, item.CreatedAt);
    }

    // T-02: Producer has no Quality permission; no such neighboring Quality role exists.
    [Fact]
    public async Task ProducerCannotReadSamplesOrResults()
    {
        var setup = await CreateSetupAsync(StandardRoleIds.Producer);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        await AssertReadForbiddenAsync(client, setup, factory.RequestCancellationToken);
    }

    // T-03: a valid quality.read assignment in B is insufficient for A.
    [Fact]
    public async Task QualityReaderWithoutRouteMembershipCannotReadSamplesOrResults()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, other.Account, factory.RequestCancellationToken);
        await AssertReadForbiddenAsync(client, setup, factory.RequestCancellationToken);
    }

    // T-04 and T-05: only correlation/trace identifiers may differ.
    [Theory]
    [InlineData(false, "QUALITY_SAMPLE_LIST_LOT_NOT_FOUND")]
    [InlineData(true, "QUALITY_RESULT_LIST_SAMPLE_NOT_FOUND")]
    public async Task ReadPathsReturnIdenticalNotFoundForForeignAndUnknownParents(bool results, string code)
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await SeedReadResultAsync(other.Sample, other.Parameter, MeasuredAt);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        var foreignPath = results
            ? ResultPath(setup.Sample.OrganizationId, other.Sample.Id)
            : SamplePath(setup.Sample.OrganizationId, other.Sample.LotId);
        var missingPath = results
            ? ResultPath(setup.Sample.OrganizationId, Guid.NewGuid())
            : SamplePath(setup.Sample.OrganizationId, Guid.NewGuid());
        using var foreign = await client.GetAsync(foreignPath, ct);
        using var missing = await client.GetAsync(missingPath, ct);
        await AssertProblemAsync(foreign, HttpStatusCode.NotFound, code, ct);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, code, ct);
        Assert.Equal(await ReadProblemFieldsAsync(foreign, ct), await ReadProblemFieldsAsync(missing, ct));
    }

    // T-06 and T-07: tied timestamps exercise Id ordering across the page boundary.
    [Fact]
    public async Task SamplePagesContainOnlyTheirLotAndHaveStableOrderingWithoutGaps()
    {
        var setup = await CreateSetupAsync();
        var lot = await CreateReadLotAsync(setup);
        var samples = new[]
        {
            await SeedReadSampleAsync(setup, lot, MeasuredAt),
            await SeedReadSampleAsync(setup, lot, MeasuredAt.AddHours(1)),
            await SeedReadSampleAsync(setup, lot, MeasuredAt.AddHours(1)),
            await SeedReadSampleAsync(setup, lot, MeasuredAt.AddHours(1)),
        };
        var foreign = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        var path = SamplePath(setup.Sample.OrganizationId, lot.Id);
        var first = await ReadPageAsync<SampleListResponse>(client, $"{path}?page=1&pageSize=2", ct);
        var second = await ReadPageAsync<SampleListResponse>(client, $"{path}?page=2&pageSize=2", ct);
        var repeated = await ReadPageAsync<SampleListResponse>(client, $"{path}?page=2&pageSize=2", ct);
        var pages = new[] { first, second };
        for (var index = 0; index < pages.Length; index++)
        {
            Assert.Equal(index + 1, pages[index].Page);
            Assert.Equal(2, pages[index].PageSize);
            Assert.Equal(4L, pages[index].TotalCount);
            Assert.Equal(2, pages[index].Items.Count);
            Assert.All(pages[index].Items, item => Assert.Equal(lot.Id, item.LotId));
        }
        var expected = samples.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => item.Id).ToArray();
        var actual = pages.SelectMany(page => page.Items).Select(item => item.Id).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(actual.Length, actual.Distinct().Count());
        Assert.Equal(second.Items.ToArray(), repeated.Items.ToArray());
        Assert.DoesNotContain(setup.Sample.Id, actual);
        Assert.DoesNotContain(foreign.Sample.Id, actual);
        var original = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal(setup.Sample.Id, Assert.Single(original.Items).Id);
        Assert.Equal(1L, original.TotalCount);
    }

    [Fact]
    public async Task ResultPagesAreScopedToSampleAndOrganizationAndDeterministicallyOrdered()
    {
        var setup = await CreateSetupAsync();
        var expectedResults = new[]
        {
            await SeedReadResultAsync(setup.Sample, setup.Parameter, MeasuredAt),
            await SeedReadResultAsync(setup.Sample, await CreateParameterAsync(), MeasuredAt.AddHours(1)),
            await SeedReadResultAsync(setup.Sample, await CreateParameterAsync(), MeasuredAt.AddHours(1)),
            await SeedReadResultAsync(setup.Sample, await CreateParameterAsync(), MeasuredAt.AddHours(1)),
        };
        var otherLot = await CreateReadLotAsync(setup);
        var otherSample = await SeedReadSampleAsync(setup, otherLot, MeasuredAt);
        await SeedReadResultAsync(otherSample, await CreateParameterAsync(), MeasuredAt);
        var foreign = await CreateSetupAsync();
        await SeedReadResultAsync(foreign.Sample, foreign.Parameter, MeasuredAt);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        var first = await ReadPageAsync<LabResultListResponse>(client, $"{ResultPath(setup)}?page=1&pageSize=2", ct);
        var second = await ReadPageAsync<LabResultListResponse>(client, $"{ResultPath(setup)}?page=2&pageSize=2", ct);
        var repeated = await ReadPageAsync<LabResultListResponse>(client, $"{ResultPath(setup)}?page=2&pageSize=2", ct);
        var pages = new[] { first, second };
        for (var index = 0; index < pages.Length; index++)
        {
            Assert.Equal(index + 1, pages[index].Page);
            Assert.Equal(2, pages[index].PageSize);
            Assert.Equal(4L, pages[index].TotalCount);
            Assert.Equal(2, pages[index].Items.Count);
            Assert.All(pages[index].Items, item => Assert.Equal(setup.Sample.Id, item.SampleId));
        }
        var expected = expectedResults.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Select(item => item.Id).ToArray();
        var actual = pages.SelectMany(page => page.Items).Select(item => item.Id).ToArray();
        Assert.Equal(expected, actual);
        Assert.Equal(actual.Length, actual.Distinct().Count());
        Assert.Equal(second.Items.ToArray(), repeated.Items.ToArray());
    }

    // T-08 also covers an existing sample without results and pages beyond the end.
    [Fact]
    public async Task ExistingParentsWithoutChildrenReturnEmptyPages()
    {
        var setup = await CreateSetupAsync();
        var emptyLot = await CreateReadLotAsync(setup);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        var samples = await ReadPageAsync<SampleListResponse>(client,
            SamplePath(setup.Sample.OrganizationId, emptyLot.Id), ct);
        Assert.Empty(samples.Items);
        Assert.Equal(0L, samples.TotalCount);
        Assert.Equal(1, samples.Page);
        Assert.Equal(50, samples.PageSize);
        var results = await ReadPageAsync<LabResultListResponse>(client, ResultPath(setup), ct);
        Assert.Empty(results.Items);
        Assert.Equal(0L, results.TotalCount);
        Assert.Equal(1, results.Page);
        Assert.Equal(50, results.PageSize);

        await SeedReadResultAsync(setup.Sample, setup.Parameter, MeasuredAt);
        var farSamples = await ReadPageAsync<SampleListResponse>(client,
            $"{SamplePath(setup)}?page={int.MaxValue}&pageSize=100", ct);
        var farResults = await ReadPageAsync<LabResultListResponse>(client,
            $"{ResultPath(setup)}?page={int.MaxValue}&pageSize=100", ct);
        Assert.Empty(farSamples.Items);
        Assert.Empty(farResults.Items);
        Assert.Equal(1L, farSamples.TotalCount);
        Assert.Equal(1L, farResults.TotalCount);
    }

    // T-09: the existing automatic model validation rejects each invalid query on both routes.
    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    public async Task ReadPathsRejectInvalidPagination(string query, string field)
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        foreach (var path in new[] { SamplePath(setup), ResultPath(setup) })
        {
            using var response = await client.GetAsync($"{path}?{query}", ct);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
            Assert.Contains(problem.RootElement.GetProperty("errors").EnumerateObject(),
                property => property.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
        }
    }

    // T-10: read after a real HTTP write; FAIL remains the persisted sample status.
    [Fact]
    public async Task ReadsExposeUppercaseAssessmentsAndStoredFailedSampleStatus()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        using var response = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { Assessment = "FAIL" }, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var secondParameter = await CreateParameterAsync();
        using var passing = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { ParameterId = secondParameter.Id }, ct);
        Assert.Equal(HttpStatusCode.Created, passing.StatusCode);
        var samples = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal("FAIL", Assert.Single(samples.Items).Status);
        var results = await ReadPageAsync<LabResultListResponse>(client, ResultPath(setup), ct);
        Assert.Equal(new[] { "FAIL", "PASS" }, results.Items.Select(item => item.Assessment).Order(StringComparer.Ordinal));
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Fail);
    }

    private async Task<Lot> CreateReadLotAsync(Setup setup)
    {
        await using var context = database.CreateQualityTraceabilityDbContext();
        var original = await context.Lots.AsNoTracking().SingleAsync(lot => lot.Id == setup.Sample.LotId);
        var lot = Lot.Create(Guid.NewGuid(), setup.Sample.OrganizationId, setup.ArticleId,
            $"READ-LOT-{Guid.NewGuid():N}", 100m, original.UnitId, MeasuredAt);
        context.Lots.Add(lot);
        await context.SaveChangesAsync();
        return lot;
    }

    private async Task<Sample> SeedReadSampleAsync(Setup setup, Lot lot, DateTimeOffset createdAt)
    {
        var eventId = Guid.NewGuid();
        await using (var context = database.CreateQualityTraceabilityDbContext())
        {
            var code = EventTypeCode.Create("SAMPLE");
            var typeId = await context.EventTypes.Where(item => item.Code == code).Select(item => item.Id).SingleAsync();
            context.TraceabilityEvents.Add(TraceabilityEvent.Create(eventId, typeId, lot.OrganizationId,
                setup.Sample.LocationId, MeasuredAt, null, null, setup.Account.UserId, createdAt,
                [EventInput.Create(Guid.NewGuid(), lot.Id, 1m, lot.UnitId)], []));
            await context.SaveChangesAsync();
        }
        var sample = Sample.Create(Guid.NewGuid(), lot.OrganizationId, lot.Id, setup.Sample.LocationId,
            eventId, $"READ-SAMPLE-{Guid.NewGuid():N}", MeasuredAt, createdAt);
        _sampleIds.Add(sample.Id);
        await using var quality = database.CreateQualityDbContext();
        quality.Samples.Add(sample);
        await quality.SaveChangesAsync();
        return sample;
    }

    private async Task<LabResult> SeedReadResultAsync(Sample sample, Parameter parameter, DateTimeOffset createdAt)
    {
        var result = LabResult.Create(Guid.NewGuid(), sample.Id, parameter.Id, 0.123456m,
            LabResultAssessment.Pass, "ISO 660", MeasuredAt, createdAt);
        await using var context = database.CreateQualityDbContext();
        context.LabResults.Add(result);
        await context.SaveChangesAsync();
        return result;
    }

    private static async Task<T> ReadPageAsync<T>(HttpClient client, string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<T>(ct)
            ?? throw new InvalidOperationException("The page response body was empty.");
    }

    private static async Task AssertReadForbiddenAsync(HttpClient client, Setup setup, CancellationToken ct)
    {
        foreach (var path in new[] { SamplePath(setup), ResultPath(setup) })
        {
            using var response = await client.GetAsync(path, ct);
            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", ct);
        }
    }

    private static string SamplePath(Setup setup) => SamplePath(setup.Sample.OrganizationId, setup.Sample.LotId);
    private static string SamplePath(Guid organizationId, Guid lotId) =>
        $"/api/v1/organizations/{organizationId}/lots/{lotId}/samples";
}
