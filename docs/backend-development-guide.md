# 后端开发指南

## 开发环境

后端位于 `backend/`，解决方案文件为 `backend/PermissionSystem.sln`，目标框架为 `net10.0`。开发前请准备：

- .NET 10 SDK
- SQL Server
- 可选 Redis
- 可选 RabbitMQ
- 可选 VS Code REST Client，用于运行 `docs/api-tests.http`

常用命令：

```powershell
cd backend
dotnet restore
dotnet build .\PermissionSystem.sln
dotnet test .\PermissionSystem.sln
```

本地启动 API：

```powershell
cd backend
dotnet run --project .\PermissionSystem.Api\PermissionSystem.Api.csproj --launch-profile http
```

默认地址：

- HTTP：`http://localhost:5264`
- HTTPS：`https://localhost:7281`
- Swagger：`http://localhost:5264/swagger`
- Health：`http://localhost:5264/health`
- Health detail：`http://localhost:5264/health/detail`
- Hangfire：`http://localhost:5264/hangfire`

## 配置方式

公共默认配置位于：

- `backend/PermissionSystem.Api/appsettings.json`
- `backend/PermissionSystem.Api/appsettings.Development.json`
- `backend/PermissionSystem.Api/appsettings.Docker.json`
- `backend/PermissionSystem.Worker/appsettings.json`
- `backend/PermissionSystem.Worker/appsettings.Development.json`

本地机密配置建议放在 `backend/PermissionSystem.Api/appsettings.Development.local.json`。该文件只在 Development 加载，不应提交。

最少需要配置：

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=PermissionSystemDb;User Id=sa;Password=your_password;TrustServerCertificate=True"
  },
  "SeedData": {
    "AdminPassword": "your_admin_password",
    "OAuthClientSecret": "your_oauth_client_secret"
  },
  "Security": {
    "SystemConfigEncryptionKey": "32_bytes_or_valid_project_key"
  }
}
```

不要把真实连接串、密码、Token、证书或客户端密钥写入已提交文件。

## 分层开发规则

Controller 放在 `PermissionSystem.Api/Controllers`，只做请求参数接收、授权声明、调用应用服务和返回结果。

应用服务放在 `PermissionSystem.Application/<Module>`，包含 Request、Response、Service 和接口。业务流程、权限内的用例编排、事务边界应优先放在这里。

实体放在 `PermissionSystem.Domain/Entities`，枚举放在 `PermissionSystem.Domain/Enums`。新增持久化实体需要继承 `BaseEntity`。

EF 配置放在 `PermissionSystem.Infrastructure/Configurations`，DbSet 和模型扫描在 `PermissionSystem.Infrastructure/Data/AppDbContext.cs`。

跨层返回类型使用 `PermissionSystem.Shared/Results/ApiResult.cs` 和 `PagedResult.cs`，不要直接暴露实体。

## 新增后端模块建议步骤

1. 在 Domain 新增实体和必要枚举。
2. 在 Infrastructure 新增 EF Configuration，并在 `AppDbContext` 中确认 DbSet。
3. 在 Application 新增模型、服务接口和服务实现。
4. 在 `PermissionSystem.Application/DependencyInjection.cs` 注册服务。
5. 在 Api 新增 Controller，并使用 `[Permission("module:resource:action")]`。
6. 生成 EF Core migration。
7. 增加或更新种子数据、权限码、菜单。
8. 补充单元测试或集成测试。

## EF Core 迁移

从 `backend` 目录生成迁移：

```powershell
dotnet ef migrations add AddYourChangeName `
  --project .\PermissionSystem.Infrastructure\PermissionSystem.Infrastructure.csproj `
  --startup-project .\PermissionSystem.Api\PermissionSystem.Api.csproj `
  --context AppDbContext `
  --output-dir Data\Migrations
