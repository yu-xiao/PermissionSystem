# 企业级底层能力补充规划

本文档基于当前 `PermissionSystem` 项目结构和 AGENTS.md 约束，规划后续需要补齐的平台底层能力。目标是优先完善权限平台的通用基础设施，再逐步承载业务模块。

当前基线：

- 后端：`backend/PermissionSystem.Api`、`Application`、`Domain`、`Infrastructure`、`Shared`、`Worker`
- 前端：`frontend/permission-admin/src`
- 已有能力：RBAC、动态菜单、OpenIddict、Serilog、Redis、RabbitMQ 抽象、Hangfire、基础 Health Check、计划任务、操作日志实体雏形

## 总体落地顺序

1. 多租户上下文、审计字段增强、登录日志、审计日志
2. 字典管理、参数配置、文件上传
3. 数据权限引擎、在线用户与强制下线
4. 幂等机制、防重复提交、API 限流、分布式锁
5. Excel 导入导出、通知中心
6. OpenTelemetry、Health Checks 增强
7. Outbox / Inbox、Hangfire 任务管理增强

## 1. 审计日志

### 目标

记录用户对系统关键资源的增删改查、权限变更、配置变更、导入导出等操作，支持追踪、检索、审计留痕和合规导出。现有 `OperationLog` 可作为基础，需要扩展为完整审计日志能力。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 扩展或新增 `AuditLog`
- `PermissionSystem.Application/AuditLogs`
  - 审计日志查询 DTO、分页查询、详情查询
- `PermissionSystem.Infrastructure/Configurations`
  - 审计日志 EF 配置
- `PermissionSystem.Infrastructure/Data`
  - `AppDbContext` 增加 DbSet
- `PermissionSystem.Api/Controllers`
  - `AuditLogController`
- `PermissionSystem.Api/Middlewares`
  - 审计日志采集中间件或 Action Filter

### 前端涉及的页面

- `src/views/system/audit-log/index.vue`
  - 查询栏：时间范围、用户、模块、操作、结果、IP
  - 表格：操作人、模块、动作、请求路径、结果、耗时、时间
  - 详情弹窗：请求参数摘要、响应摘要、异常信息

### 数据库表

- `AuditLogs`
  - `Id`
  - `TenantId`
  - `OperatorUserId`
  - `OperatorUserName`
  - `Module`
  - `Action`
  - `ResourceType`
  - `ResourceId`
  - `HttpMethod`
  - `RequestPath`
  - `RequestQuery`
  - `RequestBodyHash`
  - `IpAddress`
  - `UserAgent`
  - `TraceId`
  - `ElapsedMilliseconds`
  - `Succeeded`
  - `ErrorCode`
  - `Message`
  - `OperatedAt`
  - `CreatedAt`
  - `CreatedBy`
  - `IsDeleted`

### Redis Key 设计

- 通常不依赖 Redis 持久化审计日志
- 可选批量缓冲：
  - `ps:audit:buffer:{tenantId}`
  - TTL：5-10 分钟
  - 用途：高并发场景下批量落库

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，用于异步写入审计日志事件
- Hangfire：可选，用于日志归档、过期清理、导出任务

### 验收方式

- 调用用户、角色、菜单、权限修改接口后能生成审计日志
- 异常请求能记录失败状态和 TraceId
- 前端可按时间、用户、模块、结果分页查询
- Controller 不直接写日志业务逻辑

## 2. 登录日志

### 目标

记录登录、刷新 Token、登出、登录失败、账号锁定等安全事件，支持安全审计、异常登录排查和在线用户能力。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `LoginLog`
- `PermissionSystem.Application/LoginLogs`
  - 登录日志查询服务
- `PermissionSystem.Infrastructure/Configurations`
  - `LoginLogConfiguration`
- `PermissionSystem.Api/Controllers`
  - `LoginLogController`
- `PermissionSystem.Api/Controllers/ConnectController.cs`
  - 在认证流程中调用 Application 层记录登录事件

### 前端涉及的页面

- `src/views/system/login-log/index.vue`
  - 查询栏：账号、登录结果、时间、IP、客户端
  - 表格：用户名、租户、IP、UserAgent、登录方式、结果、失败原因、登录时间

### 数据库表

