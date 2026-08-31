# food-traceability-platform

## Local development

### Prerequisites

- Docker Desktop, or Docker Engine with Docker Compose v2

Create the local environment file before starting PostgreSQL:

```sh
cp .env.example .env
```

On PowerShell, use `Copy-Item .env.example .env` instead.

Start the database and check its status:

```sh
docker compose up -d
docker compose ps
```

Stop the database while retaining its data:

```sh
docker compose down
```

To completely reset the local database, including its named volume:

```sh
docker compose down -v
```

**Warning:** This permanently deletes all data in the local PostgreSQL database.

Put machine-specific Compose changes in `docker-compose.override.yml`. The file is excluded by `.gitignore` and must not be committed.

## Database migrations

Set the local PostgreSQL connection string through the standard .NET configuration environment variable `ConnectionStrings__FoodTraceability`. Keep its value in the local environment only; do not add credentials to tracked configuration files. Alternatively, the API can read machine-specific configuration from the already ignored `appsettings.Local.json` when the `Local` environment is selected.

Apply the platform migrations from the repository root:

```sh
dotnet ef database update --project src/Platform/FoodTraceability.Platform.Persistence --startup-project src/FoodTraceability.Api --context PlatformDbContext
```

Check that the migration matches the current model:

```sh
dotnet ef migrations has-pending-model-changes --project src/Platform/FoodTraceability.Platform.Persistence --startup-project src/FoodTraceability.Api --context PlatformDbContext
```

Migrations deliberately do not run automatically when the API starts. Apply them explicitly during deployment or local setup.

## Running the API

Set the PostgreSQL connection string through the standard .NET configuration environment variable before starting the API:

```powershell
$env:ConnectionStrings__FoodTraceability = "Host=localhost;Port=5432;Database=<database>;Username=<user>;Password=<password>"
dotnet run --project src/FoodTraceability.Api
```

Keep the real value local and do not add it to tracked configuration. The API deliberately fails at startup when `ConnectionStrings:FoodTraceability` is missing. Starting the API does not apply migrations.

In the Development environment, Swagger is available at:

```text
http://localhost:5080/swagger
http://localhost:5080/swagger/index.html
http://localhost:5080/swagger/v1/swagger.json
```

Check liveness without contacting external dependencies:

```powershell
Invoke-WebRequest http://localhost:5080/health
```

Check readiness, including PostgreSQL connectivity:

```powershell
Invoke-WebRequest http://localhost:5080/health/ready
```
