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

For local development, keep public defaults in `backend/PermissionSystem.Api/appsettings.Development.json` and put machine-specific secrets in `backend/PermissionSystem.Api/appsettings.Development.local.json`. The `.local.json` file is loaded only in `Development` and is ignored by Git. At minimum, configure `ConnectionStrings:DefaultConnection`, `SeedData:AdminPassword`, `SeedData:OAuthClientSecret`, and `Security:SystemConfigEncryptionKey`. On startup in `Development`, the API runs EF Core migrations and seed data.

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
- Number rule engine for tenant-scoped business number generation, date-based sequence reset, preview, manual test generation, and sequence reset.
- Security policy center for password complexity, login failure lockout, IP whitelist/blacklist, and sensitive operation verification.
- Open integration center for API Key clients, signed Webhook subscriptions, delivery retry, and external API call logs.

Seeded operation menus include health, outbox, inbox, Hangfire jobs, notifications, notification administration, online users, number rules, state machines, reports, security center, and open integration center. Seeded permissions include the corresponding `system:*`, `report:*`, `security:*`, and `integration:*` view/action codes.

## Number Rule Engine

The platform includes a generic number rule engine for future business documents such as purchase orders, sales orders, inbound orders, outbound orders, approval documents, and other platform records. It is a reusable platform capability and does not include WMS / ERP business logic.

Current backend model:

- `NumberRule`: rule definition, including `RuleCode`, `RuleName`, `BusinessType`, `Prefix`, `DateFormat`, `SequenceLength`, `ResetCycle`, `Separator`, `IsEnabled`, and `Remark`.
- `NumberRuleSegment`: reserved segment metadata for fixed text, date, sequence, tenant code, department code, and custom variables.
- `NumberSequence`: current sequence state by tenant, rule code, and sequence key.

Supported reset cycles:

- `None`: never reset by date.
- `Daily`: reset by day.
- `Monthly`: reset by month.
- `Yearly`: reset by year.

Example rules:

- `PurchaseOrder`: `PO{yyyyMMdd}{0001}`
- `InboundOrder`: `IN{yyyyMMdd}{0001}`
- `DemoApprovalOrder`: `DAO{yyyyMMdd}{0001}`

The generator uses `IDistributedLock` around sequence updates. Redis mode provides a cross-instance distributed lock; memory mode provides a single-process development lock. The database also has a unique index on `TenantId + RuleCode + SequenceKey` to protect sequence rows.

Management UI:

- `frontend/permission-admin/src/views/system/number-rule/index.vue`

Main APIs:

- `GET /api/system/number-rules`
- `POST /api/system/number-rules`
- `PUT /api/system/number-rules/{id}`
- `POST /api/system/number-rules/preview`
- `POST /api/system/number-rules/{ruleCode}/generate`
- `POST /api/system/number-rules/{ruleCode}/reset-sequence`

## State Machine Engine

The platform includes a generic state machine engine for business document status transitions. It is a reusable platform capability for future documents such as purchase orders, sales orders, inbound orders, outbound orders, approval documents, and other records. It does not include WMS / ERP business logic.

Current backend model:

- `StateMachineDefinition`: state machine definition by `BusinessType`.
- `StateDefinition`: states such as draft, pending, approved, rejected, withdrawn, and cancelled.
- `StateTransition`: allowed action from one state to another, including `RequiredPermission` and reserved `ConditionJson`.
- `StateTransitionLog`: transition audit log with business id, before/after state, action, operator, comment, and time.

Runtime integration:

- `IStateTransitionExecutor` validates and executes transitions.
- `IStateTransitionHandler` lets a business module provide current-state lookup, pre-transition validation, and post-transition state update without making the state machine depend on concrete business code.
- Transitions are executed in a unit-of-work transaction and write `StateTransitionLog`.
- `RequiredPermission` is checked against the current user's permission codes before executing a transition.

Workflow integration:

- Workflow remains responsible for approval tasks and approval records.
- State machine remains responsible for business document status transitions.
- The two are connected through business handlers. For example, workflow callbacks call the state transition executor to move the business document from `Pending` to `Approved`, `Rejected`, or `Withdrawn`.

Seeded demo state machine:

- `BusinessType`: `DemoApprovalOrder`
- States: `Draft`, `Pending`, `Approved`, `Rejected`, `Withdrawn`, `Cancelled`
- Actions: `Submit`, `Approve`, `Reject`, `Withdraw`, `Cancel`

Management UI:

- `frontend/permission-admin/src/views/system/state-machine/index.vue`
- `frontend/permission-admin/src/views/system/state-machine/designer.vue`

Main APIs:

- `GET /api/system/state-machines`
- `POST /api/system/state-machines`
- `PUT /api/system/state-machines/{id}`
- `GET /api/system/state-machines/{id}/states`
- `GET /api/system/state-machines/{id}/transitions`
- `POST /api/system/state-machines/transition`
- `GET /api/system/state-machines/logs`

## Print Template Engine

The platform includes a generic print template foundation for future documents, labels, approval sheets, contracts, and other business print scenarios. It is a reusable platform capability and does not include WMS / ERP business logic.

Current backend model:

- `PrintTemplate`: template definition, including `TemplateCode`, `TemplateName`, `BusinessType`, `TemplateType`, `ContentHtml`, `ContentJson`, `PaperSize`, `Orientation`, `IsDefault`, `IsEnabled`, `Version`, and `Remark`.
- `PrintRecord`: render/print audit record, including template id, business type, business id, print user, print time, and print count.

Template variables:

- Simple variables use double braces, for example `{{OrderNo}}`, `{{CreatedAt}}`, `{{ApplicantName}}`, and `{{Amount}}`.
- Detail rows use a reserved loop block, for example `{{#items}} {{Name}} {{Qty}} {{Price}} {{/items}}`.
- Variable values are HTML-encoded by the lightweight renderer. The current implementation does not add Handlebars.Net or a full expression engine.

Example template:

```html
<h1>{{OrderNo}}</h1>
<p>Applicant: {{ApplicantName}}</p>
<p>Created at: {{CreatedAt}}</p>
<table>
  {{#items}}
  <tr><td>{{Name}}</td><td>{{Qty}}</td><td>{{Price}}</td></tr>
  {{/items}}
</table>
```

Business document integration:

1. Create an enabled template and set `BusinessType` to the document integration key.
2. Query available templates with `GET /api/system/print-templates/by-business-type/{businessType}`.
3. Call preview or render with `{ businessId, data }`; `data` supplies template variables.
4. `POST /api/system/print-templates/{id}/render` writes a `PrintRecord` for audit and history.

Management UI:

- `frontend/permission-admin/src/views/system/print-template/index.vue`
- `frontend/permission-admin/src/views/system/print-template/designer.vue`

Main APIs:

- `GET /api/system/print-templates`
- `GET /api/system/print-templates/{id}`
- `POST /api/system/print-templates`
- `PUT /api/system/print-templates/{id}`
- `DELETE /api/system/print-templates/{id}`
- `GET /api/system/print-templates/by-business-type/{businessType}`
- `POST /api/system/print-templates/{id}/set-default`
- `POST /api/system/print-templates/{id}/preview`
- `POST /api/system/print-templates/{id}/render`
- `GET /api/system/print-records`

## Report Center

The platform includes a generic report center foundation for future WMS / ERP analytics. It provides report definition management, controlled SQL querying, Excel export, execution logs, and three system sample reports. It does not include business-specific reports.

Current backend model:

- `ReportDefinition`: report metadata, including `ReportCode`, `ReportName`, `Category`, `DataSourceType`, `SqlText`, `ApiUrl`, `ColumnsJson`, `ParamsJson`, `IsEnabled`, and `Remark`.
- `ReportQueryParam`: report query parameter definitions.
- `ReportExecutionLog`: query/export execution log with user, parameters, elapsed time, and row count.

