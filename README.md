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
- OpenTelemetry

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
- Password: configured by `SeedData:AdminPassword`

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

Before requesting a token, configure the local seed values through `.env`, user secrets, or environment variables:

```text
username: admin
password: <your SEED_ADMIN_PASSWORD>
client_id: permission-admin
client_secret: <your SEED_OAUTH_CLIENT_SECRET>
```

REST Client automatically stores `access_token` and `refresh_token` from the token response and reuses them in later requests through `{{accessToken}}` and `{{refreshToken}}`.

The default REST Client `host` is `http://localhost:5264` for local `dotnet run`. Change it to `http://localhost:5000` when testing the Docker Compose API port directly.

## Docker Startup

Create a local `.env` file from the example:

```powershell
Copy-Item .env.example .env
```

Edit `.env` and set strong local values for `MSSQL_SA_PASSWORD`, `SEED_ADMIN_PASSWORD`, `SEED_OAUTH_CLIENT_SECRET`, and `SYSTEM_CONFIG_ENCRYPTION_KEY`.

Start the stack:

```powershell
docker compose up -d
```

Default services:

- Frontend: `http://localhost:8080`
- Backend API: `http://localhost:5000`
- SQL Server: `localhost,1433`
- Redis: `localhost:6379`
- Hangfire Dashboard: `http://localhost:5000/hangfire`

RabbitMQ is optional and is not started by the default Compose command. To start it and enable application messaging, set the RabbitMQ flags and use the `mq` profile:

```powershell
$env:RABBITMQ_ENABLED = "true"
$env:RABBITMQ_ENABLE_CONSUMERS = "true"
$env:RABBITMQ_ENABLE_OUTBOX_PUBLISHER = "true"
docker compose --profile mq up -d
```

When enabled through the `mq` profile:

- RabbitMQ: `localhost:5672`
- RabbitMQ Management: `http://localhost:15672`

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
- RabbitMQ data is persisted in the `rabbitmq_data` volume when the `mq` profile is used.
- Backend connection strings are supplied through environment variables.
- Production passwords are not committed. Use `.env` locally or secret management in deployed environments.
- The frontend uses relative API paths in Docker so Nginx can reverse proxy requests to the backend.

## Health Checks

The API exposes ASP.NET Core health check endpoints:

- `GET /health`: simple status for load balancers and Docker healthcheck. It returns `503` when the platform is unhealthy.
- `GET /health/detail`: detailed JSON for the system monitoring page. It includes component status, duration, description, tags, and health data.

Registered checks:

- `api-self`: API process liveness.
- `sql-server`: EF Core SQL Server connectivity.
- `redis`: Redis connectivity. This check is registered only when `Cache:Provider` is `Redis` and `Cache:EnableRedis` is `true`.
- `rabbitmq`: reports `RabbitMQ is disabled` when `RabbitMQ:Enabled` is `false`; checks RabbitMQ connection and channel availability only when RabbitMQ is enabled.
- `hangfire`: Hangfire storage configuration availability.
- `disk-storage`: local file storage write probe when `FileStorage:Provider` is `Local`.

Docker Compose marks the API container healthy by calling `http://localhost:8080/health`. The frontend system monitoring page is seeded under System Management / System Health and requires `system:health:view`.

## Platform Operations

The platform now includes these operations capabilities:

