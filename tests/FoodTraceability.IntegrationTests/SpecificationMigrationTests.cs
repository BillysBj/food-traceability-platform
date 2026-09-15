using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Quality.Domain;
using FoodTraceability.Modules.Quality.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class SpecificationMigrationTests(PostgreSqlContainerFixture database)
{
    private const string VersionIndex = "ux_specification_article_id_version";
    private const string ParameterIndex = "ux_specification_parameter_specification_id_parameter_id";
    private const string ArticleForeignKey = "fk_specification_catalog_article";
    private const string SpecificationForeignKey = "fk_specification_parameter_specification";
    private const string ParameterForeignKey = "fk_specification_parameter_parameter";
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateTimeOffset ValidFrom = new(2026, 9, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 14, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TablesHaveExactlyTheDecidedColumnsAndNoDefaults()
    {
        var columns = await QueryAsync(
            """
            SELECT table_name, column_name, is_nullable, data_type, numeric_precision, numeric_scale, column_default
            FROM information_schema.columns
            WHERE table_schema = 'quality' AND table_name IN ('specification', 'specification_parameter')
            ORDER BY table_name, column_name;
            """,
            static reader => new DatabaseColumn(
                reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetInt32(4),
                reader.IsDBNull(5) ? null : reader.GetInt32(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));

        Assert.Equal(
            [
                new DatabaseColumn("specification", "article_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("specification", "created_at", "NO", "timestamp with time zone", null, null, null),
                new DatabaseColumn("specification", "specification_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("specification", "valid_from", "NO", "timestamp with time zone", null, null, null),
                new DatabaseColumn("specification", "valid_to", "YES", "timestamp with time zone", null, null, null),
                new DatabaseColumn("specification", "version", "NO", "integer", 32, 0, null),
                new DatabaseColumn("specification_parameter", "created_at", "NO", "timestamp with time zone", null, null, null),
                new DatabaseColumn("specification_parameter", "max", "YES", "numeric", 18, 6, null),
                new DatabaseColumn("specification_parameter", "min", "YES", "numeric", 18, 6, null),
                new DatabaseColumn("specification_parameter", "parameter_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("specification_parameter", "required", "NO", "boolean", null, null, null),
                new DatabaseColumn("specification_parameter", "spec_parameter_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("specification_parameter", "specification_id", "NO", "uuid", null, null, null),
                new DatabaseColumn("specification_parameter", "target", "YES", "numeric", 18, 6, null),
            ], columns);
    }

    [Fact]
    public async Task TablesHaveExactlyTheThreeExpectedRestrictForeignKeys()
    {
        var constraints = await QueryAsync(
            """
            SELECT conname, pg_get_constraintdef(oid)
            FROM pg_catalog.pg_constraint
            WHERE conrelid IN ('quality.specification'::regclass, 'quality.specification_parameter'::regclass)
              AND contype = 'f'
            ORDER BY conname;
            """,
            static reader => (Name: reader.GetString(0), Definition: reader.GetString(1)));

        Assert.Equal(
            [
                (ArticleForeignKey, "FOREIGN KEY (article_id) REFERENCES catalog.article(article_id) ON DELETE RESTRICT"),
                (ParameterForeignKey, "FOREIGN KEY (parameter_id) REFERENCES quality.parameter(parameter_id) ON DELETE RESTRICT"),
                (SpecificationForeignKey, "FOREIGN KEY (specification_id) REFERENCES quality.specification(specification_id) ON DELETE RESTRICT"),
            ], constraints);
    }

    [Fact]
    public async Task DuplicateVersionForSameArticleIsRejectedByExpectedIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.Specifications.Add(CreateSpecification(article.Id));
        await context.SaveChangesAsync(timeout.Token);
        context.Specifications.Add(CreateSpecification(article.Id));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, VersionIndex);
    }

    [Fact]
    public async Task SameVersionForDifferentArticlesIsAllowed()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var firstArticle = await CreateOwnArticleAsync(timeout.Token);
        var secondArticle = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var first = CreateSpecification(firstArticle.Id);
        var second = CreateSpecification(secondArticle.Id);
        context.Specifications.AddRange(first, second);

        Assert.Equal(2, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();
        Assert.Equal(2, await context.Specifications.CountAsync(
            row => row.Id == first.Id || row.Id == second.Id, timeout.Token));
    }

    [Fact]
    public async Task DuplicateParameterForSameSpecificationIsRejectedByExpectedIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var (specification, parameter) = await AddOwnPrincipalsAsync(context, article.Id, timeout.Token);
        context.SpecificationParameters.Add(CreateSpecificationParameter(specification.Id, parameter.Id));
        await context.SaveChangesAsync(timeout.Token);
        context.SpecificationParameters.Add(CreateSpecificationParameter(specification.Id, parameter.Id));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, ParameterIndex);
    }

    [Fact]
    public async Task UnknownArticleIsRejectedByExpectedForeignKey()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.Specifications.Add(CreateSpecification(Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, ArticleForeignKey);
    }

    [Theory]
    [InlineData(true, SpecificationForeignKey)]
    [InlineData(false, ParameterForeignKey)]
    public async Task UnknownQualityPrincipalIsRejectedByItsSpecificForeignKey(bool unknownSpecification, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var (specification, parameter) = await AddOwnPrincipalsAsync(context, article.Id, timeout.Token);
        context.SpecificationParameters.Add(CreateSpecificationParameter(
            unknownSpecification ? Guid.NewGuid() : specification.Id,
            unknownSpecification ? parameter.Id : Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));

        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, constraint);
    }

    [Fact]
    public async Task OverlappingValidityPeriodsForSameArticleAreDeliberatelyAllowedInDatabase()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var first = CreateSpecification(article.Id, version: 1, validTo: ValidFrom.AddDays(30));
        var second = Specification.Create(Guid.NewGuid(), article.Id, 2, ValidFrom.AddDays(10), null, CreatedAt);

        // T-12/D-52: this intentionally leaves ambiguity in storage. QLT-004 must
        // report multiple applicable specifications; an exclusion constraint must not decide it here.
        context.Specifications.AddRange(first, second);
        Assert.Equal(2, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();
        var takenAt = ValidFrom.AddDays(15);
        Assert.Equal(2, await context.Specifications.CountAsync(
            row => row.ArticleId == article.Id && row.ValidFrom <= takenAt
                && (row.ValidTo == null || row.ValidTo >= takenAt), timeout.Token));
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public async Task SpecificationsAndParametersRoundTripWithOptionalValues(bool hasMinimum, bool hasMaximum, bool required)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var specification = CreateSpecification(article.Id, validTo: required ? ValidFrom.AddYears(1) : null);
        var parameter = CreateParameter();
        context.Specifications.Add(specification);
        context.Parameters.Add(parameter);
        decimal? minimum = hasMinimum ? -1.123456m : null;
        decimal? maximum = hasMaximum ? 2.123456m : null;
        // Target above Maximum remains valid in the database as well as in the domain.
        decimal? target = hasMinimum || hasMaximum ? 3.123456m : null;
        var specificationParameter = SpecificationParameter.Create(
            Guid.NewGuid(), specification.Id, parameter.Id, minimum, maximum, target, required, CreatedAt);
        context.SpecificationParameters.Add(specificationParameter);

        Assert.Equal(3, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();
        var stored = await context.Specifications.SingleAsync(row => row.Id == specification.Id, timeout.Token);
        Assert.Equal(article.Id, stored.ArticleId);
        Assert.Equal(specification.Version, stored.Version);
        Assert.Equal(ValidFrom, stored.ValidFrom);
        Assert.Equal(specification.ValidTo, stored.ValidTo);
        Assert.Equal(CreatedAt, stored.CreatedAt);
        var storedParameter = await context.SpecificationParameters.SingleAsync(
            row => row.Id == specificationParameter.Id, timeout.Token);
        Assert.Equal(specification.Id, storedParameter.SpecificationId);
        Assert.Equal(parameter.Id, storedParameter.ParameterId);
        Assert.Equal(minimum, storedParameter.Minimum);
        Assert.Equal(maximum, storedParameter.Maximum);
        Assert.Equal(target, storedParameter.Target);
        Assert.Equal(required, storedParameter.Required);
        Assert.Equal(CreatedAt, storedParameter.CreatedAt);
    }

    [Theory]
    [InlineData(0, false, "ck_specification_version")]
    [InlineData(-1, false, "ck_specification_version")]
    [InlineData(1, true, "ck_specification_validity")]
    public async Task InvalidSpecificationIsRejectedByItsSpecificCheck(int version, bool invalidPeriod, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var validTo = invalidPeriod ? ValidFrom.AddSeconds(-1) : ValidFrom;

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO quality.specification (specification_id, article_id, version, valid_from, valid_to, created_at)
            VALUES ({Guid.NewGuid()}, {article.Id}, {version}, {ValidFrom}, {validTo}, {CreatedAt});
            """, timeout.Token));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal(constraint, exception.ConstraintName);
    }

    [Fact]
    public async Task MinimumAboveMaximumIsRejectedByExpectedCheck()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var article = await CreateOwnArticleAsync(timeout.Token);
        await using var context = database.CreateQualityDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var (specification, parameter) = await AddOwnPrincipalsAsync(context, article.Id, timeout.Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO quality.specification_parameter
                (spec_parameter_id, specification_id, parameter_id, min, max, target, required, created_at)
            VALUES ({Guid.NewGuid()}, {specification.Id}, {parameter.Id}, 2, 1, NULL, TRUE, {CreatedAt});
            """, timeout.Token));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_specification_parameter_bounds", exception.ConstraintName);
    }

    private static Specification CreateSpecification(Guid articleId, int version = 1, DateTimeOffset? validTo = null) =>
        Specification.Create(Guid.NewGuid(), articleId, version, ValidFrom, validTo, CreatedAt);

    private static SpecificationParameter CreateSpecificationParameter(Guid specificationId, Guid parameterId) =>
        SpecificationParameter.Create(Guid.NewGuid(), specificationId, parameterId, null, null, null, true, CreatedAt);

    private static Parameter CreateParameter() =>
        Parameter.Create(Guid.NewGuid(), ParameterCode.Create($"SPEC_{Guid.NewGuid():N}"), null, "ISO 660");

    private static async Task<(Specification Specification, Parameter Parameter)> AddOwnPrincipalsAsync(
        QualityDbContext context, Guid articleId, CancellationToken cancellationToken)
    {
        // All Quality rows belong to the caller's rollback transaction, keeping the parameter catalog empty.
        var specification = CreateSpecification(articleId);
        var parameter = CreateParameter();
        context.Specifications.Add(specification);
        context.Parameters.Add(parameter);
        await context.SaveChangesAsync(cancellationToken);
        return (specification, parameter);
    }

    private async Task<Article> CreateOwnArticleAsync(CancellationToken cancellationToken)
    {
        var organizationId = Guid.NewGuid();
        await using (var organizations = database.CreateQualityOrganizationsDbContext())
        {
            organizations.Organizations.Add(Organization.Create(
                organizationId, $"Specification Test Organization {organizationId:N}", null, null, null, null, CreatedAt));
            await organizations.SaveChangesAsync(cancellationToken);
        }

        var product = Product.Create(Guid.NewGuid(), $"PRODUCT-{Guid.NewGuid():N}", "Specification Test Product", CreatedAt);
        var article = Article.Create(Guid.NewGuid(), organizationId, product.Id, $"ARTICLE-{Guid.NewGuid():N}", null, CreatedAt);
        await using var catalog = database.CreateQualityCatalogDbContext();
        catalog.Products.Add(product);
        catalog.Articles.Add(article);
        await catalog.SaveChangesAsync(cancellationToken);
        return article;
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
        string Table, string Name, string IsNullable, string DataType, int? Precision, int? Scale, string? Default);
}
