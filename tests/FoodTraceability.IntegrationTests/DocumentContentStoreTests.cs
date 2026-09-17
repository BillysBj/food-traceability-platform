using FoodTraceability.Modules.Documents.Application;
using FoodTraceability.Modules.Documents.Domain;
using FoodTraceability.Modules.Documents.Infrastructure;
using FoodTraceability.Platform.Contracts.Transactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class DocumentContentStoreTests(PostgreSqlContainerFixture database)
{
    [Theory]
    [InlineData(256)]
    [InlineData(5 * 1024 * 1024)]
    public async Task ContentRoundTripsByteForByteThroughRegisteredStore(int length)
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var cancellationToken = factory.RequestCancellationToken;
        var transactionService = scope.ServiceProvider.GetRequiredService<IApplicationTransaction>();
        await using var transaction = await transactionService.BeginAsync(cancellationToken);
        var store = scope.ServiceProvider.GetRequiredService<IDocumentContentStore>();
        var storageKey = DocumentStorageKey.Create(Guid.NewGuid());
        var bytes = new byte[length];
        for (var index = 0; index < bytes.Length; index++)
        {
            bytes[index] = (byte)(index % 256);
        }

        await store.AddAsync(DocumentContent.Create(storageKey, bytes), cancellationToken);
        var context = scope.ServiceProvider.GetRequiredService<DocumentsDbContext>();
        context.ChangeTracker.Clear();

        await using var stream = await store.OpenReadAsync(storageKey, cancellationToken);
        Assert.NotNull(stream);
        Assert.IsType<MemoryStream>(stream);
        using var received = new MemoryStream();
        await stream.CopyToAsync(received, cancellationToken);
        Assert.Equal(bytes, received.ToArray());
        Assert.Empty(context.ChangeTracker.Entries());

        // Also exercise EF constructor binding for the aggregate with its defensive copy.
        var materialized = await context.DocumentContents.AsNoTracking()
            .SingleAsync(content => content.StorageKey == storageKey, cancellationToken);
        Assert.Equal(storageKey, materialized.StorageKey);
        Assert.Equal(bytes, materialized.Content);
        // Disposal always rolls back this test's write.
    }

    [Fact]
    public async Task UnknownStorageKeyReturnsNull()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IDocumentContentStore>();

        Assert.Null(await store.OpenReadAsync(
            DocumentStorageKey.Create(Guid.NewGuid()), factory.RequestCancellationToken));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ContentParticipatesInApplicationTransactionAndPersistsOnlyWithCommit(bool commit)
    {
        await using var factory = CreateFactory();
        var cancellationToken = factory.RequestCancellationToken;
        var storageKey = DocumentStorageKey.Create(Guid.NewGuid());
        byte[] bytes = [0x00, 0x7f, 0xff];

        using (var scope = factory.Services.CreateScope())
        {
            var transactionService = scope.ServiceProvider.GetRequiredService<IApplicationTransaction>();
            await using var transaction = await transactionService.BeginAsync(cancellationToken);
            var store = scope.ServiceProvider.GetRequiredService<IDocumentContentStore>();
            await store.AddAsync(DocumentContent.Create(storageKey, bytes), cancellationToken);
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<DocumentsDbContext>().Database.CurrentTransaction);

            if (commit)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            // Otherwise dispose without commit or explicit rollback, as DOC-003 will on failure.
        }

        using var verificationScope = factory.Services.CreateScope();
        var reader = verificationScope.ServiceProvider.GetRequiredService<IDocumentContentStore>();
        await using var stream = await reader.OpenReadAsync(storageKey, cancellationToken);
        if (commit)
        {
            Assert.NotNull(stream);
            using var received = new MemoryStream();
            await stream.CopyToAsync(received, cancellationToken);
            Assert.Equal(bytes, received.ToArray());
        }
        else
        {
            Assert.Null(stream);
        }
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.DocumentsConnectionString,
        });
}
