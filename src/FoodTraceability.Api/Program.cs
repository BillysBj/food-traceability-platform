using FoodTraceability.Platform.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("FoodTraceability")
    ?? throw new InvalidOperationException(
        "The connection string 'ConnectionStrings:FoodTraceability' is not configured.");

builder.Services.AddDbContext<PlatformDbContext>(options =>
    options.UseFoodTraceabilityPostgres(connectionString, PlatformDbContext.MigrationsHistorySchema));

var app = builder.Build();

app.Run();