- Audit and login logs with tenant, user, request, response, IP, user agent, elapsed time, and TraceId fields.
- Tenant context resolution from `X-Tenant-Id`, claims, and default configuration, with EF Core tenant filters and automatic tenant assignment.
- Data permission primitives for user, department, department tree, custom departments, and all-data scopes.
- Dictionary management and system configuration management through `ICacheService`.
- File upload and attachment management with local storage by default and MinIO options reserved.
- Excel import/export infrastructure for list export, template download, and import error rows.
- Idempotency and duplicate-submit protection through `X-Idempotency-Key`. Memory mode is single-instance only; Redis mode provides cross-instance protection.
- Redis distributed locks for seed data and background job examples when Redis is enabled. Memory mode uses a local single-process lock for development.
- API rate limiting with global, login, and refresh-token policies.
- Outbox/Inbox reliable messaging with optional RabbitMQ publishing and idempotent consumption records. When RabbitMQ is disabled, Outbox records can still be queried but asynchronous messages are not sent.
- Hangfire job management with dashboard authorization, job query APIs, trigger support, and execution logs.
- Notification center with station messages, templates, Outbox-published notification events, optional RabbitMQ consumption, and SignalR real-time delivery.
- Online user/session management with session tracking, last-active throttling, revoked-session checks, and force logout. Memory mode is suitable for single-instance development only.

Seeded operation menus include health, outbox, inbox, Hangfire jobs, notifications, notification administration, and online users. Seeded permissions include the corresponding `system:*` view/action codes.

## Infrastructure Features

Application services should depend on these abstractions instead of concrete infrastructure SDKs:

- `ICacheService`: unified cache abstraction. MemoryCache is the default provider; RedisCache is opt-in through configuration.
- `IMessageBus`: unified message bus abstraction. `NullMessageBus` is the default; RabbitMQ publishing and subscriptions are opt-in through configuration.
- `IBackgroundJobService`: Hangfire one-off, delayed, and recurring jobs.
- `IFileStorageService`: file storage abstraction. Local storage is enabled by default; MinIO options are reserved for later enablement.
- `ITraceContextAccessor`: current request or background job TraceId context.

Local defaults are configured in `backend/PermissionSystem.Api/appsettings.Development.json` and `backend/PermissionSystem.Worker/appsettings.Development.json`. Docker values are supplied from `docker-compose.yml` and `.env`.

## Cache Provider

The default cache provider is `MemoryCache`. Local development and simple single-instance runs do not require Redis unless Redis is explicitly enabled.

Default configuration:

```json
"Cache": {
  "Provider": "Memory",
  "EnableRedis": false,
  "DefaultExpirationMinutes": 30,
  "KeyPrefix": "PermissionSystem:"
}
```

Enable Redis cache by setting both `Provider` and `EnableRedis`:

```json
"Cache": {
  "Provider": "Redis",
  "EnableRedis": true,
  "DefaultExpirationMinutes": 30,
  "KeyPrefix": "PermissionSystem:"
}
```

Docker Compose enables Redis cache through environment variables:

```text
Cache__Provider=Redis
Cache__EnableRedis=true
Cache__DefaultExpirationMinutes=30
Cache__KeyPrefix=PermissionSystem:
```

MemoryCache mode is intended for local development and single-instance deployments. These capabilities are single-instance only in Memory mode and require Redis in multi-instance production:

- duplicate-submit protection
- idempotency request state
- revoked-session and online-user cache state
- local lock fallback used during startup seed data

The Redis distributed lock implementation remains available when Redis cache is enabled. Production multi-instance deployments should enable Redis and keep the Redis service healthy.

## RabbitMQ Message Bus

RabbitMQ is disabled by default. Local development and default Docker startup do not require a RabbitMQ server.

Default configuration:

```json
"RabbitMQ": {
  "Enabled": false,
  "HostName": "localhost",
  "Port": 5672,
  "UserName": "guest",
  "Password": "",
  "VirtualHost": "/",
  "ExchangeName": "permission-system.exchange",
  "RetryCount": 3,
  "RetryIntervalSeconds": 5,
  "ConnectionTimeoutSeconds": 10,
  "EnablePublisherConfirms": true,
  "EnableConsumers": false,
  "EnableOutboxPublisher": false
}
```

Enable RabbitMQ by setting `Enabled` to `true`. Consumers and the Outbox publisher are controlled independently:

