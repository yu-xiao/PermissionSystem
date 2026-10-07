# AIC-008：持久化后台运行与断线恢复实施方案

> 日期：2026-10-07
>
> 状态：用户已回复“确认”，首批代码与确定性验证已实现、待验收。已生成迁移及审核 SQL，未执行实际数据库迁移、付费模型调用或部署；实际结果见 [验收记录](ai-center-aic-008-acceptance.md)。
>
> 依据：`AGENTS.md`、[后续开发计划](ai-center-next-development-plan.md)、[总体实施计划](ai-center-mcp-implementation-plan.md)及当前代码。

## 1. 范围与结论 [Architect]

首批实现内部 AI 聊天的异步提交、持久化待执行队列、Worker 执行、刷新与断线恢复、取消及故障终止。继续复用现有 Run 状态、租约、watchdog、预算、认证授权和结构化结果，不引入依赖或另建权限体系。

建议复用现有 BackgroundService 托管模式，由 Worker 读取数据库中待执行的 Run。Pending Run 本身就是持久化任务，创建与入队为同一次数据库事务，无单独任务投递步骤。当前 Hangfire 使用独立存储客户端，直接 Enqueue 不能保证与业务事务共同提交，且其自动重试不适合结果不明的外部模型调用；本批不将 AI Run 接入 Hangfire，也不修改现有定时任务或 RabbitMQ 流程。

恢复规则分两类：未认领的 Pending 可在 Worker 重启后继续认领；已认领的 Running 在失联或超时后明确终止，不接续模型对话，不自动重放工具或业务动作。人工重试创建新 Run、记录 RetryOfRunId、重新鉴权和预留预算，保持原场景版本；可能产生新的供应商费用。

首批不做答案逐字流式输出，不接入外部 MCP 后台任务，不扩展 AIC-009 的真实单据动作。已有 Demo 草稿准备若继续在聊天中提供，必须完成后台身份、租约和重复调用兼容测试；正式单据确认与执行仍走现有独立入口，不由 Worker 自动确认或执行。

## 2. 已查证现状 [Architect]

| 当前文件 | 现状与影响 |
| --- | --- |
| `backend/PermissionSystem.Application/AiCenter/AiConversationService.cs` | SendMessageCoreAsync 分次保存消息与 Run，随后在请求内执行；请求取消可传播至执行；追问元数据目前执行开始才保存 |
| `backend/PermissionSystem.Domain/Entities/AiRun.cs` | 已有 Pending/Running/Completed/Failed/Cancelled、ActorUserId、TenantId、ExecutionLeaseId、DeadlineAt、LastHeartbeatAt、RetryOfRunId 和场景执行配置 |
| `backend/PermissionSystem.Infrastructure/Configurations/AiRunConfiguration.cs` | 已有租户/状态/时间索引和 BaseEntity RowVersion；ExecutionLeaseId 尚未配置为并发令牌，不能单凭字段存在认定全部写入受租约保护 |
| `backend/PermissionSystem.Application/AiCenter/AiRunAdmissionService.cs` | 已有分区限流、租户分布式锁及活跃 Run/Token 配额；同会话活跃 Run 检查需进入受保护的提交边界 |
| `backend/PermissionSystem.Infrastructure/Ai/AiRunWatchdogHostedService.cs` | Worker 回收过期 Pending/Running，轮换租约并保守结算 Running usage；没有待执行任务消费者，未结束工具调用也需处理 |
| `backend/PermissionSystem.Application/AiCenter/AiQueryAccessGuard.cs` | 部分工具逐次读取最新身份与权限；不能据此假定所有工具和 Demo 动作均已覆盖会话撤销检查 |
| `backend/PermissionSystem.Application/Authentication/IUserCredentialValidator.cs` | 已有最新用户、租户、角色、权限和 SecurityStamp 的读取契约 |
| `backend/PermissionSystem.Application/Abstractions/IUserSessionStatusChecker.cs` | 可校验租户/用户/SessionId/SecurityStamp，区分撤销、停用与授权过期 |
| `backend/PermissionSystem.Api/Services/CurrentUserService.cs` | 依赖 HttpContext，Worker 不能直接复用 HTTP 当前用户实现 |
| `backend/PermissionSystem.Worker/Program.cs` | 已托管 watchdog；需补齐后台执行所需当前用户与审计上下文依赖，并验证 DI |
| `backend/PermissionSystem.Api/Services/SignalRAiRunRealtimeSender.cs`、`Hubs/AiHub.cs` | 实时发送位于 API，分组目前只有用户 ID；Worker 无 HubContext，API 未配置 SignalR 跨实例 backplane |
| `frontend/permission-admin/src/components/AiChatDialog.vue` | 提交等待最终结果，取消后本地直接设为 Cancelled，切换会话清空 Run；终态事件未主动加载最终回答 |
| `frontend/permission-admin/src/utils/signalr-lite.ts` | 已有断线重连，但没有重连后的状态恢复回调或服务端订阅恢复 |

