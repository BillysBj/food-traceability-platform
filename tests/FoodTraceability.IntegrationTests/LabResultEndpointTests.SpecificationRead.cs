using System.Net;
using System.Net.Http.Json;
using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.Api.Contracts.Samples;
using FoodTraceability.Api.Contracts.Specifications;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Quality.Domain;
using Microsoft.EntityFrameworkCore;

namespace FoodTraceability.IntegrationTests;

public sealed partial class LabResultEndpointTests
{
    // QLT-009 T-04: return optional parameters, technical metadata, all limits and nullable values.
    [Fact]
    public async Task SpecificationReadReturnsAllParametersWithReferenceLimitsInCodeOrder()
    {
        var setup = await CreateSetupAsync();
        Guid unitId;
        await using (var trace = database.CreateQualityTraceabilityDbContext())
        {
            unitId = await trace.Lots.Where(lot => lot.Id == setup.Sample.LotId)
                .Select(lot => lot.UnitId).SingleAsync();
        }
        var optional = Parameter.Create(Guid.NewGuid(), ParameterCode.Create($"AAA_{Guid.NewGuid():N}"), unitId, "ISO 3960");
        _parameterIds.Add(optional.Id);
        var unbounded = await CreateParameterAsync();
        var specification = Specification.Create(Guid.NewGuid(), setup.ArticleId, 7,
            MeasuredAt.AddDays(-1), MeasuredAt.AddDays(1), DateTimeOffset.UtcNow);
        _specificationIds.Add(specification.Id);
        await using (var quality = database.CreateQualityDbContext())
        {
            quality.Parameters.Add(optional);
            quality.Specifications.Add(specification);
            quality.SpecificationParameters.AddRange(
                SpecificationParameter.Create(Guid.NewGuid(), specification.Id, setup.Parameter.Id,
                    1.123456m, 9.654321m, 5.111111m, true, DateTimeOffset.UtcNow),
                SpecificationParameter.Create(Guid.NewGuid(), specification.Id, optional.Id,
                    2m, 8m, 4m, false, DateTimeOffset.UtcNow),
                SpecificationParameter.Create(Guid.NewGuid(), specification.Id, unbounded.Id,
                    null, null, null, false, DateTimeOffset.UtcNow));
            await quality.SaveChangesAsync();
        }
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);

        var response = await ReadPageAsync<SampleSpecificationResponse>(client, SpecificationPath(setup), ct);

