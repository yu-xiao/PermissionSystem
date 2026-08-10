# 运维指南

## 运维范围

本项目运维重点包括：

- API、Worker、前端进程或容器状态
- SQL Server、Redis、可选 RabbitMQ
- Hangfire 后台任务
- 健康检查
- 日志和 TraceId
- 文件上传目录
- 租户、用户会话、安全策略和审计日志

## 健康检查

健康检查 Controller 同时支持：

- `GET /health`
- `GET /api/health`
- `GET /health/detail`
- `GET /api/health/detail`

本地：

```powershell
curl http://localhost:5264/health
curl http://localhost:5264/health/detail
```

Docker：

```powershell
curl http://localhost:5000/health
curl http://localhost:5000/health/detail
```

当前注册的检查包括：

- `api-self`
- `sql-server`
- `disk-storage`
- `hangfire`
- `rabbitmq`，关闭时返回 RabbitMQ disabled 类状态说明
- `redis`，仅 Redis 缓存启用时注册

## 日志

API 使用 Serilog，配置在 `backend/PermissionSystem.Api/appsettings.json`。默认输出：

- Console
- `logs/permission-system-api-.log`，按日滚动，保留 14 天

日志模板包含 `TraceId`。排查线上问题时优先让用户提供 TraceId，再查 API 日志、操作日志和登录日志。

Worker 日志由 Worker 宿主输出，部署时应将容器 stdout 或进程日志接入平台日志系统。

## Hangfire 运维

Dashboard 路径由 `Hangfire:DashboardPath` 配置，默认 `/hangfire`。

本地：

```text
http://localhost:5264/hangfire
```

Docker：

```text
http://localhost:5000/hangfire
```

注意：

- Dashboard 有授权过滤器，需要登录和对应权限。
- API 注册任务，Worker 执行任务。
- Worker 未启动时任务会堆积在 Hangfire 存储中。
- Hangfire 默认使用 SQL Server 存储，可用 `ConnectionStrings:HangfireConnection` 覆盖，否则使用默认业务库连接串。

## Docker 运维命令

查看状态：

```powershell
docker compose ps
```

查看日志：

```powershell
docker compose logs permission-system-api
docker compose logs permission-system-worker
docker compose logs permission-admin
docker compose logs sqlserver
docker compose logs redis
```

重启服务：

```powershell
docker compose restart permission-system-api
docker compose restart permission-system-worker
```

停止：

```powershell
docker compose down
```

删除本地数据卷：

```powershell
docker compose down -v
```

`down -v` 会删除持久化数据，仅用于本地重置。

## 数据与备份

SQL Server 是核心数据存储，应定期备份。Docker 本地数据位于 `sqlserver_data` volume。

Redis 用于缓存、幂等、分布式锁等能力。Docker 本地数据位于 `redis_data` volume。

RabbitMQ 仅启用 `mq` profile 时运行，数据位于 `rabbitmq_data` volume。

文件上传默认本地存储，配置在 `FileStorage:Local`：

- `RootPath`: `uploads`（Docker 使用 `/app/uploads`，由 `uploads_data` 命名卷持久化）
- `BucketName`: `default`
- 默认最大文件大小：20 MB

生产环境也可以选择 MinIO：将 `FileStorage:Provider` 设为 `Minio`，并配置
`FileStorage:Minio:Endpoint`、`AccessKey`、`SecretKey` 和 `BucketName`。MinIO
Provider 会在启动时校验配置，并通过 `file-storage` 健康检查验证目标 Bucket。
Production 使用 Local 时必须配置绝对路径，并将该路径挂载到持久化磁盘；两种方式都应纳入备份策略。

文件上传会先经过内容类型和恶意样本基础扫描，业务附件访问还会复用对应业务单据的数据权限。
上传与删除的存储补偿任务名称为 `files:storage-compensation`，默认每 5 分钟处理 Pending
和 PendingDelete 文件。文件下载必须通过 API，不应将对象存储目录或公开 URL 暴露给浏览器。

## 审计与安全运营

系统内已有以下运维页面或接口能力：

- 操作日志：记录请求、用户、IP、耗时、TraceId 等。
- 登录日志：记录登录、刷新 token、登出等安全事件。
- 在线用户：查看会话并强制下线。
- 健康检查：查看依赖组件状态。
- Outbox / Inbox：查看可靠消息状态。
- 任务管理和定时任务：查看任务及执行日志。
- 安全策略：维护密码复杂度、登录失败锁定、敏感操作校验和 IP 策略。

## 本地、Docker、生产差异

本地：

- 日志默认写到 API 工作目录下 `logs/`。
- Memory 缓存模式不会提供跨实例幂等、锁和缓存共享。
- Worker 需要手动启动。

Docker：

- API、Worker、前端、SQL Server、Redis 由 Compose 管理。
- Redis 默认启用。
- Worker 容器默认启动。

生产：

- 日志应接入集中式日志平台。
- 健康检查应接入负载均衡、容器编排或监控系统。
- 数据库、Redis、上传文件需要备份。
- API 和 Worker 应有滚动发布和回滚方案。

## 常见问题

### `/health` 返回 503

打开 `/health/detail` 查看具体失败组件。常见原因是 SQL Server 连接失败、Redis 未启动、磁盘上传目录不可写或 Hangfire 存储不可用。

### 日志里没有业务错误详情

检查是否有 `TraceId`，再关联操作日志或登录日志。全局异常中间件会统一返回错误结果，详细异常应在服务端日志中查看。

### 文件上传失败

检查扩展名是否在允许列表、大小是否超过 20 MB、`uploads` 目录是否可写、Docker 或生产环境是否挂载持久化目录。

### 强制下线不生效

检查用户请求是否仍带旧 token、后端会话中间件是否返回 `x-session-revoked=true`，前端是否清理 token 并跳转登录页。