```json
"RabbitMQ": {
  "Enabled": true,
  "HostName": "rabbitmq",
  "Port": 5672,
  "UserName": "guest",
  "Password": "<RABBITMQ_DEFAULT_PASS>",
  "VirtualHost": "/",
  "ExchangeName": "permission-system.exchange",
  "EnableConsumers": true,
  "EnableOutboxPublisher": true
}
```

Behavior by mode:

- `RabbitMQ:Enabled = false`: `NullMessageBus` is registered, publishes are skipped without throwing, consumers are not started, RabbitMQ connection services are not registered, and RabbitMQ health reports disabled.
- `RabbitMQ:Enabled = true`: `RabbitMqMessageBus` is registered and publishing connects to RabbitMQ on demand. Consumers start only when `EnableConsumers` is `true`.
- `OutboxPublisherJob` recurring registration happens only when both `RabbitMQ:Enabled` and `RabbitMQ:EnableOutboxPublisher` are `true`. If either flag is `false`, the recurring job is removed and manual execution is rejected.

RabbitMQ disabled mode is suitable for local development and single-instance flows that do not need asynchronous messaging. Outbox and Inbox tables remain available, but asynchronous messages are not actually sent and RabbitMQ-based notification consumption does not run. Production deployments that rely on cross-process asynchronous events should enable RabbitMQ and keep consumers and the Outbox publisher enabled.

Docker Compose keeps the RabbitMQ service behind the `mq` profile. Default startup does not need RabbitMQ:

```powershell
docker compose up -d
```

To enable RabbitMQ in Docker, either set these values in `.env` or export them before startup:

```powershell
$env:RABBITMQ_ENABLED = "true"
$env:RABBITMQ_ENABLE_CONSUMERS = "true"
$env:RABBITMQ_ENABLE_OUTBOX_PUBLISHER = "true"
docker compose --profile mq up -d
```

## OpenTelemetry and TraceId

Every HTTP request uses `X-Trace-Id` as the application trace correlation id. If the request does not provide one, the API creates a new TraceId, stores it in `ITraceContextAccessor`, writes it to Serilog log context, and returns it in the `X-Trace-Id` response header.

Trace coverage:

- ASP.NET Core requests
- Outgoing `HttpClient` calls
- EF Core database commands
- StackExchange.Redis commands when Redis cache is enabled
- RabbitMQ publish headers (`X-Trace-Id` and `traceparent`) when RabbitMQ is enabled
- Hangfire demo/outbox jobs via execution logs and structured logs
- Operation logs and login logs

OpenTelemetry is configured under `OpenTelemetry` in `backend/PermissionSystem.Api/appsettings*.json`.

```json
{
  "OpenTelemetry": {
    "Enabled": true,
    "ServiceName": "PermissionSystem.Api",
    "ServiceVersion": "1.0.0",
    "ConsoleExporterEnabled": false,
    "OtlpEndpoint": null,
    "SamplingRatio": 1.0,
    "IncludeSqlStatements": false,
    "IncludeRedisStatements": false
  }
}
```

To view traces without an external APM platform, set `ConsoleExporterEnabled` to `true` and run the API from a terminal. Trace spans are printed to console output. To send traces to an OpenTelemetry Collector later, set `OtlpEndpoint` to the collector endpoint, for example `http://localhost:4318/v1/traces`.

## File Upload Configuration

File upload is configured under `FileStorage` in `backend/PermissionSystem.Api/appsettings*.json`.

Default local settings:

```json
{
  "FileStorage": {
    "Provider": "Local",
    "MaxFileSizeBytes": 20971520,
    "AllowedExtensions": [".jpg", ".jpeg", ".png", ".gif", ".webp", ".pdf", ".txt", ".csv", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".zip", ".rar"],
    "Local": {
      "RootPath": "uploads",
      "BucketName": "default",
      "PublicBaseUrl": null
    },
    "Minio": {
      "Endpoint": "",
      "AccessKey": "",
      "SecretKey": "",
      "BucketName": "permission-system",
      "UseSsl": false,
      "PublicBaseUrl": null
    }
  }
}
```

