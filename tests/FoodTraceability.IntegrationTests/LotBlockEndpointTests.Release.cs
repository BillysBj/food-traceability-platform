using System.Net;
using System.Net.Http.Json;
using FoodTraceability.Api.Contracts.LotBlocks;
using FoodTraceability.BuildingBlocks;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Quality.Application.LotBlocks;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

public sealed partial class LotBlockEndpointTests
{
    [Fact]
    public async Task QualityManagerReleasesBlockAndLotWithPersistedDecisionFields()
    {
        var setup = await CreateSetupAsync();
        var releaser = await CreateMemberAsync(setup.OrganizationId, StandardRoleIds.QualityManager);
        var now = TimestampPrecision.TruncateToMicroseconds(DateTimeOffset.UtcNow);
        await using var factory = CreateFactory(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now.AddTicks(7))));
        using var client = factory.CreateClient();
        var original = await CreateBlockAsync(client, factory, setup);
        await AuthenticateAsync(client, releaser, factory.RequestCancellationToken);

        using var response = await client.PostAsync(ReleasePath(setup, original.Id), null, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);
        var released = await ReadReleaseAsync(response, factory.RequestCancellationToken);
        Assert.Equal(original.Id, released.Id);
        Assert.Equal(original.LotId, released.LotId);
        Assert.Equal(original.Reason, released.Reason);
        Assert.Equal(original.BlockedAt, released.BlockedAt);
        Assert.Equal(original.BlockedBy, released.BlockedBy);
        Assert.Equal(now, released.ReleasedAt);
        Assert.Equal(releaser.UserId, released.ReleasedBy);
        Assert.Equal("RELEASED", released.QualityStatus);
        await AssertReleasedAsync(factory, setup, released);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReleaseWithoutMembershipOrWithLaboratoryRoleReturns403WithoutWrites(bool laboratory)
    {
        var setup = await CreateSetupAsync();
        var denied = laboratory
            ? await CreateMemberAsync(setup.OrganizationId, StandardRoleIds.Laboratory)
            : await CreateAccountAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var original = await CreateBlockAsync(client, factory, setup);
        await AuthenticateAsync(client, denied, factory.RequestCancellationToken);

        using var response = await client.PostAsync(ReleasePath(setup, original.Id), null, factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "AUTHORIZATION_DENIED", factory.RequestCancellationToken);
        await AssertOpenAsync(factory, setup, original);
    }

    [Fact]
    public async Task ForeignAndUnknownBlocksReturnIdentical404AndLeaveBothOrganizationsUntouched()
    {
        var own = await CreateSetupAsync();
        var foreign = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var foreignBlock = await CreateBlockAsync(client, factory, foreign);
        var ownBlock = await CreateBlockAsync(client, factory, own);

        using var foreignResponse = await client.PostAsync(ReleasePath(own, foreignBlock.Id), null, factory.RequestCancellationToken);
        using var unknownResponse = await client.PostAsync(ReleasePath(own, Guid.NewGuid()), null, factory.RequestCancellationToken);
        // Matching the foreign lot as well must not bypass the organization predicate.
        using var foreignTupleResponse = await client.PostAsync(
            $"{BlockPath(own.OrganizationId, foreign.Lot.Id)}/{foreignBlock.Id}/release", null,
            factory.RequestCancellationToken);

        var foreignProblem = await AssertProblemAsync(foreignResponse, HttpStatusCode.NotFound,
            "LOT_BLOCK_RELEASE_NOT_FOUND", factory.RequestCancellationToken);
        var unknownProblem = await AssertProblemAsync(unknownResponse, HttpStatusCode.NotFound,
            "LOT_BLOCK_RELEASE_NOT_FOUND", factory.RequestCancellationToken);
        var foreignTupleProblem = await AssertProblemAsync(foreignTupleResponse, HttpStatusCode.NotFound,
            "LOT_BLOCK_RELEASE_NOT_FOUND", factory.RequestCancellationToken);
        Assert.Equal(ComparableProblem(unknownProblem), ComparableProblem(foreignProblem));
        Assert.Equal(ComparableProblem(unknownProblem), ComparableProblem(foreignTupleProblem));
        await AssertOpenAsync(factory, own, ownBlock);
        await AssertOpenAsync(factory, foreign, foreignBlock);
    }

    [Fact]
    public async Task BlockUnderAnotherLotOfSameOrganizationReturns404WithoutWrites()
    {
        var setup = await CreateSetupAsync();
        var otherLot = Lot.Create(Guid.NewGuid(), setup.OrganizationId, setup.Lot.ArticleId,
            $"RELEASE-OTHER-{Guid.NewGuid():N}", 10m, KilogramId, DateTimeOffset.UtcNow);
        await using (var context = database.CreateQualityTraceabilityDbContext())
        {
            context.Lots.Add(otherLot);
            await context.SaveChangesAsync();
        }
        var other = setup with { Lot = otherLot };
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var original = await CreateBlockAsync(client, factory, setup);

        using var response = await client.PostAsync(ReleasePath(other, original.Id), null, factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "LOT_BLOCK_RELEASE_NOT_FOUND", factory.RequestCancellationToken);
        await AssertOpenAsync(factory, setup, original);
        Assert.Equal(LotQualityStatus.Pending, await ReadStatusAsync(factory, other));
    }

    [Fact]
    public async Task SecondReleaseReturns409WithoutChangingReleaseFields()
    {
        var setup = await CreateSetupAsync();
        var secondReleaser = await CreateMemberAsync(setup.OrganizationId, StandardRoleIds.QualityManager);
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var original = await CreateBlockAsync(client, factory, setup);
        var released = await ReleaseBlockAsync(client, factory, setup, original.Id);
        await AuthenticateAsync(client, secondReleaser, factory.RequestCancellationToken);

        using var response = await client.PostAsync(ReleasePath(setup, original.Id), null, factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "LOT_BLOCK_RELEASE_CONFLICT", factory.RequestCancellationToken);
        await AssertReleasedAsync(factory, setup, released);
    }

    [Fact]
    public async Task ReleasedBlockCannotReleaseNewBlockOfSameLot()
    {
        var setup = await CreateSetupAsync();
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var first = await CreateBlockAsync(client, factory, setup);
        var released = await ReleaseBlockAsync(client, factory, setup, first.Id);
        // Exercises QLT-005 again through HTTP and requires 201 (T-08).
        var second = await CreateBlockAsync(client, factory, setup);
        Assert.NotEqual(first.Id, second.Id);

        using var response = await client.PostAsync(ReleasePath(setup, first.Id), null, factory.RequestCancellationToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "LOT_BLOCK_RELEASE_CONFLICT", factory.RequestCancellationToken);
        var persisted = await ReadBlocksAsync(factory, setup.OrganizationId);
        Assert.Equal(2, persisted.Count);
        var a = Assert.Single(persisted, block => block.Id == first.Id);
        Assert.Equal(released.ReleasedAt, a.ReleasedAt);
        Assert.Equal(released.ReleasedBy, a.ReleasedBy);
        var b = Assert.Single(persisted, block => block.Id == second.Id);
        Assert.Null(b.ReleasedAt);
        Assert.Null(b.ReleasedBy);
        Assert.Equal(second.Reason, b.Reason);
        Assert.Equal(second.BlockedAt, b.BlockedAt);
        Assert.Equal(second.BlockedBy, b.BlockedBy);
        Assert.Equal(LotQualityStatus.Blocked, await ReadStatusAsync(factory, setup));
    }

    [Fact]
    public async Task ConcurrentHttpReleasesBothReadOpenBlockButExactlyOneConditionalWriteWins()
    {
        var setup = await CreateSetupAsync();
        var secondReleaser = await CreateMemberAsync(setup.OrganizationId, StandardRoleIds.QualityManager);
        var observation = new ReleaseObservation { SynchronizeReads = true };
        await using var factory = CreateFactory(services => ObserveReleaseWriter(services, observation));
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var original = await CreateBlockAsync(firstClient, factory, setup);
        await AuthenticateAsync(secondClient, secondReleaser, factory.RequestCancellationToken);

        var responses = await Task.WhenAll(
            firstClient.PostAsync(ReleasePath(setup, original.Id), null, factory.RequestCancellationToken),
            secondClient.PostAsync(ReleasePath(setup, original.Id), null, factory.RequestCancellationToken));
        try
        {
            var winner = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var loser = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            await AssertProblemAsync(loser, HttpStatusCode.Conflict, "LOT_BLOCK_RELEASE_CONFLICT", factory.RequestCancellationToken);
            Assert.Equal(2, observation.OpenReads);
            Assert.Equal(2, observation.SaveAttempts);
            Assert.Equal(1, observation.CompletedSaves);
            Assert.Equal(1, observation.RealConflicts);
            var released = await ReadReleaseAsync(winner, factory.RequestCancellationToken);
            Assert.Equal(ReferenceEquals(winner, responses[0]) ? setup.Account.UserId : secondReleaser.UserId,
                released.ReleasedBy);
            await AssertReleasedAsync(factory, setup, released);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FailureBeforeOrAfterReleaseWriteRollsBackStatusAndReleasePair(bool afterSave)
    {
        var setup = await CreateSetupAsync();
        var observation = new ReleaseObservation { FailBeforeSave = !afterSave, FailAfterSave = afterSave };
        await using var factory = CreateFactory(services => ObserveReleaseWriter(services, observation));
        using var client = factory.CreateClient();
        var original = await CreateBlockAsync(client, factory, setup);

        using var response = await client.PostAsync(ReleasePath(setup, original.Id), null, factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(1, observation.SaveAttempts);
        Assert.Equal(afterSave ? 1 : 0, observation.CompletedSaves);
        // The decorator first observes RELEASED inside the actual transaction. A fresh
        // scope must see BLOCKED and the original open block after either injected failure.
        await AssertOpenAsync(factory, setup, original);
    }

    private async Task<TestAccount> CreateMemberAsync(Guid organizationId, Guid roleId)
    {
        var account = await CreateAccountAsync();
        var now = DateTimeOffset.UtcNow;
        await using var context = database.CreateQualityIdentityDbContext();
        context.OrganizationMemberships.Add(OrganizationMembership.Create(account.UserId, organizationId, now));
        context.OrganizationRoleAssignments.Add(OrganizationRoleAssignment.Create(
            Guid.NewGuid(), account.UserId, organizationId, roleId, locationId: null, now));
        await context.SaveChangesAsync();
        return account;
    }

    private static async Task<LotBlockResponse> CreateBlockAsync(HttpClient client, ApiWebApplicationFactory factory, Setup setup)
    {
        await AuthenticateAsync(client, setup.Account, factory.RequestCancellationToken);
        using var response = await client.PostAsJsonAsync(BlockPath(setup),
            new BlockLotRequest("Investigation required"), factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadBlockAsync(response, factory.RequestCancellationToken);
    }

    private static async Task<ReleasedLotBlockResponse> ReleaseBlockAsync(
        HttpClient client, ApiWebApplicationFactory factory, Setup setup, Guid blockId)
    {
        using var response = await client.PostAsync(ReleasePath(setup, blockId), null, factory.RequestCancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadReleaseAsync(response, factory.RequestCancellationToken);
    }

    private static async Task<ReleasedLotBlockResponse> ReadReleaseAsync(HttpResponseMessage response, CancellationToken token) =>
        await response.Content.ReadFromJsonAsync<ReleasedLotBlockResponse>(token)
        ?? throw new InvalidOperationException("The released lot block response body was empty.");

    private static async Task AssertOpenAsync(ApiWebApplicationFactory factory, Setup setup, LotBlockResponse original)
    {
        var block = Assert.Single(await ReadBlocksAsync(factory, setup.OrganizationId));
        Assert.Equal(original.Id, block.Id);
        Assert.Equal(original.LotId, block.LotId);
        Assert.Equal(original.Reason, block.Reason);
        Assert.Equal(original.BlockedAt, block.BlockedAt);
        Assert.Equal(original.BlockedBy, block.BlockedBy);
        Assert.Null(block.ReleasedAt);
        Assert.Null(block.ReleasedBy);
        Assert.Equal(LotQualityStatus.Blocked, await ReadStatusAsync(factory, setup));
    }

    private static async Task AssertReleasedAsync(ApiWebApplicationFactory factory, Setup setup, ReleasedLotBlockResponse released)
    {
        var block = Assert.Single(await ReadBlocksAsync(factory, setup.OrganizationId));
        Assert.Equal(released.Id, block.Id);
        Assert.Equal(released.LotId, block.LotId);
        Assert.Equal(released.Reason, block.Reason);
        Assert.Equal(released.BlockedAt, block.BlockedAt);
        Assert.Equal(released.BlockedBy, block.BlockedBy);
        Assert.Equal(released.ReleasedAt, block.ReleasedAt);
        Assert.Equal(released.ReleasedBy, block.ReleasedBy);
        Assert.Equal(LotQualityStatus.Released, await ReadStatusAsync(factory, setup));
    }

    private static string ReleasePath(Setup setup, Guid blockId) => $"{BlockPath(setup)}/{blockId}/release";

    private static void ObserveReleaseWriter(IServiceCollection services, ReleaseObservation observation)
    {
        var descriptor = Assert.Single(services, item => item.ServiceType == typeof(ILotBlockWriter));
        var implementationType = descriptor.ImplementationType
            ?? throw new InvalidOperationException("Expected the production lot-block writer registration.");
        services.Remove(descriptor);
        services.AddScoped<ILotBlockWriter>(provider => new ObservingReleaseWriter(
            (ILotBlockWriter)ActivatorUtilities.CreateInstance(provider, implementationType),
            provider.GetRequiredService<TraceabilityDbContext>(),
            provider.GetRequiredService<ScopedTransaction>(), observation));
    }

    private sealed class ReleaseObservation
    {
        public bool SynchronizeReads { get; init; }
        public bool FailBeforeSave { get; init; }
        public bool FailAfterSave { get; init; }
        public TaskCompletionSource BothRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int OpenReads;
        public int SaveAttempts;
        public int CompletedSaves;
        public int RealConflicts;
    }

    private sealed class ObservingReleaseWriter(
        ILotBlockWriter inner, TraceabilityDbContext trace,
        ScopedTransaction transaction, ReleaseObservation observation) : ILotBlockWriter
    {
        public Task AddAsync(LotBlock block, CancellationToken cancellationToken) => inner.AddAsync(block, cancellationToken);

        public async Task<LotBlock?> FindAsync(Guid organizationId, Guid lotId, Guid blockId, CancellationToken cancellationToken)
        {
            var block = await inner.FindAsync(organizationId, lotId, blockId, cancellationToken);
            Assert.True(transaction.IsActive);
            Assert.NotNull(block);
            Assert.Null(block.ReleasedAt);
            if (Interlocked.Increment(ref observation.OpenReads) == 2)
            {
                observation.BothRead.TrySetResult();
            }
            if (observation.SynchronizeReads)
            {
                // Both real SELECTs finish before either HTTP request writes. No delays
                // or scheduler assumptions; the existing request timeout only bounds failure.
                await observation.BothRead.Task.WaitAsync(cancellationToken);
            }
            return block;
        }

        public async Task SaveReleaseAsync(LotBlock block, CancellationToken cancellationToken)
        {
            Assert.True(transaction.IsActive);
            Assert.NotNull(trace.Database.CurrentTransaction);
            Assert.Equal(LotQualityStatus.Released, await trace.Lots.AsNoTracking()
                .Where(lot => lot.OrganizationId == block.OrganizationId && lot.Id == block.LotId)
                .Select(lot => lot.QualityStatus).SingleAsync(cancellationToken));
            Interlocked.Increment(ref observation.SaveAttempts);
            if (observation.FailBeforeSave)
            {
                throw new InvalidOperationException("Injected failure before release save.");
            }
            try
            {
                await inner.SaveReleaseAsync(block, cancellationToken);
                Interlocked.Increment(ref observation.CompletedSaves);
            }
            catch (LotBlockAlreadyReleasedException)
            {
                Interlocked.Increment(ref observation.RealConflicts);
                throw;
            }
            if (observation.FailAfterSave)
            {
                throw new InvalidOperationException("Injected failure after release save.");
            }
        }
    }
}
