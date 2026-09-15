using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using FoodTraceability.Api.Contracts.Samples;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Platform.Contracts.Traceability;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

public sealed partial class LabResultEndpointTests
{
    // QLT-004 T-01: the result being created must count before it is persisted.
    [Fact]
    public async Task LastRequiredPassingResultCompletesSampleAndReportsPass()
    {
        var setup = await CreateSetupAsync();
        var second = await CreateParameterAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-1), null,
            (setup.Parameter.Id, true), (second.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var firstResponse = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal("PENDING", (await ReadResultAsync(firstResponse, factory.RequestCancellationToken)).SampleStatus);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);

        using var lastResponse = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { ParameterId = second.Id }, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, lastResponse.StatusCode);
        Assert.Equal("PASS", (await ReadResultAsync(lastResponse, factory.RequestCancellationToken)).SampleStatus);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pass);
        await AssertResultCountAsync(factory, setup, 2);
    }

    // T-02 and AC-06: optional parameters and reference limits do not govern PASS.
    [Fact]
    public async Task MissingOptionalResultAndMeasurementOutsideReferenceLimitsDoNotPreventPass()
    {
        var setup = await CreateSetupAsync();
        var optional = await CreateParameterAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-1), null,
            (setup.Parameter.Id, true), (optional.Id, false));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        // The configured minimum/maximum/target are 10/20/15; the laboratory's PASS is authoritative.
        using var response = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { Value = 999m }, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PASS", (await ReadResultAsync(response, factory.RequestCancellationToken)).SampleStatus);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pass);
        await AssertResultCountAsync(factory, setup, 1);
    }

    // T-03: even a matching time/parameter on a different article is not applicable.
    [Fact]
    public async Task SpecificationForAnotherArticleDoesNotCompleteSample()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await CreateSpecificationAsync(other.ArticleId, 1, MeasuredAt.AddDays(-1), null,
            (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PENDING", (await ReadResultAsync(response, factory.RequestCancellationToken)).SampleStatus);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
        await AssertResultCountAsync(factory, setup, 1);
    }

    // T-04: exclude both future and expired specifications at the sampling time.
    [Theory]
    [InlineData(1, 2)]
    [InlineData(-2, -1)]
    public async Task SpecificationOutsideSamplingTimeIsIgnored(int fromHours, int toHours)
    {
        var setup = await CreateSetupAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1,
            MeasuredAt.AddHours(fromHours), MeasuredAt.AddHours(toHours), (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { MeasuredAt = MeasuredAt.AddHours(fromHours) }, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PENDING", (await ReadResultAsync(response, factory.RequestCancellationToken)).SampleStatus);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
    }

    // T-05 plus inclusive boundary coverage. Measurement time is deliberately outside the finite interval.
    [Theory]
    [InlineData(-1, null)]
    [InlineData(0, 1)]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    public async Task OpenEndedAndInclusiveValidityUseSamplingTime(int fromHours, int? toHours)
    {
        var setup = await CreateSetupAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddHours(fromHours),
            toHours.HasValue ? MeasuredAt.AddHours(toHours.Value) : null, (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(ResultPath(setup),
            ValidRequest(setup) with { MeasuredAt = MeasuredAt.AddDays(10) }, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PASS", (await ReadResultAsync(response, factory.RequestCancellationToken)).SampleStatus);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pass);
    }

    // T-06: use a separate failing parameter, since sample/parameter pairs are unique.
    [Fact]
    public async Task CompleteRequiredPassingResultsNeverReverseEarlierFailure()
    {
        var setup = await CreateSetupAsync();
        var second = await CreateParameterAsync();
        var failed = await CreateParameterAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-1), null,
            (setup.Parameter.Id, true), (second.Id, true), (failed.Id, false));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        foreach (var request in new[]
        {
            ValidRequest(setup) with { ParameterId = failed.Id, Assessment = "FAIL" },
            ValidRequest(setup),
            ValidRequest(setup) with { ParameterId = second.Id },
        })
        {
            using var response = await client.PostAsJsonAsync(ResultPath(setup), request, factory.RequestCancellationToken);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Equal("FAIL", (await ReadResultAsync(response, factory.RequestCancellationToken)).SampleStatus);
        }
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Fail);
        await AssertResultCountAsync(factory, setup, 3);
    }

    // T-07: neither the result nor a sample change may survive ambiguity.
    [Fact]
    public async Task OverlappingSpecificationsReturnDedicatedConflictWithoutWritingResult()
    {
        var setup = await CreateSetupAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-1), null, (setup.Parameter.Id, true));
        await CreateSpecificationAsync(setup.ArticleId, 2, MeasuredAt, MeasuredAt.AddDays(1), (setup.Parameter.Id, true));
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        using var response = await client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken);
        await AssertProblemAsync(response, HttpStatusCode.Conflict, "QUALITY_SPECIFICATION_AMBIGUOUS", factory.RequestCancellationToken);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
        await AssertResultCountAsync(factory, setup, 0);
    }

    // T-08: use the real reachable requests; do not manufacture a sample violating its composite FK.
    [Fact]
    public async Task ForeignLotCannotResolveOrEnterEvaluationThroughSampleOrResultEndpoints()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await using (var identity = database.CreateQualityIdentityDbContext())
        {
            identity.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
                Guid.NewGuid(), setup.Account.UserId, setup.Sample.OrganizationId,
                StandardRoleIds.QualityManager, null, DateTimeOffset.UtcNow));
            await identity.SaveChangesAsync();
        }
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var reader = scope.ServiceProvider.GetRequiredService<ILotArticleReader>();
            Assert.Equal(setup.ArticleId, await reader.FindArticleIdAsync(
                setup.Sample.OrganizationId, setup.Sample.LotId, factory.RequestCancellationToken));
            Assert.Null(await reader.FindArticleIdAsync(
                setup.Sample.OrganizationId, other.Sample.LotId, factory.RequestCancellationToken));
            Assert.Null(await reader.FindArticleIdAsync(
                setup.Sample.OrganizationId, Guid.NewGuid(), factory.RequestCancellationToken));
        }

        using var sampleResponse = await client.PostAsJsonAsync(
            $"/api/v1/organizations/{setup.Sample.OrganizationId}/samples",
            new CreateSampleRequest($"FOREIGN-{Guid.NewGuid():N}", other.Sample.LotId,
                setup.Sample.LocationId, 1m, MeasuredAt), factory.RequestCancellationToken);
        await AssertProblemAsync(sampleResponse, HttpStatusCode.BadRequest, "SAMPLE_VALIDATION_FAILED", factory.RequestCancellationToken);

        using var resultResponse = await client.PostAsJsonAsync(
            ResultPath(setup.Sample.OrganizationId, other.Sample.Id), ValidRequest(other), factory.RequestCancellationToken);
        await AssertProblemAsync(resultResponse, HttpStatusCode.NotFound, "LAB_RESULT_SAMPLE_NOT_FOUND", factory.RequestCancellationToken);
        await AssertResultCountAsync(factory, setup, 0);
        await AssertResultCountAsync(factory, other, 0);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pending);
        await AssertSampleStatusAsync(factory, other, SampleStatus.Pending);
        await using var quality = database.CreateQualityDbContext();
        Assert.Equal(1, await quality.Samples.CountAsync(
            sample => sample.OrganizationId == setup.Sample.OrganizationId, factory.RequestCancellationToken));
    }

    [Fact]
    public async Task ConcurrentRequiredResultsAreSerializedUntilCommitAndCompleteSample()
    {
        var setup = await CreateSetupAsync();
        var second = await CreateParameterAsync();
        await CreateSpecificationAsync(setup.ArticleId, 1, MeasuredAt.AddDays(-1), null,
            (setup.Parameter.Id, true), (second.Id, true));
        var probe = new ConcurrentEvaluationProbe();
        await using var factory = CreateFactory(commandProbe: probe);
        using var client = factory.CreateClient();
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);

        var requests = new[]
        {
            client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup), factory.RequestCancellationToken),
            client.PostAsJsonAsync(ResultPath(setup), ValidRequest(setup) with { ParameterId = second.Id }, factory.RequestCancellationToken),
        };
        var responses = await Task.WhenAll(requests);
        try
        {
            var statuses = new List<string>();
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                statuses.Add((await ReadResultAsync(response, factory.RequestCancellationToken)).SampleStatus);
            }
            Assert.Equal(new[] { "PASS", "PENDING" }, statuses.Order(StringComparer.Ordinal));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
        Assert.Equal(2, probe.LockCount);
        await AssertSampleStatusAsync(factory, setup, SampleStatus.Pass);
        await AssertResultCountAsync(factory, setup, 2);
    }

    private async Task CreateSpecificationAsync(
        Guid articleId, int version, DateTimeOffset validFrom, DateTimeOffset? validTo,
        params (Guid ParameterId, bool Required)[] parameters)
    {
        var now = DateTimeOffset.UtcNow;
        var specification = Specification.Create(Guid.NewGuid(), articleId, version, validFrom, validTo, now);
        _specificationIds.Add(specification.Id);
        await using var context = database.CreateQualityDbContext();
        context.Specifications.Add(specification);
        foreach (var parameter in parameters)
        {
            context.SpecificationParameters.Add(SpecificationParameter.Create(
                Guid.NewGuid(), specification.Id, parameter.ParameterId, 10m, 20m, 15m, parameter.Required, now));
        }
        await context.SaveChangesAsync();
    }

    private sealed class ConcurrentEvaluationProbe : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _bothInitialReads = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondLockAttempted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _sampleReads;
        private int _lockCount;
        public int LockCount => _lockCount;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM quality.sample", StringComparison.Ordinal)
                && Interlocked.Increment(ref _sampleReads) <= 2)
            {
                if (Volatile.Read(ref _sampleReads) == 2)
                {
                    _bothInitialReads.TrySetResult();
                }
                await _bothInitialReads.Task.WaitAsync(cancellationToken);
            }
            if (command.CommandText.Contains("FROM quality.lab_result", StringComparison.Ordinal)
                && Volatile.Read(ref _lockCount) > 0)
            {
                // Keep the first transaction open until the other request attempts its lock.
                await _secondLockAttempted.Task.WaitAsync(cancellationToken);
            }
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal))
            {
                Assert.NotNull(command.Transaction);
                if (Interlocked.Increment(ref _lockCount) == 2)
                {
                    _secondLockAttempted.TrySetResult();
                }
            }
            return ValueTask.FromResult(result);
        }
    }
}
