using System.Globalization;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Traceability.Domain;
using FoodTraceability.Modules.Traceability.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class TraceabilityEventMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly Guid EventTypeId =
        Guid.Parse("b2830815-b10e-5507-9ed3-13679fc08e5e");
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 9, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task EventTablesHaveExactlyTheExpectedColumns()
    {
        const string sql = """
            SELECT table_name,
                   column_name,
                   is_nullable,
                   data_type,
                   character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'trace'
              AND table_name IN ('event_input', 'event_output', 'traceability_event')
            ORDER BY table_name, ordinal_position;
            """;

        var columns = await QueryAsync(
            sql,
            static reader => new DatabaseColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4)));

        Assert.Equal(
            [
                new DatabaseColumn("event_input", "event_input_id", "NO", "uuid", null),
                new DatabaseColumn("event_input", "event_id", "NO", "uuid", null),
                new DatabaseColumn("event_input", "organization_id", "NO", "uuid", null),
                new DatabaseColumn("event_input", "lot_id", "NO", "uuid", null),
                new DatabaseColumn("event_input", "quantity", "NO", "numeric", null),
                new DatabaseColumn("event_input", "unit_id", "NO", "uuid", null),
                new DatabaseColumn("event_output", "event_output_id", "NO", "uuid", null),
                new DatabaseColumn("event_output", "event_id", "NO", "uuid", null),
                new DatabaseColumn("event_output", "organization_id", "NO", "uuid", null),
                new DatabaseColumn("event_output", "lot_id", "NO", "uuid", null),
                new DatabaseColumn("event_output", "quantity", "NO", "numeric", null),
                new DatabaseColumn("event_output", "unit_id", "NO", "uuid", null),
                new DatabaseColumn("traceability_event", "event_id", "NO", "uuid", null),
                new DatabaseColumn("traceability_event", "event_type_id", "NO", "uuid", null),
                new DatabaseColumn("traceability_event", "organization_id", "NO", "uuid", null),
                new DatabaseColumn("traceability_event", "location_id", "NO", "uuid", null),
                new DatabaseColumn(
                    "traceability_event",
                    "occurred_at",
                    "NO",
                    "timestamp with time zone",
                    null),
                new DatabaseColumn(
                    "traceability_event",
                    "external_reference",
                    "YES",
                    "character varying",
                    TraceabilityEvent.MaximumExternalReferenceLength),
                new DatabaseColumn(
                    "traceability_event",
                    "description",
                    "YES",
                    "character varying",
                    TraceabilityEvent.MaximumDescriptionLength),
                new DatabaseColumn("traceability_event", "created_by", "NO", "uuid", null),
                new DatabaseColumn(
                    "traceability_event",
                    "created_at",
                    "NO",
                    "timestamp with time zone",
                    null),
            ],
            columns);
    }

    [Fact]
    public async Task EventSideQuantitiesUsePrecisionEighteenAndScaleSix()
    {
        const string sql = """
            SELECT table_name, numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'trace'
              AND table_name IN ('event_input', 'event_output')
              AND column_name = 'quantity'
            ORDER BY table_name;
            """;

        var precisions = await QueryAsync(
            sql,
            static reader => new QuantityPrecision(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2)));

        Assert.Equal(
            [
                new QuantityPrecision("event_input", 18, 6),
                new QuantityPrecision("event_output", 18, 6),
            ],
            precisions);
    }

    [Fact]
    public async Task CompleteEventCanBeSavedAndLoadedWithItsInputAndOutput()
    {
        var data = await CreateEventDataAsync();
        var input = data.NewInput(data.InputLotId, data.UnitId, 100m);
        var output = data.NewOutput(data.OutputLotId, data.UnitId, 80m);
        var traceabilityEvent = data.NewEvent([input], [output]);

        await using (var writeContext = database.CreateTraceabilityDbContext())
        {
            writeContext.TraceabilityEvents.Add(traceabilityEvent);
            Assert.Equal(3, await writeContext.SaveChangesAsync());
        }

        await using var readContext = database.CreateTraceabilityDbContext();
        var storedEvent = await readContext.TraceabilityEvents
            .AsNoTracking()
            .Include(candidate => candidate.Inputs)
            .Include(candidate => candidate.Outputs)
            .SingleAsync(candidate => candidate.Id == traceabilityEvent.Id);

        Assert.Equal(EventTypeId, storedEvent.EventTypeId);
        Assert.Equal(data.OrganizationId, storedEvent.OrganizationId);
        Assert.Equal(data.LocationId, storedEvent.LocationId);
        Assert.Equal(OccurredAt, storedEvent.OccurredAt);
        Assert.Equal("EXT-TRC-007", storedEvent.ExternalReference);
        Assert.Equal("Traceability persistence test", storedEvent.Description);
        Assert.Equal(data.UserId, storedEvent.CreatedBy);
        Assert.Equal(CreatedAt, storedEvent.CreatedAt);
        Assert.Equal(input.Id, Assert.Single(storedEvent.Inputs).Id);
        Assert.Equal(output.Id, Assert.Single(storedEvent.Outputs).Id);
    }

    [Fact]
    public async Task EventWithUnknownOrganizationIsRejected()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            [],
            organizationId: Guid.NewGuid());

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_traceability_event_org_organization");
    }

    [Fact]
    public async Task EventWithLocationFromAnotherOrganizationIsRejected()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            [],
            locationId: data.ForeignLocationId);

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_traceability_event_org_location");
    }

    [Fact]
    public async Task EventWithUnknownCreatorIsRejected()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            [],
            createdBy: Guid.NewGuid());

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_traceability_event_identity_user");
    }

    [Fact]
    public async Task EventWithUnknownEventTypeIsRejected()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            [],
            eventTypeId: Guid.NewGuid());

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_traceability_event_trace_event_type");
    }

    [Fact]
    public async Task InputWithLotFromAnotherOrganizationIsRejected()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.ForeignLotId, data.UnitId)],
            []);

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_event_input_trace_lot");
    }

    [Fact]
    public async Task OutputWithLotFromAnotherOrganizationIsRejected()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [],
            [data.NewOutput(data.ForeignLotId, data.UnitId)]);

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_event_output_trace_lot");
    }

    [Fact]
    public async Task InputWithUnitDifferentFromReferencedLotIsRejected()
    {
        var data = await CreateEventDataAsync();

        // Both the lot and the other unit exist. Only their combination is invalid because
        // the lot was created with UnitId, while the input uses OtherUnitId.
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.OtherUnitId)],
            []);

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_event_input_trace_lot");
    }

    [Fact]
    public async Task OutputWithUnitDifferentFromReferencedLotIsRejected()
    {
        var data = await CreateEventDataAsync();

        // Both the lot and the other unit exist. Only their combination is invalid because
        // the lot was created with UnitId, while the output uses OtherUnitId.
        var traceabilityEvent = data.NewEvent(
            [],
            [data.NewOutput(data.OutputLotId, data.OtherUnitId)]);

        await AssertEventRejectedAsync(
            traceabilityEvent,
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_event_output_trace_lot");
    }

    [Fact]
    public async Task DuplicateInputLotOnTheSameEventIsRejectedByTheExpectedIndex()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            []);
        await SaveEventAsync(traceabilityEvent);

        var duplicate = data.NewInput(data.InputLotId, data.UnitId);
        await using var context = database.CreateTraceabilityDbContext();
        context.Set<EventInput>().Add(duplicate);
        context.Entry(duplicate).Property("EventId").CurrentValue = traceabilityEvent.Id;
        context.Entry(duplicate).Property("OrganizationId").CurrentValue = data.OrganizationId;

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation,
            "ux_event_input_event_id_lot_id");
    }

    [Fact]
    public async Task DuplicateOutputLotOnTheSameEventIsRejectedByTheExpectedIndex()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [],
            [data.NewOutput(data.OutputLotId, data.UnitId)]);
        await SaveEventAsync(traceabilityEvent);

        var duplicate = data.NewOutput(data.OutputLotId, data.UnitId);
        await using var context = database.CreateTraceabilityDbContext();
        context.Set<EventOutput>().Add(duplicate);
        context.Entry(duplicate).Property("EventId").CurrentValue = traceabilityEvent.Id;
        context.Entry(duplicate).Property("OrganizationId").CurrentValue = data.OrganizationId;

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.UniqueViolation,
            "ux_event_output_event_id_lot_id");
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task NonPositiveInputQuantityIsRejectedByTheDatabase(string quantity)
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [],
            [data.NewOutput(data.OutputLotId, data.UnitId)]);
        await SaveEventAsync(traceabilityEvent);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertEventSideDirectlyAsync(
                "event_input",
                "event_input_id",
                traceabilityEvent.Id,
                data.OrganizationId,
                data.InputLotId,
                data.UnitId,
                quantity));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_event_input_quantity_positive", exception.ConstraintName);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task NonPositiveOutputQuantityIsRejectedByTheDatabase(string quantity)
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            []);
        await SaveEventAsync(traceabilityEvent);

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            InsertEventSideDirectlyAsync(
                "event_output",
                "event_output_id",
                traceabilityEvent.Id,
                data.OrganizationId,
                data.OutputLotId,
                data.UnitId,
                quantity));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_event_output_quantity_positive", exception.ConstraintName);
    }

    [Fact]
    public async Task DeletingEventCascadesToInputsAndOutputs()
    {
        var data = await CreateEventDataAsync();
        var input = data.NewInput(data.InputLotId, data.UnitId);
        var output = data.NewOutput(data.OutputLotId, data.UnitId);
        var traceabilityEvent = data.NewEvent([input], [output]);
        await SaveEventAsync(traceabilityEvent);

        await using (var deleteContext = database.CreateTraceabilityDbContext())
        {
            var storedEvent = await deleteContext.TraceabilityEvents.SingleAsync(
                candidate => candidate.Id == traceabilityEvent.Id);
            deleteContext.TraceabilityEvents.Remove(storedEvent);
            Assert.Equal(1, await deleteContext.SaveChangesAsync());
        }

        Assert.Equal(0L, await CountEventSideRowsAsync("event_input", input.Id));
        Assert.Equal(0L, await CountEventSideRowsAsync("event_output", output.Id));
    }

    [Fact]
    public async Task LotReferencedByInputCannotBeDeleted()
    {
        var data = await CreateEventDataAsync();
        var traceabilityEvent = data.NewEvent(
            [data.NewInput(data.InputLotId, data.UnitId)],
            []);
        await SaveEventAsync(traceabilityEvent);

        await using var context = database.CreateTraceabilityDbContext();
        var lot = await context.Lots.SingleAsync(candidate => candidate.Id == data.InputLotId);
        context.Lots.Remove(lot);

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            PostgresErrorCodes.ForeignKeyViolation,
            "fk_event_input_trace_lot");
    }

    [Fact]
    public async Task EventTablesHaveTheExpectedForeignKeys()
    {
        const string sql = """
            SELECT source_table.relname,
                   foreign_key.conname,
                   target_schema.nspname,
                   target_table.relname,
                   CASE foreign_key.confdeltype
                       WHEN 'a' THEN 'NO ACTION'
                       WHEN 'r' THEN 'RESTRICT'
                       WHEN 'c' THEN 'CASCADE'
                       WHEN 'n' THEN 'SET NULL'
                       WHEN 'd' THEN 'SET DEFAULT'
                   END,
                   string_agg(source_column.attname, ',' ORDER BY key_pair.ordinal_position),
                   string_agg(target_column.attname, ',' ORDER BY key_pair.ordinal_position)
            FROM pg_catalog.pg_constraint AS foreign_key
            JOIN pg_catalog.pg_class AS source_table
              ON source_table.oid = foreign_key.conrelid
            JOIN pg_catalog.pg_namespace AS source_schema
              ON source_schema.oid = source_table.relnamespace
            JOIN pg_catalog.pg_class AS target_table
              ON target_table.oid = foreign_key.confrelid
            JOIN pg_catalog.pg_namespace AS target_schema
              ON target_schema.oid = target_table.relnamespace
            CROSS JOIN LATERAL unnest(foreign_key.conkey, foreign_key.confkey)
                WITH ORDINALITY AS key_pair(source_attnum, target_attnum, ordinal_position)
            JOIN pg_catalog.pg_attribute AS source_column
              ON source_column.attrelid = source_table.oid
             AND source_column.attnum = key_pair.source_attnum
            JOIN pg_catalog.pg_attribute AS target_column
              ON target_column.attrelid = target_table.oid
             AND target_column.attnum = key_pair.target_attnum
            WHERE source_schema.nspname = 'trace'
              AND source_table.relname IN
                  ('event_input', 'event_output', 'traceability_event')
              AND foreign_key.contype = 'f'
            GROUP BY source_table.relname,
                     foreign_key.conname,
                     target_schema.nspname,
                     target_table.relname,
                     foreign_key.confdeltype
            ORDER BY source_table.relname, foreign_key.conname;
            """;

        var foreignKeys = await QueryAsync(
            sql,
            static reader => new ForeignKey(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6)));

        Assert.Equal(
            [
                new ForeignKey(
                    "event_input",
                    "fk_event_input_trace_lot",
                    "trace",
                    "lot",
                    "RESTRICT",
                    "lot_id,organization_id,unit_id",
                    "lot_id,organization_id,unit_id"),
                new ForeignKey(
                    "event_input",
                    "fk_event_input_trace_traceability_event",
                    "trace",
                    "traceability_event",
                    "CASCADE",
                    "event_id,organization_id",
                    "event_id,organization_id"),
                new ForeignKey(
                    "event_output",
                    "fk_event_output_trace_lot",
                    "trace",
                    "lot",
                    "RESTRICT",
                    "lot_id,organization_id,unit_id",
                    "lot_id,organization_id,unit_id"),
                new ForeignKey(
                    "event_output",
                    "fk_event_output_trace_traceability_event",
                    "trace",
                    "traceability_event",
                    "CASCADE",
                    "event_id,organization_id",
                    "event_id,organization_id"),
                new ForeignKey(
                    "traceability_event",
                    "fk_traceability_event_identity_user",
                    "identity",
                    "user",
                    "RESTRICT",
                    "created_by",
                    "user_id"),
                new ForeignKey(
                    "traceability_event",
                    "fk_traceability_event_org_location",
                    "org",
                    "location",
                    "RESTRICT",
                    "location_id,organization_id",
                    "location_id,organization_id"),
                new ForeignKey(
                    "traceability_event",
                    "fk_traceability_event_org_organization",
                    "org",
                    "organization",
                    "RESTRICT",
                    "organization_id",
                    "organization_id"),
                new ForeignKey(
                    "traceability_event",
                    "fk_traceability_event_trace_event_type",
                    "trace",
                    "event_type",
                    "RESTRICT",
                    "event_type_id",
                    "event_type_id"),
            ],
            foreignKeys);
    }

    [Fact]
    public async Task CompositeAlternateKeysHaveTheExpectedNamesAndColumnOrder()
    {
        const string sql = """
            SELECT source_schema.nspname,
                   source_table.relname,
                   unique_constraint.conname,
                   string_agg(source_column.attname, ',' ORDER BY key_column.ordinal_position)
            FROM pg_catalog.pg_constraint AS unique_constraint
            JOIN pg_catalog.pg_class AS source_table
              ON source_table.oid = unique_constraint.conrelid
            JOIN pg_catalog.pg_namespace AS source_schema
              ON source_schema.oid = source_table.relnamespace
            CROSS JOIN LATERAL unnest(unique_constraint.conkey)
                WITH ORDINALITY AS key_column(source_attnum, ordinal_position)
            JOIN pg_catalog.pg_attribute AS source_column
              ON source_column.attrelid = source_table.oid
             AND source_column.attnum = key_column.source_attnum
            WHERE unique_constraint.contype = 'u'
              AND unique_constraint.conname IN
                  ('ak_location_location_id_organization_id',
                   'ak_lot_lot_id_organization_id_unit_id',
                   'ak_traceability_event_event_id_organization_id')
            GROUP BY source_schema.nspname,
                     source_table.relname,
                     unique_constraint.conname
            ORDER BY source_schema.nspname, source_table.relname;
            """;

        var alternateKeys = await QueryAsync(
            sql,
            static reader => new AlternateKey(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));

        Assert.Equal(
            [
                new AlternateKey(
                    "org",
                    "location",
                    "ak_location_location_id_organization_id",
                    "location_id,organization_id"),
                new AlternateKey(
                    "trace",
                    "lot",
                    "ak_lot_lot_id_organization_id_unit_id",
                    "lot_id,organization_id,unit_id"),
                new AlternateKey(
                    "trace",
                    "traceability_event",
                    "ak_traceability_event_event_id_organization_id",
                    "event_id,organization_id"),
            ],
            alternateKeys);
    }

    [Fact]
    public async Task EventSideIndexesIncludeLotLookupAndPerEventLotUniqueness()
    {
        const string sql = """
            SELECT tablename,
                   indexname,
                   indexdef LIKE 'CREATE UNIQUE INDEX%'
            FROM pg_catalog.pg_indexes
            WHERE schemaname = 'trace'
              AND indexname IN
                  ('ix_event_input_lot_id',
                   'ux_event_input_event_id_lot_id',
                   'ix_event_output_lot_id',
                   'ux_event_output_event_id_lot_id')
            ORDER BY tablename, indexname;
            """;

        var indexes = await QueryAsync(
            sql,
            static reader => new DatabaseIndex(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetBoolean(2)));

        Assert.Equal(
            [
                new DatabaseIndex("event_input", "ix_event_input_lot_id", false),
                new DatabaseIndex("event_input", "ux_event_input_event_id_lot_id", true),
                new DatabaseIndex("event_output", "ix_event_output_lot_id", false),
                new DatabaseIndex("event_output", "ux_event_output_event_id_lot_id", true),
            ],
            indexes);
    }

    private async Task<EventData> CreateEventDataAsync()
    {
        var organization = Organization.Create(
            Guid.NewGuid(),
            $"Event Test Organization {Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            CreatedAt);
        var foreignOrganization = Organization.Create(
            Guid.NewGuid(),
            $"Foreign Event Test Organization {Guid.NewGuid():N}",
            null,
            null,
            null,
            null,
            CreatedAt);
        var location = Location.Create(
            Guid.NewGuid(),
            organization.Id,
            "Event Test Location",
            null,
            null,
            null,
            null,
            null,
            CreatedAt);
        var foreignLocation = Location.Create(
            Guid.NewGuid(),
            foreignOrganization.Id,
            "Foreign Event Test Location",
            null,
            null,
            null,
            null,
            null,
            CreatedAt);

        await using (var organizations = database.CreateTraceabilityOrganizationsDbContext())
        {
            organizations.Organizations.AddRange(organization, foreignOrganization);
            organizations.Locations.AddRange(location, foreignLocation);
            await organizations.SaveChangesAsync();
        }

        var user = User.Create(
            Guid.NewGuid(),
            EmailAddress.Create($"event-{Guid.NewGuid():N}@example.com"),
            "Traceability",
            "Tester",
            CreatedAt);

        await using (var identity = database.CreateTraceabilityIdentityDbContext())
        {
            identity.Users.Add(user);
            await identity.SaveChangesAsync();
        }

        var product = Product.Create(
            Guid.NewGuid(),
            $"PRODUCT-{Guid.NewGuid():N}",
            "Event Persistence Test Product",
            CreatedAt);
        var article = Article.Create(
            Guid.NewGuid(),
            organization.Id,
            product.Id,
            $"ARTICLE-{Guid.NewGuid():N}",
            null,
            CreatedAt);
        var foreignArticle = Article.Create(
            Guid.NewGuid(),
            foreignOrganization.Id,
            product.Id,
            $"FOREIGN-{Guid.NewGuid():N}",
            null,
            CreatedAt);
        var unit = Unit.Create(
            Guid.NewGuid(),
            UnitCode.Create($"U{Guid.NewGuid():N}"[..8]),
            "u",
            UnitDimension.Mass,
            CreatedAt);
        var otherUnit = Unit.Create(
            Guid.NewGuid(),
            UnitCode.Create($"V{Guid.NewGuid():N}"[..8]),
            "v",
            UnitDimension.Mass,
            CreatedAt);

        await using (var catalog = database.CreateTraceabilityCatalogDbContext())
        {
            catalog.Products.Add(product);
            catalog.Articles.AddRange(article, foreignArticle);
            catalog.Units.AddRange(unit, otherUnit);
            await catalog.SaveChangesAsync();
        }

        var inputLot = Lot.Create(
            Guid.NewGuid(),
            organization.Id,
            article.Id,
            $"INPUT-{Guid.NewGuid():N}",
            100m,
            unit.Id,
            CreatedAt);
        var outputLot = Lot.Create(
            Guid.NewGuid(),
            organization.Id,
            article.Id,
            $"OUTPUT-{Guid.NewGuid():N}",
            80m,
            unit.Id,
            CreatedAt);
        var foreignLot = Lot.Create(
            Guid.NewGuid(),
            foreignOrganization.Id,
            foreignArticle.Id,
            $"FOREIGN-{Guid.NewGuid():N}",
            100m,
            unit.Id,
            CreatedAt);

        await using (var traceability = database.CreateTraceabilityDbContext())
        {
            traceability.Lots.AddRange(inputLot, outputLot, foreignLot);
            await traceability.SaveChangesAsync();
        }

        return new EventData(
            organization.Id,
            location.Id,
            foreignLocation.Id,
            user.Id,
            inputLot.Id,
            outputLot.Id,
            foreignLot.Id,
            unit.Id,
            otherUnit.Id);
    }

    private async Task SaveEventAsync(TraceabilityEvent traceabilityEvent)
    {
        await using var context = database.CreateTraceabilityDbContext();
        context.TraceabilityEvents.Add(traceabilityEvent);
        await context.SaveChangesAsync();
    }

    private async Task AssertEventRejectedAsync(
        TraceabilityEvent traceabilityEvent,
        string expectedSqlState,
        string expectedConstraintName)
    {
        await using var context = database.CreateTraceabilityDbContext();
        context.TraceabilityEvents.Add(traceabilityEvent);

        await AssertDatabaseErrorAsync(
            () => context.SaveChangesAsync(),
            expectedSqlState,
            expectedConstraintName);
    }

    private async Task InsertEventSideDirectlyAsync(
        string tableName,
        string idColumnName,
        Guid eventId,
        Guid organizationId,
        Guid lotId,
        Guid unitId,
        string quantity)
    {
        var sql = tableName switch
        {
            "event_input" when idColumnName == "event_input_id" => """
                INSERT INTO trace.event_input
                    (event_input_id, event_id, organization_id, lot_id, quantity, unit_id)
                VALUES
                    (@side_id, @event_id, @organization_id, @lot_id, @quantity, @unit_id);
                """,
            "event_output" when idColumnName == "event_output_id" => """
                INSERT INTO trace.event_output
                    (event_output_id, event_id, organization_id, lot_id, quantity, unit_id)
                VALUES
                    (@side_id, @event_id, @organization_id, @lot_id, @quantity, @unit_id);
                """,
            _ => throw new ArgumentOutOfRangeException(
                nameof(tableName),
                tableName,
                "Only traceability event side tables are supported."),
        };

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("side_id", Guid.NewGuid());
        command.Parameters.AddWithValue("event_id", eventId);
        command.Parameters.AddWithValue("organization_id", organizationId);
        command.Parameters.AddWithValue("lot_id", lotId);
        command.Parameters.AddWithValue(
            "quantity",
            decimal.Parse(quantity, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("unit_id", unitId);

        await command.ExecuteNonQueryAsync(timeout.Token);
    }

    private async Task<long> CountEventSideRowsAsync(string tableName, Guid sideId)
    {
        var sql = tableName switch
        {
            "event_input" =>
                "SELECT COUNT(*) FROM trace.event_input WHERE event_input_id = @side_id;",
            "event_output" =>
                "SELECT COUNT(*) FROM trace.event_output WHERE event_output_id = @side_id;",
            _ => throw new ArgumentOutOfRangeException(
                nameof(tableName),
                tableName,
                "Only traceability event side tables are supported."),
        };

        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("side_id", sideId);

        return (long)(await command.ExecuteScalarAsync(timeout.Token))!;
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> map)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.TraceabilityConnectionString);
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

    private static async Task AssertDatabaseErrorAsync(
        Func<Task> action,
        string expectedSqlState,
        string expectedConstraintName)
    {
        var exception = await Assert.ThrowsAsync<DbUpdateException>(action);
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(expectedSqlState, postgresException.SqlState);
        Assert.Equal(expectedConstraintName, postgresException.ConstraintName);
    }

    private sealed record EventData(
        Guid OrganizationId,
        Guid LocationId,
        Guid ForeignLocationId,
        Guid UserId,
        Guid InputLotId,
        Guid OutputLotId,
        Guid ForeignLotId,
        Guid UnitId,
        Guid OtherUnitId)
    {
        public EventInput NewInput(Guid lotId, Guid unitId, decimal quantity = 100m) =>
            EventInput.Create(Guid.NewGuid(), lotId, quantity, unitId);

        public EventOutput NewOutput(Guid lotId, Guid unitId, decimal quantity = 80m) =>
            EventOutput.Create(Guid.NewGuid(), lotId, quantity, unitId);

        public TraceabilityEvent NewEvent(
            IReadOnlyList<EventInput> inputs,
            IReadOnlyList<EventOutput> outputs,
            Guid? organizationId = null,
            Guid? locationId = null,
            Guid? createdBy = null,
            Guid? eventTypeId = null) =>
            TraceabilityEvent.Create(
                Guid.NewGuid(),
                eventTypeId ?? EventTypeId,
                organizationId ?? OrganizationId,
                locationId ?? LocationId,
                OccurredAt,
                "EXT-TRC-007",
                "Traceability persistence test",
                createdBy ?? UserId,
                CreatedAt,
                inputs,
                outputs);
    }

    private sealed record DatabaseColumn(
        string TableName,
        string Name,
        string IsNullable,
        string DataType,
        int? MaximumLength);

    private sealed record QuantityPrecision(string TableName, int Precision, int Scale);

    private sealed record ForeignKey(
        string SourceTable,
        string Name,
        string TargetSchema,
        string TargetTable,
        string DeleteRule,
        string SourceColumns,
        string TargetColumns);

    private sealed record AlternateKey(
        string Schema,
        string Table,
        string Name,
        string Columns);

    private sealed record DatabaseIndex(string Table, string Name, bool IsUnique);
}
