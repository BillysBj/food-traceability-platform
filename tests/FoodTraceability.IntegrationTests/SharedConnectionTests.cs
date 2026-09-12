using System.Data;
using FoodTraceability.Modules.Catalog.Domain;
using FoodTraceability.Modules.Catalog.Infrastructure;
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
    public void AllSixDbContextsInOneScopeShareTheScopedConnection()
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
            await using var transaction = await organizations.BeginSharedTransactionAsync(
                catalog,
                cancellationToken);

            Assert.NotNull(catalog.Database.CurrentTransaction);
            Assert.Same(
                transaction.GetDbTransaction(),
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

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            owner.BeginSharedTransactionAsync(participant, factory.RequestCancellationToken));

        Assert.Contains("same DbConnection instance", exception.Message);
        Assert.Null(owner.Database.CurrentTransaction);
        Assert.Null(participant.Database.CurrentTransaction);
        Assert.Equal(ConnectionState.Closed, owner.Database.GetDbConnection().State);
        Assert.Equal(ConnectionState.Closed, participant.Database.GetDbConnection().State);
    }

    private ApiWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string?>
        {
            ["ConnectionStrings:FoodTraceability"] = database.ArticleApiConnectionString,
        });
}