- `LoginLogs`
  - `Id`
  - `TenantId`
  - `UserId`
  - `UserName`
  - `LoginProvider`
  - `GrantType`
  - `IpAddress`
  - `UserAgent`
  - `Succeeded`
  - `FailureReason`
  - `TraceId`
  - `LoggedAt`
  - `CreatedAt`
  - `IsDeleted`

### Redis Key 设计

- `ps:login:failed:{tenantId}:{userName}`：登录失败次数，TTL 15-30 分钟
- `ps:login:lock:{tenantId}:{userName}`：账号临时锁定标记
- `ps:login:last:{tenantId}:{userId}`：最近一次登录信息

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，异步记录登录事件
- Hangfire：可选，定期清理历史登录日志

### 验收方式

- 登录成功、密码错误、刷新 Token、登出均有日志
- 多租户下日志隔离
- 登录失败次数超限后产生锁定记录
- 前端可分页查询并查看失败原因

## 3. 多租户上下文

### 目标

统一解析当前租户，保证数据库查询、缓存 Key、审计日志、权限校验均带租户上下文，避免跨租户数据泄露。

### 后端涉及的项目和目录

- `PermissionSystem.Application/Abstractions`
  - 新增 `ICurrentTenantService`
- `PermissionSystem.Api/Services`
  - `CurrentTenantService`
- `PermissionSystem.Api/Middlewares`
  - `TenantResolutionMiddleware`
- `PermissionSystem.Infrastructure/Data`
  - `AppDbContext` 查询过滤器增加 `TenantId` 过滤
- `PermissionSystem.Shared/Constants`
  - 租户 Claim、Header 常量

### 前端涉及的页面

- `src/views/system/tenant/index.vue`
  - 租户管理
- `src/layouts`
  - 当前租户展示与切换入口
- `src/stores/auth.ts`
  - 当前租户状态

### 数据库表

- 已有 `Tenants`
- 需要确保所有业务实体继承 `BaseEntity` 并正确写入 `TenantId`
- 可新增：
  - `TenantSettings`
  - `TenantDomains`

### Redis Key 设计

- `ps:tenant:info:{tenantId}`：租户基础信息
- `ps:tenant:domain:{host}`：域名到租户映射
- `ps:tenant:user:{userId}`：用户可访问租户列表

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：可选，租户初始化、租户数据归档任务

### 验收方式

- Header、域名、Token Claim 至少支持一种租户解析方式
- 普通查询自动按 `TenantId` 隔离
- 超级管理员可按授权切换租户
- Redis Key 全部包含租户维度

## 4. 数据权限引擎

### 目标

在 RBAC 基础上补充数据范围控制，支持本人、本部门、本部门及下级、自定义部门、全部数据等策略，为业务模块预留统一数据过滤能力。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `DataPermissionRule`
- `PermissionSystem.Application/DataPermissions`
  - 数据权限策略查询、保存、解析服务
- `PermissionSystem.Application/Abstractions`
  - `IDataPermissionService`
- `PermissionSystem.Infrastructure/Repositories`
  - 支持按数据权限构造查询表达式
- `PermissionSystem.Api/Controllers`
  - `DataPermissionController`

### 前端涉及的页面

- `src/views/system/data-permission/index.vue`
  - 数据权限规则管理
- `src/views/system/role/index.vue`
  - 角色授权弹窗增加数据范围配置

### 数据库表

- `DataPermissionRules`
  - `Id`
  - `TenantId`
  - `RoleId`
  - `ResourceCode`
  - `ScopeType`
  - `DepartmentIds`
  - `UserIds`
  - `ConditionJson`
  - `Enabled`
  - 审计字段

### Redis Key 设计

- `ps:data-permission:role:{tenantId}:{roleId}`
- `ps:data-permission:user:{tenantId}:{userId}`
- `ps:data-permission:resource:{tenantId}:{resourceCode}`

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：否

### 验收方式

- 不同角色查询同一资源返回不同数据范围
- 修改角色数据权限后缓存失效
- Application 层可复用统一过滤服务
- 预留复杂条件 JSON 但首期只实现部门/本人范围

## 5. 字典管理

### 目标

提供系统通用枚举、状态、类型、标签等字典配置，避免硬编码，支持前端下拉、标签渲染和缓存。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `DictionaryType`、`DictionaryItem`
- `PermissionSystem.Application/Dictionaries`
  - 字典类型和字典项 CRUD、启停、排序