## 3. 提交与执行契约 [Architect]

- 新增异步提交入口 `POST /api/ai/conversations/{id}/runs` 及异步重试入口 `POST /api/ai/runs/{runId}/retry-async`，返回 HTTP 202、现有 ApiResult<AiRunResponse> 与 Location；响应中的 Pending 只表示已持久化接受。
- 保留旧 messages/retry 入口的最终响应语义：内部复用持久化提交后等待同一个 Run，不在请求内执行模型。旧请求断开只结束等待，后台任务继续；不再把断开连接等同于用户显式取消。离线评测用独立执行适配器继续驱动同一编排逻辑，不要求运行真实 Worker。
- 保留现有 IdempotencyKey 过滤器，并增加数据库可验证的提交幂等引用：服务端从受控入口取得幂等键并按租户、调用人和会话绑定、计算摘要；不在日志或 DTO 返回原值。重复请求返回同一 Run，不创建第二条用户消息。摘要同时绑定请求内容与追问参数，复用键但请求不同返回 Conflict。
- 前端为一次逻辑提交显式保留同一 X-Idempotency-Key，复用已有 Axios 入口；当前拦截器会为未带键的每次新请求生成新值，不能将重新调用发送函数视为安全恢复。现有 IdempotencyFilter 的重复日志包含原键，本批同步改为摘要；其他幂等行为保持现有模式。
- 将会话标题/保留期、用户消息、Run、最小追问引用及时区元数据置于同一 IUnitOfWork 事务。缺少有效会话身份、请求不合法、版本不兼容或配置关闭时不产生可消费任务。
- 详情返回最新 Run 摘要与活跃 Run，Run 查询返回已提交的状态和受控工具进度。恢复依赖数据库查询，不依赖浏览器本地保存 Run ID 或收到全部推送。

Worker 原子认领条件为：后台执行模式、当前 Pending、未软删除、未取消、排队未超时。认领在短数据库事务内改为 Running，设置新的 ExecutionLeaseId、StartedAt、LastHeartbeatAt 与执行 DeadlineAt；只允许一个消费者成功。扫描有批次上限和本地并发上限，复用已有租户准入与配额规则，不因扫描次数重复消耗提交限流额度。

后台执行前重读请求及元数据，校验摘要、保留期、会话归属、场景版本/构建兼容和模型资格；不得把旧 Run 静默升级到当前版本。复用已有主/灰度/备用路由，在调用前校验最新供应商配置；提交时的 ProviderConfigId 表示准入选择，实际调用继续由 usage 和 FinalProviderConfigId 留证。

## 4. 身份、租约与终态 [Architect]

持久化身份仅使用服务端取得的租户、ActorUserId、SessionId 引用和提交时 SecurityStamp，不保存 Bearer Token、Cookie、ClaimsPrincipal、密码或权限列表。身份引用不输出到客户端，也不进入普通日志。

后台使用独立 scoped 当前用户上下文，来源为可信 Run 引用和既有身份校验服务。系统租户 scope 只用于有审计的队列扫描/故障回收；每次执行必须退出系统 scope 并明确设置目标租户，遵守现有跨租户写入及超级管理员显式选租户约束。不得借后台上下文提升权限。

以下检查点复核会话有效性、用户/租户启用、原 SecurityStamp、最新权限、AI 开关、场景资格和取消状态：认领后、每次模型请求前、每次工具/草稿写入前、模型/工具返回后及最终回答提交前。授权已变化则终止该 Run，不以最新扩大的权限继续旧请求；用户可在刷新认证后明确提交新请求。工具自身已有授权和数据范围校验继续保留。

租约保护必须覆盖 Run、assistant/tool 消息、工具结果和预算写入，而非仅检查最终 Run 状态。Application 定义持久化协调接口，Infrastructure 实现短事务中的状态/租约条件校验与写入；不跨外部网络调用持有数据库事务。独立心跳与取消使用限定字段更新，避免覆盖其他字段或因长时间跟踪实体丢失取消请求。心跳不能刷新排队或执行的绝对截止时间。

状态规则：

| 情况 | 行为 |
| --- | --- |
| Pending，重复扫描或多个 Worker | 原子认领；失败者不调用模型或工具 |
| Pending，显式取消 | 原子进入 Cancelled，后续不能认领 |
| Running，显式取消 | 记录 CancellationRequestedAt；执行器协作停止，最终持久化 Cancelled；前端显示取消请求已提交 |
| Running，完成与取消竞态 | 同一受保护提交边界裁定；已终态不可逆，取消已被接受的活跃任务不得再写成功回答 |
| Pending，进程重启 | 排队未超时则重新发现；不需要补发另一套队列消息 |
| Running，进程重启/失去心跳 | watchdog 终止并轮换租约；结果不明不自动重放 |
| 排队或执行截止 | Failed，分别使用可区分错误码；显式取消优先记 Cancelled |
| 旧进程返回或失去租约 | 禁止提交新回答/工具结果，不覆盖终态；未知 usage 保守结算 |

