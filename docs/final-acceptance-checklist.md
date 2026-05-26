# PermissionSystem Final Acceptance Checklist

> 本清单用于最终人工验收。先完成构建验证，再按模块逐项操作；涉及 Redis、RabbitMQ、Docker 的项目按实际启用状态验收。

## 1. 登录认证验收

- 验收目标：验证 OpenIddict password、refresh_token、client_credentials 和 revoke 基础能力。
- 操作步骤：运行 API 和前端；用 `admin / SeedData:AdminPassword` 登录；用 `docs/api-tests.http` 请求 `/connect/token`、刷新 token、调用 `/connect/revoke`。
- 期望结果：登录成功返回 access token 和 refresh token；access token 包含 `user_id`、`user_name`、`tenant_id`、`role`、`permission_code`；刷新成功；吊销后旧 refresh token 不再可用。
- 常见失败原因：SeedData 未执行；客户端密钥不一致；未请求 `offline_access`；数据库连接失败；系统时间偏差。

## 2. 权限验收

- 验收目标：验证 RBAC、菜单权限、按钮权限和接口权限。
- 操作步骤：使用 SuperAdmin 访问用户、角色、菜单、权限接口；创建普通角色并只分配少量权限；用普通用户登录访问未授权接口。
- 期望结果：SuperAdmin 拥有全部权限；普通用户只能看到授权菜单和按钮；未登录返回 401；无权限返回 403。
- 常见失败原因：角色权限未分配；token 中权限码未更新；前端动态菜单未加载；`PermissionAttribute` 权限码与 SeedData 不一致。

## 3. 多租户验收

- 验收目标：验证 `X-Tenant-Id`、Claims TenantId、EF Core 租户过滤和自动写入 TenantId。
- 操作步骤：创建第二租户；在不同租户下创建用户/角色；分别带不同 `X-Tenant-Id` 查询。
- 期望结果：普通用户只能访问自己租户数据；新增实体写入当前租户；SuperAdmin 可按 Header 切换租户，未指定 Header 时预留跨租户能力。
- 常见失败原因：TenantMiddleware 顺序错误；token 中无 tenant_id；SeedData 默认租户不存在；手工写入数据 TenantId 为空。

## 4. 数据权限验收

- 验收目标：验证 `All`、`CurrentUser`、`CurrentDepartment`、`CurrentDepartmentAndChildren`、`CustomDepartments`。
- 操作步骤：创建部门树；为不同角色设置数据权限；用业务查询或可复用数据权限服务验证过滤结果。
- 期望结果：部门树可用；角色数据范围可保存；过滤服务可复用；系统基础接口不被误过滤。
- 常见失败原因：用户未绑定部门；部门 `TreePath` 异常；角色未设置数据范围；业务查询未调用数据权限过滤。

## 5. 缓存切换验收

- 验收目标：验证 MemoryCache 默认可用，RedisCache 可按配置启用。
- 操作步骤：设置 `Cache:Provider=Memory`、`Cache:EnableRedis=false` 启动；再设置 Redis 模式并启动 Redis。
- 期望结果：Memory 模式不连接 Redis；Redis 模式注册 RedisCache；`ICacheService`、字典缓存、参数缓存、权限/菜单缓存和 `RemoveByPrefixAsync` 可用。
- 常见失败原因：Redis 连接串为空；业务直接依赖 Redis 实现；多实例场景误用 Memory 模式。

## 6. RabbitMQ 开关验收

- 验收目标：验证 RabbitMQ 为可选基础设施。
- 操作步骤：默认 `RabbitMQ:Enabled=false` 启动；再用 Docker `--profile mq` 并开启 RabbitMQ flags。
- 期望结果：关闭时不连接 RabbitMQ，使用 `NullMessageBus`，消费者和 Outbox publisher 不启动；开启时可发布消息并按配置启动消费者/Outbox。
- 常见失败原因：只设置 Enabled 未启动 mq profile；HostName 配错；消费者和 Outbox 开关未同步。

## 7. Hangfire 验收

- 验收目标：验证任务管理、Dashboard 权限和执行日志。
- 操作步骤：运行 API 和 Worker；访问 `/hangfire`；在任务页面触发 demo task；查看执行日志。
- 期望结果：只有 SuperAdmin 或 `system:job:view` 可访问 Dashboard；任务执行写入日志；失败记录错误；关键任务有分布式锁保护。
- 常见失败原因：Worker 未运行；SQL Server 不可用；Dashboard 未携带认证 Cookie/token；队列名不匹配。

## 8. Outbox / Inbox 验收

- 验收目标：验证可靠消息记录、重试、失败标记和幂等消费。
- 操作步骤：写入 Outbox；RabbitMQ 关闭时查询记录；RabbitMQ 开启时触发 Outbox publisher；模拟重复消费写入 Inbox。
- 期望结果：RabbitMQ 关闭不影响启动；发送失败可重试；超过重试次数标记 Failed；Inbox 阻止重复消费。
- 常见失败原因：Outbox publisher 未启用；RabbitMQ exchange/route 配置错误；消息体不是合法 JSON；Inbox consumer 标识不稳定。

