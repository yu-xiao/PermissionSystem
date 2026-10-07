# AIC-011 首批 Demo 异常提醒实施与验收记录

> 日期：2026-10-07
> 状态：用户已确认方案；首批实现、待环境验收，真实业务批次待确认。
> 依据：[AGENTS.md](../AGENTS.md)、[后续开发计划](ai-center-next-development-plan.md)、[已确认实施方案](ai-center-aic-011-implementation-plan.md)。代码复核通过不表示数据库、目标环境或发布验收通过。

## 1. 改了什么 [Architect] [Developer]

本批沿用 AIC-007 本地 Demo，只检查当前执行人授权范围内非删除单据 `ApprovalStatus = Pending` 的完整数量是否 ≥ 1。每 5 分钟按上海时区执行；本人创建、启用、接收站内通知，关闭浏览器不撤销周期授权。未接入真实 ERP／WMS，不定义超时／金额／审批 SLA，不修改源单据，不调用模型，模型费用为 0。

| 模块 | 实际变更 |
| --- | --- |
| Domain | `AiAnomalyRule`、`AiAnomalyEvent` 继承 BaseEntity；确定性周期、冷却、恢复、关闭及有限发送状态 |
| Application／AiAnomalies | 个人固定规则创建、启停、检查、事件列表／详情／关闭、逐次身份与权限验证、只读查询和投递编排 |
| 调度 | 新固定类型 `AiDemoPendingReminder`，只保存服务端任务绑定；复用 Hangfire、启动同步、租户停用／恢复及分布式锁；通用任务 CRUD／Trigger 拒绝操作该类型 |
| Infrastructure | 独立租户／后台身份作用域、SQL 提交／证据读取保护、等待锁后的元数据刷新、受控消息来源适配与幂等查询 |
| Notifications | 可空 DeliveryKey、受控单人发送、事务内效果与来源回执、提交后尽力实时推送、Outbox 延迟消费重新授权 |
| API | `/api/ai/anomalies` 专用 DTO／ApiResult／PagedResult、原 Permission 策略、NoStore、CancellationToken；Controller 不访问 DbContext |
| 前端 | `/system/ai-anomalies` 规则与事件页面，启用授权说明、暂停／检查／关闭、历史证据隐藏、运输状态、租户／撤权响应清理；通知中心仅允许严格匹配的站内事件 UUID 链接 |
| 注册与菜单 | 复用既有 DI、Axios、Pinia、路由守卫和权限点；默认租户菜单种子沿用现有模式，不新增权限或扩大普通角色授权 |
| 测试与文档 | 核心、来源宿主、HTTP 契约、可选隔离 SQL 和 Vue 测试；迁移、审核 SQL、方案及阶段状态同步 |

功能开关 `Ai:EnableDemoAnomalyReminders` 默认 false，规则默认暂停。首批一人一条固定规则，无任意 SQL、表达式、接收人、租户、Cron 或模型参数入口，无新运行依赖。

## 2. 安全与可靠性复核 [Reviewer]

- 复用 `system:scheduled-task:view/create/update/trigger`；操作和业务证据另需 `demo-business-order:view`，运行／启用另需 `system:notification:view` 和 update／view。拥有任务管理权限不能读取他人证据，不自动授予权限。
- API 与 Application 均校验当前租户／本人，执行持续读取当前有效用户、租户、角色、权限和数据范围，不持久化密码、Token、Cookie 或登录会话。后台身份只在不存在 HTTP 上下文时适配现有 CurrentUserService，不能替代匿名 HTTP 身份。
- 系统范围只定位受控任务；查询／写入在明确租户的独立作用域。执行、投递、历史证据读取使用同租户事务，保护用户／角色／权限／用户范围／角色范围／部门元数据，并刷新等待锁前已跟踪的旧元数据；最终幂等依赖数据库，不依赖分布式锁的恰好一次假设。
- 复用公开只读查询的完整 TotalCount，展示 Limit 为 1。事件只保存数量、原观察时间和范围指纹；历史范围变化隐藏旧数量，不将当前查询冒充历史快照。无权时拒绝读取，范围指纹本身不能授权。
- 通知、实时消息及 Outbox 不含单据、数量、部门、用户名或规则名称，只提供通用提示及不透明事件引用。受控入口绑定非空本人接收人，不进入既有空接收人广播路径。
- Direct 中通知、个人回执、事件状态和冷却在同一事务提交；实时推送失败仍可读取已保存通知，不重建通知。Notification 的非空幂等键不因软删除释放，专用 lookup 显式限租户读取已删除键。
- Outbox 的稳定 MessageId 取自事件 DeliveryKey；Queued 与 Delivered 分开。消费时重新校验授权／范围／规则／异常，暂停、关闭、撤权或停用使旧效果抑制并释放预留；消息运输终态失败在后续成功检查时协调为 Failed，不重新发布相同事件。
- 同一持续异常只成功通知一次，另有 24 小时成功冷却；恢复数量为 0 才结束周期，不发送恢复通知。人工关闭抑制至自然恢复；暂停／恢复不清空键或冷却。失败、超时、取消和授权变化不当作数量 0。
- 先提交异常事实，再持久化发送尝试，最后提交效果；最多 3 次发送尝试，退避 5／15 分钟。Disabled 不消耗成功冷却，立即检查不能解除上限。Failed 可核对运输错误、暂停或关闭；不新增任意重发入口，已有消息管理仍受原权限约束，过期／失败事件不能被重放为新通知。