Supported data sources:

- `Sql`: implemented. SQL is executed through the platform SQL report executor.
- `Api`: configuration field is reserved for later service data sources; execution is not enabled in the current baseline.

SQL safety rules:

- Only a single `SELECT` statement is allowed.
- `INSERT`, `UPDATE`, `DELETE`, `DROP`, `ALTER`, `EXEC`, `MERGE`, `TRUNCATE`, `CREATE`, and similar dangerous keywords are rejected.
- Semicolons and SQL comments are rejected.
- Query parameters are passed as database parameters, not string-concatenated.
- Tenant filtering is wrapped around SQL results when the current tenant is resolved, so report SQL should include `TenantId` in the inner SELECT.
- `Reports:SqlReportsEnabled`, `Reports:QueryTimeoutSeconds`, and `Reports:MaxRows` control whether SQL reports are allowed, timeout, and maximum rows.

Export:

- `POST /api/reports/{id}/export` returns an `.xlsx` file.
- The frontend report viewer downloads the returned Blob as Excel.
- Export uses the same query path as preview and writes an execution log.

Seeded sample reports:

- `SystemUserList`: user list report.
- `SystemLoginLogs`: login log report.
- `SystemOperationLogs`: operation log report.

Management UI:

- `frontend/permission-admin/src/views/report/definition/index.vue`
- `frontend/permission-admin/src/views/report/viewer/index.vue`

Main APIs:

- `GET /api/reports`
- `GET /api/reports/{id}`
- `POST /api/reports`
- `PUT /api/reports/{id}`
- `DELETE /api/reports/{id}`
- `POST /api/reports/{id}/query`
- `POST /api/reports/{id}/export`
- `GET /api/reports/execution-logs`

## Security Policy Center

The platform includes a tenant-scoped security policy center for password complexity, login failure lockout, sensitive operation verification, and IP allow/block rules. It is a generic platform capability and keeps OpenIddict as the token authority.

Current backend model:

- `SecurityPolicy`: password rules, login failure threshold, lock duration, MFA flag, sensitive operation verification flag, and IP whitelist/blacklist switches.
- `LoginFailureRecord`: failed login counter by tenant, username, and IP, including lock expiration.
- `SensitiveOperationVerification`: short-lived verification codes for sensitive operations.
- `IpAccessRule`: enabled whitelist or blacklist rule with simple exact or prefix-wildcard IP matching.

Current integrations:

- User creation, password reset, and current-user password change validate the configured password policy.
- Password login checks IP access and lockout before credential validation.
- Failed password login records a failure count; successful password login clears the matching failure record.
- Request pipeline checks IP whitelist/blacklist through `IpAccessMiddleware`.
- Deleting users, resetting passwords, assigning SuperAdmin, modifying SuperAdmin permissions/users, and changing the security policy support `X-Sensitive-Verification-Code`.

Sensitive verification:

- `POST /api/security/verification/send` creates a 6-digit code valid for five minutes.
- The current baseline logs the code and returns it to the management UI for local/admin workflows.
- Production deployments should route the code through the notification center or an MFA provider and avoid exposing it in responses.

Management UI:

- `frontend/permission-admin/src/views/security/policy/index.vue`
- `frontend/permission-admin/src/views/security/ip-rule/index.vue`
- `frontend/permission-admin/src/views/security/login-failure/index.vue`
- `frontend/permission-admin/src/components/SensitiveVerificationDialog/index.vue`

Main APIs:

- `GET /api/security/policy`
- `PUT /api/security/policy`
- `POST /api/security/verification/send`
- `POST /api/security/verification/verify`
- `GET /api/security/ip-rules`
- `POST /api/security/ip-rules`
- `PUT /api/security/ip-rules/{id}`
- `DELETE /api/security/ip-rules/{id}`
- `GET /api/security/login-failures`

## Open Integration Center