## 9. 审计日志验收

- 验收目标：验证 OperationLog 记录请求、响应、TraceId 且脱敏。
- 操作步骤：执行新增/编辑/删除请求；查看操作日志页面和数据库。
- 期望结果：记录租户、用户、路径、方法、状态码、耗时、TraceId；`password`、`access_token`、`refresh_token`、`client_secret` 被脱敏；超长 Body 截断。
- 常见失败原因：请求不在 `/api` 下；内容类型被识别为二进制；日志写入异常被降级为 warning。

## 10. 登录日志验收

- 验收目标：验证登录成功/失败都记录 LoginLog。
- 操作步骤：使用正确密码和错误密码分别登录；查询登录日志。
- 期望结果：成功记录用户、租户、IP、UserAgent、TraceId；失败记录用户名和失败原因。
- 常见失败原因：登录接口未经过 TraceIdMiddleware；数据库不可写；用户名不存在。

## 11. 文件上传验收

- 验收目标：验证文件大小、扩展名、ContentType、路径穿越和可执行文件限制。
- 操作步骤：上传允许文件；上传超大文件、`.exe`、脚本、带 `../` 的文件名、危险 ContentType。
- 期望结果：允许文件成功入库并可下载；危险文件被拒绝；本地存储路径不越界。
- 常见失败原因：Nginx `client_max_body_size` 小于 API 限制；浏览器上传 ContentType 为空；文件存储根目录无写权限。

## 12. Excel 导入导出验收

- 验收目标：验证用户导出、模板下载和导入预览。
- 操作步骤：导出用户列表；下载导入模板；上传包含重复用户名、缺少必填列、类型错误的 Excel。
- 期望结果：导出文件可打开；模板列完整；导入预览返回成功行和错误行。
- 常见失败原因：上传文件扩展名不允许；Excel 第一张 sheet 缺失；表头和模板不一致。

## 13. API 限流验收

- 验收目标：验证全局限流、登录限流和 refresh token 限流。
- 操作步骤：短时间内连续请求登录接口、刷新接口和普通接口。
- 期望结果：超过阈值返回 429 和友好错误；响应带 TraceId；前端显示频率提示。
- 常见失败原因：`RateLimit:Enabled=false`；反向代理未透传客户端 IP；多实例 Memory 模式限流不共享。

## 14. 幂等和防重复提交验收

- 验收目标：验证 `X-Idempotency-Key` 和重复提交保护。
- 操作步骤：对带幂等特性的 POST 使用相同 key 重放；快速重复提交新增用户/菜单。
- 期望结果：相同 key 返回缓存结果或拒绝重复处理；并发重复提交被拦截。
- 常见失败原因：客户端未传 `X-Idempotency-Key`；Memory 模式多实例不共享；接口未标注对应 attribute。

## 15. Health Checks 验收

- 验收目标：验证 `/health` 和 `/health/detail` 按启用组件返回状态。
- 操作步骤：默认配置访问健康检查；分别开启 Redis、RabbitMQ、Hangfire 后再次访问。
- 期望结果：未启用组件不导致 unhealthy；启用组件不可用时显示 unhealthy；Docker healthcheck 命中 `/health`。
- 常见失败原因：Docker CLI/容器环境不可用；SQL Server 未启动；RabbitMQ 开关与容器 profile 不一致。

## 16. 前端页面验收

- 验收目标：验证登录、动态菜单、路由守卫、按钮权限、Token 自动刷新、SignalR 不影响构建。
- 操作步骤：登录前访问受保护路由；登录后检查所有系统页面；模拟 401、429、强制下线；观察通知中心。
- 期望结果：未登录跳转登录；菜单动态渲染；`v-permission` 生效；并发 401 只刷新一次 token；强制下线后回登录页。
- 常见失败原因：OAuth 客户端 env 不一致；Seed 菜单 component/path 错误；浏览器 localStorage 留有旧 token；SignalR 代理未配置 WebSocket。

## 17. Docker 验收

- 验收目标：验证 Compose 配置、SQL Server、Redis、可选 RabbitMQ、API、前端 Nginx。
- 操作步骤：安装 Docker 后执行 `docker compose config`；默认 `docker compose up -d`；可选 `docker compose --profile mq up -d`。
- 期望结果：默认不强依赖 RabbitMQ；API 读取环境变量；前端代理 `/api`、`/connect`、`/hubs`；volume 和 healthcheck 正常。
- 常见失败原因：未设置 `MSSQL_SA_PASSWORD`；Docker 未安装或不在 PATH；SQL Server 初始化慢；RabbitMQ flags 开启但未启用 mq profile。
## 18. 系统内置资源保护验收

