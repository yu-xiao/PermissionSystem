# PermissionSystem

PermissionSystem 是一个前后端分离的企业权限管理平台，后端基于 ASP.NET Core Web API (.NET 10)，前端基于 Vue 3、TypeScript、Vite、Pinia、Vue Router、Axios 和 Element Plus。

## 技术栈

- 后端：ASP.NET Core Web API、EF Core、SQL Server、OpenIddict、Redis、Hangfire、Serilog、OpenTelemetry、可选 RabbitMQ。
- 前端：Vue 3、TypeScript、Vite、Pinia、Vue Router、Axios、Element Plus。
- 架构：模块化单体，按 Api、Application、Domain、Infrastructure、Shared、Worker 分层。

## 项目结构

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
    PermissionSystem.Tests/
    PermissionSystem.UnitTests/
    PermissionSystem.IntegrationTests/
  frontend/
    permission-admin/
  docs/
  scripts/
  docker-compose.yml
  .env.example
  AGENTS.md
  README.md
```

## 本地快速启动

后端需要 .NET 10 SDK 和 SQL Server。开发环境配置优先放到 `backend/PermissionSystem.Api/appsettings.Development.local.json`，不要提交真实密码、连接串或密钥。

```powershell
cd backend
dotnet restore
dotnet build .\PermissionSystem.sln
dotnet run --project .\PermissionSystem.Api\PermissionSystem.Api.csproj --launch-profile http
```

后端默认地址：

- API：`http://localhost:5264`
- Swagger：`http://localhost:5264/swagger`
- 健康检查：`http://localhost:5264/health`
- 健康详情：`http://localhost:5264/health/detail`
- Hangfire Dashboard：`http://localhost:5264/hangfire`

前端：

```powershell
cd frontend/permission-admin
npm install
npm run dev
```

前端默认地址：`http://localhost:5173`。

## Docker 快速启动

先从 `.env.example` 创建本地 `.env`，并填写 SQL Server、种子账号、OAuth 客户端密钥和系统配置加密密钥等值。

```powershell
Copy-Item .env.example .env
docker compose up -d
```

默认服务：

- 前端：`http://localhost:8080`
- 后端 API：`http://localhost:5000`
- SQL Server：`localhost,1433`
- Redis：`localhost:6379`

RabbitMQ 默认不启用。如需启用消息队列和 Outbox 发布：

```powershell
$env:RABBITMQ_ENABLED = "true"
$env:RABBITMQ_ENABLE_CONSUMERS = "true"
$env:RABBITMQ_ENABLE_OUTBOX_PUBLISHER = "true"
docker compose --profile mq up -d
```

## 测试与构建

```powershell
cd backend
dotnet test .\PermissionSystem.sln

cd ..\frontend\permission-admin
npm run build
```

`docs/api-tests.http` 可配合 VS Code REST Client 调试 OAuth token、用户、角色、菜单、权限等接口。

## 文档

- [架构说明](docs/architecture.md)
- [后端开发指南](docs/backend-development-guide.md)
- [前端开发指南](docs/frontend-development-guide.md)
- [部署指南](docs/deployment-guide.md)
- [运维指南](docs/operation-guide.md)
- [安全指南](docs/security-guide.md)
- [工作流指南](docs/workflow-guide.md)
- [SSO 指南](docs/sso-guide.md)
- [测试指南](docs/testing-guide.md)
- [故障排查](docs/troubleshooting.md)

更多专题文档仍保留在 `docs/` 目录，例如 `workflow-design.md`、`sso-design.md`、`role-permission-matrix-design.md`、`production-readiness-review.md` 和 `api-tests.http`。