The platform includes a baseline open integration center for external systems that need API Key access and signed Webhook callbacks. It is a generic integration foundation and does not expose WMS / ERP business APIs.

Current backend model:

- `ApiClient`: external client metadata, scopes, IP allow list, enabled flag, and per-minute rate limit.
- `ApiClientSecret`: hashed API secret with optional expiration and last-used timestamp. The raw secret is shown only once when generated.
- `WebhookSubscription`: event subscription, target URL, encrypted signing secret, enabled flag, and retry count.
- `WebhookDeliveryLog`: delivery payload, status, response code/body, and retry attempt.
- `ExternalApiCallLog`: API Key call audit log with client, path, method, IP, status code, and elapsed time.

API Key usage:

```text
X-Api-Key: <ClientCode>
X-Api-Secret: <generated secret shown once>
```

When these headers are present, `ApiKeyAuthenticationMiddleware` validates the client, checks enabled status, checks the client IP allow list, applies a per-minute in-memory rate limit, sets the API client context, and writes an external API call log. Normal OAuth/OpenIddict and RBAC management flows remain unchanged when these headers are absent.

Webhook delivery:

- Supported seed/example event types: `user.created`, `workflow.approved`, `workflow.rejected`, `notification.created`.
- Test delivery enqueues a Hangfire job.
- Requests include `X-Webhook-Event`, `X-Webhook-Timestamp`, and `X-Webhook-Signature`.
- Signature format is `sha256=<hex hmac>`, calculated over `{timestamp}.{payload}` with the subscription secret.
- Failed deliveries are logged and retried by Hangfire up to the subscription `RetryCount`.

Security notes:

- API Secret is never stored in plain text.
- Webhook Secret is stored through the existing configuration value protector and shown as `******` in API responses.
- Logs store request metadata and webhook response bodies, but never API Secret or Webhook Secret.
- Webhook target URLs must be HTTPS, with local HTTP allowed for development testing.

Management UI:

- `frontend/permission-admin/src/views/integration/client/index.vue`
- `frontend/permission-admin/src/views/integration/webhook/index.vue`
- `frontend/permission-admin/src/views/integration/log/index.vue`

Main APIs:

- `GET /api/integration/clients`
- `POST /api/integration/clients`
- `PUT /api/integration/clients/{id}`
- `DELETE /api/integration/clients/{id}`
- `POST /api/integration/clients/{id}/generate-secret`
- `POST /api/integration/clients/{id}/enable`
- `POST /api/integration/clients/{id}/disable`
- `GET /api/integration/webhooks`
- `POST /api/integration/webhooks`
- `PUT /api/integration/webhooks/{id}`
- `DELETE /api/integration/webhooks/{id}`
- `POST /api/integration/webhooks/{id}/test`
- `GET /api/integration/webhook-logs`
- `GET /api/integration/api-call-logs`

## Workflow / Approval Module

The platform includes a baseline enterprise workflow module for approval scenarios.

Current capabilities:

- Workflow definition management: create, edit, delete unpublished definitions, design, publish, disable, and copy versions.
- Visual workflow designer: Start, Approver, Cc, Condition, and End nodes; condition branches with a default branch; save and reopen designer data.
- Runtime engine: start workflow by `BusinessType`, evaluate conditions from `FormDataJson`, create tasks, approve, reject, withdraw, transfer, add-sign, cc, complete, and record timeline events.
- My approval pages: todo tasks, done tasks, my-started instances, cc-to-me list, and instance detail.
- RBAC integration: workflow menus and buttons use `workflow:*` permission codes; SuperAdmin receives seeded permissions.
- Notification integration: task, cc, rejected, and completed events enqueue approval notifications through the existing notification/outbox pipeline. Notification failure is logged and does not block the main workflow transaction.

Important notes:

- Workflow definitions are bound to business modules through `wf_business_binding.BusinessType`.
- Published definitions are immutable in structure; copy a new version before changing the designer.
- The module does not include WMS / ERP business code. Business modules should own their own document lifecycle and call the workflow start API when needed.
- Business document integration is available through `WorkflowBusinessBinding`, `IApprovalBusinessEntity`, and `IWorkflowBusinessHandler`.
- `BusinessType` is the stable integration key. Bind it to one enabled published workflow definition, then submit business documents with business id, title, and form data.
- `DemoApprovalOrder` is included as a lightweight sample document. It can be used to verify draft creation, submit approval, withdraw, approve/reject callbacks, and condition branches such as `amount > 10000`.
- Department manager, position, direct leader, timeout handling,催办, and richer business callback retry/audit policies are reserved extension points.

Business document approval flow:

1. Create and publish a workflow definition.
2. Add a business binding for a `BusinessType`, for example `DemoApprovalOrder`.
3. Create a business document in its own module.
4. Submit the document. The backend finds the enabled binding, starts a workflow instance, and stores `WorkflowInstanceId`.
5. Workflow callbacks update the document approval status: `Pending`, `Approved`, `Rejected`, or `Withdrawn`.
6. Real purchase order, sales order, inbound, outbound, and reimbursement modules should follow the same pattern without adding their business logic to the workflow module.

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

## SSO / OIDC Login

The platform can act as a business system connected to an external SSO identity provider. Local username/password login remains available and is not disabled by SSO configuration.

Implemented SSO capabilities:

- OIDC Provider management under Security Center / SSO.
- OIDC Authorization Code login with state, nonce, and PKCE.
- External identity binding to local users through `SsoUserBinding`.
- Optional local user auto-bind by email, phone, or username.
- Optional local user auto-create.
- External role and department mapping to local Role and Department.
- One-time `login_code` exchange through OpenIddict, so access and refresh tokens are not exposed in URLs.
- SSO login audit through `SsoLoginLog`.

Supported provider types:

- OIDC: implemented.
- SAML2: reserved in model and UI, login flow not implemented yet.
- OAuth2: reserved in model and UI, login flow not implemented yet.

OIDC callback URLs:

```text
Backend callback:  {BackendBaseUrl}/api/sso/oidc/{providerCode}/callback
Frontend callback: {FrontendBaseUrl}/sso/callback
```

Configure the backend callback URL in the external IdP. The frontend callback is used only after the backend has validated the external identity and generated a short-lived `login_code`.

Typical Provider configuration:

- Keycloak `Authority`: `https://idp.example.com/realms/{realm}`.
- Microsoft Entra ID `Authority`: `https://login.microsoftonline.com/{tenantId}/v2.0`.
- Authing `Authority` or `MetadataAddress`: use the OIDC issuer/discovery URL from the Authing application settings.
- `Scopes`: `openid profile email` or `openid profile email phone`.
- `ResponseType`: `code`.
- `UsePkce`: `true`.
- `UserIdClaim`: `sub`.
- `UserNameClaim`: `preferred_username`, `username`, or the actual username claim returned by the IdP.
- `EmailClaim`: `email`.
- `RoleClaim`: `roles` or a custom mapped claim.
- `DepartmentClaim`: `department` or a custom mapped claim.

Security notes:

- `ClientSecret` is stored encrypted and returned to the frontend only as a masked value.
- Disabling a Provider removes it from the login page and prevents new challenges.
- Providers with bound users cannot be deleted.
- Automatic role assignment and role mapping cannot assign `SuperAdmin`.
- `login_code` is short-lived and can be consumed only once.
- Operation logs redact `client_secret`, `access_token`, `refresh_token`, `id_token`, `login_code`, and `code_verifier`.

## Current User Account Features

The authenticated current-user surface is exposed through `MeController` and application-layer `IMeService`:

- `GET /api/me/profile`: returns the current user's profile, tenant, department, roles, permissions, last login time, and creation time.
- `PUT /api/me/profile`: updates only the current user's basic profile fields: nickname/display name, avatar URL, email, and phone number.
- `PUT /api/me/password`: validates the old password, enforces the local password policy, hashes the new password, revokes the user's refresh tokens, and revokes active user sessions.
- `POST /api/me/logout`: revokes the submitted refresh token when possible, revokes the current tracked user session, and writes a logout login log.
- `POST /api/me/logout-all`: revokes all refresh tokens and tracked sessions for the current user.