复核修正了通知按钮的模板语法、角色范围保护缺口和历史证据读取中的跟踪状态风险。没有将预算／组件运营告警、通用定时 Agent 或模型解释一并标为完成。

## 3. 数据影响与回退 [DBA]

迁移：[20261007043054_AddAiAnomalyReminders](../backend/PermissionSystem.Infrastructure/Data/Migrations/20261007043054_AddAiAnomalyReminders.cs)。增量 [审核 SQL](sql/aic-011-anomaly-review.sql) 从 `20261007033743_AddAiKnowledgeBase` 生成，仅用于审核，不是自动或任意基线的幂等部署脚本。

- 新增 `ai_anomaly_rule`、`ai_anomaly_event`，保留 TenantId、审计、软删除和 rowversion。
- ScheduledTasks 新增 `(TenantId, Id)` 组合唯一约束，规则→任务和事件→规则采用同租户组合外键、Restrict。
- 规则本人／任务绑定、事件周期及投递键使用唯一约束；一人一规则与去重唯一键保留至软删除之后，本批不开放规则删除／重建绕过冷却。
- Notifications 新增可空 DeliveryKey 及 `(TenantId, DeliveryKey)` 唯一索引，仅过滤 NULL，不过滤软删除。旧通知仍为 NULL，没有伪造历史键或修改原消息表。
- 新表固定契约、计数／周期和发送次数有检查约束，无批量回填、硬删除或证据清理任务。

使用忽略目录中的独立 EF 设计入口离线生成，只构建模型，连接参数为不可用的本机端口；未读取业务连接配置、启动 API／Worker 或执行数据库操作。审核 SQL 和 ModelSnapshot 差异限本批结构；`has-pending-model-changes` 返回没有待迁移模型变化。EF CLI 10.0.7 对运行时 10.0.10 有版本提示，未升级依赖。

实际迁移锁、唯一索引构建耗时、事务锁覆盖／死锁、SQL 查询翻译及计划仍需隔离 SQL Server 验收。当前保护按租户锁部分元数据，牺牲同租户检查并发以保证保守授权；正式容量需测量，不能用 InMemory 推断性能。

回退先关闭本功能、暂停个人规则并停止新类型调度／消费，再回退应用；保留规则、事件、通知、Outbox／Inbox 及审计。不开启 EF Down、不清表；旧程序不具备新类型处理器，不能直接回退后继续执行已登记的新任务。证据保留／清理政策仍由 Owner 另行签认。

## 4. 验证了什么 [Developer] [Reviewer]

最终后端使用独立 `obj/aic011-final` 输出，避免已有 DLL 写入限制；未修改 Windows ACL、系统环境或测试断言。验证产物在忽略目录 `artifacts/aic-011/`，不提交合成报告。

| 验证 | 最终结果 | 证据／边界 |
| --- | --- | --- |
| 解决方案 Release 构建 | 成功，0 error；2 条既有 IntegrationTests ASPDEPR004／008 warning | `dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic011-final -p:UseSharedCompilation=false` |
| AIC-011 核心专项 | 51/51 通过 | `test-results/aic011-core-closure.trx` |
| 全量 UnitTests，工作区 TEMP／TMP | 916 项：915 通过、1 失败、0 跳过 | `test-results/unit-closure-local-temp.trx`；失败见下文 |
| IntegrationTests | 82 通过、37 SQL 环境跳过、0 失败 | `test-results/integration-closure.trx`；本批 HTTP 5/5，SQL 5 项全部跳过 |
| Legacy Tests | 45/45 通过 | `test-results/legacy-closure.trx` |
| 前端 Vitest | 16 文件、74/74 通过 | 包含新增 API 链接及页面 7 项 |
| 前端类型、lint、格式、production build／包体预算 | 通过，lint 0 error | 既有 UploadFile.vue 两条默认 prop warning；Vite Element Plus 大 chunk 提示保留，预算检查通过 |
| AIC-004 原离线回归 | 41/41 变体、确定性检查通过 | `evaluations/20261007-044003-71dc53f26ca74caeb1d503003c30cbbc/report.json`；人工发布门槛仍 pending／failed |
| EF 增量 SQL、Snapshot 与模型一致性 | 通过离线审核，没有待迁移模型变化 | 未连接实际数据库；不代替关系约束／并发运行证据 |

