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

匿名探针分为：

- `GET /health/live`：仅验证 API 进程存活，不访问 SQL Server、Redis、Hangfire、RabbitMQ 或文件存储。用于容器/编排平台重启判定。
- `GET /health/ready`：验证 SQL Server、Hangfire、通知通道、文件存储，以及启用时的 Redis、RabbitMQ。用于负载均衡摘流和恢复流量判定。
- `GET /health` 和 `GET /api/health`：兼容入口，等同于 `/health/ready`。

详细信息端点保留为：

- `GET /health/detail`
- `GET /api/health/detail`

详细端点必须已认证且具备 `system:health:view` 权限，响应中可能包含依赖错误、存储空间或组件状态；不得将该权限授予普通业务角色，也不得经公网匿名暴露。

本地：

```powershell
curl http://localhost:5264/health/live
curl http://localhost:5264/health/ready
```

Docker：

```powershell
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready
```

当前注册的检查包括：

- `api-self`（仅 liveness）
- `sql-server`
- `disk-storage`
- `hangfire`
- `rabbitmq`，关闭时返回 RabbitMQ disabled 类状态说明
- `redis`，仅 Redis 缓存启用时注册

## 指标与告警

API 与 Worker 使用同一个 OTLP Endpoint 接收 Trace 和 Metrics。生产通过环境变量设置 `OTEL_EXPORTER_OTLP_ENDPOINT`，例如 Collector 的 HTTP/protobuf 地址；该地址不含认证密钥，认证信息应由部署平台的 Secret/Collector 配置管理。

默认采集的低基数指标包括：

- HTTP 请求量、状态码与请求耗时；可在 OTLP 平台计算 p95/p99 延迟。
- 登录成功、失败、策略拒绝和锁定次数。
- EF Core 命令耗时以及超过 `OpenTelemetry:SlowSqlThresholdMilliseconds` 的慢 SQL 警告日志。
- Hangfire 队列长度与执行服务器数。
- Outbox 积压、发布、重试和最终失败数。
- 本地文件存储可用空间，以及文件扫描失败数。
- .NET Runtime、ASP.NET Core 和 HTTP Client 基础指标。

RabbitMQ Consumer Lag/DLQ 应继续由 RabbitMQ Management Plugin 或 Exporter 采集，避免应用进程通过管理接口轮询产生额外权限和可用性依赖。MinIO 空间指标应由 MinIO Exporter 采集。

在既有 OTLP 监控平台配置以下告警基线，告警按服务、环境和实例聚合，不以用户、租户、IP、TraceId 等高基数字段分组：

| 告警 | 触发建议 | 首要处理 |
| --- | --- | --- |
| Readiness 不可用 | 任一实例连续 2 分钟 `/health/ready` 为 503 | 从负载均衡摘流，查看受保护健康详情与依赖告警。 |
| API 5xx | 5 分钟错误率超过 1% | 用 TraceId 关联 API 日志、Trace 和操作日志。 |
| 401/403/429 异常增长 | 5 分钟较基线上升 3 倍或超过容量阈值 | 排查认证、权限发布、IP 规则或限流策略。 |
| API p95 延迟 | 10 分钟 p95 超过 1 秒 | 检查慢 SQL、下游 HTTP 和 Redis/RabbitMQ Trace。 |
| 慢 SQL | 10 分钟内出现持续慢 SQL 警告 | 结合参数脱敏日志、执行计划和数据库负载处理。 |
| Hangfire 堆积/失败 | 队列长度持续增长 10 分钟或失败任务增加 | 确认 Worker 实例、队列和 SQL Server 存储状态。 |
| Outbox 积压/失败 | 积压持续增长 10 分钟或最终失败数大于 0 | 检查 RabbitMQ 连通性、DLQ 和发布错误。 |
| RabbitMQ DLQ/Consumer Lag | 任一队列大于 0 并持续 10 分钟 | 停止盲目重放，先定位消费者失败原因。 |
| 文件存储空间/扫描失败 | 可用空间低于 20% 或扫描失败持续增加 | 扩容存储，排查上传内容与扫描策略。 |

## 日志

API 和 Worker 使用 Serilog，配置分别位于 `backend/PermissionSystem.Api/appsettings.json` 与 `backend/PermissionSystem.Worker/appsettings.json`。默认输出：

- Console
- `logs/permission-system-api-.log` 或 `logs/permission-system-worker-.log`，按日滚动

`LogArchive` 后台服务每小时处理一次已关闭的日志文件：活动日志保留 7 天，达到期限后压缩为 `.gz` 移入 `logs/archive`；压缩归档保留 45 天后删除。归档服务不会处理当日仍在写入的文件，且压缩失败会保留原文件。Docker 使用独立的 `api_logs`、`worker_logs` 命名卷，生产需将活动和归档目录纳入集中日志/备份策略。

日志模板包含 `TraceId`。排查线上问题时优先让用户提供 TraceId，再查 API 日志、操作日志和登录日志。`OpenTelemetry:IncludeSqlStatements` 默认关闭；生产启用前必须完成参数脱敏和数据合规评审。

除文件归档外，生产仍应将 API/Worker 的容器 stdout 或进程日志接入集中日志平台，避免仅依赖本地持久卷排障。

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

### `/health/ready` 返回 503

使用具备 `system:health:view` 权限的运维账号打开 `/health/detail` 查看具体失败组件。常见原因是 SQL Server 连接失败、Redis 未启动、磁盘上传目录不可写或 Hangfire 存储不可用。`/health/live` 正常而 `/health/ready` 失败时，不应重启进程，应先修复依赖或摘流实例。

### 日志里没有业务错误详情

检查是否有 `TraceId`，再关联操作日志或登录日志。全局异常中间件会统一返回错误结果，详细异常应在服务端日志中查看。

### 文件上传失败

检查扩展名是否在允许列表、大小是否超过 20 MB、`uploads` 目录是否可写、Docker 或生产环境是否挂载持久化目录。

### 强制下线不生效

检查用户请求是否仍带旧 token、后端会话中间件是否返回 `x-session-revoked=true`，前端是否清理 token 并跳转登录页。
