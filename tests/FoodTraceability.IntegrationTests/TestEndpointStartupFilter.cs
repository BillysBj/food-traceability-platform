using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

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
            app.Map(
                ApiWebApplicationFactory.SuccessEndpoint,
                branch => branch.Run(static context => context.Response.WriteAsync("OK")));
        };
    }
}
