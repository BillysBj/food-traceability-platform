using FoodTraceability.Modules.Documents.Domain;
using FoodTraceability.Modules.Documents.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class DocumentsMigrationTests(PostgreSqlContainerFixture database)
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(30);
    private static readonly DateOnly DocumentDate = new(2026, 9, 17);
    private static readonly DateTimeOffset CreatedAt = new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero)
        .AddTicks(1234560);

    [Fact]
    public async Task DocumentsMigrationAppliesToEmptyDatabase()
    {
        // The fixture migrates Organizations, then Documents into a fresh dedicated database.
        await using var context = database.CreateDocumentsDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await context.Database.MigrateAsync(timeout.Token);

        var migrations = await context.Database.GetAppliedMigrationsAsync(timeout.Token);
        Assert.Equal("20260917120000_InitialDocuments", Assert.Single(migrations));
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public async Task DocumentsSchemaContainsOnlyDocumentDocumentTypeAndMigrationHistory()
    {
        var tables = await QueryAsync(
            """
            SELECT table_name FROM information_schema.tables
            WHERE table_schema = 'docs' ORDER BY table_name;
            """, static reader => reader.GetString(0));

        Assert.Equal([PersistenceConventions.MigrationsHistoryTableName, "document", "document_type"], tables);
    }

    [Fact]
    public async Task DocumentsMigrationHistoryIsStoredOnlyInDocumentsSchema()
    {
        var histories = await QueryAsync(
            """
            SELECT table_schema FROM information_schema.tables
            WHERE table_name = '__ef_migrations_history' ORDER BY table_schema;
            """, static reader => reader.GetString(0));
        Assert.Equal(["docs", "org"], histories);

        var migrations = await QueryAsync(
            "SELECT migration_id FROM docs.__ef_migrations_history;",
            static reader => reader.GetString(0));
        Assert.Equal("20260917120000_InitialDocuments", Assert.Single(migrations));

        var foreignHistory = await QueryAsync(
            """
            SELECT COUNT(*) FROM org.__ef_migrations_history
            WHERE migration_id = '20260917120000_InitialDocuments';
            """, static reader => reader.GetInt64(0));
        Assert.Equal(0L, Assert.Single(foreignHistory));
    }

    [Theory]
    [InlineData("document")]
    [InlineData("document_type")]
    public async Task TablesHaveExactlyTheExpectedColumns(string table)
    {
        var columns = await QueryAsync(
            """
            SELECT column_name, is_nullable, data_type, character_maximum_length
            FROM information_schema.columns
            WHERE table_schema = 'docs' AND table_name = @table
            ORDER BY column_name;
            """,
            static reader => new DatabaseColumn(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3)),
            new NpgsqlParameter("table", table));

        DatabaseColumn[] expected = table == "document_type"
            ?
            [
                new("code", "NO", "character varying", 64),
                new("document_type_id", "NO", "uuid", null),
            ]
            :
            [
                new("created_at", "NO", "timestamp with time zone", null),
                new("document_date", "NO", "date", null),
                new("document_id", "NO", "uuid", null),
                new("document_type_id", "NO", "uuid", null),
                new("file_name", "NO", "character varying", 255),
                new("mime_type", "NO", "character varying", 255),
                new("name", "NO", "character varying", 200),
                new("organization_id", "NO", "uuid", null),
                new("sha256", "NO", "character", 64),
                new("storage_key", "NO", "character varying", 1024),
            ];
        Assert.Equal(expected, columns);
    }

    [Fact]
    public async Task ExactlyThreeDocumentTypesAreSeededWithDeterministicIds()
    {
        await using var context = database.CreateDocumentsDbContext();
        using var timeout = new CancellationTokenSource(QueryTimeout);
        var types = await context.DocumentTypes.AsNoTracking().OrderBy(type => type.Code).ToListAsync(timeout.Token);

        Assert.Equal(
            [
                (new Guid("b4f4b593-4b19-5f13-8d15-b1d51e126862"), "CERTIFICATE"),
                (new Guid("ae9fd5c5-e18b-5bf4-ac1a-b7ae088e14aa"), "DELIVERY_NOTE"),
                (new Guid("5b9827b8-61e4-5046-8787-ff0d1418b4a7"), "LAB_REPORT"),
            ],
            types.Select(type => (type.Id, type.Code.Value)));
    }

    [Fact]
    public async Task DuplicateTypeCodeIsRejectedByExpectedIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateDocumentsDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        context.DocumentTypes.Add(DocumentType.Create(Guid.NewGuid(), DocumentTypeCode.Create(" lab_report ")));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));
        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, "ix_document_type_code");
    }

    [Fact]
    public async Task DuplicateStorageKeyIsRejectedByExpectedIndex()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateDocumentsDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var organizationId = await InsertOrganizationAsync(context, timeout.Token);
        var document = CreateDocument(organizationId);
        context.Documents.Add(document);
        await context.SaveChangesAsync(timeout.Token);
        context.Documents.Add(CreateDocument(organizationId, storageKey: document.StorageKey));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));
        AssertConstraint(exception, PostgresErrorCodes.UniqueViolation, "ix_document_storage_key");
    }

    [Theory]
    [InlineData(true, "fk_document_org_organization")]
    [InlineData(false, "fk_document_document_type_document_type_id")]
    public async Task UnknownOrganizationOrDocumentTypeIsRejected(bool unknownOrganization, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateDocumentsDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var organizationId = await InsertOrganizationAsync(context, timeout.Token);
        context.Documents.Add(CreateDocument(
            unknownOrganization ? Guid.NewGuid() : organizationId,
            documentTypeId: unknownOrganization ? StandardDocumentTypeIds.LabReport : Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(timeout.Token));
        AssertConstraint(exception, PostgresErrorCodes.ForeignKeyViolation, constraint);
    }

    [Theory]
    [InlineData(true, "fk_document_org_organization")]
    [InlineData(false, "fk_document_document_type_document_type_id")]
    public async Task ReferencedOrganizationOrDocumentTypeCannotBeDeleted(bool deleteOrganization, string constraint)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateDocumentsDbContext();
        // Disposal rolls back even if the DELETE unexpectedly succeeds or an assertion fails.
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var organizationId = await InsertOrganizationAsync(context, timeout.Token);
        var document = CreateDocument(organizationId);
        context.Documents.Add(document);
        await context.SaveChangesAsync(timeout.Token);

        var exception = await Assert.ThrowsAsync<PostgresException>(() => deleteOrganization
            ? context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM org.organization WHERE organization_id = {organizationId}", timeout.Token)
            : context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM docs.document_type WHERE document_type_id = {document.DocumentTypeId}", timeout.Token));
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, exception.SqlState);
        Assert.Equal(constraint, exception.ConstraintName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task InvalidSha256IsRejectedByCheckConstraintViaSql(bool uppercase)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateDocumentsDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var organizationId = await InsertOrganizationAsync(context, timeout.Token);
        var document = CreateDocument(organizationId);
        var invalidHash = uppercase ? new string('A', 64) : new string('a', 63);

        // Bypass the domain; character(64) pads the short value, which must still fail.
        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO docs.document
                    (document_id, document_type_id, organization_id, name, file_name,
                     storage_key, mime_type, document_date, sha256, created_at)
                VALUES ({document.Id}, {document.DocumentTypeId}, {organizationId},
                    {document.Name}, {document.FileName}, {document.StorageKey},
                    {document.MimeType}, {document.DocumentDate}, {invalidHash}, {document.CreatedAt})
                """, timeout.Token));
        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_document_sha256", exception.ConstraintName);
    }

    [Fact]
    public async Task DocumentRoundTripsAllFieldsIncludingDateAndMicrosecondTimestamp()
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var context = database.CreateDocumentsDbContext();
        await using var transaction = await context.Database.BeginTransactionAsync(timeout.Token);
        var organizationId = await InsertOrganizationAsync(context, timeout.Token);
        var document = CreateDocument(organizationId);
        context.Documents.Add(document);
        Assert.Equal(1, await context.SaveChangesAsync(timeout.Token));
        context.ChangeTracker.Clear();

        var stored = await context.Documents.AsNoTracking().SingleAsync(row => row.Id == document.Id, timeout.Token);
        Assert.Equal(document.Id, stored.Id);
        Assert.Equal(document.DocumentTypeId, stored.DocumentTypeId);
        Assert.Equal(document.OrganizationId, stored.OrganizationId);
        Assert.Equal(document.Name, stored.Name);
        Assert.Equal(document.FileName, stored.FileName);
        Assert.Equal(document.StorageKey, stored.StorageKey);
        Assert.Equal(document.MimeType, stored.MimeType);
        Assert.Equal(document.DocumentDate, stored.DocumentDate);
        Assert.Equal(document.Sha256, stored.Sha256);
        Assert.Equal(document.CreatedAt, stored.CreatedAt);
    }

    [Fact]
    public void DocumentsModelContainsOnlyDocumentAndDocumentTypeAndNoCrossModuleForeignKey()
    {
        using var context = database.CreateDocumentsDbContext();
        var entityTypes = context.Model.GetEntityTypes().OrderBy(entity => entity.ClrType.Name).ToArray();
        Assert.Equal([typeof(Document), typeof(DocumentType)], entityTypes.Select(entity => entity.ClrType));
        Assert.All(entityTypes, entityType =>
        {
            Assert.Equal("docs", entityType.GetSchema());
            Assert.All(entityType.GetForeignKeys(), foreignKey =>
                Assert.Contains(foreignKey.PrincipalEntityType, entityTypes));
            Assert.Empty(entityType.GetNavigations());
        });

        var typeForeignKey = Assert.Single(context.Model.FindEntityType(typeof(Document))!.GetForeignKeys());
        Assert.Equal(typeof(DocumentType), typeForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, typeForeignKey.DeleteBehavior);
        Assert.Equal(nameof(Document.DocumentTypeId), Assert.Single(typeForeignKey.Properties).Name);
    }

    private static Document CreateDocument(Guid organizationId, Guid? documentTypeId = null, string? storageKey = null) =>
        Document.Create(Guid.NewGuid(), documentTypeId ?? StandardDocumentTypeIds.LabReport, organizationId,
            "Laboratory report", "report.pdf", storageKey ?? $"documents/{Guid.NewGuid():N}",
            "application/pdf", DocumentDate, new string('a', 64), CreatedAt);

    private static async Task<Guid> InsertOrganizationAsync(DocumentsDbContext context, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var name = $"Document test organization {id:N}";
        // Test setup only: every writing test owns its organization in the same rollback transaction.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO org.organization (organization_id, name, created_at, updated_at)
            VALUES ({id}, {name}, {CreatedAt}, {CreatedAt})
            """, cancellationToken);
        return id;
    }

    private static void AssertConstraint(DbUpdateException exception, string sqlState, string name)
    {
        var postgresException = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(sqlState, postgresException.SqlState);
        Assert.Equal(name, postgresException.ConstraintName);
    }

    private async Task<IReadOnlyList<T>> QueryAsync<T>(
        string sql, Func<NpgsqlDataReader, T> map, params NpgsqlParameter[] parameters)
    {
        using var timeout = new CancellationTokenSource(QueryTimeout);
        await using var connection = new NpgsqlConnection(database.DocumentsConnectionString);
        await connection.OpenAsync(timeout.Token);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        await using var reader = await command.ExecuteReaderAsync(timeout.Token);
        var rows = new List<T>();
        while (await reader.ReadAsync(timeout.Token))
        {
            rows.Add(map(reader));
        }

        return rows;
    }

    private sealed record DatabaseColumn(string Name, string IsNullable, string DataType, int? MaximumLength);
}
