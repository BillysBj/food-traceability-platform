using FoodTraceability.Api;
using FoodTraceability.Api.Errors;
using FoodTraceability.Api.Middleware;
using FoodTraceability.Api.OpenApi;
using FoodTraceability.Platform.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) => loggerConfiguration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

builder.Services.AddDbContext<PlatformDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = configuration.GetConnectionString("FoodTraceability")
        ?? throw new InvalidOperationException(
            "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

    options.UseFoodTraceabilityPostgres(connectionString, PlatformDbContext.MigrationsHistorySchema);
});
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions.TryAdd(
            "correlationId",
            CorrelationIdMiddleware.GetCorrelationId(context.HttpContext));
        context.ProblemDetails.Extensions.TryAdd(
            "traceId",
            CorrelationIdMiddleware.GetTraceId(context.HttpContext));
    };
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddApiSwagger();
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<PlatformDbContext>(
        "database",
        tags: [HealthCheckTags.Readiness]);

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Food Traceability API v1"));
}

app.MapHealthChecks(
    "/health",
    new HealthCheckOptions
    {
        Predicate = static _ => false
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = static registration => registration.Tags.Contains(HealthCheckTags.Readiness)
    });

app.Run();

public partial class Program;
