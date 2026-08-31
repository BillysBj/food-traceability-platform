using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace FoodTraceability.IntegrationTests;

internal sealed class TestEndpointStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);
            app.Map(
                ApiWebApplicationFactory.ExceptionEndpoint,
                branch => branch.Run(static _ => Task.FromException(
                    new InvalidOperationException(ApiWebApplicationFactory.TestExceptionMessage))));
        };
    }
}
