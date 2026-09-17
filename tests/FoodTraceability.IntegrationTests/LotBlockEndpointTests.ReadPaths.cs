using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Quality.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

public sealed partial class LotBlockEndpointTests
{
    // QLT-009 T-01: create, release and block again through the real HTTP API.
    [Fact]
    public async Task BlockHistoryShowsNewestDecisionFirstAndNullReleaseFieldsForOpenBlock()
    {
        var setup = await CreateSetupAsync();
        // JWT lifetime validation uses the real clock: keep nbf in the past even after advancing
        // this clock, while the 15-minute token lifetime still leaves exp in the future.
        var clock = new BlockHistoryTimeProvider(TimestampPrecision.TruncateToMicroseconds(DateTimeOffset.UtcNow.AddMinutes(-5)));
        await using var factory = CreateFactory(services => services.AddSingleton<TimeProvider>(clock));
        using var client = factory.CreateClient();
        var first = await CreateBlockAsync(client, factory, setup);
        clock.Now = clock.Now.AddSeconds(1);
        var released = await ReleaseBlockAsync(client, factory, setup, first.Id);
        clock.Now = clock.Now.AddSeconds(1);
        var second = await CreateBlockAsync(client, factory, setup);

        var page = await ReadBlockPageAsync(client, BlockPath(setup), factory.RequestCancellationToken);

        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
        Assert.Equal(2L, page.TotalCount);
        Assert.Equal(new[] { second.Id, first.Id }, page.Items.Select(item => item.Id));
        var open = page.Items[0];
        Assert.Equal(second.LotId, open.LotId);
        Assert.Equal(second.Reason, open.Reason);
        Assert.Equal(second.BlockedAt, open.BlockedAt);
        Assert.Equal(second.BlockedBy, open.BlockedBy);
        Assert.Null(open.ReleasedAt);
        Assert.Null(open.ReleasedBy);
        var closed = page.Items[1];
        Assert.Equal(first.LotId, closed.LotId);
        Assert.Equal(first.Reason, closed.Reason);
        Assert.Equal(first.BlockedAt, closed.BlockedAt);
        Assert.Equal(first.BlockedBy, closed.BlockedBy);
        Assert.Equal(released.ReleasedAt, closed.ReleasedAt);
        Assert.Equal(released.ReleasedBy, closed.ReleasedBy);
    }

    // T-02: distinguish an empty history from a missing or foreign parent.
    [Fact]
    public async Task BlockHistoryHidesForeignLotsAndReturnsEmptyPageForExistingLot()
    {
        var setup = await CreateSetupAsync();
        var other = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        await CreateBlockAsync(client, factory, other);
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        using var foreign = await client.GetAsync(BlockPath(setup.OrganizationId, other.Lot.Id), ct);
        using var missing = await client.GetAsync(BlockPath(setup.OrganizationId, Guid.NewGuid()), ct);
        var foreignProblem = await AssertProblemAsync(foreign, HttpStatusCode.NotFound,
            "QUALITY_BLOCK_LIST_LOT_NOT_FOUND", ct);
        var missingProblem = await AssertProblemAsync(missing, HttpStatusCode.NotFound,
            "QUALITY_BLOCK_LIST_LOT_NOT_FOUND", ct);
        Assert.Equal(ComparableProblem(missingProblem), ComparableProblem(foreignProblem));

        var page = await ReadBlockPageAsync(client, BlockPath(setup), ct);
        Assert.Empty(page.Items);
        Assert.Equal(0L, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(50, page.PageSize);
    }

    // T-03: timestamp ties span page boundaries; another lot in the same tenant is excluded.
    [Fact]
    public async Task BlockHistoryPaginationHasStableOrderingWithoutOverlapOrGaps()
    {
        var setup = await CreateSetupAsync();
        var now = TimestampPrecision.TruncateToMicroseconds(DateTimeOffset.UtcNow);
        var blocks = Enumerable.Range(0, 5).Select(index =>
        {
            var block = LotBlock.Create(Guid.NewGuid(), setup.OrganizationId, setup.Lot.Id,
                $"Decision {index}", index == 0 ? now.AddDays(-1) : now, setup.Account.UserId, now);
            block.Release(now, setup.Account.UserId);
            return block;
        }).ToArray();
        var otherLot = FoodTraceability.Modules.Traceability.Domain.Lot.Create(
            Guid.NewGuid(), setup.OrganizationId, setup.Lot.ArticleId,
            $"HISTORY-OTHER-{Guid.NewGuid():N}", 10m, KilogramId, now);
        await using (var trace = database.CreateQualityTraceabilityDbContext())
        {
            trace.Lots.Add(otherLot);
            await trace.SaveChangesAsync();
        }
        await using (var quality = database.CreateQualityDbContext())
        {
            quality.LotBlocks.AddRange(blocks);
            quality.LotBlocks.Add(LotBlock.Create(Guid.NewGuid(), setup.OrganizationId, otherLot.Id,
                "Other lot", now, setup.Account.UserId, now));
            await quality.SaveChangesAsync();
        }
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        var items = new List<LotBlockListItemResponse>();
        for (var pageNumber = 1; pageNumber <= 3; pageNumber++)
        {
            var path = $"{BlockPath(setup)}?page={pageNumber}&pageSize=2";
            var page = await ReadBlockPageAsync(client, path, ct);
            var repeated = await ReadBlockPageAsync(client, path, ct);
            Assert.Equal(page.Items.ToArray(), repeated.Items.ToArray());
            Assert.Equal(pageNumber, page.Page);
            Assert.Equal(2, page.PageSize);
            Assert.Equal(5L, page.TotalCount);
            Assert.Equal(pageNumber == 3 ? 1 : 2, page.Items.Count);
            Assert.All(page.Items, item => Assert.Equal(setup.Lot.Id, item.LotId));
            items.AddRange(page.Items);
        }
        var expected = blocks.OrderByDescending(block => block.BlockedAt).ThenByDescending(block => block.Id)
            .Select(block => block.Id).ToArray();
        Assert.Equal(expected, items.Select(item => item.Id));
        Assert.Equal(5, items.Select(item => item.Id).Distinct().Count());
        var farPage = await ReadBlockPageAsync(client,
            $"{BlockPath(setup)}?page={int.MaxValue}&pageSize=100", ct);
        Assert.Empty(farPage.Items);
        Assert.Equal(5L, farPage.TotalCount);
    }

    [Theory]
    [InlineData("page=0", "page")]
    [InlineData("page=-1", "page")]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    public async Task BlockHistoryRejectsInvalidPagination(string query, string field)
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var ct = factory.RequestCancellationToken;
        await AuthenticateAsync(client, setup.Account, ct);
        using var response = await client.GetAsync($"{BlockPath(setup)}?{query}", ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(problem.RootElement.GetProperty("errors").EnumerateObject(),
            property => property.Name.Equals(field, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<LotBlockListResponse> ReadBlockPageAsync(
        HttpClient client, string path, CancellationToken ct)
    {
        using var response = await client.GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<LotBlockListResponse>(ct)
            ?? throw new InvalidOperationException("The block page response body was empty.");
    }

    private sealed class BlockHistoryTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