表中报告路径相对于 `artifacts/aic-011/`。核心覆盖固定状态／完整计数、业务范围／租户、撤权／停用／开关、冷却与人工关闭、查询失败、有限退避、Direct 推送失败、Outbox 重复效果／延迟消费、运输终态释放、来源宿主抑制、锁占用、历史读取等待保护后的范围变化、任务入口限制及 HTTP 身份不替换。

来源宿主测试使用真实 `AiAnomalyExecutionHost` 与 Application 编排，但仓储、凭据、锁和事务设施为测试替身；HTTP 使用 TestServer 和服务契约替身，不是真实 OpenIddict 登录／数据库链路。SQL 用例覆盖组合外键、周期唯一约束、软删除幂等键、rowversion／元数据刷新和效果事务回滚，尚未执行，也未证明真实多进程并发／崩溃恢复。前端组件测试及生产构建不代替浏览器体验验收。

全量唯一失败为既有 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`（第 22 行）：Windows 在导入测试 PKCS12 时抛出 `CryptographicException: 拒绝访问`。失败发生在 TLS 请求前，不能说 TLS 行为已通过；没有跳过该用例或弱化校验。最初默认 TEMP 还造成旧文件测试失败，测试进程专用工作区 TEMP／TMP 后这些失败消除；变量结束后恢复，系统设置未变。

复现后端专项（先执行表中 Release build）：

```powershell
dotnet test backend/PermissionSystem.UnitTests -c Release --artifacts-path obj/aic011-final --no-build --filter FullyQualifiedName~Aic011
dotnet test backend/PermissionSystem.IntegrationTests -c Release --artifacts-path obj/aic011-final --no-build
dotnet test backend/PermissionSystem.Tests -c Release --artifacts-path obj/aic011-final --no-build
```

前端在 `frontend/permission-admin` 执行 `npm run test:unit`、`npm run lint`、`npm run format:check`、`npm run build`；本批 API／页面和通知页另执行 Prettier check。离线回归可使用既有 `scripts/aic-004-evaluate.ps1` 的 Offline 默认模式，未执行 Live 或付费请求。

## 5. 还有什么风险／环境退出条件 [Reviewer]

**通过本轮代码及安全边界复核，完整环境／发布验收未通过，AIC-011 保持首批待验收。** 本轮未执行数据库迁移、开启功能／规则、启动应用、部署或 commit／push。

| 待办 | 验收证据 |
| --- | --- |
| 隔离 SQL Server | DBA 审核并在明确隔离库准备基线及本迁移，检查备份、锁耗时、外键／唯一约束、并发撤权、并发通知、任务／来源事务与查询计划；不能默认连接业务库 |
| 可选 SQL 测试入口 | 同时显式配置 `PERMISSION_SYSTEM_AIC011_SQL_TEST_CONNECTION` 与 `PERMISSION_SYSTEM_AIC011_SQL_TEST_ISOLATED=1`；测试只使用回滚合成夹具，不自动迁移。连接信息不写入仓库或日志 |
| 实际调度／通知故障矩阵 | Hangfire 重复／重启、锁失效、查询取消、Direct 提交后推送失败、Outbox／RabbitMQ 延迟／重复／死信及消费时停用租户；验证只持久化一条本人通知，无旧效果补发 |
| 浏览器与认证 | 实际 OpenIddict、菜单／角色、启用授权确认、租户切换、撤权、分页、窄屏／键盘与通知跳转；非默认租户菜单沿用既有配置管理，不由本批自动生成 |
| 测试环境 | 修复 Windows 证书导入权限后重跑全量，保留 TLS 拒绝行为的真实证据 |
| 留存与真实业务 | Owner 确认证据保留政策；真实来源、异常条件／阈值、SLA、接收人和渠道独立确认。可选模型解释需独立预算与合规设计 |

环境验收准备完成后，由管理员明确允许租户，设置 AI 总开关和 `Ai:EnableDemoAnomalyReminders`，验证既有 Hangfire／通知模式，赋予必要的原权限。本人进入页面创建默认暂停规则，经授权说明后启用；暂停和关闭在功能开关关闭时仍可操作，但仍需要有效身份与原业务／操作权限。AI 模型配置不是本批判定／投递的依赖。本轮没有替用户执行这些启用动作。

现有 AIC-005 构建兼容约束继续生效，代码／构建变化不能静默升级旧场景版本；需要重新冻结／评测的场景按原门禁处理。首批 Demo 通过不能替代真实业务、P5-E 通用 Agent 或整个 AI 中心的验收。

后续用户明确要求提交并推送本批改动至 GitHub；Git 提交／推送不表示环境验收、数据库迁移或功能启用，任务仍保持“首批待验收；真实批次待确认”。
