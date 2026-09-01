using FoodTraceability.Platform.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace FoodTraceability.IntegrationTests;

public sealed class PostgreSqlContainerFixture : IAsyncLifetime
{
    private static readonly TimeSpan InitializationTimeout = TimeSpan.FromMinutes(2);

    private PostgreSqlContainer? _container;

    public string ConnectionString => GetContainer().GetConnectionString();

    public async Task InitializeAsync()
    {
        using var timeout = new CancellationTokenSource(InitializationTimeout);

        try
        {
            _container = new PostgreSqlBuilder("postgres:17")
                .WithDatabase($"food_traceability_tests_{Guid.NewGuid():N}")
                .WithUsername("test_user")
                .WithPassword(Guid.NewGuid().ToString("N"))
                .WithEnvironment("POSTGRES_INITDB_ARGS", "--encoding=UTF8")
                .Build();
            await _container.StartAsync(timeout.Token);
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

    private PostgreSqlContainer GetContainer()
    {
        return _container
            ?? throw new InvalidOperationException("The PostgreSQL test container is not initialized.");
    }
}
