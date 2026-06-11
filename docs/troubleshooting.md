# 故障排查

## 快速定位顺序

1. 确认运行方式：本地、Docker 还是生产。
2. 查看健康检查：`/health` 和 `/health/detail`。
3. 查看 API 日志和 TraceId。
4. 查看操作日志、登录日志、Hangfire Dashboard。
5. 检查配置：连接串、Redis、OAuth 客户端密钥、系统加密密钥。
6. 区分认证问题、授权问题、数据问题、前端代理问题和后台任务问题。

## 常用命令

本地后端：

```powershell
cd backend
dotnet build .\PermissionSystem.sln
dotnet run --project .\PermissionSystem.Api\PermissionSystem.Api.csproj --launch-profile http
```

本地前端：

```powershell
cd frontend/permission-admin
npm run dev
npm run build
```

Docker：

```powershell
docker compose ps
docker compose logs permission-system-api
docker compose logs permission-system-worker
docker compose logs permission-admin
docker compose logs sqlserver
docker compose logs redis
```

健康检查：

```powershell
curl http://localhost:5264/health
curl http://localhost:5264/health/detail
curl http://localhost:5000/health
curl http://localhost:5000/health/detail
```

## API 无法启动

常见原因：

- `ConnectionStrings:DefaultConnection` 为空。
- SQL Server 未启动或密码错误。
- `Security:SystemConfigEncryptionKey` 未配置。
- 种子数据需要的 `SeedData:AdminPassword` 或 `SeedData:OAuthClientSecret` 未配置。
- 端口 `5264`、`7281` 或 Docker 映射端口被占用。

排查方式：

```powershell
cd backend
dotnet run --project .\PermissionSystem.Api\PermissionSystem.Api.csproj --launch-profile http
```

查看控制台第一处异常。Docker 环境用：

```powershell
docker compose logs permission-system-api
```

## 数据库相关错误

### `Invalid object name` 或 `列名无效`

可能原因：

- migration 未生成。
- migration 未执行到当前数据库。
- Production 环境没有自动迁移。
- 数据库连接到了错误实例。

处理：

```powershell
cd backend
dotnet ef migrations list `
  --project .\PermissionSystem.Infrastructure\PermissionSystem.Infrastructure.csproj `
  --startup-project .\PermissionSystem.Api\PermissionSystem.Api.csproj `
  --context AppDbContext
```

本地和 Docker 可以重启 API 触发自动迁移。生产应按部署流程执行迁移脚本。

### Docker SQL Server 不健康

检查 `.env`：

- `MSSQL_SA_PASSWORD`
- `SQLSERVER_CONNECTION_STRING`
- `SQLSERVER_PORT`

再看日志：

```powershell
docker compose logs sqlserver
```

## 登录失败

可能原因：

- 管理员密码与 `SeedData:AdminPassword` 不一致。
- OAuth 客户端密钥与 `SeedData:OAuthClientSecret` 不一致。
- 用户或租户被禁用。
- 触发登录失败锁定或限流。
- 前端 `VITE_API_BASE_URL` 指向错误 API。

排查：

- 查看 `GET /health` 确认 API 可用。
- 查看登录日志页面或 API 日志。
- 用 `docs/api-tests.http` 直接请求 `/connect/token`。
- Docker 下确认前端使用相对路径或 API 地址正确。

## 401 与 403

401 表示未认证或 token 无效。常见原因：

- access token 过期且 refresh token 续期失败。
- 会话被强制下线。
- token 被撤销。
- 前端没有正确保存或发送 token。

403 表示已认证但无权限。常见原因：

- 角色没有对应权限码。
- 接口 `[Permission]` 和前端权限码不一致。
- 用户调整权限后未重新登录。

排查：

- 调用 `/api/me/permissions` 查看当前权限。
- 检查角色权限矩阵。
- 重新登录刷新 claims。

## 前端页面空白或接口失败

本地：

- 检查 Vite 控制台。
- 检查浏览器 Network 中 API 地址。
- 检查 `.env.local` 中 `VITE_API_BASE_URL`。

Docker：

- 检查 `permission-admin` 容器日志。
- 检查 Nginx 代理路径。
- 检查 API 容器健康状态。

构建验证：

```powershell
cd frontend/permission-admin
npm run build
```

## Swagger 无法访问

当前 API 只在 Development 环境开启 Swagger。本地 Development 地址：

```text
http://localhost:5264/swagger
```

Docker 环境默认不保证 Swagger 可用，即使 Nginx 配置了 `/swagger/` 代理。

## Hangfire 任务不执行

可能原因：

- Worker 未启动。
- API 和 Worker 使用不同数据库。
- Hangfire 存储连接失败。
- 队列名配置不一致。
- RabbitMQ/Outbox 开关配置与预期不一致。

排查：

```powershell
docker compose logs permission-system-worker
```

打开 `/hangfire` 查看队列、失败任务和异常详情。

## Redis、RabbitMQ 相关问题

Redis：

- 本地默认 Memory 缓存，不一定连接 Redis。
- Docker 默认启用 Redis。
- 多实例、SSO login_code、分布式锁和幂等建议使用 Redis。

RabbitMQ：

- 默认关闭。
- 需要 `mq` profile 和环境变量。
- 关闭时使用 `NullMessageBus`，Outbox 可记录但不发布。

启用示例：

```powershell
$env:RABBITMQ_ENABLED = "true"
$env:RABBITMQ_ENABLE_CONSUMERS = "true"
$env:RABBITMQ_ENABLE_OUTBOX_PUBLISHER = "true"
docker compose --profile mq up -d
```

## 文件上传失败

检查：

- 文件大小是否超过 20 MB。
- 扩展名是否在允许列表。
- 是否属于阻止扩展名。
- `uploads` 目录是否可写。
- Docker 或生产环境是否挂载持久化目录。

## SSO 登录失败

按顺序检查：

1. Provider 是否启用。
2. callback URL 是否与外部身份提供商登记值一致。
3. `ClientId` 和 `ClientSecret` 是否正确。
4. `Scopes` 和 claim 映射是否正确。
5. login_code 是否过期或重复消费。
6. Redis 或缓存是否可用。
7. SSO 登录日志中的失败原因。

## 工作流异常

发起失败：

- 流程未发布。
- 业务类型未绑定流程。
- 当前用户没有发起权限。

审批失败：

- 任务已处理。
- 当前用户不是审批人。
- 权限码缺失。

业务状态未变化：

- 业务处理器未实现或未被扫描注册。
- 状态机配置不完整。

## 本地、Docker、生产排查差异

本地：

- 优先看控制台和 `logs/`。
- Swagger 可辅助调试。
- 可直接修改 local 配置重启。

Docker：

- 优先看 `docker compose ps` 和服务日志。
- 注意容器内服务名和宿主机端口不同。
- Redis 默认参与健康检查。

生产：

- 不依赖 Swagger。
- 通过集中日志、TraceId、监控和健康检查定位。
- 数据库修复前必须备份。
- 不直接删除数据卷或清空数据库。
