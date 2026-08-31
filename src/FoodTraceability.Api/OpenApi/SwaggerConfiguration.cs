using System.Reflection;
using Microsoft.OpenApi.Models;

namespace FoodTraceability.Api.OpenApi;

public static class SwaggerConfiguration
{
    private const string DocumentName = "v1";
    private const string BearerSchemeName = "Bearer";

    public static IServiceCollection AddApiSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc(DocumentName, new OpenApiInfo
            {
                Title = "Food Traceability API",
                Version = DocumentName
            });

            var xmlDocumentationPath = Path.Combine(
                AppContext.BaseDirectory,
                $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
            options.IncludeXmlComments(xmlDocumentationPath);

            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Description = "Enter a JWT bearer token.",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });
        });

        return services;
    }
}