- `PermissionSystem.Infrastructure/Configurations`
  - 字典 EF 配置
- `PermissionSystem.Api/Controllers`
  - `DictionaryController`

### 前端涉及的页面

- `src/views/system/dictionary/index.vue`
  - 左侧字典类型，右侧字典项
- `src/api/system/dictionary.ts`
- `src/stores/dictionary.ts`

### 数据库表

- `DictionaryTypes`
  - `Id`
  - `TenantId`
  - `Code`
  - `Name`
  - `Description`
  - `Enabled`
  - 审计字段
- `DictionaryItems`
  - `Id`
  - `TenantId`
  - `DictionaryTypeId`
  - `Label`
  - `Value`
  - `Color`
  - `SortOrder`
  - `Enabled`
  - 审计字段

### Redis Key 设计

- `ps:dict:type:{tenantId}:{code}`
- `ps:dict:all:{tenantId}`

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：否

### 验收方式

- 前端页面可维护字典类型和字典项
- 字典启停、排序生效
- 字典更新后缓存刷新
- API 返回结构可直接用于 Element Plus 下拉选项

## 6. 参数配置

### 目标

统一管理系统参数、租户参数和安全参数，例如密码策略、文件大小限制、登录锁定策略、功能开关等。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `SystemSetting`
- `PermissionSystem.Application/Settings`
  - 参数读取、保存、刷新缓存
- `PermissionSystem.Application/Abstractions`
  - `ISettingProvider`
- `PermissionSystem.Api/Controllers`
  - `SettingController`

### 前端涉及的页面

- `src/views/system/setting/index.vue`
  - 参数列表、参数编辑、按分组筛选

### 数据库表

- `SystemSettings`
  - `Id`
  - `TenantId`
  - `GroupCode`
  - `Key`
  - `Value`
  - `ValueType`
  - `IsEncrypted`
  - `Description`
  - `Editable`
  - 审计字段

### Redis Key 设计

- `ps:setting:{tenantId}:{key}`
- `ps:setting:group:{tenantId}:{groupCode}`

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，用于参数变更事件广播
- Hangfire：否

### 验收方式

- Application 层可通过 `ISettingProvider` 获取参数
- 修改参数后缓存立即失效或刷新
- 敏感参数不在前端明文展示
- 系统参数和租户参数隔离

## 7. 文件上传

### 目标

提供统一文件上传、下载、预览、访问控制和存储抽象，首期可本地存储，后续扩展对象存储。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `FileObject`
- `PermissionSystem.Application/Files`
  - 上传、下载、删除、元数据查询
- `PermissionSystem.Application/Abstractions`
  - `IFileStorageService`
- `PermissionSystem.Infrastructure/FileStorage`
  - 本地存储实现，对象存储预留
- `PermissionSystem.Api/Controllers`
  - `FileController`

### 前端涉及的页面

- `src/views/system/file/index.vue`
  - 文件列表、上传、下载、删除
- 通用组件：
  - `src/components/FileUploader`

### 数据库表

- `FileObjects`
  - `Id`
  - `TenantId`
  - `OriginalName`
  - `StorageName`
  - `StorageProvider`
  - `Bucket`
  - `Path`
  - `ContentType`
  - `Size`
  - `Hash`
  - `AccessPolicy`
  - `UploadedBy`
  - `UploadedAt`
  - 审计字段

### Redis Key 设计

- `ps:file:token:{tenantId}:{fileId}:{token}`：临时下载令牌
- `ps:file:quota:{tenantId}`：租户文件容量统计缓存

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，发布文件上传事件
- Hangfire：可选，缩略图生成、病毒扫描、孤儿文件清理

### 验收方式

- 支持上传大小和类型限制
- 下载接口校验租户和权限
- 删除文件只软删除元数据，物理清理由后台任务执行
- 文件元数据可分页查询

## 8. Excel 导入导出

### 目标

提供通用 Excel 导入导出框架，支持模板下载、异步导入、错误行回传、导出任务化和审计记录。

### 后端涉及的项目和目录

- `PermissionSystem.Application/Excel`
  - 导入导出任务模型、模板定义、校验结果
- `PermissionSystem.Application/Abstractions`
  - `IExcelImportService`、`IExcelExportService`
- `PermissionSystem.Infrastructure/Excel`
  - 具体 Excel 库适配
- `PermissionSystem.Api/Controllers`
  - `ExcelTaskController`

