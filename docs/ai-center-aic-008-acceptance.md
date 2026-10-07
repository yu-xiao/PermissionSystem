# AIC-008：持久化后台运行与断线恢复验收记录

> 日期：2026-10-07
>
> 状态：首批代码实现与确定性专项复核通过，待环境验收；不表示已完成或可以发布。
>
> 确认依据：用户对 [实施方案](ai-center-aic-008-implementation-plan.md) 回复“确认”。依据 `AGENTS.md` 执行 Architect、DBA、Developer、Reviewer 流程。

## 1. 实际行为 [Architect] [Developer]

复用现有 Run 状态、租约、watchdog、预算、场景版本和后台托管设施，无新增框架或依赖。Pending 后台 Run 本身就是持久化队列；消息、会话更新、Run 和追问引用在同一提交事务内保存，没有数据库提交后另行投递任务的窗口。

- `POST /api/ai/conversations/{id}/runs` 与 `POST /api/ai/runs/{runId}/retry-async` 返回 HTTP 202、Run ID 和 Location。旧 messages/retry 入口提交后等待同一持久化 Run，客户端断开只结束等待。
- `GET /api/ai/conversations/{id}/submission` 使用 `X-Idempotency-Key` 恢复提交引用。数据库摘要绑定租户、调用人、会话，另保存请求内容／引用／时区／重试来源摘要。相同键恢复原 Run，不同内容拒绝；不返回或记录原始键。前端响应不明时保留原参数和键，由用户点击恢复，先查询引用，404 后复用原键提交。
- Worker 扫描 Pending、以 SQL Server 行锁在短事务内认领，创建执行租约和绝对执行截止；本地并发受限。未认领 Pending 可重启恢复，Running 失联或结果不明明确终止，不自动接续或重放模型／工具。
- 后台只保存可信租户／调用人／会话引用及 SecurityStamp，不保存 Bearer Token、Cookie 或权限快照。复用最新身份与会话校验，在认领、模型／工具调用前后及最终提交前检查配置、权限、会话、场景和取消。
- scoped 执行 fence 在 `AppDbContext.SaveChangesAsync` 的短事务内锁定 Run，拒绝旧租约、非 Running、取消及超时的正常结果写入，覆盖消息、工具结果、Run 和 usage。合并最新 RowVersion、心跳和取消字段，避免长跟踪实体覆盖取消。失败结算丢弃未提交消息／工具结果。
- 独立心跳不延长绝对截止。取消 Pending 直接终态；取消 Running 提交取消请求，由执行器或 watchdog 结束。完成与取消由同一锁定写入边界裁定，终态不能由迟到结果覆盖。
- 模型 usage 先预留，返回后记录实际可知 usage。超时、断连或不明结果不自动切换备用；明确供应商限流可受控切换。未知费用用既有保守估算结算，重复回收不重复结算；人工重试生成新 Run 并可能再次计费。
- Worker 持久化进度。API 按本实例具体连接读取状态，每次重验可信订阅身份，再转发受控状态／工具代码／调用 ID。无需 Worker HubContext 或 SignalR backplane，不输出原始参数、工具结果或身份引用。数据库查询是最终依据，瞬时进度可合并。
- 前端从会话 LatestRun 恢复，订阅重连后恢复并查 Run；按 Run／版本拒绝旧事件，按 InvocationId 合并工具进度。活跃 Run 禁止再次发送；取消显示“取消请求已提交”；终态重载最终消息与卡片，失败时继续退避轮询，关闭窗口停止轮询。
- 保留 AIC-005 构建／场景完整性检查；API 与 Worker 必须使用匹配构建，旧任务不静默升级。活跃会话内容不提前清理。

**兼容限制：后台 Run 不提供 Demo 草稿动作。** 实施采用已确认方案的限制分支，尚未证明草稿在后台重复／取消／失联下的兼容性，因此禁止注册到后台可用工具列表。历史草稿读取及原人工确认／执行入口保留，Worker 不自动创建、确认或执行正式单据。后台草稿兼容不能记为通过。

首批仍是内部 AI 聊天，不扩展外部 MCP 后台任务，不实现答案逐字流式输出。

## 2. 数据影响与部署条件 [DBA]

