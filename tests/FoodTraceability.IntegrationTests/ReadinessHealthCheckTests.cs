using System.Net;
using FoodTraceability.Api.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog.Events;

namespace FoodTraceability.IntegrationTests;

public sealed class ReadinessHealthCheckTests
{
    [Fact]
    public async Task MissingConnectionStringReturnsUnhealthyWhileLivenessRemainsHealthy()
    {
        await AssertDatabaseIsUnhealthyAsync(null);
    }

    [Fact]
    public async Task WhitespaceConnectionStringReturnsUnhealthyWhileLivenessRemainsHealthy()
    {
        await AssertDatabaseIsUnhealthyAsync("   ");
    }

    [Fact]
    public async Task UnreachableDatabaseReturnsUnhealthyWhileLivenessRemainsHealthy()
    {
        const string unreachableConnectionString =
            "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1";

        await AssertDatabaseIsUnhealthyAsync(unreachableConnectionString);
    }

    [Fact]
    public async Task RequestCancellationIsPropagated()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var healthCheck = new DatabaseHealthCheck(
            new CancelingScopeFactory(cancellation.Token),
            NullLogger<DatabaseHealthCheck>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            healthCheck.CheckHealthAsync(new HealthCheckContext(), cancellation.Token));
    }

    private static async Task AssertDatabaseIsUnhealthyAsync(string? connectionString)
    {
        await using var factory = new ApiWebApplicationFactory(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] = connectionString
            });
        using var client = factory.CreateClient();

        using var readinessResponse = await client.GetAsync(
            "/health/ready",
            factory.RequestCancellationToken);
        using var livenessResponse = await client.GetAsync(
            "/health",
            factory.RequestCancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, readinessResponse.StatusCode);
        Assert.Equal(
            "Unhealthy",
            await readinessResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        Assert.Equal(HttpStatusCode.OK, livenessResponse.StatusCode);
        Assert.Equal(
            "Healthy",
            await livenessResponse.Content.ReadAsStringAsync(factory.RequestCancellationToken));
        Assert.Contains(factory.LogSink.Events, logEvent =>
            logEvent.Level == LogEventLevel.Error
            && logEvent.RenderMessage().Contains(
                "database readiness check",
                StringComparison.OrdinalIgnoreCase));
    }

    private sealed class CancelingScopeFactory(CancellationToken cancellationToken)
        : IServiceScopeFactory
    {
        public IServiceScope CreateScope()
        {
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