### 前端涉及的页面

- `src/views/system/excel-task/index.vue`
  - 导入导出任务记录、进度、错误文件下载
- 通用组件：
  - `src/components/ExcelImportDialog`
  - `src/components/ExcelExportButton`

### 数据库表

- `ExcelTasks`
  - `Id`
  - `TenantId`
  - `TaskType`
  - `BusinessCode`
  - `Status`
  - `SourceFileId`
  - `ResultFileId`
  - `ErrorFileId`
  - `TotalRows`
  - `SuccessRows`
  - `FailedRows`
  - `Message`
  - `StartedAt`
  - `CompletedAt`
  - 审计字段

### Redis Key 设计

- `ps:excel:task:progress:{tenantId}:{taskId}`
- `ps:excel:export:lock:{tenantId}:{businessCode}:{userId}`

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，导入导出任务事件
- Hangfire：是，建议长耗时导入导出走后台任务

### 验收方式

- 能下载模板
- 导入错误行能生成错误文件
- 大数据量导出不阻塞 HTTP 请求
- 前端能轮询或订阅任务进度

## 9. 幂等机制

### 目标

保证创建、支付类业务预留、导入触发、异步任务提交等关键写操作在重复请求时只执行一次。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `IdempotencyRecord`
- `PermissionSystem.Application/Idempotency`
  - 幂等记录检查和结果复用
- `PermissionSystem.Api/Filters`
  - `IdempotencyFilter`
- `PermissionSystem.Shared/Constants`
  - 幂等 Header 常量

### 前端涉及的页面

- 无独立页面
- `src/utils/request.ts`
  - 对指定 POST/PUT 请求携带 `Idempotency-Key`

### 数据库表

- `IdempotencyRecords`
  - `Id`
  - `TenantId`
  - `IdempotencyKey`
  - `RequestHash`
  - `Status`
  - `ResponseBody`
  - `LockedUntil`
  - `ExpiresAt`
  - 审计字段

### Redis Key 设计

- `ps:idempotency:{tenantId}:{idempotencyKey}`
- TTL：按业务设置，默认 10-30 分钟

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：可选，清理过期幂等记录

### 验收方式

- 同一 `Idempotency-Key` 重复提交只执行一次
- 请求体不同但 Key 相同返回冲突
- 执行中重复请求返回处理中或复用最终结果
- 过期后可重新提交

## 10. 分布式锁

### 目标

为计划任务、导入导出、缓存重建、关键资源修改提供跨实例互斥能力，避免多副本重复执行。

### 后端涉及的项目和目录

- `PermissionSystem.Application/Abstractions`
  - `IDistributedLockService`
- `PermissionSystem.Infrastructure/DistributedLocks`
  - Redis 分布式锁实现
- `PermissionSystem.Shared/Constants`
  - 锁 Key 常量

### 前端涉及的页面

- 无独立页面
- 可在计划任务、Excel 任务页面展示锁冲突提示

### 数据库表

- 首期不需要数据库表
- 可选兜底表：
  - `DistributedLocks`

### Redis Key 设计

- `ps:lock:{tenantId}:{resource}:{resourceId}`
- `ps:lock:global:{resource}`
- Value：锁持有者、TraceId、过期时间
- TTL：必须设置，避免死锁

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：是，Hangfire 作业执行前可使用锁防重

### 验收方式

- 多实例同时执行同一任务时只有一个实例获得锁
- 锁过期后可被重新获取
- 释放锁时校验持有者，不能误删其他请求锁
- 日志记录锁获取失败原因

## 11. API 限流

### 目标

限制登录、验证码、导出、上传、敏感 API 等高风险接口访问频率，保护系统稳定性。

### 后端涉及的项目和目录

- `PermissionSystem.Api/Middlewares`
  - 限流中间件或使用 ASP.NET Core Rate Limiting
- `PermissionSystem.Application/RateLimiting`
  - 限流策略读取
- `PermissionSystem.Domain/Entities`
  - 可选新增 `RateLimitPolicy`
- `PermissionSystem.Api/Controllers`
  - 可选 `RateLimitPolicyController`

### 前端涉及的页面

- `src/views/system/rate-limit/index.vue`
  - 限流策略管理，可作为二期

### 数据库表

