using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Quality.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Contracts.Traceability;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ContractStatus = FoodTraceability.Platform.Contracts.Traceability.LotQualityStatus;
using DomainStatus = FoodTraceability.Modules.Traceability.Domain.LotQualityStatus;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LotBlockPersistenceTests(PostgreSqlContainerFixture database)
{
    private const string OpenIndex = "ux_lot_block_lot_id_open";
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task LotBlockHasExactlyTheExpectedColumnsWithoutStatus()
    {
        var columns = await QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'quality' AND table_name = 'lot_block'
            ORDER BY column_name;
            """,
            reader => (reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3)));

        Assert.Equal(
            [
                ("blocked_at", "NO", "timestamp with time zone", (int?)null),
                ("blocked_by", "NO", "uuid", (int?)null),
                ("created_at", "NO", "timestamp with time zone", (int?)null),
                ("lot_block_id", "NO", "uuid", (int?)null),
                ("lot_id", "NO", "uuid", (int?)null),
                ("organization_id", "NO", "uuid", (int?)null),
                ("reason", "NO", "character varying", (int?)LotBlock.MaximumReasonLength),
                ("released_at", "YES", "timestamp with time zone", (int?)null),
                ("released_by", "YES", "uuid", (int?)null),
            ], columns);
    }

    [Fact]
    public async Task LotBlockHasExactlyTheThreeRestrictForeignKeys()
    {
        var constraints = await QueryAsync(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_catalog.pg_constraint
            WHERE conrelid = 'quality.lot_block'::regclass AND contype = 'f'
            ORDER BY conname;
            """, reader => (reader.GetString(0), reader.GetString(1)));

        Assert.Equal(
            [
                ("fk_lot_block_blocked_by_identity_user", "FOREIGN KEY (blocked_by) REFERENCES identity.\"user\"(user_id) ON DELETE RESTRICT"),
                ("fk_lot_block_released_by_identity_user", "FOREIGN KEY (released_by) REFERENCES identity.\"user\"(user_id) ON DELETE RESTRICT"),
                ("fk_lot_block_trace_lot", "FOREIGN KEY (lot_id, organization_id) REFERENCES trace.lot(lot_id, organization_id) ON DELETE RESTRICT"),
            ], constraints);
    }

    [Fact]
    public async Task SecondOpenBlockIsRejectedByThePartialUniqueIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        await using var quality = database.CreateQualityDbContext();
        await using var transaction = await quality.Database.BeginTransactionAsync(timeout.Token);
        quality.LotBlocks.Add(references.NewBlock());
        Assert.Equal(1, await quality.SaveChangesAsync(timeout.Token));
        quality.LotBlocks.Add(references.NewBlock());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            quality.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, OpenIndex);
    }

    [Fact]
    public async Task ReleasedBlockAllowsANewOpenBlockForTheSameLotAndHistoryRoundTrips()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        await using var quality = database.CreateQualityDbContext();
        await using var transaction = await quality.Database.BeginTransactionAsync(timeout.Token);
        var first = references.NewBlock();
        quality.LotBlocks.Add(first);
        Assert.Equal(1, await quality.SaveChangesAsync(timeout.Token));
        first.Release(Now.AddHours(1), references.UserId);
        Assert.Equal(1, await quality.SaveChangesAsync(timeout.Token));

        var second = references.NewBlock();
        quality.LotBlocks.Add(second);
        Assert.Equal(1, await quality.SaveChangesAsync(timeout.Token));
        quality.ChangeTracker.Clear();

        var history = await quality.LotBlocks.Where(block => block.LotId == references.LotId)
            .ToListAsync(timeout.Token);
        Assert.Equal(2, history.Count);
        var released = Assert.Single(history, block => block.ReleasedAt is not null);
        var open = Assert.Single(history, block => block.ReleasedAt is null);
        Assert.Equal(first.Id, released.Id);
        Assert.Equal(Now.AddHours(1), released.ReleasedAt);
        Assert.Equal(references.UserId, released.ReleasedBy);
        Assert.Equal(second.Id, open.Id);
        Assert.Null(open.ReleasedBy);
        Assert.All(history, block =>
        {
            Assert.Equal(references.OrganizationId, block.OrganizationId);
            Assert.Equal(references.LotId, block.LotId);
            Assert.Equal(references.UserId, block.BlockedBy);
            Assert.Equal("Investigation required", block.Reason);
            Assert.Equal(Now, block.BlockedAt);
            Assert.Equal(Now, block.CreatedAt);
        });
    }

    [Fact]
    public async Task LotFromAnotherOrganizationIsRejectedByTheCompositeForeignKey()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var own = await CreateReferencesAsync(timeout.Token);
        var foreign = await CreateReferencesAsync(timeout.Token);
        await using var quality = database.CreateQualityDbContext();
        quality.LotBlocks.Add((own with { LotId = foreign.LotId }).NewBlock());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            quality.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, "fk_lot_block_trace_lot");
    }

    [Theory]
    [InlineData(false, "fk_lot_block_blocked_by_identity_user")]
    [InlineData(true, "fk_lot_block_released_by_identity_user")]
    public async Task UnknownUserIsRejectedByItsSpecificForeignKey(bool released, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        var block = released
            ? references.NewBlock()
            : (references with { UserId = Guid.NewGuid() }).NewBlock();
        if (released)
        {
            block.Release(Now.AddHours(1), Guid.NewGuid());
        }

        await using var quality = database.CreateQualityDbContext();
        quality.LotBlocks.Add(block);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            quality.SaveChangesAsync(timeout.Token));
        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, constraint);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task IncompleteReleaseIsRejectedByThePairCheck(bool timestampOnly)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        await using var quality = database.CreateQualityDbContext();
        await using var transaction = await quality.Database.BeginTransactionAsync(timeout.Token);
        var block = references.NewBlock();
        quality.LotBlocks.Add(block);
        await quality.SaveChangesAsync(timeout.Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => timestampOnly
            ? quality.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE quality.lot_block SET released_at = {Now} WHERE lot_block_id = {block.Id};",
                timeout.Token)
            : quality.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE quality.lot_block SET released_by = {references.UserId} WHERE lot_block_id = {block.Id};",
                timeout.Token));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_lot_block_release_pair", exception.ConstraintName);
    }

    [Theory]
    [InlineData("PASS")]
    [InlineData("FAIL")]
    [InlineData("pending")]
    [InlineData("UNKNOWN")]
    public async Task LotQualityStatusOutsideTheThreeCodesIsRejected(string status)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        await using var traceability = database.CreateQualityTraceabilityDbContext();
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            traceability.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE trace.lot SET quality_status = {status} WHERE lot_id = {references.LotId};",
                timeout.Token));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_lot_quality_status", exception.ConstraintName);
    }

    [Theory]
    [InlineData(ContractStatus.Pending, DomainStatus.Pending)]
    [InlineData(ContractStatus.Blocked, DomainStatus.Blocked)]
    [InlineData(ContractStatus.Released, DomainStatus.Released)]
    public async Task ContractStoresEachStatusAndReturnsTrueForAnUnchangedStatus(
        ContractStatus status, DomainStatus expected)
    {
        await using var factory = CreateFactory();
        var token = factory.RequestCancellationToken;
        var references = await CreateReferencesAsync(token);
        using (var scope = factory.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<ILotQualityStatusWriter>();
            Assert.True(await writer.SetAsync(references.OrganizationId, references.LotId, status, token));
            Assert.True(await writer.SetAsync(references.OrganizationId, references.LotId, status, token));
            Assert.False(scope.ServiceProvider.GetRequiredService<ScopedTransaction>().IsActive);
        }

        await using var traceability = database.CreateQualityTraceabilityDbContext();
        var lot = await traceability.Lots.SingleAsync(lot => lot.Id == references.LotId, token);
        Assert.Equal(expected, lot.QualityStatus);
        Assert.Equal(references.OrganizationId, lot.OrganizationId);
        Assert.Equal(references.ArticleId, lot.ArticleId);
        Assert.Equal(references.UnitId, lot.UnitId);
        Assert.Equal("LOT", lot.LotNumber);
        Assert.Equal(100m, lot.Quantity);
        Assert.Equal(Now, lot.CreatedAt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContractReturnsFalseForUnknownOrForeignLotWithoutChangingIt(bool foreignLot)
    {
        await using var factory = CreateFactory();
        var token = factory.RequestCancellationToken;
        var own = await CreateReferencesAsync(token);
        var foreign = await CreateReferencesAsync(token);
        using (var scope = factory.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<ILotQualityStatusWriter>();
            Assert.False(await writer.SetAsync(own.OrganizationId,
                foreignLot ? foreign.LotId : Guid.NewGuid(), ContractStatus.Blocked, token));
        }

        await using var traceability = database.CreateQualityTraceabilityDbContext();
        var lots = await traceability.Lots
            .Where(lot => lot.Id == own.LotId || lot.Id == foreign.LotId).ToListAsync(token);
        Assert.Equal(2, lots.Count);
        Assert.All(lots, lot => Assert.Equal(DomainStatus.Pending, lot.QualityStatus));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OuterTransactionCommitsOrRollsBackBothBlockAndContractStatus(bool commit)
    {
        await using var factory = CreateFactory();
        var token = factory.RequestCancellationToken;
        var references = await CreateReferencesAsync(token);
        var block = references.NewBlock();

        using (var scope = factory.Services.CreateScope())
        {
            var scopedTransaction = scope.ServiceProvider.GetRequiredService<ScopedTransaction>();
            var quality = scope.ServiceProvider.GetRequiredService<QualityDbContext>();
            await using var transaction = await scopedTransaction.BeginAsync(token);
            await scopedTransaction.EnlistAsync(quality, token);
            quality.LotBlocks.Add(block);
            Assert.Equal(1, await quality.SaveChangesAsync(token));

            var writer = scope.ServiceProvider.GetRequiredService<ILotQualityStatusWriter>();
            Assert.True(await writer.SetAsync(
                references.OrganizationId, references.LotId, ContractStatus.Blocked, token));
            Assert.True(scopedTransaction.IsActive);
            var traceability = scope.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
            Assert.Equal(DomainStatus.Blocked, await traceability.Lots
                .Where(lot => lot.Id == references.LotId).Select(lot => lot.QualityStatus).SingleAsync(token));

            if (commit)
            {
                await transaction.CommitAsync(token);
            }
            else
            {
                await transaction.RollbackAsync(token);
            }
        }

        using var verification = factory.Services.CreateScope();
        var persistedQuality = verification.ServiceProvider.GetRequiredService<QualityDbContext>();
        Assert.Equal(commit, await persistedQuality.LotBlocks.AsNoTracking()
            .AnyAsync(candidate => candidate.Id == block.Id, token));
        var persistedTraceability = verification.ServiceProvider.GetRequiredService<TraceabilityDbContext>();
        var status = await persistedTraceability.Lots.AsNoTracking()
            .Where(lot => lot.Id == references.LotId).Select(lot => lot.QualityStatus).SingleAsync(token);
        Assert.Equal(commit ? DomainStatus.Blocked : DomainStatus.Pending, status);
    }

    [Fact]
    public async Task UndefinedContractStatusIsRejectedBeforeWriting()
    {
        await using var factory = CreateFactory();
        var token = factory.RequestCancellationToken;
        var references = await CreateReferencesAsync(token);
        using (var scope = factory.Services.CreateScope())
        {
            var writer = scope.ServiceProvider.GetRequiredService<ILotQualityStatusWriter>();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                writer.SetAsync(references.OrganizationId, references.LotId, (ContractStatus)99, token));
            Assert.False(scope.ServiceProvider.GetRequiredService<ScopedTransaction>().IsActive);
        }

        await using var traceability = database.CreateQualityTraceabilityDbContext();
        Assert.Equal(DomainStatus.Pending, await traceability.Lots
            .Where(lot => lot.Id == references.LotId).Select(lot => lot.QualityStatus).SingleAsync(token));
    }

    private async Task<References> CreateReferencesAsync(CancellationToken token)
    {
        var organizationId = Guid.NewGuid();
        await using (var organizations = database.CreateQualityOrganizationsDbContext())
        {
            organizations.Organizations.Add(Organization.Create(
                organizationId, $"Lot block organization {organizationId:N}", null, null, null, null, Now));
            await organizations.SaveChangesAsync(token);
        }

        var product = Product.Create(Guid.NewGuid(), $"P-{Guid.NewGuid():N}", "Lot block product", Now);
        var article = Article.Create(
            Guid.NewGuid(), organizationId, product.Id, $"A-{Guid.NewGuid():N}", null, Now);
        var unit = Unit.Create(
            Guid.NewGuid(), UnitCode.Create($"U{Guid.NewGuid():N}"[..UnitCode.MaximumLength]),
            "u", UnitDimension.Mass, Now);
        await using (var catalog = database.CreateQualityCatalogDbContext())
        {
            catalog.Products.Add(product);
            catalog.Articles.Add(article);
            catalog.Units.Add(unit);
            await catalog.SaveChangesAsync(token);
        }

        var user = User.Create(
            Guid.NewGuid(), EmailAddress.Create($"block-{Guid.NewGuid():N}@example.com"), "Block", "Tester", Now);
        await using (var identity = database.CreateQualityIdentityDbContext())
        {
            identity.Users.Add(user);
            await identity.SaveChangesAsync(token);
        }

        var lot = Lot.Create(Guid.NewGuid(), organizationId, article.Id, "LOT", 100m, unit.Id, Now);
        await using (var traceability = database.CreateQualityTraceabilityDbContext())
        {
            traceability.Lots.Add(lot);
            await traceability.SaveChangesAsync(token);
        }

        return new References(organizationId, lot.Id, user.Id, article.Id, unit.Id);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.QualityConnectionString,
        });

    private static void AssertConstraint(DbUpdateException exception, string state, string name)
    {
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(state, postgres.SqlState);
        Assert.Equal(name, postgres.ConstraintName);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.QualityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(timeout.Token);
        var rows = new List<T>();
        while (await reader.ReadAsync(timeout.Token))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private sealed record References(Guid OrganizationId, Guid LotId, Guid UserId, Guid ArticleId, Guid UnitId)
    {
        public LotBlock NewBlock() =>
            LotBlock.Create(Guid.NewGuid(), OrganizationId, LotId, "Investigation required", Now, UserId, Now);
    }
}
