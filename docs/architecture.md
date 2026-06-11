# 架构说明

## 项目定位

PermissionSystem 是一个企业权限管理平台，当前代码库采用前后端分离和模块化单体架构。后端以 ASP.NET Core Web API (.NET 10) 为入口，使用 EF Core Code First 访问 SQL Server，使用 OpenIddict 提供 OAuth2 / OpenID Connect 能力；前端是 Vue 3 + TypeScript + Vite 管理后台。

## 真实目录

```text
PermissionSystem/
  backend/
    PermissionSystem.sln
    PermissionSystem.Api/              # Controller、中间件、认证授权、Swagger、SignalR、Hangfire Dashboard
    PermissionSystem.Application/      # 应用服务、DTO、用例编排、业务流程
    PermissionSystem.Domain/           # 实体、枚举、仓储接口、领域基础类型
    PermissionSystem.Infrastructure/   # EF Core、仓储、缓存、消息、文件、OpenIddict、Hangfire、健康检查
    PermissionSystem.Shared/           # ApiResult、PagedResult、错误码、常量、异常
    PermissionSystem.Worker/           # Hangfire Worker 宿主
    PermissionSystem.Tests/            # xUnit 测试项目
    PermissionSystem.UnitTests/        # 单元测试项目
    PermissionSystem.IntegrationTests/ # 集成测试项目
  frontend/
    permission-admin/
      src/api/
      src/components/
      src/directives/
      src/layouts/
      src/router/
      src/stores/
      src/utils/
      src/views/
  docs/
  docker-compose.yml
  .env.example
```

## 后端分层

Api 层负责 HTTP 边界，包括 Controller、认证授权、中间件、Swagger、CORS、Rate Limit、SignalR、Hangfire Dashboard 和依赖注入组合。业务逻辑不应写在 Controller 中，也不应直接访问 `AppDbContext`。

Application 层负责用例编排和应用服务，例如用户、角色、菜单、权限、租户、字典、文件、通知、任务、工作流、状态机、报表、安全策略、SSO、开放集成等服务。这里可以协调仓储、领域实体和基础设施抽象，但不直接依赖具体实现。

Domain 层负责实体和领域概念，当前实体位于 `backend/PermissionSystem.Domain/Entities`，枚举位于 `backend/PermissionSystem.Domain/Enums`。实体遵循项目规则继承 `BaseEntity`，包含 `Id`、`TenantId`、审计字段和软删除字段。

Infrastructure 层负责 EF Core、Repository、UnitOfWork、Redis/Memory 缓存、分布式锁、幂等、文件存储、RabbitMQ/Null 消息总线、OpenIddict token 撤销、OIDC 客户端、Hangfire、健康检查和种子数据。

Shared 层提供跨层基础类型，例如 `ApiResult`、`PagedResult`、`ErrorCode`、`ClaimConstants` 和 `BusinessException`。

Worker 项目是后台任务宿主，当前使用 Hangfire Server 消费队列。API 负责注册任务、提供 Dashboard 和接口，Worker 负责执行队列任务。

## 前端结构

前端主项目在 `frontend/permission-admin`：

```text
src/
  api/          # API 封装
  components/   # 通用组件
  directives/   # v-permission 等指令
  layouts/      # 后台布局
  router/       # 静态路由、动态路由守卫
  stores/       # Pinia 状态
  utils/        # Axios、token、进度条、工具函数
  views/        # 页面
```

前端通过 `src/utils/request.ts` 创建 Axios 实例，自动附加 Bearer Token，对非 GET 请求附加 `X-Idempotency-Key`，并在 401 时尝试 refresh token。菜单和权限由当前用户接口加载，`v-permission` 用于按钮级权限控制。

## 运行时组件

本地开发常用组件：

- API：`backend/PermissionSystem.Api`
- 前端：`frontend/permission-admin`
- SQL Server：本机或 Docker
- Redis：可选，开发默认配置是 Memory 缓存
- Worker：需要验证后台任务时单独启动

Docker Compose 默认组件：

- `permission-system-api`
- `permission-system-worker`
- `permission-admin`
- `sqlserver`
- `redis`
- `rabbitmq` 仅在 `mq` profile 下启用

生产部署建议组件：

- API 与 Worker 独立进程或容器部署
- SQL Server 使用受管或独立生产实例
- Redis 使用持久化和访问控制配置
- RabbitMQ 仅在确实启用消息能力时部署
- 前端静态文件由 Nginx 或同类反向代理托管

## 请求链路

典型后台请求链路：

1. 浏览器访问 Vue 页面。
2. 前端 Axios 调用 `/api/...` 或 `/connect/...`。
3. API 中间件处理 TraceId、异常、日志、认证、会话、限流、租户、API Key、IP 策略和授权。
4. Controller 调用 Application Service。
5. Application Service 使用仓储、UnitOfWork、缓存、消息、文件或其他基础设施抽象。
6. EF Core 访问 SQL Server，返回 `ApiResult` 或 `PagedResult`。

## 环境差异

本地 Development：

- `launchSettings.json` 默认 HTTP `http://localhost:5264`，HTTPS `https://localhost:7281`。
- API 会加载 `appsettings.Development.local.json`。
- API 启动时执行 EF Core migration 和 SeedData。
- Swagger 只在 Development 开启。
- 默认缓存是 Memory，Redis 可按配置启用。

Docker：

- API 环境为 `Docker`，监听容器内 `http://+:8080`，宿主机默认映射到 `http://localhost:5000`。
- API 启动时执行 EF Core migration 和 SeedData。
- Redis 默认启用。
- 前端容器 Nginx 反向代理 `/api`、`/connect`、`/hangfire`、`/hubs` 和 `/health`。
- RabbitMQ 默认不启用，需要 `mq` profile 和环境变量。

生产：

- 不应使用 Development 配置和开发证书策略。
- 当前代码只在 Development 或 Docker 自动迁移数据库；Production 不会自动迁移。
- Swagger 当前只在 Development 开启。
- 密钥、连接串、OAuth 客户端密钥应通过环境变量或密钥管理系统提供。
- HTTPS/TLS、反向代理、日志采集、备份和监控需要由部署环境保证。

## 常见问题

### 为什么 Docker 默认访问不到 Swagger

`Program.cs` 中 Swagger 只在 `Development` 环境启用。Docker 的 Nginx 虽然配置了 `/swagger/` 代理，但 API 在 `Docker` 环境默认没有注册 Swagger 中间件。

### API 启动时报连接串未配置

`Infrastructure.DependencyInjection` 会读取 `ConnectionStrings:DefaultConnection`。本地请在 `appsettings.Development.local.json` 配置；Docker 请在 `.env` 中填写 `SQLSERVER_CONNECTION_STRING`。

### Redis 关闭时健康检查是否失败

开发默认 `Cache:Provider=Memory` 且 `Cache:EnableRedis=false`，不会注册 Redis 健康检查。Docker 默认启用 Redis，Redis 不可用会影响健康检查。

### Worker 没启动时任务不执行

API 可以注册 Hangfire 任务并提供 Dashboard，但真正消费队列的是 `PermissionSystem.Worker` 或 Docker 中的 `permission-system-worker`。