        Assert.Equal(specification.Id, response.Id);
        Assert.Equal(setup.ArticleId, response.ArticleId);
        Assert.Equal(7, response.Version);
        Assert.Equal(specification.ValidFrom, response.ValidFrom);
        Assert.Equal(specification.ValidTo, response.ValidTo);
        var expected = new[]
        {
            new SpecificationParameterResponse(setup.Parameter.Id, setup.Parameter.Code.Value,
                setup.Parameter.StandardMethod, null, 1.123456m, 9.654321m, 5.111111m, true),
            new SpecificationParameterResponse(optional.Id, optional.Code.Value,
                optional.StandardMethod, unitId, 2m, 8m, 4m, false),
            new SpecificationParameterResponse(unbounded.Id, unbounded.Code.Value,
                unbounded.StandardMethod, null, null, null, null, false),
        }.OrderBy(parameter => parameter.ParameterCode, StringComparer.Ordinal).ToArray();
        Assert.Equal(expected, response.Parameters.ToArray());
        var samples = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal("PENDING", Assert.Single(samples.Items).Status);
    }

    // T-05: the displayed B and the real PASS write must agree, including both inclusive boundaries
    // and an open end. Measurement time is in A, deliberately different from sampling time in B.
    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    public async Task DisplayAndPassEvaluationUseSpecificationBAtSamplingTime(int fromHours, int? toHours)
    {
        var setup = await CreateSetupAsync();
        var firstB = await CreateParameterAsync();
        var lastB = await CreateParameterAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-3), MeasuredAt.AddDays(-2),
            (setup.Parameter.Id, true));
        var validFrom = MeasuredAt.AddHours(fromHours);
        DateTimeOffset? validTo = toHours.HasValue ? MeasuredAt.AddHours(toHours.Value) : null;
        await CreateSpecificationAsync(setup.ArticleId, 2, validFrom, validTo,
            (setup.Parameter.Id, false), (firstB.Id, true), (lastB.Id, true));
        var specificationBId = _specificationIds[^1];
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);

        var specification = await ReadPageAsync<SampleSpecificationResponse>(client, SpecificationPath(setup), ct);
        Assert.Equal(specificationBId, specification.Id);
        Assert.Equal(setup.ArticleId, specification.ArticleId);
        Assert.Equal(2, specification.Version);
        Assert.Equal(validFrom, specification.ValidFrom);
        Assert.Equal(validTo, specification.ValidTo);
        Assert.Equal(new[] { firstB.Id, lastB.Id }.Order(),
            specification.Parameters.Where(parameter => parameter.Required).Select(parameter => parameter.ParameterId).Order());
        var before = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal("PENDING", Assert.Single(before.Items).Status);

        using var first = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup) with
        {
            ParameterId = firstB.Id, MeasuredAt = MeasuredAt.AddDays(-2), Value = 999m,
        }, ct);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("PENDING", (await ReadResultAsync(first, ct)).SampleStatus);
        using var last = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup) with
        {
            ParameterId = lastB.Id, MeasuredAt = MeasuredAt.AddDays(-2), Value = 999m,
        }, ct);
        Assert.Equal(HttpStatusCode.Created, last.StatusCode);
        Assert.Equal("PASS", (await ReadResultAsync(last, ct)).SampleStatus);
        var after = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal("PASS", Assert.Single(after.Items).Status);
        var afterSpecification = await ReadPageAsync<SampleSpecificationResponse>(client, SpecificationPath(setup), ct);
        Assert.Equal(specificationBId, afterSpecification.Id);
        Assert.Equal(specification.Parameters.ToArray(), afterSpecification.Parameters.ToArray());
    }

    // T-06: identical missing-parent responses, even when the foreign sample has a specification.
    [Fact]
    public async Task SpecificationReadHidesForeignSamplesLikeUnknownSamples()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await CreateSpecificationAsync(other.ArticleId, 1, MeasuredAt, null, (other.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        using var foreign = await client.GetAsync(SpecificationPath(setup.Sample.OrganizationId, other.Sample.Id), ct);
        using var missing = await client.GetAsync(SpecificationPath(setup.Sample.OrganizationId, Guid.NewGuid()), ct);
        await AssertProblemAsync(foreign, HttpStatusCode.NotFound, "QUALITY_SPECIFICATION_SAMPLE_NOT_FOUND", ct);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "QUALITY_SPECIFICATION_SAMPLE_NOT_FOUND", ct);
        Assert.Equal(await ReadProblemFieldsAsync(missing, ct), await ReadProblemFieldsAsync(foreign, ct));
    }

    // T-07: wrong article, expired and future configurations do not apply to this visible sample.
    [Fact]
    public async Task VisibleSampleWithoutApplicableSpecificationHasItsOwnNotFoundCode()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await CreateSpecificationAsync(other.ArticleId, 1, MeasuredAt, null, (setup.Parameter.Id, true));
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-2), MeasuredAt.AddDays(-1),
            (setup.Parameter.Id, true));
        await CreateSpecificationAsync(setup.ArticleId, 2, MeasuredAt.AddDays(1), null, (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        using var response = await client.GetAsync(SpecificationPath(setup), ct);
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "QUALITY_APPLICABLE_SPECIFICATION_NOT_FOUND", ct);
        using var missing = await client.GetAsync(SpecificationPath(setup.Sample.OrganizationId, Guid.NewGuid()), ct);
        await AssertProblemAsync(missing, HttpStatusCode.NotFound, "QUALITY_SPECIFICATION_SAMPLE_NOT_FOUND", ct);
    }

    // T-08: touching inclusive boundaries are an overlap, with the same conflict as QLT-004.
    [Fact]
    public async Task SpecificationReadReportsExistingAmbiguityCodeWithoutChangingSample()
    {
        var setup = await CreateSetupAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-1), MeasuredAt, (setup.Parameter.Id, true));
        await CreateSpecificationAsync(setup.ArticleId, 2, MeasuredAt, null, (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        using var response = await client.GetAsync(SpecificationPath(setup), ct);
        var detail = await AssertProblemAsync(response, HttpStatusCode.Conflict, "QUALITY_SPECIFICATION_AMBIGUOUS", ct);
        using var write = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), ct);
        var writeDetail = await AssertProblemAsync(write, HttpStatusCode.Conflict, "QUALITY_SPECIFICATION_AMBIGUOUS", ct);
        Assert.Equal(writeDetail, detail);
        var samples = await ReadPageAsync<SampleListResponse>(client, SamplePath(setup), ct);
        Assert.Equal("PENDING", Assert.Single(samples.Items).Status);
    }

    // T-09: real JWT authentication, existing roles and unchanged permission matrix on both routes.
    [Theory]
    [InlineData("QualityManager")]
    [InlineData("Laboratory")]
    [InlineData("Auditor")]
    public async Task QualityReadersCanReadBlockHistoryAndApplicableSpecification(string role)
    {
        var roleId = role switch
        {
            "QualityManager" => StandardRoleIds.QualityManager,
            "Laboratory" => StandardRoleIds.Laboratory,
            "Auditor" => StandardRoleIds.Auditor,
            _ => throw new InvalidOperationException(),
        };
        var setup = await CreateSetupAsync(roleId);
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt, null, (setup.Parameter.Id, true));
        var block = LotBlock.Create(Guid.NewGuid(), setup.Sample.OrganizationId, setup.Sample.LotId,
            "Read role fixture", MeasuredAt, setup.Account.UserId, MeasuredAt);
        await using (var quality = database.CreateQualityDbContext())
        {
            quality.LotBlocks.Add(block);
            await quality.SaveChangesAsync();
        }
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        var blocks = await ReadPageAsync<LotBlockListResponse>(client, HistoryPath(setup), ct);
        Assert.Equal(block.Id, Assert.Single(blocks.Items).Id);
        var specification = await ReadPageAsync<SampleSpecificationResponse>(client, SpecificationPath(setup), ct);
        Assert.Equal(setup.ArticleId, specification.ArticleId);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProducerOrNonmemberCannotReadBlockHistoryOrSpecification(bool nonmember)
    {
        var setup = await CreateSetupAsync(StandardRoleIds.Producer);
        var account = nonmember ? (await CreateSetupAsync()).Account : setup.Account;
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt, null, (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, account, ct);
        foreach (var path in new[] { HistoryPath(setup), SpecificationPath(setup) })
        {
            using var response = await client.GetAsync(path, ct);
            await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", ct);
        }
    }

    private static string HistoryPath(Setup setup) =>
        $"/api/v1/organizations/{setup.Sample.OrganizationId}/lots/{setup.Sample.LotId}/blocks";

    private static string SpecificationPath(Setup setup) => SpecificationPath(setup.Sample.OrganizationId, setup.Sample.Id);
    private static string SpecificationPath(Guid organizationId, Guid sampleId) =>
        $"/api/v1/organizations/{organizationId}/samples/{sampleId}/specification";
}
