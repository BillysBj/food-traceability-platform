using FoodTraceability.Modules.Identity.Infrastructure;
using FoodTraceability.Modules.Organizations.Infrastructure;
using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace FoodTraceability.IntegrationTests;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromMinutes(2);

    private PostgreSqlContainer? _container;
    private string? _identityConnectionString;
    private string? _organizationsConnectionString;

    public string ConnectionString => GetContainer().GetConnectionString();

    public string IdentityConnectionString => _identityConnectionString
        ?? throw new InvalidOperationException("The Identity test database is not initialized.");

    public string OrganizationsConnectionString => _organizationsConnectionString
        ?? throw new InvalidOperationException("The Organizations test database is not initialized.");

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

            _identityConnectionString = await CreateDatabaseAsync(
                $"food_traceability_identity_tests_{Guid.NewGuid():N}",
                timeout.Token);
            _organizationsConnectionString = await CreateDatabaseAsync(
                $"food_traceability_organizations_tests_{Guid.NewGuid():N}",
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

            // Identity owns no Organizations entities. Its migration-level cross-schema FKs
            // nevertheless require the referenced org tables to exist in the same database.
            await using var identityOrganizationsContext = CreateIdentityOrganizationsDbContext();
            await identityOrganizationsContext.Database.MigrateAsync(timeout.Token);

            await using var identityContext = CreateIdentityDbContext();
            await identityContext.Database.MigrateAsync(timeout.Token);

            await using var organizationsContext = CreateOrganizationsDbContext();
            await organizationsContext.Database.MigrateAsync(timeout.Token);
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

    public OrganizationsDbContext CreateIdentityOrganizationsDbContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrganizationsDbContext>();
        optionsBuilder.UseFoodTraceabilityPostgres(
            IdentityConnectionString,
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