- 可选：
  - `RateLimitPolicies`
    - `Id`
    - `TenantId`
    - `PolicyCode`
    - `PathPattern`
    - `HttpMethod`
    - `Limit`
    - `WindowSeconds`
    - `Enabled`
    - 审计字段

### Redis Key 设计

- `ps:rate-limit:{tenantId}:{policyCode}:{identity}:{window}`
- identity 可为 userId、IP、clientId

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：否

### 验收方式

- 登录接口按 IP 和用户名限流
- 超限返回统一 `ApiResult`
- Redis 计数过期准确
- 可通过配置调整限流窗口和阈值

## 12. 防重复提交

### 目标

防止用户快速点击按钮导致短时间重复提交，作为前端交互和后端短窗口拦截的组合能力。它与幂等机制不同，重点是“短时间重复点击防护”。

### 后端涉及的项目和目录

- `PermissionSystem.Api/Filters`
  - `RepeatSubmitFilter`
- `PermissionSystem.Application/RepeatSubmit`
  - 生成请求指纹并写入 Redis
- `PermissionSystem.Shared/Constants`
  - 防重提交 Header 和错误码

### 前端涉及的页面

- 无独立页面
- `src/utils/request.ts`
  - 提交中状态管理
- 表单页面：
  - 用户、角色、菜单、权限、计划任务等新增/编辑弹窗按钮禁用

### 数据库表

- 不需要数据库表

### Redis Key 设计

- `ps:repeat-submit:{tenantId}:{userId}:{method}:{path}:{bodyHash}`
- TTL：2-10 秒

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：否
- Hangfire：否

### 验收方式

- 快速连续点击保存按钮只产生一次有效请求
- 后端对重复请求返回明确错误码
- 不影响查询接口
- 前端按钮 loading 状态正常恢复

## 13. OpenTelemetry

### 目标

统一采集 Trace、Metric、Log，贯通 API、EF Core、Redis、RabbitMQ、Hangfire，用于性能分析和故障定位。

### 后端涉及的项目和目录

- `PermissionSystem.Api/Program.cs`
  - 注册 OpenTelemetry
- `PermissionSystem.Infrastructure/Telemetry`
  - 遥测扩展方法和 Options
- `PermissionSystem.Infrastructure/Options`
  - `OpenTelemetryOptions`
- `docker-compose.yml`
  - 可选增加 Collector、Jaeger、Prometheus、Grafana

### 前端涉及的页面

- 首期无业务页面
- 可选运维页面：
  - `src/views/system/observability/index.vue`
  - 链接到 Jaeger/Grafana

### 数据库表

- 不需要业务数据库表

### Redis Key 设计

- 不需要业务 Redis Key

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：不依赖，但需要采集 RabbitMQ 调用链
- Hangfire：不依赖，但需要采集任务执行 Trace

### 验收方式

- 每个请求生成 TraceId
- 日志、审计日志、异常响应可关联 TraceId
- 慢 SQL、外部依赖调用可在追踪系统查看
- Hangfire 作业执行能记录 Trace

## 14. Health Checks

### 目标

将当前基础健康检查升级为生产可用的 liveness/readiness/startup 检查，覆盖 SQL Server、Redis、RabbitMQ、Hangfire、磁盘和关键依赖。

### 后端涉及的项目和目录

- `PermissionSystem.Api/Controllers/HealthController.cs`
  - 增强返回结构
- `PermissionSystem.Infrastructure/HealthChecks`
  - 已有 Redis、RabbitMQ 检查，补充 SQL Server、Hangfire、Storage
- `PermissionSystem.Api/Program.cs`
  - 映射 `/health/live`、`/health/ready`

### 前端涉及的页面

- `src/views/system/health/index.vue`
  - 服务状态面板

### 数据库表

- 不需要业务表

### Redis Key 设计

- `ps:health:probe:{instanceId}`：可选，用于 Redis 读写探测

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：需要检查连接可用性
- Hangfire：需要检查 Server 和 Storage 状态

### 验收方式

- `/health/live` 不依赖外部服务
- `/health/ready` 检查 SQL Server、Redis、RabbitMQ、Hangfire
- Docker/Kubernetes 可直接使用健康检查端点
- 前端能展示依赖状态和异常原因

## 15. Outbox / Inbox 消息可靠性

### 目标

