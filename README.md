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
