# PermissionSystem

权限管理框架。

PermissionSystem is an enterprise-grade permission management platform built with Vue 3 and ASP.NET Core Web API (.NET 10).

## Tech Stack

Backend:

- ASP.NET Core Web API (.NET 10)
- Modular Monolith
- DDD-inspired layered architecture
- EF Core
- SQL Server
- OpenIddict
- Redis
- RabbitMQ
- Hangfire
- Serilog

Frontend:

- Vue 3
- TypeScript
- Vite
- Pinia
- Vue Router
- Axios
- Element Plus

## Repository Structure

```text
PermissionSystem/
  backend/
    PermissionSystem.sln
    PermissionSystem.Api/
    PermissionSystem.Application/
    PermissionSystem.Domain/
    PermissionSystem.Infrastructure/
    PermissionSystem.Shared/
    PermissionSystem.Worker/
  docs/
  frontend/
    permission-admin/
  scripts/
  docker-compose.yml
  .env.example
  AGENTS.md
  IMPLEMENTATION_PLAN.md
  README.md
```

## Local Backend Commands

The project targets .NET 10. Install .NET 10 SDK or use the local SDK under `.tools/dotnet10` if present.

```powershell
cd backend
dotnet restore
dotnet build
```

For local development, configure `backend/PermissionSystem.Api/appsettings.Development.json` with a SQL Server connection string. On startup in `Development`, the API runs EF Core migrations and seed data.

Default local API URLs from `launchSettings.json`:

- HTTP: `http://localhost:5264`
- HTTPS: `https://localhost:7281`

## Local Frontend Commands

```powershell
cd frontend/permission-admin
npm install
npm run build
```

Default login after seed data:

- Username: `admin`
- Password: `admin123456`

## API Testing With VSCode REST Client

Install the VSCode extension `REST Client`, then open:

```text
docs/api-tests.http
```

Before sending requests, make sure the backend API is running and the seed data has been initialized. The test file defines all variables at the top, including:

- `host`
- `clientId`
- `clientSecret`
- `username`
- `password`
- `pageIndex`
- `pageSize`

Recommended order:

1. Run `Get token - password grant`.
2. Run the current user and permission requests.
3. Run user, role, menu, and permission query requests.
4. Run `Refresh token` when you need to verify refresh token behavior.

The token request uses the default development account:

```text
username: admin
password: admin123456
client_id: permission-admin
client_secret: permission-admin-secret
```

REST Client automatically stores `access_token` and `refresh_token` from the token response and reuses them in later requests through `{{accessToken}}` and `{{refreshToken}}`.

The default REST Client `host` is `http://localhost:5264` for local `dotnet run`. Change it to `http://localhost:5000` when testing the Docker Compose API port directly.

## Docker Startup

Create a local `.env` file from the example:

```powershell
Copy-Item .env.example .env
```

Edit `.env` and set a strong `MSSQL_SA_PASSWORD`.

Start the stack:

```powershell
docker compose up -d
```

Default services:

- Frontend: `http://localhost:8080`
- Backend API: `http://localhost:5000`
- SQL Server: `localhost,1433`
- Redis: `localhost:6379`
- RabbitMQ: `localhost:5672`
- RabbitMQ Management: `http://localhost:15672`
- Hangfire Dashboard: `http://localhost:5000/hangfire`

In the `Docker` environment, the API runs EF Core migrations and seed data at startup. Nginx in the frontend container proxies these paths to the backend container:

- `/api`
- `/connect`
- `/swagger`
- `/hangfire`
- `/health`

Background jobs are executed by the `permission-system-worker` container. The API registers Hangfire storage and dashboard access; the worker hosts the Hangfire server and processes the configured queues.

Stop the stack:

```powershell
docker compose down
```

Remove containers and persisted data volumes:

```powershell
docker compose down -v
```

## Docker Notes

- SQL Server data is persisted in the `sqlserver_data` volume.
- Redis data is persisted in the `redis_data` volume.
- RabbitMQ data is persisted in the `rabbitmq_data` volume.
- Backend connection strings are supplied through environment variables.
- Production passwords are not committed. Use `.env` locally or secret management in deployed environments.
- The frontend uses relative API paths in Docker so Nginx can reverse proxy requests to the backend.

## Infrastructure Features

Application services should depend on these abstractions instead of concrete infrastructure SDKs:

- `ICacheService`: Redis-backed distributed cache.
- `IMessageBus`: RabbitMQ message publishing.
- `IBackgroundJobService`: Hangfire one-off, delayed, and recurring jobs.

Local defaults are configured in `backend/PermissionSystem.Api/appsettings.Development.json` and `backend/PermissionSystem.Worker/appsettings.Development.json`. Docker values are supplied from `docker-compose.yml` and `.env`.

## Scheduled Task Demo

The system includes a frontend-configurable Hangfire recurring task demo.

- Menu: System Management / Scheduled Tasks
- Seed task code: `demo-minute-log`
- Job type: `DemoLog`
- Default Cron: `* * * * *`
- Result: each execution writes a row to `ScheduledTaskExecutionLogs` and updates the task's latest execution status.

To test it locally, run the API and the worker at the same time. The API runs migrations, seeds the demo task, and syncs enabled tasks into Hangfire. The worker executes the Hangfire jobs.