Security defaults:

- Files larger than `MaxFileSizeBytes` are rejected.
- Only extensions in `AllowedExtensions` are accepted.
- Executable extensions such as `.exe`, `.dll`, `.bat`, `.cmd`, `.msi`, `.ps1`, `.sh`, `.js`, `.vbs`, `.jar`, and `.apk` are blocked.
- Dangerous content types such as Windows executables, shell scripts, JavaScript, Java archives, and APK packages are blocked. Configure `AllowedContentTypes` when a stricter allow-list is needed.
- Uploaded file names are normalized and rejected when they contain path traversal characters.
- Each file is stored with its MD5 hash in `FileResources`.

## Scheduled Task Demo

The system includes a frontend-configurable Hangfire recurring task demo.

- Menu: System Management / Scheduled Tasks
- Seed task code: `demo-minute-log`
- Job type: `DemoLog`
- Default Cron: `* * * * *`
- Result: each execution writes a row to `ScheduledTaskExecutionLogs` and updates the task's latest execution status.

To test it locally, run the API and the worker at the same time. The API runs migrations, seeds the demo task, and syncs enabled tasks into Hangfire. The worker executes the Hangfire jobs.

## OAuth2 Client Configuration

Seed data creates the default OpenIddict client for local administration:

- `client_id`: `permission-admin`
- `client_secret`: value from `SeedData:OAuthClientSecret`
- Grant types: `password`, `refresh_token`, `client_credentials`, and reserved `authorization_code` with PKCE.
- Scopes: `openid`, `profile`, `roles`, `offline_access`, and `permission-system-api`.

The password grant access token includes `user_id`, `user_name`, `tenant_id`, `role`, `permission_code`, session id, access token id, and refresh token id claims. The local `admin` account password and OAuth client secret are configured through environment-specific settings and should never be committed.

## Hangfire Notes

Hangfire storage uses SQL Server. The API registers the dashboard at `/hangfire`, and the worker process hosts the Hangfire server. Dashboard access is protected by `HangfireDashboardAuthorizationFilter`; only `SuperAdmin` or users with `system:job:view` can open it.

Outbox publisher registration is controlled by RabbitMQ flags. It runs only when `RabbitMQ:Enabled = true` and `RabbitMQ:EnableOutboxPublisher = true`; otherwise the recurring job is removed and manual trigger returns a validation error.

## Common Issues

- `dotnet restore` fails with `NU1301`: verify NuGet network/TLS access, then rerun restore.
- `dotnet build` fails because DLL files are locked: stop the running `PermissionSystem.Api` process and rebuild.
- Login succeeds but menus are empty: confirm seed data completed, the user has roles, and role-menu/role-permission relations exist.
- Redis is unexpectedly required: verify `Cache:Provider = Memory` and `Cache:EnableRedis = false`.
- RabbitMQ is unexpectedly required: verify `RabbitMQ:Enabled = false`, or start Compose with `--profile mq` and RabbitMQ flags enabled.
- Docker checks cannot run: install Docker Desktop or make sure `docker` is available on `PATH`.

## Acceptance Checklist

Use `docs/final-acceptance-checklist.md` for the full manual验收流程. At minimum before release:

- `dotnet restore` and `dotnet build` pass.
- `npm install` and `npm run build` pass.
- `docker compose config` passes in an environment with Docker CLI.
- Default seed data creates tenant `default`, user `admin`, role `SuperAdmin`, system menus, permission codes, and OpenIddict client `permission-admin`.
- `/connect/token`, `/connect/revoke`, `/health`, `/health/detail`, core RBAC APIs, and frontend system pages are verified.
- Production secrets, SQL Server password, OAuth client secret, encryption key, CORS origins, Redis/RabbitMQ/Hangfire topology, logs, backups, and monitoring are reviewed.
