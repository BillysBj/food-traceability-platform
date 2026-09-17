using System.Data;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
using FoodTraceability.Modules.Documents.Infrastructure;
using FoodTraceability.Modules.Identity.Infrastructure;
using FoodTraceability.Modules.Organizations.Domain;
using FoodTraceability.Modules.Organizations.Infrastructure;
using FoodTraceability.Modules.Quality.Infrastructure;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FoodTraceability.IntegrationTests;

[Collection(PostgreSqlDatabaseCollection.Name)]
[Trait("Category", "Database")]
public sealed class SharedConnectionTests(PostgreSqlContainerFixture database)
{
    [Fact]
    public void AllDbContextsInOneScopeShareTheScopedConnection()
    {
        using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var connection = services.GetRequiredService<NpgsqlConnection>();
        DbContext[] contexts =
        [
            services.GetRequiredService<PlatformDbContext>(),
            services.GetRequiredService<OrganizationsDbContext>(),
            services.GetRequiredService<IdentityDbContext>(),
            services.GetRequiredService<CatalogDbContext>(),
            services.GetRequiredService<QualityDbContext>(),
            services.GetRequiredService<DocumentsDbContext>(),
            services.GetRequiredService<TraceabilityDbContext>(),
        ];

        Assert.All(contexts, context => Assert.Same(connection, context.Database.GetDbConnection()));
        Assert.Equal(ConnectionState.Closed, connection.State);
    }

    [Fact]
    public void DifferentScopesHaveDifferentConnections()
    {
        using var factory = CreateFactory();
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        var second = secondScope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        Assert.NotSame(first.Database.GetDbConnection(), second.Database.GetDbConnection());
    }

    // Organization and Product have existing factories and need no related rows.
    // The Article API database already contains both owning modules' schemas.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedTransactionPersistsBothModuleRowsOnlyWhenCommitted(bool commit)
    {
        await using var factory = CreateFactory();
        var cancellationToken = factory.RequestCancellationToken;
        var organizationId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using (var scope = factory.Services.CreateScope())
        {
            var organizations = scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var scopedTransaction = scope.ServiceProvider.GetRequiredService<ScopedTransaction>();
            await using var transaction = await scopedTransaction.BeginAsync(cancellationToken);
            await scopedTransaction.EnlistAsync(organizations, cancellationToken);
            await scopedTransaction.EnlistAsync(catalog, cancellationToken);

            Assert.NotNull(organizations.Database.CurrentTransaction);
            Assert.NotNull(catalog.Database.CurrentTransaction);
            Assert.Same(
                organizations.Database.CurrentTransaction.GetDbTransaction(),
                catalog.Database.CurrentTransaction.GetDbTransaction());

            organizations.Organizations.Add(Organization.Create(
                organizationId,
                $"Shared transaction organization {organizationId:N}",
                vatId: null,
                taxNumber: null,
                email: null,
                phone: null,
                createdAt: now));
            Assert.Equal(1, await organizations.SaveChangesAsync(cancellationToken));

            catalog.Products.Add(Product.Create(
                productId,
                $"SHARED-{productId:N}",
                "Shared transaction product",
                now));
            Assert.Equal(1, await catalog.SaveChangesAsync(cancellationToken));

            if (commit)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }
        }

