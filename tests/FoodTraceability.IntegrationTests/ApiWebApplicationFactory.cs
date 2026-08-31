using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Serilog.Core;

namespace FoodTraceability.IntegrationTests;

public sealed class ApiWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string ExceptionEndpoint = "/_test/unhandled-exception";
    public const string SuccessEndpoint = "/_test/success";
    public const string TestExceptionMessage = "Synthetic failure for API integration tests.";

    private readonly IReadOnlyDictionary<string, string?> _configuration;
    private readonly bool _disableHealthChecks;
    private readonly string _environment;

    public ApiWebApplicationFactory()
        : this(Environments.Development, null)
    {
    }

    public ApiWebApplicationFactory(string environment)
        : this(environment, null)
    {
    }

    public ApiWebApplicationFactory(IReadOnlyDictionary<string, string?> configuration)
        : this(Environments.Development, configuration, false)
    {
    }

    public ApiWebApplicationFactory(
        IReadOnlyDictionary<string, string?> configuration,
        bool disableHealthChecks)
        : this(Environments.Development, configuration, disableHealthChecks)
    {
    }

    public ApiWebApplicationFactory(
        string environment,
        IReadOnlyDictionary<string, string?>? configuration,
        bool disableHealthChecks = false)
    {
        _environment = environment;
        _configuration = configuration ?? new Dictionary<string, string?>();
        _disableHealthChecks = disableHealthChecks;
    }

    public TestLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] =
                    "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1"
            };

            foreach (var pair in _configuration)
            {
                configuration[pair.Key] = pair.Value;
            }

            configurationBuilder.AddInMemoryCollection(configuration);
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestEndpointStartupFilter>();
            services.AddSingleton<ILogEventSink>(LogSink);

            if (_disableHealthChecks)
            {
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                    options.Registrations.Clear());
            }
        });
    }
}
