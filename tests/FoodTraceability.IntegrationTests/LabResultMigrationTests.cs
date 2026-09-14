using System.Globalization;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Identity.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Quality.Infrastructure;
using FoodTraceability.Modules.Traceability.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class LabResultMigrationTests(PostgreSqlContainerFixture database)
{
    private const string UniqueIndex = "ux_lab_result_sample_id_parameter_id";
    private const string SampleForeignKey = "fk_lab_result_sample";
    private const string ParameterForeignKey = "fk_lab_result_parameter";
    private const string AssessmentCheck = "ck_lab_result_assessment";
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset MeasuredAt = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 14, 10, 0, 0, TimeSpan.Zero);
    private static readonly Guid SampleEventTypeId = Guid.Parse("49163869-4ef0-501f-984d-ab3adb5e1996");

    [Fact]
    public async Task LabResultHasExactlyTheEightExpectedRequiredColumns()
    {
        var columns = await QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length,
                   numeric_precision, numeric_scale
            FROM information_schema.columns
            WHERE table_schema = 'quality' AND table_name = 'lab_result'
            ORDER BY column_name;
            """,
            static reader => new DatabaseColumn(
                reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5)));

        Assert.Equal(
            [
                new DatabaseColumn("assessment", "NO", "character varying", 4, null, null),
                new DatabaseColumn("created_at", "NO", "timestamp with time zone", null, null, null),
                new DatabaseColumn("lab_result_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("measured_at", "NO", "timestamp with time zone", null, null, null),
                new DatabaseColumn("method", "NO", "character varying", 128, null, null),
                new DatabaseColumn("parameter_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("sample_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("value", "NO", "numeric", null, 18, 6),
            ], columns);
    }

    [Fact]
    public async Task LabResultHasExactlyTheTwoQualityRestrictForeignKeys()
    {
        var constraints = await QueryAsync(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_catalog.pg_constraint
            WHERE conrelid = 'quality.lab_result'::regclass AND contype = 'f'
            ORDER BY conname;
            """,
            static reader => (Name: reader.GetString(0), Definition: reader.GetString(1)));

        Assert.Equal(
            [
                (ParameterForeignKey, "FOREIGN KEY (parameter_id) REFERENCES quality.parameter(parameter_id) ON DELETE RESTRICT"),
                (SampleForeignKey, "FOREIGN KEY (sample_id) REFERENCES quality.sample(sample_id) ON DELETE RESTRICT"),
            ], constraints);
    }

    [Fact]
    public async Task DuplicateSampleAndParameterIsRejectedByExpectedIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var sample = await CreateSampleWithOwnReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = await AddSampleAndParameterAsync(context, sample, timeout.Token);
        context.LabResults.Add(CreateResult(sample.Id, parameter.Id));
        Assert.Equal(1, await context.SaveChangesAsync(timeout.Token));
        context.LabResults.Add(CreateResult(sample.Id, parameter.Id, LabResultAssessment.Fail));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, UniqueIndex);
    }

    [Fact]
    public async Task DifferentParametersForSameSampleAreAllowedAndBothAssessmentsRoundTrip()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var sample = await CreateSampleWithOwnReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var firstParameter = await AddSampleAndParameterAsync(context, sample, timeout.Token);
        var secondParameter = CreateParameter();
        context.Parameters.Add(secondParameter);
        await context.SaveChangesAsync(timeout.Token);
        var first = CreateResult(sample.Id, firstParameter.Id);
        var second = CreateResult(sample.Id, secondParameter.Id, LabResultAssessment.Fail);
        context.LabResults.AddRange(first, second);

        Assert.Equal(2, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();
        var stored = await context.LabResults.AsNoTracking()
            .Where(result => result.SampleId == sample.Id).ToListAsync(timeout.Token);
        Assert.Equal(2, stored.Count);
        foreach (var expected in new[] { first, second })
        {
            var actual = Assert.Single(stored, result => result.Id == expected.Id);
            Assert.Equal(expected.SampleId, actual.SampleId);
            Assert.Equal(expected.ParameterId, actual.ParameterId);
            Assert.Equal(expected.Value, actual.Value);
            Assert.Equal(expected.Assessment, actual.Assessment);
            Assert.Equal(expected.Method, actual.Method);
            Assert.Equal(expected.MeasuredAt, actual.MeasuredAt);
            Assert.Equal(expected.CreatedAt, actual.CreatedAt);
        }

        // Even a FAIL result does not change the sample in this persistence-only task.
        Assert.Equal(SampleStatus.Pending,
            (await context.Samples.SingleAsync(row => row.Id == sample.Id, timeout.Token)).Status);
    }

    [Theory]
    [InlineData(true, SampleForeignKey)]
    [InlineData(false, ParameterForeignKey)]
    public async Task UnknownReferenceIsRejectedByItsSpecificForeignKey(bool unknownSample, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var sample = await CreateSampleWithOwnReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = await AddSampleAndParameterAsync(context, sample, timeout.Token);
        context.LabResults.Add(CreateResult(
            unknownSample ? Guid.NewGuid() : sample.Id,
            unknownSample ? parameter.Id : Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, constraint);
    }

    [Theory]
    [InlineData(true, SampleForeignKey)]
    [InlineData(false, ParameterForeignKey)]
    public async Task ReferencedPrincipalCannotBeDeleted(bool deleteSample, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var sample = await CreateSampleWithOwnReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = await AddSampleAndParameterAsync(context, sample, timeout.Token);
        context.LabResults.Add(CreateResult(sample.Id, parameter.Id));
        await context.SaveChangesAsync(timeout.Token);

        // Both possible targets were created for this test alone.
        var deleteSql = deleteSample
            ? (FormattableString)$"DELETE FROM quality.sample WHERE sample_id = {sample.Id}"
            : (FormattableString)$"DELETE FROM quality.parameter WHERE parameter_id = {parameter.Id}";
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => context.Database.ExecuteSqlInterpolatedAsync(deleteSql, timeout.Token));

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Equal(constraint, exception.ConstraintName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NONE")]
    [InlineData("pass")]
    [InlineData("fail")]
    public async Task AssessmentOutsideTheTwoCodesIsRejectedByCheckConstraint(string assessment)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var sample = await CreateSampleWithOwnReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = await AddSampleAndParameterAsync(context, sample, timeout.Token);

        // Bypass the domain/converter and use <= 4 characters to reach the CHECK itself.
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => InsertRawResultAsync(context, Guid.NewGuid(), sample.Id, parameter.Id, 1m, assessment, timeout.Token));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(AssessmentCheck, exception.ConstraintName);
    }

    [Theory]
    [InlineData("1.1234564", "1.123456")]
    [InlineData("1.1234565", "1.123457")]
    [InlineData("-1.1234564", "-1.123456")]
    [InlineData("-1.1234565", "-1.123457")]
    public async Task PostgreSqlRoundsExcessFractionalDigitsWithMidpointsAwayFromZero(string input, string expected)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var sample = await CreateSampleWithOwnReferencesAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var parameter = await AddSampleAndParameterAsync(context, sample, timeout.Token);
        var resultId = Guid.NewGuid();
        var value = decimal.Parse(input, CultureInfo.InvariantCulture);

        // T-10: numeric(18,6) coerces scale by rounding, not by a CHECK violation.
        // Direct SQL deliberately bypasses LabResult.Create's lossless-value guard.
        Assert.Equal(1, await InsertRawResultAsync(
            context, resultId, sample.Id, parameter.Id, value, "PASS", timeout.Token));

        var stored = await context.LabResults.AsNoTracking()
            .SingleAsync(result => result.Id == resultId, timeout.Token);
        Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), stored.Value);
        Assert.NotEqual(value, stored.Value);
    }

    private static LabResult CreateResult(
        Guid sampleId, Guid parameterId, LabResultAssessment assessment = LabResultAssessment.Pass) =>
        LabResult.Create(Guid.NewGuid(), sampleId, parameterId, -26.123456m, assessment, "ISO 660", MeasuredAt, CreatedAt);

    private static Parameter CreateParameter() => Parameter.Create(
        Guid.NewGuid(), ParameterCode.Create($"RESULT_{Guid.NewGuid():N}"), null, "ISO 660");

    private static async Task<Parameter> AddSampleAndParameterAsync(
        QualityDbContext context, Sample sample, CancellationToken cancellationToken)
    {
        // The caller's rollback transaction keeps quality.parameter empty for other tests.
        var parameter = CreateParameter();
        context.Samples.Add(sample);
        context.Parameters.Add(parameter);
        await context.SaveChangesAsync(cancellationToken);
        return parameter;
    }

    private static Task<int> InsertRawResultAsync(
        QualityDbContext context, Guid id, Guid sampleId, Guid parameterId,
        decimal value, string assessment, CancellationToken cancellationToken) =>
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO quality.lab_result
                (lab_result_id, sample_id, parameter_id, value, assessment, method, measured_at, created_at)
            VALUES ({id}, {sampleId}, {parameterId}, {value}, {assessment}, 'ISO 660', {MeasuredAt}, {CreatedAt});
            """, cancellationToken);

    private async Task<Sample> CreateSampleWithOwnReferencesAsync(CancellationToken cancellationToken)
    {
        var organizationId = Guid.NewGuid();
        var location = Location.Create(
            Guid.NewGuid(), organizationId, "Lab Result Test Location", null, null, null, null, null, CreatedAt);
        await using (var organizations = database.CreateQualityOrganizationsDbContext())
        {
            organizations.Organizations.Add(Organization.Create(
                organizationId, $"Lab Result Test Organization {organizationId:N}",
                null, null, null, null, CreatedAt));
            organizations.Locations.Add(location);
            await organizations.SaveChangesAsync(cancellationToken);
        }

        var product = Product.Create(
            Guid.NewGuid(), $"PRODUCT-{Guid.NewGuid():N}", "Lab Result Test Product", CreatedAt);
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
            Guid.NewGuid(), EmailAddress.Create($"lab-result-{Guid.NewGuid():N}@example.com"),
            "Lab Result", "Tester", CreatedAt);
        await using (var identity = database.CreateQualityIdentityDbContext())
        {
            identity.Users.Add(user);
            await identity.SaveChangesAsync(cancellationToken);
        }

        var lot = Lot.Create(
            Guid.NewGuid(), organizationId, article.Id, $"LOT-{Guid.NewGuid():N}", 100m, unit.Id, CreatedAt);
        var traceabilityEvent = TraceabilityEvent.Create(
            Guid.NewGuid(), SampleEventTypeId, organizationId, location.Id, MeasuredAt,
            null, null, user.Id, CreatedAt,
            [EventInput.Create(Guid.NewGuid(), lot.Id, 1m, unit.Id)], []);
        await using (var traceability = database.CreateQualityTraceabilityDbContext())
        {
            traceability.Lots.Add(lot);
            await traceability.SaveChangesAsync(cancellationToken);
            traceability.TraceabilityEvents.Add(traceabilityEvent);
            await traceability.SaveChangesAsync(cancellationToken);
        }

        return Sample.Create(
            Guid.NewGuid(), organizationId, lot.Id, location.Id, traceabilityEvent.Id,
            $"RESULT-SAMPLE-{Guid.NewGuid():N}", MeasuredAt, CreatedAt);
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

    private sealed record DatabaseColumn(
        string Name, string IsNullable, string DataType, int? MaximumLength, int? Precision, int? Scale);
}
