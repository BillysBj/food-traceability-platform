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

Apply the Identity module migrations through its design-time factory:

```sh
dotnet ef database update --project src/Modules/Identity/FoodTraceability.Modules.Identity.Infrastructure --startup-project src/FoodTraceability.Api --context IdentityDbContext
```

Check that the Identity migration matches the current model:

```sh
dotnet ef migrations has-pending-model-changes --project src/Modules/Identity/FoodTraceability.Modules.Identity.Infrastructure --startup-project src/FoodTraceability.Api --context IdentityDbContext
```

Apply the Organizations module migrations through its design-time factory:

```sh
dotnet ef database update --project src/Modules/Organizations/FoodTraceability.Modules.Organizations.Infrastructure --startup-project src/FoodTraceability.Api --context OrganizationsDbContext
```

Check that the Organizations migration matches the current model:

```sh
dotnet ef migrations has-pending-model-changes --project src/Modules/Organizations/FoodTraceability.Modules.Organizations.Infrastructure --startup-project src/FoodTraceability.Api --context OrganizationsDbContext
```

Migrations deliberately do not run automatically when the API starts. Apply them explicitly during deployment or local setup.

## Running the tests

The solution contains three test projects:

- Unit tests cover domain behavior in isolation, without database or HTTP: entity
  invariants and normalization, value objects, and the authentication and authorization
  use cases against test doubles.
- Integration tests run the API through `WebApplicationFactory` and the modules against a
  real PostgreSQL instance provided by Testcontainers. They cover every EF Core migration
  and the constraints it creates, the authentication flow including refresh-token rotation
  and replay detection, permission and tenant-scope enforcement, and the write endpoints
  with their negative cases.
- Architecture tests cover project, package and Compose guards, compiled type dependencies
  between layers and modules, and the decision log: they enforce that the permission lists
  in `docs/DECISIONS.md`, `AGENTS.md` and `ARCHITECTURE.md` stay identical and that no
  Domain or Application project reaches ASP.NET Core.

Run the complete test suite from the repository root:

```sh
dotnet test FoodTraceability.sln
```

Database integration tests require a running Docker Desktop or Docker Engine. They start one
`postgres:17` Testcontainer on a random host port, initialize it with UTF-8, apply the platform
migration, and remove the container after the test collection. The fixture uses a generated
connection string and never reads `ConnectionStrings__FoodTraceability`, so it cannot use or
modify the local Compose database.

Run only the fast tests without Docker:

```sh
dotnet test FoodTraceability.sln --filter "Category!=Database"
```

API, unit, and architecture test classes may run in parallel because each API test owns its
`WebApplicationFactory` and log sink and the other tests do not share mutable state. All tests
tagged `Category=Database` belong to one xUnit collection: they share one migrated container,
run serially, and are read-only after fixture initialization. Future database tests that mutate
state must add transaction rollback or explicit cleanup so one test cannot affect another.

## Continuous integration

[![CI](https://github.com/BillysBj/food-traceability-platform/actions/workflows/ci.yml/badge.svg)](https://github.com/BillysBj/food-traceability-platform/actions/workflows/ci.yml)

The CI workflow restores dependencies, builds the complete solution in Release configuration,
and runs the full test suite, including the PostgreSQL tests backed by Testcontainers. It runs
for pull requests targeting `main`, pushes to `main`, and manual reviewer dispatches. The single
job uses `ubuntu-latest` because the Testcontainers tests require an available Linux Docker
daemon. Its name, `Build and test`, is the intended Required Status Check and must remain stable
so branch-protection rules continue to apply.

## Running the API

Set the PostgreSQL connection string and a private JWT signing key of at least 32 bytes through standard .NET configuration environment variables before starting the API:

```powershell
$env:ConnectionStrings__FoodTraceability = "Host=localhost;Port=5432;Database=<database>;Username=<user>;Password=<password>"
$env:Jwt__SigningKey = "<a-random-secret-with-at-least-32-bytes>"
dotnet run --project src/FoodTraceability.Api
```

Keep the real values local and do not add them to tracked configuration. The API deliberately fails at startup when the JWT signing key is missing or shorter than 256 bits. Starting the API does not apply migrations.

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

## API security baseline

Every API response includes these transport-level security headers:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: no-referrer`
- `X-Permitted-Cross-Domain-Policies: none`
- `Content-Security-Policy`

The regular Content Security Policy is restrictive. Swagger uses a narrowly scoped,
development-only policy on `/swagger` paths because Swashbuckle requires inline scripts and
styles. The server implementation header is suppressed.

CORS denies all cross-origin access by default. Configure only explicitly trusted origins as
an array under `Cors:AllowedOrigins`; no frontend origin is preconfigured. For example, use
the standard .NET configuration keys `Cors__AllowedOrigins__0`,
`Cors__AllowedOrigins__1`, and so on in the deployment environment.

### Rate limiting

Three independently configurable blocks protect the API and authentication flow:

- `RateLimiting:PermitLimit` and `RateLimiting:WindowSeconds` configure the global
  fixed-window limiter per remote IP address.
- `RateLimiting:Authentication:PermitLimit` and
  `RateLimiting:Authentication:WindowSeconds` configure the stricter fixed-window limiter
  per remote IP address for authentication endpoints.
- `Authentication:LoginAttempts:PermitLimit` and
  `Authentication:LoginAttempts:WindowSeconds` configure the failed-login counter per
  entered email address.

The configured values are deployment defaults and can be adjusted operationally. They are
not recommendations for a particular environment. Requests rejected by an IP-based limiter
receive an HTTP 429 Problem Details response. `/health` and `/health/ready` are exempt from
the global limiter so monitoring cannot lock itself out.

HTTPS redirection and HSTS are enabled only outside the `Development` environment.