Profile and password requests only require authentication. They do not require `system:user:*` RBAC permissions because they operate only on the current principal.

Frontend account features are available from the top-right user dropdown:

- Personal Center opens `/account/profile` as a hidden route that can still appear in TabsView.
- Change Password opens a dialog and performs client-side checks for required fields, minimum length, letters plus numbers, and confirmation match.
- Logout asks for confirmation, calls `POST /api/me/logout`, then clears local state even if the server call fails.

After password change, the frontend clears `access_token`, `refresh_token`, current user/profile state, dynamic menus, permission codes, notification connection state, dynamic routes, and TabsView state, then redirects to `/login`. Logout uses the same local cleanup path. Browser back navigation will hit the route guard without a token and be redirected to login instead of showing protected content.

## Role Permission Matrix

Role permissions can be assigned from the role management page through the `分配权限` action.

- `GET /api/roles/{roleId}/permission-matrix` loads the matrix grouped by first-level menu module.
- `PUT /api/roles/{roleId}/permission-matrix` saves selected menu ids and button/API permission ids.
- `GET /api/roles/{roleId}/users` loads pageable users for the role and marks associated users with `checked`.
- `PUT /api/roles/{roleId}/users` replaces the role-user relations with the submitted `userIds`.
- Menu selections are written to `RoleMenus`.
- Button/API permission selections are written to `RolePermissions`.
- Role-user selections are written to `UserRoles`.
- The save flow auto-completes parent menus and the matching `:view` permission when an action permission can be mapped by `Permission.Resource`.

The role operation column keeps `编辑`, `分配权限`, `关联用户`, `数据范围`, and `删除`. The old separate `菜单` and `权限` entries are intentionally removed from the role page because `分配权限` is now the unified entry for menu permissions, button/API permissions, and data scope configuration. Existing backend menu and permission assignment endpoints remain available for compatibility.

The frontend permission dialog displays modules, menu rows, permission checkboxes, data scope, and field authorization entry points. Data scope is currently role-level and is saved to `RoleDataScopes`; row-level data scope is not implemented yet. Field authorization is reserved in the UI and request shape, but no field authorization table is persisted yet.

After a matrix save, the backend removes the role matrix cache key and the affected users' menu/permission cache keys. After role-user relations are saved, the backend removes menu, permission, and user-role cache keys for both old and new related users. Button/API permissions are still embedded in access token claims for normal users, so affected users should log in again to receive the latest permission claims. Non-SuperAdmin users cannot modify the `SuperAdmin` role's associated users, and disabled or cross-tenant users cannot be assigned.

## Built-in Account And Role Protection

The platform treats the seeded `admin` user and `SuperAdmin` role as built-in resources. Seed data marks both records with `IsBuiltin = true` and re-applies the flag on every startup, so older databases are repaired automatically after migrations run.

Protection rules:

- `admin` and other built-in users cannot be deleted or disabled.
- The current user cannot delete or disable itself.
- Non-SuperAdmin users cannot delete, disable, reset password for, or reassign roles on SuperAdmin users.
- `admin` must always keep the `SuperAdmin` role.
- The system must always keep at least one SuperAdmin user.
- Built-in roles and the `SuperAdmin` role cannot be deleted or disabled.
- `SuperAdmin` role menu, permission matrix, and role data scope are protected from ordinary administrators.
- Non-SuperAdmin users cannot assign the `SuperAdmin` role to themselves or others.

Frontend user and role management pages hide dangerous buttons for built-in and SuperAdmin records, and show `系统内置` / `超级管理员` tags. These UI checks are only usability helpers; the backend service layer enforces the actual protection and returns business errors for blocked operations.

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