        using var verificationScope = factory.Services.CreateScope();
        var persistedOrganizations = verificationScope.ServiceProvider
            .GetRequiredService<OrganizationsDbContext>();
        var persistedCatalog = verificationScope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        Assert.Equal(commit, await persistedOrganizations.Organizations
            .AsNoTracking()
            .AnyAsync(organization => organization.Id == organizationId, cancellationToken));
        Assert.Equal(commit, await persistedCatalog.Products
            .AsNoTracking()
            .AnyAsync(product => product.Id == productId, cancellationToken));
    }

    [Fact]
    public async Task SharedTransactionRejectsContextsWithDifferentConnections()
    {
        await using var factory = CreateFactory();
        using var ownerScope = factory.Services.CreateScope();
        using var participantScope = factory.Services.CreateScope();
        var owner = ownerScope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        var participant = participantScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var scopedTransaction = ownerScope.ServiceProvider.GetRequiredService<ScopedTransaction>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scopedTransaction.EnlistAsync(participant, factory.RequestCancellationToken));

        Assert.Contains("same DbConnection instance", exception.Message);
        Assert.Null(owner.Database.CurrentTransaction);
        Assert.Null(participant.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, owner.Database.GetDbConnection().State);
        Assert.Equal(ConnectionState.Closed, participant.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task EnlistingWithoutTransactionDoesNotOpenConnection()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        var scopedTransaction = scope.ServiceProvider.GetRequiredService<ScopedTransaction>();

        await scopedTransaction.EnlistAsync(context, factory.RequestCancellationToken);

        Assert.False(scopedTransaction.IsActive);
        Assert.Null(context.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, context.Database.GetDbConnection().State);
    }

    [Fact]
    public async Task OpeningTwiceRejectsNestedTransactionAndLeavesOriginalActive()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var scopedTransaction = scope.ServiceProvider.GetRequiredService<ScopedTransaction>();
        var cancellationToken = factory.RequestCancellationToken;
        await using var transaction = await scopedTransaction.BeginAsync(cancellationToken);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scopedTransaction.BeginAsync(cancellationToken));

        Assert.Equal(
            "A transaction is already active in this DI scope; nested transactions are not supported.",
            exception.Message);
        Assert.True(scopedTransaction.IsActive);
        await transaction.RollbackAsync(cancellationToken);
        Assert.False(scopedTransaction.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletingTransactionClearsEnlistmentsAndAllowsContextReuse(bool commit)
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();
        var scopedTransaction = scope.ServiceProvider.GetRequiredService<ScopedTransaction>();
        var context = scope.ServiceProvider.GetRequiredService<OrganizationsDbContext>();
        var cancellationToken = factory.RequestCancellationToken;
        await using var transaction = await scopedTransaction.BeginAsync(cancellationToken);
        await scopedTransaction.EnlistAsync(context, cancellationToken);
        var enlistment = context.Database.CurrentTransaction;
        Assert.NotNull(enlistment);
        await scopedTransaction.EnlistAsync(context, cancellationToken);
        Assert.Same(enlistment, context.Database.CurrentTransaction);

        if (commit)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await transaction.RollbackAsync(cancellationToken);
        }

        Assert.False(scopedTransaction.IsActive);
        Assert.Null(context.Database.CurrentTransaction);
        await using var nextTransaction = await scopedTransaction.BeginAsync(cancellationToken);
        await scopedTransaction.EnlistAsync(context, cancellationToken);
        Assert.NotNull(context.Database.CurrentTransaction);
        Assert.NotSame(enlistment, context.Database.CurrentTransaction);
        await nextTransaction.RollbackAsync(cancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisposingScopeRollsBackUncompletedTransaction(bool disposeAsync)
    {
        await using var factory = CreateFactory();
        var cancellationToken = factory.RequestCancellationToken;
        var productId = Guid.NewGuid();
        var scope = factory.Services.CreateAsyncScope();
        try
        {
            var scopedTransaction = scope.ServiceProvider.GetRequiredService<ScopedTransaction>();
            // Deliberately leave completion and handle disposal to the owning scope.
            _ = await scopedTransaction.BeginAsync(cancellationToken);
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            await scopedTransaction.EnlistAsync(catalog, cancellationToken);
            catalog.Products.Add(Product.Create(
                productId,
                $"SCOPE-{productId:N}",
                "Uncommitted scope product",
                DateTimeOffset.UtcNow));
            Assert.Equal(1, await catalog.SaveChangesAsync(cancellationToken));
        }
        finally
        {
            if (disposeAsync)
            {
                await scope.DisposeAsync();
            }
            else
            {
                scope.Dispose();
            }
        }

        using var verificationScope = factory.Services.CreateScope();
        var persisted = verificationScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        Assert.False(await persisted.Products.AsNoTracking()
            .AnyAsync(product => product.Id == productId, cancellationToken));
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.ArticleApiConnectionString,
        });
}
