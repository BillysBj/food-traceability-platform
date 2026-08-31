using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog.Core;

namespace FoodTraceability.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ExceptionEndpoint = "/_test/unhandled-exception";
    public const string TestExceptionMessage = "Synthetic failure for API integration tests.";

    private readonly string _environment;

    public ApiWebApplicationFactory()
        : this(Environments.Development)
    {
    }

    public ApiWebApplicationFactory(string environment)
    {
        _environment = environment;
    }

    public TestLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] =
                    "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestEndpointStartupFilter>();
            services.AddSingleton<ILogEventSink>(LogSink);
        });
    }
}
