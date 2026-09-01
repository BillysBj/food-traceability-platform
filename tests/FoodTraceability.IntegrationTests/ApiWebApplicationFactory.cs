using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
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
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(30);

    public const string ExceptionEndpoint = "/_test/unhandled-exception";
    public const string SuccessEndpoint = "/_test/success";
    public const string TestExceptionMessage = "Synthetic failure for API integration tests.";
    public const string TestJwtSigningKey =
        "food-traceability-integration-test-signing-key-32-bytes-minimum";

    private readonly IReadOnlyDictionary<string, string?> _configuration;
    private readonly Action<IServiceCollection>? _configureTestServices;
    private readonly bool _disableHealthChecks;
    private readonly string _environment;
    private readonly Lazy<CancellationTokenSource> _requestTimeout = new(
        () => new CancellationTokenSource(RequestTimeout));

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
        bool disableHealthChecks = false,
        Action<IServiceCollection>? configureTestServices = null)
    {
        _environment = environment;
        _configuration = configuration ?? new Dictionary<string, string?>();
        _disableHealthChecks = disableHealthChecks;
        _configureTestServices = configureTestServices;
    }

    public TestLogSink LogSink { get; } = new();

    public CancellationToken RequestCancellationToken => _requestTimeout.Value.Token;

    protected override void Dispose(bool disposing)
    {
        if (disposing && _requestTimeout.IsValueCreated)
        {
            _requestTimeout.Value.Dispose();
        }

        base.Dispose(disposing);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var configuration = new Dictionary<string, string?>
            {
                ["ConnectionStrings:FoodTraceability"] =
                    "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                ["Jwt:SigningKey"] = TestJwtSigningKey
            };

            foreach (var pair in _configuration)
            {
                configuration[pair.Key] = pair.Value;
            }

            configurationBuilder.AddInMemoryCollection(configuration);
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.PostConfigure<KeyManagementOptions>(options =>
                options.XmlRepository = new InMemoryXmlRepository());
            services.AddSingleton<IStartupFilter, TestEndpointStartupFilter>();
            services.AddSingleton<ILogEventSink>(LogSink);

            if (_disableHealthChecks)
            {
                services.PostConfigure<HealthCheckServiceOptions>(options =>
                    options.Registrations.Clear());
            }

            _configureTestServices?.Invoke(services);
        });
    }

    private sealed class InMemoryXmlRepository : IXmlRepository
    {
        private readonly List<XElement> _elements = [];

        public IReadOnlyCollection<XElement> GetAllElements()
        {
            lock (_elements)
            {
                return _elements.Select(static element => new XElement(element)).ToArray();
            }
        }

        public void StoreElement(XElement element, string friendlyName)
        {
            lock (_elements)
            {
                _elements.Add(new XElement(element));
            }
        }
    }
}