- 验收目标：验证 `admin` 内置账号、`SuperAdmin` 内置角色和超级管理员权限链路不会被普通管理员破坏。
- 操作步骤：使用普通管理员登录，打开用户管理和角色管理；尝试通过页面和接口删除、禁用、修改 `admin` 用户；尝试删除、禁用、修改 `SuperAdmin` 角色；尝试给自己或别人分配 `SuperAdmin` 角色。
- 期望结果：`admin` 用户显示 `系统内置` / `超级管理员` 标签，删除、禁用、重置密码、分配角色等危险按钮不可用；直接调用接口返回明确业务错误；`SuperAdmin` 角色显示保护标签，删除、分配权限、数据范围等危险入口不可用；直接调用接口返回明确业务错误。
- 额外检查：SuperAdmin 也不能删除 `admin` 用户或 `SuperAdmin` 角色；`admin` 始终保留 `SuperAdmin` 角色；系统至少保留一个 SuperAdmin 用户；SeedData 重跑后 `admin` 和 `SuperAdmin` 的 `IsBuiltin` 均为 `true`。
- 常见失败原因：历史数据库未执行新增迁移；SeedData 未重跑；前端缓存旧用户/角色字段；普通管理员残留过高权限。

## 19. 审批流验收

- 验收目标：验证审批流定义、设计器、运行引擎、我的审批、权限、通知、事务的一次完整闭环。
- 前置条件：运行 API 和前端；执行最新 EF Core migrations；确认 SuperAdmin 拥有 `workflow:*` 权限；至少准备两个启用用户和一个普通角色。
- 流程定义步骤：进入 审批管理 / 流程定义；新增流程并填写 `Code`、`Name`、`BusinessType`；编辑基础信息；复制版本；发布；停用。
- 设计器步骤：进入设计器；保留发起人节点；添加审批人、抄送人、条件分支、默认条件、结束节点；配置审批人为指定用户或角色；配置条件表达式；保存；退出后重新进入检查回显。
- 运行步骤：通过 `/api/workflow/instances/start` 使用已绑定 `BusinessType` 发起流程，传入能命中条件分支的 `FormDataJson`；检查是否创建待办任务和发起记录；分别验证同意、拒绝、撤回、转交、加签、抄送。
- 我的审批步骤：检查待我审批、我已审批、我发起的、抄送我的和审批详情；审批记录时间线应包含发起、审批、拒绝、撤回、转交、加签、抄送、完成等动作。
- 权限步骤：用无设计权限用户访问设计器应返回 403；用非任务审批人审批他人任务应返回 403；未登录访问接口应返回 401；无权限访问接口应返回 403。
- 通知步骤：创建审批任务、抄送、拒绝、完成时应写入审批通知 Outbox；RabbitMQ/通知消费者关闭时，主审批流程仍应成功提交。
- 事务步骤：保存设计器、发布、审批动作失败时不应留下半保存状态；同一任务重复审批应返回冲突，不应重复推进流程。
- 期望结果：流程定义与业务类型绑定可用；设计器保存和回显一致；运行状态、任务状态、记录、抄送和通知保持一致；构建通过。
- 常见失败原因：业务类型未绑定或绑定到未发布版本；普通用户 token 中缺少最新权限码；流程没有 Start/End 节点；审批节点未配置审批人；条件分支没有默认分支；通知 Outbox/RabbitMQ 配置与实际环境不一致。
## 20. 业务单据接入审批流验收

- 验收目标：验证不同业务单据可以通过 `BusinessType`、`WorkflowBusinessBinding` 和 `IWorkflowBusinessHandler` 接入统一审批流。
- 前置条件：已执行最新 EF Core migrations；SuperAdmin 拥有 `workflow:business-binding:*` 和 `demo-approval-order:*` 权限；至少准备一个可审批用户。
- 绑定步骤：创建流程定义；进入设计器配置 Start -> Approver -> End 或包含 Condition 的流程；发布流程；在 审批管理 / 业务流程绑定 中新增 `BusinessType = DemoApprovalOrder`，选择已发布流程并启用。
- Demo 单据步骤：进入 示例模块 / Demo 审批单；新增草稿；提交审批；确认单据状态变为 `Pending` 且生成 `WorkflowInstanceId`。
- 审批步骤：审批人进入 待我审批；同意后单据状态应变为 `Approved`；重新用新单据验证拒绝后状态变为 `Rejected`；发起人撤回应变为 `Withdrawn`。
- 条件步骤：在流程中配置 `amount > 10000` 与默认分支；分别提交不同金额 Demo 单据，确认进入不同审批路径。
- 安全步骤：未绑定或已禁用 `BusinessType` 时不能提交新审批；同一业务单据存在未完成审批时不能重复提交；非任务审批人不能审批他人任务。
- 期望结果：业务状态由业务 Handler 回写，审批流实例、任务、记录保持一致；禁用绑定不影响已发起实例；构建通过。
- 常见失败原因：流程未发布；绑定未启用；Demo 单据状态不是 Draft/Rejected/Withdrawn；普通用户 token 中缺少最新权限；审批节点未配置审批人；条件分支缺少默认条件。
