# 部署指南

## 部署形态

当前项目支持三类运行方式：

- 本地开发：API、前端、SQL Server、可选 Worker 分别运行。
- Docker Compose：使用 `docker-compose.yml` 启动 SQL Server、Redis、API、Worker、前端，RabbitMQ 可选。
- 生产部署：建议基于容器或发布包部署 API、Worker 和前端，并使用生产级 SQL Server、Redis、反向代理、日志和备份方案。

## 本地开发部署

后端：

```powershell
cd backend
dotnet restore
dotnet build .\PermissionSystem.sln
dotnet run --project .\PermissionSystem.Api\PermissionSystem.Api.csproj --launch-profile http
```

前端：

```powershell
cd frontend/permission-admin
npm install
npm run dev
```

Worker 如需执行后台任务：

```powershell
cd backend
dotnet run --project .\PermissionSystem.Worker\PermissionSystem.Worker.csproj
```

本地 Development 环境 API 会自动执行数据库迁移和种子数据初始化。

## Docker Compose 部署

创建 `.env`：

```powershell
Copy-Item .env.example .env
```

必须填写的关键项：

```text
MSSQL_SA_PASSWORD=
SQLSERVER_CONNECTION_STRING=
VITE_OAUTH_CLIENT_SECRET=
SEED_ADMIN_PASSWORD=
SEED_OAUTH_CLIENT_SECRET=
SYSTEM_CONFIG_ENCRYPTION_KEY=
```

启动默认服务：

```powershell
docker compose up -d
```

默认地址：

- 前端：`http://localhost:8080`
- API：`http://localhost:5000`
- SQL Server：`localhost,1433`
- Redis：`localhost:6379`
- Hangfire：`http://localhost:5000/hangfire` 或通过前端 Nginx `http://localhost:8080/hangfire`

启用 RabbitMQ：

```powershell
$env:RABBITMQ_ENABLED = "true"
$env:RABBITMQ_ENABLE_CONSUMERS = "true"
$env:RABBITMQ_ENABLE_OUTBOX_PUBLISHER = "true"
docker compose --profile mq up -d
```

RabbitMQ 地址：

- AMQP：`localhost:5672`
- 管理界面：`http://localhost:15672`

停止服务：

```powershell
docker compose down
```

停止并删除数据卷：

```powershell
docker compose down -v
```

`down -v` 会删除 SQL Server、Redis 和 RabbitMQ 数据卷，仅在明确要清空本地数据时使用。

## 生产部署建议

生产环境不要直接沿用 Development 配置。建议：

- API 和 Worker 独立部署，便于扩容和重启。
- SQL Server 使用独立生产实例，启用备份、监控和权限隔离。
- Redis 使用独立生产实例，配置认证、持久化和内存策略。
- RabbitMQ 仅在启用 Outbox 发布或消费者时部署。
- 前端使用 Nginx、CDN 或对象存储托管构建产物。
- 外层反向代理负责 HTTPS/TLS、请求体大小、超时、真实 IP 头和安全响应头。
- 密钥使用环境变量、Secret Manager 或平台密钥服务，不提交到仓库。

生产发布前执行：

```powershell
cd backend
dotnet test .\PermissionSystem.sln
dotnet publish .\PermissionSystem.Api\PermissionSystem.Api.csproj -c Release
dotnet publish .\PermissionSystem.Worker\PermissionSystem.Worker.csproj -c Release

cd ..\frontend\permission-admin
npm install
npm run build
```

## 数据库迁移策略

当前代码只在 `Development` 或 `Docker` 环境自动执行：

```csharp
await dbContext.Database.MigrateAsync();
```

Production 环境不会自动迁移。生产迁移建议流程：

1. 备份数据库。
2. 审查 migration 内容。
3. 在预发布库执行迁移并验证。
4. 在维护窗口对生产库执行迁移。
5. 发布 API 和 Worker。
6. 验证 `/health`、登录、核心查询和后台任务。

生成 SQL 脚本可使用 EF Core CLI，例如：

```powershell
cd backend
dotnet ef migrations script `
  --project .\PermissionSystem.Infrastructure\PermissionSystem.Infrastructure.csproj `
  --startup-project .\PermissionSystem.Api\PermissionSystem.Api.csproj `
  --context AppDbContext `
  --output .\migration.sql
```

## 配置差异

本地 Development：

- `appsettings.Development.local.json` 存放本地机密。
- Swagger 开启。
- Memory 缓存默认启用。

Docker：

- `.env` 注入配置。
- Redis 默认启用。
- API 和 Worker 使用 Docker 内部服务名访问 SQL Server、Redis、RabbitMQ。
- API 默认将 `uploads_data` 命名卷挂载到 `/app/uploads`，重建 API 容器后文件仍可用。
- 如需使用 MinIO，填写 `.env` 中的 `MINIO_ROOT_USER`、`MINIO_ROOT_PASSWORD`，设置
  `FILE_STORAGE_PROVIDER=Minio`，并执行 `docker compose --profile object-storage up -d`。
- `appsettings.Docker.json` 配置本地 CORS、AllowedHosts 和可信代理地址；Compose 为前端 Nginx 固定分配 `172.28.0.10`，API 只信任该代理。

生产：

- 非敏感网络边界配置可写入 `appsettings.Production.json`；密钥继续使用环境变量或密钥管理。
- 发布前必须配置明确的 `AllowedHosts`、`Cors:AllowedOrigins` 以及实际代理的 `ReverseProxy:KnownProxies` 或 `KnownNetworks`，否则 API 会拒绝启动。
- Swagger 当前默认不开启。
- 不自动迁移数据库。
- 建议使用 Redis 支撑缓存、幂等、分布式锁和 SSO 登录码。

## 发布后验证

基础验证：

```powershell
curl http://localhost:5000/health
curl http://localhost:5000/health/detail
```

业务验证：

- 管理员登录。
- `/api/me`、`/api/me/menus`、`/api/me/permissions` 返回正常。
- 用户、角色、菜单、权限列表可访问。
- Worker 运行时 Hangfire 任务可执行。
- 如启用 RabbitMQ，Outbox 消息可发布，消费者无错误。

## 常见问题

### 容器启动后 API 一直 unhealthy

检查 `.env` 中 `SQLSERVER_CONNECTION_STRING`、`MSSQL_SA_PASSWORD`、Redis 连接和 API 日志。

### 前端页面打开但接口 404

检查 Nginx 代理路径和 `VITE_API_BASE_URL`。Docker 模式下建议 `VITE_API_BASE_URL` 为空，由 Nginx 代理。

### 生产发布后表结构不匹配

Production 不会自动迁移。检查迁移脚本是否执行、`__EFMigrationsHistory` 是否包含最新 migration。

### Worker 无任务消费

检查 Worker 进程是否运行、Hangfire 存储连接串是否与 API 一致、队列名是否匹配。