有兼容数据库扩展。`ai_run` 新增 7 个可空字段：ExecutionMode、ActorSessionId、ActorSecurityStamp、QueueDeadlineAt、SubmissionHash、RequestHash、ProgressVersion。沿用 BaseEntity 的租户、审计、软删除与 RowVersion。旧记录新增字段为 NULL，不能被误认领为后台任务。

- 队列扫描索引：ExecutionMode／Status／CreatedAt。
- 提交摘要过滤唯一索引：TenantId／ActorUserId／ConversationId／SubmissionHash，仅非删除且摘要非 NULL。
- 同会话活跃 Run 过滤唯一索引：TenantId／ConversationId，非删除且 Pending／Running。迁移先检查历史重复活跃 Run，发现重复即拒绝，不自动改写或删除数据。
- 原有工具 TenantId／InvocationId 与草稿来源调用唯一约束继续复用，没有新增业务单据表。

已生成 `backend/PermissionSystem.Infrastructure/Data/Migrations/20261007011942_AddAiBackgroundRuns.cs`、Designer、ModelSnapshot，以及本地 `artifacts/aic-008/aic-008-migration.sql`。EF 模型检查返回无待迁移差异。**未执行实际数据库迁移。** EF 工具 10.0.7 低于运行时 10.0.10 的提示保留，未升级工具或依赖。

新增选项默认：扫描 2 秒、心跳 10 秒、排队最长 300 秒、本地并发 3；启动校验范围及心跳小于失联期限。内置执行时限 90 秒，发布场景继续使用既有时限。已有 AI 开关、批准租户、供应商配置、权限与配额继续生效；必须同时部署并运行匹配版本 Worker。

上线前由负责人审核历史数据／SQL 并备份目标库，先应用兼容迁移，再部署匹配 API／Worker、验证消费／心跳／watchdog，最后开放提交。旧代码回退前关闭新提交、处理活跃后台 Run；保留费用和审计记录，不默认执行 Down 或清表。

## 3. 最终验证 [Developer] [Reviewer]

测试使用合成身份／数据及模型替身，没有真实供应商调用。以下均为本轮最终代码结果；本地构建、TRX、审核 SQL 和离线报告位于 `artifacts/aic-008/`，已加入 Git 忽略，不提交生成产物。

| 检查 | 结果与证据边界 |
| --- | --- |
| 解决方案 Release 构建 | 通过，11 个项目，0 警告／0 错误；使用独立 artifacts 路径 |
| AIC-008 核心专项 | 27/27；`aic-008-core-final.trx`。认领、取消、租约、心跳、保守结算、撤销、实际提交到执行、提交恢复、旧 Stamp、停止等待、订阅隔离、未知供应商超时及无后台动作 |
| Unit 全量 | 743 通过／5 失败／748 总计；`aic-008-unit-final.trx`。5 项环境失败如下，不能记为全绿 |
| 非 SQL HTTP 集成 | 61/61；`aic-008-integration-final.trx`。新增 202／权限／Location 契约；HTTP 用例包含编排替身，不能替代实际 Worker 验收 |
| Legacy 测试项目 | 45/45；`aic-008-legacy-final.trx` |
| 新 SQL 专项 | 0 通过／3 跳过；`aic-008-sql-final.trx`。未提供专用隔离连接；不得把 InMemory 结果当作 SQL 并发或事务证明 |
| 前端单元测试 | 50/50，聊天组件 9/9；覆盖活跃恢复、旧事件拒绝、取消请求中、丢失响应同键恢复、终态详情失败后继续加载 |
| 前端质量检查 | 类型检查、任务文件 Prettier、生产构建与包体预算通过。lint 0 错误，保留 UploadFile.vue 两条既有默认值警告；构建保留大 chunk 提示 |
| 离线评测 | 41/41 变体自动检查通过；人工发布门槛 pending/failed。最终报告：`evaluation-final/20261007-013831-fc930e7f965a4f428313ab8f326e106c/report.json` |
| 迁移模型检查 | `has-pending-model-changes` 返回无变更；没有执行库迁移 |
| 差异复核 | 分层、可信身份、租户、取消／租约、受控进度和后台动作禁用检查通过；`git diff --check` 通过 |

全量 Unit 的失败均复现 Windows 权限环境问题，未关闭证书校验、修改 ACL 或弱化断言：

