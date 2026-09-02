using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace FoodTraceability.Api.OpenApi;

internal sealed class AuthorizeOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointAttributes = context.MethodInfo
            .GetCustomAttributes(inherit: true)
            .Concat(context.MethodInfo.DeclaringType?.GetCustomAttributes(inherit: true) ?? [])
            .ToArray();
        if (endpointAttributes.OfType<AllowAnonymousAttribute>().Any()
            || !endpointAttributes.OfType<AuthorizeAttribute>().Any())
        {
            return;
        }

        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            }] = [],
        });
    }
}