默认建议：队列扫描 2 秒、心跳 10 秒、排队最长 5 分钟；执行时限保持已有内置 90 秒/发布场景时限；watchdog 继续沿用现有配置。所有数值进入选项并校验合法范围，均为待本方案确认的技术默认值。

## 5. 费用与动作兼容 [Architect]

每次模型请求先保存 usage/预留，返回后结算。watchdog 对中断 usage 使用现有 SettleCost 的保守估算，不写零成本、不编造实际 usage；结算与 Run/工具故障终态共同提交，重复回收不能重复结算。

认领失败、队列重复扫描和未调用模型的取消不创建额外 usage。供应商超时、连接中断及返回不明不无条件自动重试或切换备用；保留明确可判定未成功条件的受控路由行为，并补充测试。人工重试保持独立预算记录。

Demo 草稿工具在受保护写入边界内验证当前身份、租约和调用唯一性。若不能证明重复投递、失联及取消下不会重复创建草稿，禁止为后台 Run 提供该工具并作为兼容阻断项报告，不能未经确认改变为自动创建正式单据。正式单据确认/执行链路及其幂等性不在本批重构。

## 6. 实时进度与前端恢复 [Architect]

保留已有 SignalR 通道。Worker 只持久化 Run/工具进度，API 增加针对本实例已授权订阅的进度转发器，从数据库读取受控状态快照并发送到当前连接；不依赖 Worker 的默认 NullAiRunRealtimeSender，也不要求引入 RabbitMQ 或 SignalR backplane。进度允许合并，数据库 Run 查询是最终依据，不承诺恢复每条瞬时事件。

客户端提交取得 Run ID 后订阅该 Run；订阅入口由服务端检查租户、调用人、会话归属和查看权限。分组至少绑定租户与用户；转发检查连接身份/会话仍有效，不能向撤销的旧连接推送。传输仅含 Run/调用标识、状态、时间、受控工具代码和错误码，不发送原始参数、工具数据、身份引用或内部推理。

状态快照增加可比较的版本/进度标识，工具进度使用真实 invocation ID，避免以 toolCode 合并两个独立调用。客户端按 Run 和版本去重，拒绝旧状态覆盖新状态；重连必须重新授权和恢复订阅，再查 Run。

打开/刷新/切换会话读取活跃 Run；Pending/Running 禁止重复发送；收到终态后加载最终消息、引用和结构化卡片。取消仅显示请求中，最终状态来自服务端。提交响应丢失时先通过会话与提交引用恢复，不自动再发一个新请求。新增少量退避轮询兜底，关闭窗口或无活跃 Run 时停止；异步请求返回、轮询及实时事件都校验当前会话，避免切换后的旧响应污染界面。

## 7. 数据影响、迁移与回退 [DBA]

有数据库变更，采用兼容扩展：

- `ai_run` 新增可空执行模式、会话引用、提交 SecurityStamp、排队截止时间、提交幂等摘要/请求摘要及状态版本字段；已有身份、租约、执行时限、重试和场景字段复用。
- 旧 Run 新字段保留 NULL，不能误认领历史同步 Pending；watchdog 保留旧记录回收路径，不伪造历史身份。
- 新增后台待执行扫描索引和租户/调用人/会话/提交摘要的过滤唯一索引；数据库约束与事务内检查共同避免同会话并发创建活跃 Run。应用迁移前检查历史重复活跃 Run；不得在迁移中自动删除或改写历史异常记录。
- 工具调用若缺少同 Run 调用唯一约束，按当前调用标识补兼容约束；先查历史重复，不能未经审核清理。
- 不新增业务单据表、事件日志表或永久权限快照；队列和进度复用现有 Run/Invocation。身份引用随既有保留清理策略处理，活跃 Run 不提前清理；Run/usage/audit 的保留关系在测试中复核。

生成 EF 迁移、ModelSnapshot 与增量审核 SQL；仅生成文件，不连接/迁移实际业务库。审核列长度、可空性、默认值、唯一性预检、索引和旧数据兼容。SQL Server 原子认领、租约 fencing、事务及索引证明必须在隔离环境执行，InMemory 不能替代。

上线顺序：审核并备份目标库 → 应用兼容迁移 → 部署匹配构建的 API/Worker → 验证消费/心跳/watchdog → 再开放异步入口。代码回退前关闭新提交并处理所有活跃后台 Run；不能让旧版本消费新任务。不默认执行 Down、不删除 Run 或费用记录。