1. `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：PFX 导入拒绝访问。
2. `LogArchiveServiceTests.ArchiveAsync_ShouldDeleteExpiredCompressedArchive`：Temp 目录拒绝访问。
3. `LogArchiveServiceTests.ArchiveAsync_ShouldCompressExpiredActiveLogAndKeepArchiveWithinRetention`：Temp 目录拒绝访问。
4. `FileSecurityAndCompensationTests.FileService_ShouldStreamHashAndActivateAfterStorageSave`：Temp 文件拒绝访问。
5. `FileStorageConfigurationTests.LocalStorageHealthCheck_ShouldProbeConfiguredDirectory`：Temp 探测失败导致 Unhealthy。

标准 Unit 构建曾复现已有评测 DLL 输出路径访问失败，改用隔离 `--artifacts-path` 验证。初次隔离全解决方案 `--no-restore` 缺少 Worker／Legacy 的资产文件，正常还原后最终构建通过。评测测试的仓库定位改为向上查找实际 cases.json，支持独立产物目录，不变更评测判定逻辑。

主要复现命令（仓库根目录）：

```powershell
dotnet build backend/PermissionSystem.sln -c Release --artifacts-path artifacts/aic-008/build
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj -c Release --artifacts-path artifacts/aic-008/build --no-build --no-restore --logger "trx;LogFileName=aic-008-unit-final.trx" --results-directory artifacts/aic-008
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj -c Release --artifacts-path artifacts/aic-008/build --no-build --no-restore --filter "FullyQualifiedName~Aic008BackgroundRunTests"
dotnet test backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj -c Release --artifacts-path artifacts/aic-008/build --no-build --no-restore --filter "Category!=SqlServer"
dotnet test backend/PermissionSystem.Tests/PermissionSystem.Tests.csproj -c Release --artifacts-path artifacts/aic-008/build --no-build --no-restore
dotnet artifacts/aic-008/build/bin/PermissionSystem.AiEvaluations/release/PermissionSystem.AiEvaluations.dll --mode offline --output artifacts/aic-008/evaluation-final
dotnet ef migrations has-pending-model-changes --project backend/PermissionSystem.Infrastructure --startup-project backend/PermissionSystem.Api --configuration Release --no-build
```

前端在 `frontend/permission-admin` 执行 `npm run test:unit`、`npm run lint`、`npm run build`（含类型检查和包体预算），对 ai.ts、AiChatDialog.vue、AiChatDialog.test.ts、signalr-lite.ts 执行 Prettier 检查。

SQL 专项仅接受 `PERMISSION_SYSTEM_AIC008_SQL_TEST_CONNECTION` 指向已审核迁移的独立测试库；测试不自动 Migrate，使用新生成租户夹具并只清理该夹具。覆盖双消费者认领、取消后迟到结果 fencing、提交事务回滚，当前只编译并跳过。

## 4. 复核结论及未完成验收 [Reviewer]

**代码与本轮确定性专项复核：通过，无已知代码阻断项。完整环境／发布验收：未通过，保持待验收。** 修正了取消入口 EF 跟踪冲突，以及终态消息读取失败后停止轮询的问题；新增测试已验证。

- 必须在审核后的隔离 SQL Server 验证行锁、RowVersion、过滤唯一索引、并发提交与回收事务；目前 3 个专项未实际执行。
- 必须用真实 API／Worker 进程验证未认领重启、已认领崩溃、心跳失联、取消／完成竞态及迟到结果。当前测试未运行完整多进程部署，不证明生产锁等待、死锁和负载表现。
- 必须在目标浏览器验证刷新／断网／重连、多会话切换、乱序进度、取消及卡片恢复；Vue 组件测试不能替代真实 WebSocket 和浏览器验收。
- 真实供应商计费、超时／断连语义、受控备用和配额需要对应环境证据。离线评测不证明真实模型效果，人工黄金案例／事实审核及 AIC-005 对应构建重新冻结发布仍需完成。
- 全量 Unit 5 项环境失败需解决后重新执行；没有隐藏失败或将跳过记为通过。
- 后台草稿动作兼容未完成，保持禁用；未来开放必须另补身份、租约、调用幂等与故障验证。
- 进度转发每连接读取数据库与最新授权，默认 2 秒，轮询按失败退避到 30 秒；连接规模、数据库负载、公平性和延迟尚未压测。

本轮未执行实际库迁移、付费模型调用、部署、commit 或 push。