保证领域事件、集成事件、RabbitMQ 消息发送和消费的可靠性，避免数据库提交成功但消息丢失，或消息重复消费导致数据异常。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Common`
  - 领域事件基类
- `PermissionSystem.Domain/Entities`
  - 新增 `OutboxMessage`、`InboxMessage`
- `PermissionSystem.Application/Messaging`
  - 事件发布接口、消息处理器
- `PermissionSystem.Infrastructure/Messaging`
  - 基于 RabbitMQ 的发布订阅、Outbox 扫描发布
- `PermissionSystem.Worker`
  - 后台消息发布和重试消费

### 前端涉及的页面

- `src/views/system/message-reliability/index.vue`
  - Outbox/Inbox 消息查询、重试、失败原因

### 数据库表

- `OutboxMessages`
  - `Id`
  - `TenantId`
  - `MessageType`
  - `Payload`
  - `Status`
  - `RetryCount`
  - `NextRetryAt`
  - `ProcessedAt`
  - `ErrorMessage`
  - 审计字段
- `InboxMessages`
  - `Id`
  - `TenantId`
  - `MessageId`
  - `Consumer`
  - `Status`
  - `ProcessedAt`
  - `ErrorMessage`
  - 审计字段

### Redis Key 设计

- `ps:outbox:publish-lock`
- `ps:inbox:dedup:{tenantId}:{messageId}:{consumer}`
- TTL：按消息幂等窗口设置

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：是，作为集成事件消息总线
- Hangfire：是或 Worker 二选一，用于 Outbox 扫描、失败重试、死信补偿

### 验收方式

- 业务数据和 Outbox 消息在同一事务提交
- RabbitMQ 临时不可用时消息保留并可重试
- 重复消费同一消息不会重复处理
- 前端可查看失败消息并手动重试

## 16. Hangfire 任务管理增强

### 目标

在现有计划任务基础上增强任务编排、执行历史、失败重试、手动触发、暂停恢复、并发控制和租户隔离。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 扩展 `ScheduledTask`、`ScheduledTaskExecutionLog`
- `PermissionSystem.Application/ScheduledTasks`
  - 增强任务调度、手动执行、重试、停启
- `PermissionSystem.Infrastructure/BackgroundJobs`
  - 增强 Hangfire 适配
- `PermissionSystem.Api/Controllers/ScheduledTaskController.cs`
  - 增强 API

### 前端涉及的页面

- 已有：
  - `src/views/system/scheduled-task/index.vue`
- 增强：
  - 执行历史抽屉
  - 手动触发
  - 暂停/恢复
  - 最近失败原因
  - Hangfire Dashboard 链接

### 数据库表

- 已有 `ScheduledTasks`
- 已有 `ScheduledTaskExecutionLogs`
- 可扩展字段：
  - `Queue`
  - `CronExpression`
  - `ConcurrencyKey`
  - `TimeoutSeconds`
  - `RetryCount`
  - `LastRunAt`
  - `NextRunAt`

### Redis Key 设计

- `ps:job:lock:{tenantId}:{taskCode}`
- `ps:job:progress:{tenantId}:{executionId}`
- Hangfire 自身 Redis/SQL Storage Key 按配置保留

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，任务完成事件通知
- Hangfire：是，核心任务调度引擎

### 验收方式

- 可新增、停启、手动执行计划任务
- 执行日志记录开始、结束、耗时、结果、异常
- 同一任务并发执行可被限制
- 失败任务可按策略重试

## 17. 通知中心

### 目标

提供站内通知、系统公告、任务完成提醒、异常告警等统一通知能力，支持已读未读和后续 WebSocket/SSE 推送扩展。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `Notification`、`UserNotification`
- `PermissionSystem.Application/Notifications`
  - 通知创建、查询、标记已读
- `PermissionSystem.Application/Abstractions`
  - `INotificationService`
- `PermissionSystem.Api/Controllers`
  - `NotificationController`
- `PermissionSystem.Infrastructure/Messaging`
  - 可选接入 RabbitMQ 通知事件

### 前端涉及的页面

- `src/views/system/notification/index.vue`
  - 通知列表
- `src/layouts`
  - 顶部通知铃铛、未读数

### 数据库表

- `Notifications`
  - `Id`
  - `TenantId`
  - `Title`
  - `Content`
  - `NotificationType`
  - `Level`
  - `BusinessType`
  - `BusinessId`
  - `PublishedAt`
  - 审计字段
- `UserNotifications`
  - `Id`
  - `TenantId`
  - `NotificationId`
  - `UserId`
  - `ReadAt`
  - `DeletedByUser`
  - 审计字段

### Redis Key 设计

- `ps:notification:unread:{tenantId}:{userId}`
- `ps:notification:latest:{tenantId}:{userId}`
- `ps:notification:channel:{tenantId}`：推送通道预留

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，用于异步通知分发
- Hangfire：可选，用于定时公告、通知清理

### 验收方式

- 用户能看到自己的通知列表和未读数
- 标记已读后未读数准确变化
- 任务完成或失败可创建通知
- 通知按租户隔离

## 18. 在线用户与强制下线

### 目标

记录在线会话，展示当前在线用户，支持管理员强制下线、Token 撤销、异常会话清理和多端登录策略。

### 后端涉及的项目和目录

- `PermissionSystem.Domain/Entities`
  - 新增 `UserSession`
- `PermissionSystem.Application/UserSessions`
  - 在线用户查询、强制下线、会话刷新
- `PermissionSystem.Api/Controllers`
  - `OnlineUserController`
- `PermissionSystem.Api/Controllers/ConnectController.cs`
  - 登录、刷新、登出时维护会话
- `PermissionSystem.Infrastructure/Caching`
  - 在线会话 Redis 存储

### 前端涉及的页面

- `src/views/system/online-user/index.vue`
  - 在线用户列表、强制下线按钮
- `src/stores/auth.ts`
  - 被强制下线后的状态清理
- `src/utils/request.ts`
  - 处理会话失效错误码并跳转登录

### 数据库表

- `UserSessions`
  - `Id`
  - `TenantId`
  - `UserId`
  - `UserName`
  - `SessionId`
  - `ClientId`
  - `IpAddress`
  - `UserAgent`
  - `LoginAt`
  - `LastActiveAt`
  - `ExpiresAt`
  - `RevokedAt`
  - `RevokedReason`
  - 审计字段

### Redis Key 设计

- `ps:session:{tenantId}:{sessionId}`：会话详情
- `ps:session:user:{tenantId}:{userId}`：用户会话集合
- `ps:session:revoked:{tenantId}:{sessionId}`：强制下线标记
- `ps:online-users:{tenantId}`：在线用户集合

### 是否依赖 RabbitMQ / Hangfire

- RabbitMQ：可选，多实例广播强制下线事件
- Hangfire：可选，定期清理过期会话

### 验收方式

- 登录后在线用户列表出现当前用户
- 请求 API 时更新最后活跃时间
- 管理员强制下线后目标用户下一次请求返回会话失效
- 登出后在线用户列表移除会话

## 横向规范

### API 约定

- 所有接口返回 `ApiResult` 或 `PagedResult`
- Controller 只做参数接收、模型验证、返回结果
- 业务逻辑统一放在 Application 层
- 所有接口使用 `async/await` 和 `CancellationToken`
- 不直接暴露 Entity，统一使用 Request/Response/DTO

### 多租户约定

- 所有业务表默认包含 `TenantId`
- 所有 Redis Key 默认以 `ps:{capability}:{tenantId}:...` 组织
- 超级管理员跨租户访问必须显式授权
- Hangfire、RabbitMQ 消息 Payload 必须包含租户上下文

### 权限约定

- 每个新增页面同步新增菜单和权限码
- 后端接口使用 `PermissionAttribute`
- 前端按钮使用 `v-permission`
- 权限码建议格式：`system:{resource}:{action}`

### 任务与消息约定

- 长耗时任务优先使用 Hangfire
- 跨模块异步事件优先走 Outbox / Inbox
- 需要跨实例互斥的任务必须使用分布式锁

### 可观测性约定

- 所有日志、审计、登录、安全事件包含 `TraceId`
- 所有后台任务记录执行日志
- 所有外部依赖纳入 Health Checks
- 关键能力暴露基础指标，后续接入 OpenTelemetry Collector

## 建议首批迭代范围

第一阶段建议只做以下能力，形成企业级底座闭环：

1. 多租户上下文
2. 审计日志
3. 登录日志
4. 字典管理
5. 参数配置
6. Health Checks 增强

完成第一阶段后，再进入数据权限、文件、Excel、幂等、限流、消息可靠性等能力，避免一次性改动过大影响可运行性。