## 8. 预计改动文件 [Developer]

路径相对仓库根目录；新增名称为计划，未声称已存在。

| 模块 | 预计文件/职责 |
| --- | --- |
| Application | 修改 `AiCenter/AiConversationService.cs`、`AiConversationModels.cs`、`AiRunAdmissionService.cs`、`AiCenterDependencyInjection.cs`；新增 `AiRunExecutionService.cs`、`AiRunExecutionModels.cs`、`AiRunExecutionIdentity.cs`，分别承担已有编排分离、持久化执行协调契约和可信后台身份；必要时调整总 DI 与 `Tenants/SystemTenantOperations.cs` |
| Domain | 修改 `Entities/AiRun.cs`；仅必要时补 `AiToolInvocation` 调用唯一引用，不加入 EF/HTTP 技术依赖 |
| Infrastructure | 新增 `Ai/AiRunQueueHostedService.cs`、`AiRunExecutionStore.cs`；修改 `AiRunWatchdogHostedService.cs`、`Configurations/AiRunConfiguration.cs`、必要的工具映射、`Options/AiCenterOptions.cs`、DI、迁移和 ModelSnapshot |
| Worker | 修改 `Program.cs`，注册队列执行、后台当前用户与审计上下文；核对 AI 运行配置，不打印配置密钥或连接串 |
| Api | 修改 `Controllers/AiConversationController.cs`、`Hubs/AiHub.cs`、`Services/SignalRAiRunRealtimeSender.cs`、`Idempotency/IdempotencyFilter.cs`、`Program.cs`；新增 `Services/AiRunProgressRelayHostedService.cs`，复用已有认证与幂等过滤器 |
| 前端 | 修改 `src/api/ai.ts`、`components/AiChatDialog.vue`、`utils/signalr-lite.ts` 及相应测试；复用现有 Axios/认证状态与卡片 |
| 测试/评测 | 补充 UnitTests/AiCenter、IntegrationTests/AiCenter，更新受影响的旧同步契约及离线评测适配；不让确定性测试调用付费模型 |
| 文档 | 本方案、AIC-008 验收记录、后续开发计划及总体实施计划 |

仅对拆分现有请求内编排、后台身份和安全持久化所必需的部分调整，不重构其他 AI 场景或业务模块。

## 9. 验证与退出条件 [Reviewer]

实施后验证：

1. 提交事务失败无半提交；同幂等请求/并发请求只产生一条消息与一个 Run；HTTP 断开不取消任务；旧入口仍返回最终响应。
2. 两个消费者争抢、重复扫描、未认领重启、已认领崩溃、心跳与 watchdog 竞态、迟到结果和取消/完成竞态；终态及消息不可被旧租约覆盖。
3. 跨租户/用户、会话撤销/过期、SecurityStamp 改变、用户/租户停用、工具撤权、场景停用/版本不兼容、缺少身份引用；每个检查点不得调用受限能力或提交受限结果。
4. 无调用取消、模型已调用但 usage 不明、重复回收、手工重试、预算硬限制与备用路由；未知费用保守处理，重复任务不重复预留/扣减。
5. Demo 草稿重复调用和取消兼容；正式单据不会被后台自动确认或执行。
6. 前端刷新、响应丢失、重连、多会话切换、推送重复/乱序、轮询终态、取消请求中、引用和历史卡片恢复；同工具多调用分别显示。
7. 后端 Release build、相关与全量测试、AIC-004 离线回归；前端 unit/lint/build；差异与新增文件检查。
8. 隔离 SQL Server 并发/事务测试与审核 SQL；有条件时验证 API/Worker 进程重启与浏览器故障恢复。真实供应商、目标部署、负载与人工体验无证据则记录待验收，不借用历史测试通过结果。

通过条件：用户确认的代码范围与确定性验证完成，Reviewer 无阻断问题；实际 SQL Server、多进程及目标环境未通过时保持“待验收”，不记为“已完成”。既有 Windows 测试证书导入权限与 UnitTests.dll 访问问题若复现，应如实记录，不能关闭安全校验或忽略失败。

## 10. 确认与实施记录 [Architect][DBA]

用户已确认上述首批方案：Worker 以 Run 表消费任务；新增异步入口、保留旧最终响应入口；未认领可恢复、已开始不自动重放；原会话撤销/授权变化终止 Run；技术默认时限；兼容数据库扩展仅生成待审核迁移。

实现采用第 5 节兼容限制分支：后台 Run 不提供 Demo 草稿动作；历史草稿读取与原人工确认／执行入口保留。不能将后台草稿兼容记为已通过。实际数据库迁移、真实供应商费用、生产部署以及 commit/push 不包含在本轮授权中。
