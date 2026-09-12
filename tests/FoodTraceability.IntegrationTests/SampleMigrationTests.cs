using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class SampleMigrationTests(PostgreSqlContainerFixture database)
{
    private const string UniqueIndex = "ux_sample_organization_id_sample_number_upper";
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset TakenAt = new(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 12, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SampleEventTypeId = Guid.Parse("49163869-4ef0-501f-984d-ab3adb5e1996");

    [Fact]
    public async Task SampleHasExactlyTheNineExpectedRequiredColumns()
    {
        var columns = await QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'quality' AND table_name = 'sample'
            ORDER BY column_name;
            """,
            static reader => new DatabaseColumn(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)));

        Assert.Equal(
            [
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null),
                new DatabaseColumn("location_id", "NO", "uuid", null),
                new DatabaseColumn("lot_id", "NO", "uuid", null),
                new DatabaseColumn("organization_id", "NO", "uuid", null),
                new DatabaseColumn("sample_id", "NO", "uuid", null),
                new DatabaseColumn("sample_number", "NO", "character varying", Sample.MaximumSampleNumberLength),
                new DatabaseColumn("status", "NO", "character varying", 7),
                new DatabaseColumn("taken_at", "NO", "timestamp with time zone", null),
                new DatabaseColumn("traceability_event_id", "NO", "uuid", null),
            ],
            columns);
    }

    [Fact]
    public async Task SampleHasExactlyTheThreeTenantSafeRestrictForeignKeys()
    {
        var constraints = await QueryAsync(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_catalog.pg_constraint
            WHERE conrelid = 'quality.sample'::regclass AND contype = 'f'
            ORDER BY conname;
            """,
            static reader => (Name: reader.GetString(0), Definition: reader.GetString(1)));

        Assert.Equal(
            [
                ("fk_sample_org_location", "FOREIGN KEY (location_id, organization_id) REFERENCES org.location(location_id, organization_id) ON DELETE RESTRICT"),
                ("fk_sample_trace_lot", "FOREIGN KEY (lot_id, organization_id) REFERENCES trace.lot(lot_id, organization_id) ON DELETE RESTRICT"),
                ("fk_sample_trace_traceability_event", "FOREIGN KEY (traceability_event_id, organization_id) REFERENCES trace.traceability_event(event_id, organization_id) ON DELETE RESTRICT"),
            ],
            constraints);
    }

    [Fact]
    public async Task LotRetainsBothAlternateKeysWithTheirExactColumnOrder()
    {
        var constraints = await QueryAsync(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_catalog.pg_constraint
            WHERE conrelid = 'trace.lot'::regclass AND contype = 'u'
            ORDER BY conname;
            """,
            static reader => (Name: reader.GetString(0), Definition: reader.GetString(1)));

        Assert.Equal(
            [
                ("ak_lot_lot_id_organization_id", "UNIQUE (lot_id, organization_id)"),
                ("ak_lot_lot_id_organization_id_unit_id", "UNIQUE (lot_id, organization_id, unit_id)"),
            ],
            constraints);
    }

    [Theory]
    [InlineData("Sample-123")]
    [InlineData("sAMPLE-123")]
    public async Task DuplicateNumberWithinOrganizationIsRejectedEvenAcrossLots(string duplicateNumber)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var first = await CreateReferencesAsync(timeout.Token);
        var second = await CreateReferencesAsync(timeout.Token, first.OrganizationId);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.Samples.Add(first.NewSample("Sample-123"));
        Assert.Equal(1, await context.SaveChangesAsync(timeout.Token));
        context.Samples.Add(second.NewSample(duplicateNumber));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, UniqueIndex);
    }

    [Fact]
    public async Task SameNumberInDifferentOrganizationsIsAcceptedAndSamplesRoundTrip()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var first = await CreateReferencesAsync(timeout.Token);
        var second = await CreateReferencesAsync(timeout.Token);
        var firstSample = first.NewSample("Sample-123");
        var secondSample = second.NewSample("Sample-123");
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.Samples.AddRange(firstSample, secondSample);

        Assert.Equal(2, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();
        foreach (var expected in new[] { firstSample, secondSample })
        {
            var stored = await context.Samples.SingleAsync(sample => sample.Id == expected.Id, timeout.Token);
            Assert.Equal(expected.OrganizationId, stored.OrganizationId);
            Assert.Equal(expected.LotId, stored.LotId);
            Assert.Equal(expected.LocationId, stored.LocationId);
            Assert.Equal(expected.TraceabilityEventId, stored.TraceabilityEventId);
            Assert.Equal(expected.SampleNumber, stored.SampleNumber);
            Assert.Equal(expected.TakenAt, stored.TakenAt);
            Assert.Equal(expected.CreatedAt, stored.CreatedAt);
            Assert.Equal(SampleStatus.Pending, stored.Status);
        }
    }

    [Theory]
    [InlineData(nameof(Sample.LotId), "fk_sample_trace_lot")]
    [InlineData(nameof(Sample.TraceabilityEventId), "fk_sample_trace_traceability_event")]
    [InlineData(nameof(Sample.LocationId), "fk_sample_org_location")]
    public async Task UnknownReferenceIsRejectedByItsSpecificForeignKey(string field, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        var invalid = references.WithReference(field, Guid.NewGuid());

        await AssertSampleRejectedAsync(invalid.NewSample("UNKNOWN-REFERENCE"), constraint, timeout.Token);
    }

    [Theory]
    [InlineData(nameof(Sample.LotId), "fk_sample_trace_lot")]
    [InlineData(nameof(Sample.TraceabilityEventId), "fk_sample_trace_traceability_event")]
    [InlineData(nameof(Sample.LocationId), "fk_sample_org_location")]
    public async Task ExistingReferenceFromAnotherOrganizationIsRejected(string field, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var own = await CreateReferencesAsync(timeout.Token);
        var foreign = await CreateReferencesAsync(timeout.Token);
        var foreignId = field switch
        {
            nameof(Sample.LotId) => foreign.LotId,
            nameof(Sample.TraceabilityEventId) => foreign.EventId,
            nameof(Sample.LocationId) => foreign.LocationId,
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };

        // Only one reference changes; the other references still belong to the sample's
        // organization. This detects a missing organization column in each individual FK.
        await AssertSampleRejectedAsync(
            own.WithReference(field, foreignId).NewSample("FOREIGN-REFERENCE"), constraint, timeout.Token);
    }

    [Theory]
    [InlineData("BLOCKED")]
    [InlineData("UNKNOWN")]
    [InlineData("pending")]
    public async Task StatusOutsideTheThreeCodesIsRejectedByCheckConstraint(string status)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var id = Guid.NewGuid();

        // Direct SQL bypasses the converter to exercise the database CHECK itself.
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO quality.sample
                    (sample_id, organization_id, lot_id, location_id, traceability_event_id,
                     sample_number, taken_at, status, created_at)
                VALUES ({id}, {references.OrganizationId}, {references.LotId}, {references.LocationId},
                        {references.EventId}, {"INVALID-STATUS"}, {TakenAt}, {status}, {CreatedAt});
                """, timeout.Token));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_sample_status", exception.ConstraintName);
    }

    [Theory]
    [InlineData("PENDING", SampleStatus.Pending)]
    [InlineData("PASS", SampleStatus.Pass)]
    [InlineData("FAIL", SampleStatus.Fail)]
    public async Task EachDecidedStatusCanBeStoredAndMaterialized(string code, SampleStatus expectedStatus)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var references = await CreateReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var id = Guid.NewGuid();

        // Schema/converter coverage only: no status-change API is introduced in QLT-002a.
        Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO quality.sample
                (sample_id, organization_id, lot_id, location_id, traceability_event_id,
                 sample_number, taken_at, status, created_at)
            VALUES ({id}, {references.OrganizationId}, {references.LotId}, {references.LocationId},
                    {references.EventId}, {"VALID-STATUS"}, {TakenAt}, {code}, {CreatedAt});
            """, timeout.Token));

        var stored = await context.Samples.SingleAsync(sample => sample.Id == id, timeout.Token);
        Assert.Equal(expectedStatus, stored.Status);
    }

    private async Task<SampleReferences> CreateReferencesAsync(
        CancellationToken cancellationToken,
        Guid? existingOrganizationId = null)
    {
        var organizationId = existingOrganizationId ?? Guid.NewGuid();
        var location = Location.Create(
            Guid.NewGuid(), organizationId, "Sample Test Location", null, null, null, null, null, CreatedAt);
        await using (var organizations = database.CreateQualityOrganizationsDbContext())
        {
            if (existingOrganizationId is null)
            {
                organizations.Organizations.Add(Organization.Create(
                    organizationId, $"Sample Test Organization {organizationId:N}",
                    null, null, null, null, CreatedAt));
            }

            organizations.Locations.Add(location);
            await organizations.SaveChangesAsync(cancellationToken);
        }

        var product = Product.Create(
            Guid.NewGuid(), $"PRODUCT-{Guid.NewGuid():N}", "Sample Test Product", CreatedAt);
        var article = Article.Create(
            Guid.NewGuid(), organizationId, product.Id, $"ARTICLE-{Guid.NewGuid():N}", null, CreatedAt);
        var unit = Unit.Create(
            Guid.NewGuid(), UnitCode.Create($"U{Guid.NewGuid():N}"[..UnitCode.MaximumLength]),
            "u", UnitDimension.Mass, CreatedAt);
        await using (var catalog = database.CreateQualityCatalogDbContext())
        {
            catalog.Units.Add(unit);
            catalog.Products.Add(product);
            catalog.Articles.Add(article);
            await catalog.SaveChangesAsync(cancellationToken);
        }

        var user = User.Create(
            Guid.NewGuid(), EmailAddress.Create($"sample-{Guid.NewGuid():N}@example.com"),
            "Sample", "Tester", CreatedAt);
        await using (var identity = database.CreateQualityIdentityDbContext())
        {
            identity.Users.Add(user);
            await identity.SaveChangesAsync(cancellationToken);
        }

        var lot = Lot.Create(
            Guid.NewGuid(), organizationId, article.Id, $"LOT-{Guid.NewGuid():N}", 100m, unit.Id, CreatedAt);
        var traceabilityEvent = TraceabilityEvent.Create(
            Guid.NewGuid(), SampleEventTypeId, organizationId, location.Id, TakenAt,
            null, null, user.Id, CreatedAt,
            [EventInput.Create(Guid.NewGuid(), lot.Id, 1m, unit.Id)], []);
        await using (var traceability = database.CreateQualityTraceabilityDbContext())
        {
            traceability.Lots.Add(lot);
            await traceability.SaveChangesAsync(cancellationToken);
            traceability.TraceabilityEvents.Add(traceabilityEvent);
            await traceability.SaveChangesAsync(cancellationToken);
        }

        return new SampleReferences(organizationId, lot.Id, location.Id, traceabilityEvent.Id);
    }

    private async Task AssertSampleRejectedAsync(
        Sample sample,
        string constraint,
        CancellationToken cancellationToken)
    {
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        context.Samples.Add(sample);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(cancellationToken));

        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, constraint);
    }

    private static void AssertConstraint(DbUpdateException exception, string sqlState, string constraint)
    {
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgresException.SqlState);
        Assert.Equal(constraint, postgresException.ConstraintName);
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

    private sealed record SampleReferences(Guid OrganizationId, Guid LotId, Guid LocationId, Guid EventId)
    {
        public Sample NewSample(string number) => Sample.Create(
            Guid.NewGuid(), OrganizationId, LotId, LocationId, EventId, number, TakenAt, CreatedAt);

        public SampleReferences WithReference(string field, Guid id) => field switch
        {
            nameof(Sample.LotId) => this with { LotId = id },
            nameof(Sample.LocationId) => this with { LocationId = id },
            nameof(Sample.TraceabilityEventId) => this with { EventId = id },
            _ => throw new ArgumentOutOfRangeException(nameof(field)),
        };
    }

    private sealed record DatabaseColumn(string Name, string IsNullable, string DataType, int? MaximumLength);
}