```

如果缺少 `dotnet ef`：

```powershell
dotnet tool install --global dotnet-ef --version 10.*
```

迁移文件应包含：

- `yyyyMMddHHmmss_MigrationName.cs`
- `yyyyMMddHHmmss_MigrationName.Designer.cs`
- 更新后的 `AppDbContextModelSnapshot.cs`

本地 Development 和 Docker 环境启动 API 时会自动执行 `Database.MigrateAsync()` 和 SeedData。Production 环境当前不会自动执行迁移，生产发布前应使用受控流程执行迁移并备份数据库。

## 认证与授权

认证基于 OpenIddict，不要新增自定义 JWT 签发服务。当前支持：

- Password Flow
- Refresh Token
- Client Credentials
- Authorization Code + PKCE
- SSO OIDC 登录码自定义 grant：`sso_oidc`

接口授权使用：

```csharp
[Permission("system:user:view")]
```

权限码会进入 token claims，前端菜单和按钮权限也依赖这些权限码。Refresh Token 流程必须以已签名 principal 中的 `tenant_id`、`user_id` 和 `session_id` 为主体边界，在写入 Token 租户上下文后重新检查活动租户、该租户 IP 策略、启用用户和有效会话，并使用当前启用角色及权限关系重建动态 claims；不得让默认租户、请求 Header 或附带的 Bearer Access Token 参与 Refresh 的租户和会话判断，也不得直接复用旧 principal 中的用户授权声明。已经签发的 Access Token 即时失效属于 EA-010。

## 租户与数据

租户上下文由 `TenantMiddleware` 和 `TenantResolver` 处理，来源包括：

- `X-Tenant-Id` Header
- Token claims
- 默认租户配置 `Tenant:DefaultTenantId`

实体继承 `BaseEntity` 后受软删除和租户字段约束。租户上下文缺失时查询默认返回空结果、写入默认拒绝。普通业务服务不得绕过全局过滤；Seed、Outbox、跨租户后台任务等受控系统入口必须通过 `ISystemTenantScope` 显式声明用途，HTTP 请求不能开启系统作用域。认证前置查询只能使用强制指定 TenantId 且保留软删除条件的受限查询。

密码登录失败日志和锁定计数必须归属当前请求已经解析的租户。非默认租户使用现有 `X-Tenant-Id` 契约选择登录租户；Refresh Token 的主体租户以 Token 内已签名的 `tenant_id` 为准，刷新请求不依赖调用方重复发送租户 Header。

## 缓存、锁和幂等

开发默认使用 Memory 缓存，Docker 默认使用 Redis。相关配置：

- `Cache:Provider`
- `Cache:EnableRedis`
- `ConnectionStrings:Redis`

非 GET 请求前端会自动附加 `X-Idempotency-Key`。后端通过 `IdempotencyFilter` 和 `PreventDuplicateSubmitFilter` 做幂等和重复提交保护。Memory 模式只适合单实例开发；多实例或生产应使用 Redis。

## 后台任务与消息

Hangfire 存储使用 SQL Server。API 注册任务并提供 `/hangfire`，Worker 通过：

```powershell
cd backend
dotnet run --project .\PermissionSystem.Worker\PermissionSystem.Worker.csproj
```

RabbitMQ 默认关闭。关闭时使用 `NullMessageBus`，Outbox 记录可查询，但不会发布到队列。启用时需要同时配置 `RabbitMQ:Enabled`、`RabbitMQ:EnableConsumers` 和 `RabbitMQ:EnableOutboxPublisher`。

## 本地、Docker、生产差异

本地 Development：

- 使用 `appsettings.Development.local.json` 存放本机配置。
- API 自动迁移和种子初始化。
- Swagger 开启。
- 默认 Memory 缓存。

Docker：

- 使用 `.env` 注入连接串和密钥。
- API 自动迁移和种子初始化。
- Redis 默认启用。
- Worker 容器默认启动。

生产：

- 使用生产环境变量或密钥管理系统。
- 不依赖 Development local 配置。
- 当前代码不自动迁移数据库。
- 需要独立安排 API、Worker、SQL Server、Redis、日志和监控。

## 常见问题

### `Connection string 'DefaultConnection' is not configured`

检查本地 `appsettings.Development.local.json` 或 Docker `.env` 中的 `SQLSERVER_CONNECTION_STRING`。

### `dotnet ef` 找不到命令

安装 EF Core CLI：

```powershell
dotnet tool install --global dotnet-ef --version 10.*
```

### API 403 但用户已有菜单

菜单权限和接口权限都依赖权限码。检查角色是否拥有接口上的 `[Permission]` 代码，并让用户重新登录刷新 claims。

### 后台任务不执行

检查 Worker 是否启动，Hangfire Dashboard 是否有待处理任务，SQL Server 是否可用，队列名是否在 `Hangfire:Queues` 中。

### Docker 健康检查失败

按顺序检查 SQL Server、Redis、API 连接串和 `.env` 中的密码。API 健康检查地址是容器内 `http://localhost:8080/health`，宿主机默认是 `http://localhost:5000/health`。
