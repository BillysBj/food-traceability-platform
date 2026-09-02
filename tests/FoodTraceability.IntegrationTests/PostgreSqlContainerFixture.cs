using FoodTraceability.Modules.Catalog.Infrastructure;
using FoodTraceability.Modules.Identity.Infrastructure;
using FoodTraceability.Modules.Organizations.Infrastructure;
using FoodTraceability.Modules.Traceability.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FoodTraceability.IntegrationTests;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromMinutes(2);

    private PostgreSqlContainer? _container;
    private string? _catalogConnectionString;
    private string? _identityConnectionString;
    private string? _organizationsConnectionString;
    private string? _traceabilityConnectionString;

    public string ConnectionString => GetContainer().GetConnectionString();

    public string CatalogConnectionString => _catalogConnectionString
        ?? throw new InvalidOperationException("The Catalog test database is not initialized.");

    public string IdentityConnectionString => _identityConnectionString
        ?? throw new InvalidOperationException("The Identity test database is not initialized.");

    public string OrganizationsConnectionString => _organizationsConnectionString
        ?? throw new InvalidOperationException("The Organizations test database is not initialized.");

    public string TraceabilityConnectionString => _traceabilityConnectionString
        ?? throw new InvalidOperationException("The Traceability test database is not initialized.");

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(InitializationTimeout);

        try
        {
            _container = new PostgreSqlBuilder("postgres:17")
                .WithDatabase($"food_traceability_platform_tests_{Guid.NewGuid():N}")
                .WithUsername("test_user")
                .WithPassword(Guid.NewGuid().ToString("N"))
                .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8")
                .Build();
            await _container.StartAsync(timeout.Token);

            _catalogConnectionString = await CreateDatabaseAsync(
                $"food_traceability_catalog_tests_{Guid.NewGuid():N}",
                timeout.Token);
            _identityConnectionString = await CreateDatabaseAsync(
                $"food_traceability_identity_tests_{Guid.NewGuid():N}",
                timeout.Token);
            _organizationsConnectionString = await CreateDatabaseAsync(
                $"food_traceability_organizations_tests_{Guid.NewGuid():N}",
                timeout.Token);
            _traceabilityConnectionString = await CreateDatabaseAsync(
                $"food_traceability_traceability_tests_{Guid.NewGuid():N}",
                timeout.Token);
        }
        catch (Exception exception)
        {
            if (_container is not null)
            {
                await _container.DisposeAsync();
            }

            throw new InvalidOperationException(
                "The PostgreSQL test container could not start. Ensure Docker Desktop or "
                + "Docker Engine is installed and running and that the current user can access "
                + "the Docker daemon. Database tests are intentionally not skipped.",
                exception);
        }

        try
        {
            await using var context = CreateDbContext();
            await context.Database.MigrateAsync(timeout.Token);

            // Catalog owns no Organizations entities. Its migration-level cross-schema FK
            // requires the referenced org table to exist in the same database first.
            await using var catalogOrganizationsContext =
                CreateCatalogOrganizationsDbContext();
            await catalogOrganizationsContext.Database.MigrateAsync(timeout.Token);

            await using var catalogContext = CreateCatalogDbContext();
            await catalogContext.Database.MigrateAsync(timeout.Token);

            // Identity owns no Organizations entities. Its migration-level cross-schema FKs
            // nevertheless require the referenced org tables to exist in the same database.
            await using var identityOrganizationsContext = CreateIdentityOrganizationsDbContext();
            await identityOrganizationsContext.Database.MigrateAsync(timeout.Token);

            await using var identityContext = CreateIdentityDbContext();
            await identityContext.Database.MigrateAsync(timeout.Token);

            await using var organizationsContext = CreateOrganizationsDbContext();
            await organizationsContext.Database.MigrateAsync(timeout.Token);

            // Traceability owns no Organizations entities. Its migration-level cross-schema FK
            // requires the referenced org table to exist in the same database first.
            await using var traceabilityOrganizationsContext =
                CreateTraceabilityOrganizationsDbContext();
            await traceabilityOrganizationsContext.Database.MigrateAsync(timeout.Token);

            await using var traceabilityContext = CreateTraceabilityDbContext();
            await traceabilityContext.Database.MigrateAsync(timeout.Token);
        }
        catch
        {
            await GetContainer().DisposeAsync();
            throw;
        }
    }

    public Task DisposeAsync()
    {
        return _container is null
            ? Task.CompletedTask
            : _container.DisposeAsync().AsTask();
    }

    public PlatformDbContext CreateDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<PlatformDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            ConnectionString,
            PlatformDbContext.MigrationsHistorySchema);

        return new PlatformDbContext(optionsBuilder.Options);
    }

    public CatalogDbContext CreateCatalogDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            CatalogConnectionString,
            CatalogDbContext.Schema);

        return new CatalogDbContext(optionsBuilder.Options);
    }

    public IdentityDbContext CreateIdentityDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            IdentityConnectionString,
            IdentityDbContext.Schema);

        return new IdentityDbContext(optionsBuilder.Options);
    }

    public OrganizationsDbContext CreateOrganizationsDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            OrganizationsConnectionString,
            OrganizationsDbContext.Schema);

        return new OrganizationsDbContext(optionsBuilder.Options);
    }

    public OrganizationsDbContext CreateCatalogOrganizationsDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            CatalogConnectionString,
            OrganizationsDbContext.Schema);

        return new OrganizationsDbContext(optionsBuilder.Options);
    }

    public OrganizationsDbContext CreateIdentityOrganizationsDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            IdentityConnectionString,
            OrganizationsDbContext.Schema);

        return new OrganizationsDbContext(optionsBuilder.Options);
    }

    public TraceabilityDbContext CreateTraceabilityDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<TraceabilityDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            TraceabilityConnectionString,
            TraceabilityDbContext.Schema);

        return new TraceabilityDbContext(optionsBuilder.Options);
    }

    public OrganizationsDbContext CreateTraceabilityOrganizationsDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            TraceabilityConnectionString,
            OrganizationsDbContext.Schema);

        return new OrganizationsDbContext(optionsBuilder.Options);
    }

    private async Task<string> CreateDatabaseAsync(
        string databaseName,
        CancellationToken cancellationToken)
    {
        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(ConnectionString);
        await using var connection = new NpgsqlConnection(connectionStringBuilder.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var quotedDatabaseName = new NpgsqlCommandBuilder().QuoteIdentifier(databaseName);
        await using var command = new NpgsqlCommand(
            $"CREATE DATABASE {quotedDatabaseName}",
            connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        connectionStringBuilder.Database = databaseName;
        return connectionStringBuilder.ConnectionString;
    }

    private PostgreSqlContainer GetContainer()
    {
        return _container
            ?? throw new InvalidOperationException("The PostgreSQL test container is not initialized.");
    }
}
