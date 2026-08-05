# 测试指南

## 测试目标

本项目测试覆盖后端单元测试、集成测试、API 手工测试、前端构建验证和 Docker 冒烟测试。当前前端 `package.json` 没有单独 test 或 lint 脚本，因此前端主要使用 `npm run build` 做类型和构建验证。

## 后端测试项目

```text
backend/
  PermissionSystem.Tests/
  PermissionSystem.UnitTests/
  PermissionSystem.IntegrationTests/
```

测试框架为 xUnit，目标框架为 `net10.0`。

运行全部测试：

```powershell
cd backend
dotnet test .\PermissionSystem.sln
```

运行单个测试项目：

```powershell
cd backend
dotnet test .\PermissionSystem.UnitTests\PermissionSystem.UnitTests.csproj
dotnet test .\PermissionSystem.IntegrationTests\PermissionSystem.IntegrationTests.csproj
dotnet test .\PermissionSystem.Tests\PermissionSystem.Tests.csproj
```

构建验证：

```powershell
cd backend
dotnet restore
dotnet build .\PermissionSystem.sln
```

## 前端验证

```powershell
cd frontend/permission-admin
npm install
npm run build
```

`npm run build` 会执行：

```text
vue-tsc -b && vite build
```

这会覆盖 TypeScript 类型检查和 Vite 生产构建。

## API 手工测试

REST Client 文件位于：

```text
docs/api-tests.http
```

建议顺序：

1. 启动 API。
2. 确认种子数据已初始化。
3. 在 `docs/api-tests.http` 顶部配置 `host`、`clientId`、`clientSecret`、`username`、`password`。
4. 先执行 password grant 获取 token。
5. 执行 `/api/me`、菜单、权限、用户、角色等接口。
6. 使用 refresh token 验证续期。

本地 API host：

```text
http://localhost:5264
```

Docker API host：

```text
http://localhost:5000
```

## Docker 冒烟测试

启动：

```powershell
Copy-Item .env.example .env
docker compose up -d
```

检查：

```powershell
docker compose ps
curl http://localhost:5000/health
curl http://localhost:5000/health/detail
```

浏览器验证：

- `http://localhost:8080`
- 登录管理员账号
- 打开系统管理页面
- 查看健康检查页面
- 访问用户、角色、菜单、权限列表

如需 RabbitMQ：

```powershell
$env:RABBITMQ_ENABLED = "true"
$env:RABBITMQ_ENABLE_CONSUMERS = "true"
$env:RABBITMQ_ENABLE_OUTBOX_PUBLISHER = "true"
docker compose --profile mq up -d
```

## 核心回归场景

认证授权：

- 管理员登录成功。
- 错误密码登录失败并记录到当前请求租户；相同用户名在不同租户的失败计数和锁定互不影响。
- 非默认租户的 refresh token 在不重复发送 `X-Tenant-Id` 时仍可续期。
- 用户或租户禁用、会话撤销或过期后，refresh token 立即返回 `invalid_grant`。
- 角色、权限或部门变化后，刷新所得 Access Token 使用数据库中的最新 claims。
- 无 token 请求受保护接口返回 401。
- 无权限用户访问接口返回 403。

RBAC：

- 用户分配角色后重新登录获得菜单和权限。
- 移除权限后重新登录不可见对应菜单或按钮。
- 接口权限与前端按钮权限一致。

租户：

- 使用 `X-Tenant-Id` 验证租户上下文。
- 普通业务数据不跨租户泄露。
- Refresh Token 中的租户、用户和会话主体不匹配时拒绝刷新。
- Refresh Token 缺少 Header 或携带冲突 `X-Tenant-Id` 时，仍以签名 Token 租户刷新。
- Refresh Token 必须执行签名 Token 租户自己的 IP 黑白名单策略。
- Refresh 请求附带过期、撤销或其他会话的 Bearer Access Token 时，只按 Refresh Token 自身会话判断。

安全策略：

- 密码复杂度生效。
- 登录失败锁定生效。
- IP 黑白名单规则生效。
- 敏感操作校验生效。

后台能力：

- Hangfire Dashboard 可访问。
- Worker 启动后任务可执行。
- Outbox/Inbox 状态可查看。
- RabbitMQ 关闭时主业务不应被消息发送阻断。

工作流：

- 创建、设计、发布流程。
- 绑定业务类型。
- 发起实例。
- 待办审批、拒绝、转办、加签。
- 查看我发起、已办、抄送。

SSO：

- Provider 测试连接。
- OIDC challenge 跳转。
- callback 生成 login_code。
- exchange 换取 token。
- 自动绑定、自动创建和角色映射按配置生效。

## 本地、Docker、生产差异

本地：

- Development 自动迁移和种子初始化。
- Swagger 可用于调试。
- Memory 缓存可能无法暴露多实例问题。

Docker：

- 接近集成环境，Redis 默认启用。
- API 和 Worker 同时运行。
- RabbitMQ 需要显式启用。

生产：

- 测试应先在预发布环境完成。
- 不依赖 Swagger。
- 数据迁移、回滚、备份必须纳入验证。
- 生产冒烟测试避免破坏性操作。

## 常见问题

### `dotnet test` 集成测试失败

检查测试是否依赖 SQL Server、连接串或环境变量。先运行 `dotnet build` 区分编译问题和运行问题。

### 前端 build 类型错误

优先查看 TypeScript 报错文件，检查 API 类型、组件 props、路由 meta 和 Element Plus 表单引用。

### REST Client 获取 token 失败

检查 API 是否启动、`client_secret` 是否与后端种子配置一致、管理员密码是否正确、是否被登录限流。

### Docker 冒烟测试 API 不健康

先看 `docker compose ps`，再看 `docker compose logs permission-system-api` 和 `docker compose logs sqlserver`。

### 权限回归不稳定

确认测试用户重新登录，避免使用旧 token 中的旧权限 claims。
