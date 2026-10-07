# AIC-009：动作权限解耦首批验收记录

> 日期：2026-10-07
>
> 状态：首批已实现、待验收；真实单据批次待业务规则，AIC-009 整体未完成，不表示可以发布。
>
> 确认依据：用户对 [实施方案](ai-center-aic-009-implementation-plan.md) 回复“确认”。按 `AGENTS.md` 依次完成 Architect、DBA、Developer、Reviewer 流程。

## 1. 实现与边界 [Architect] [Developer]

首批复用现有动作注册器、权限服务、Demo Payload 和正式业务服务，没有新增框架、依赖或生产业务动作。

- 处理器通过 `AiBusinessActionDefinition` 声明业务类型、版本、工具及执行权限；共用访问策略复核 AI 开关、允许租户、可信身份和租户上下文。注册时拒绝无效定义及重复工具代码、函数名、业务类型／版本。
- 工具展示和调用按每个处理器授权。移除注册器、会话公共判断、执行公共授权和聊天组件的 Demo 全局门槛，Demo 权限仍由当前 Demo 动作声明并在服务端复核。
- 准备草稿验证所属用户、租户、会话、Run 状态、取消和截止时间；重复调用需匹配 Actor、会话、Run、业务类型及版本。历史列表只投影有权且受支持的类型，无权或未知类型不返回 Payload、候选项或执行结果。
- 草稿响应提供 `CanEdit`、`CanCancel`、`CanConfirm`、`CanExecute`。前端按每份卡片控制按钮，缺少能力字段时保守禁用，未知类型／版本不展示 Demo 表单；人工确认及二次验证返回后重新核对草稿版本、Hash、并发令牌和能力。
- 确认／执行按已加载草稿的业务类型及版本重新授权；只接受现有 Demo 类型／版本并调用 `IDemoBusinessOrderService`。保留人工确认、业务校验、Hash、确认版本、并发令牌、强制二次验证、事务、幂等、恢复和 Outbox；成功重放也检查当前权限及执行绑定。
- 复核发现通用幂等过滤器可能先返回缓存，已在取消、确认和执行 API 增加专用资源过滤器（顺序 `-2100`，早于幂等过滤器 `-2000`）。缓存返回前通过 Application 检查草稿归属、业务类型／版本及当前权限；用例入口仍再次授权，通用幂等过滤器未修改。

**后台 Run 继续禁止全部草稿动作。** 正常聊天已采用持久化后台执行，本批不会恢复其草稿生成能力。历史草稿可通过现有人工入口处理；后台重试不会自动确认或执行。未来开放必须独立验证可信身份、租约／取消、调用幂等及写入事务边界。

真实单据类型、Owner、必填字段、主数据口径、状态机、审批、组织／仓库／账套范围及服务契约尚未提供，真实批次暂停，没有按经验新增 ERP／WMS 单据。

## 2. 数据影响与回退 [DBA]

**无数据库变更。** 沿用现有草稿、校验、确认、执行、Run、调用和业务表，以及租户、审计、软删除、RowVersion 和过滤唯一索引。能力字段只在响应中计算，没有实体、映射、索引或迁移修改，没有执行数据库迁移或历史数据处理。

数据库并发、事务、唯一约束及失败回滚仍需隔离 SQL Server 验证；InMemory 和 HTTP 替身不能证明这些行为。回退先关闭受影响动作／场景入口、保持后台禁用，保留草稿、业务、费用和审计记录，沿用 AIC-005 构建兼容规则，不自动删除数据或执行降级迁移。

## 3. 最终验证 [Developer] [Reviewer]

以下为本轮最终代码结果。后端 TRX、构建及离线报告位于 Git 忽略目录 `artifacts/aic-009/`；未调用真实付费模型。

| 检查 | 结果与证据边界 |
| --- | --- |
| 解决方案 Release 构建 | 11 个项目通过；使用隔离 `artifacts/aic-009/build` 输出。保留 `ApiAuthorizationIntegrationTests.cs` 的 ASPDEPR004／ASPDEPR008 两项既有弃用警告 |
| 核心专项 | 133/133；`tests/aic-009-core-final.trx`。注册器 2、AIC-009 授权 37、Demo 草稿 30、执行 17、会话 19、后台兼容 28 |
| Unit 全量 | 814 通过／5 失败／819 总计；`tests/aic-009-unit-final.trx`。失败为下列已在 AIC-008 记录的 Windows 权限环境问题，不能记为全绿 |
| HTTP 集成 | 73 通过／28 SQL 用例跳过／101 总计；`tests/aic-009-integration-final.trx`。新增 AIC-009 12/12，覆盖公共权限、业务撤权及缓存重放的 Actor／类型／版本变化 |
| HTTP 证据边界 | 使用真实 Controller、访问策略、注册器、专用资源过滤器和通用幂等过滤器；执行服务为替身，无真实业务写入，不能替代真实执行事务验收 |
| Legacy | 45/45；`tests/aic-009-legacy-final.trx` |
| 前端单元测试 | 59/59；覆盖逐卡片能力、旧响应、未知类型、确认期间预览变化及现有聊天兼容 |
| 前端质量检查 | type-check、lint、修改文件 Prettier、生产 build 与包体预算通过。lint 保留 UploadFile 两条既有 warning，Vite 保留大包提示 |
| AIC-004 原离线回归 | 41/41 变体自动检查通过；报告 `evaluations-final/20261007-022838-396d1ef62b41421092c48eae4413da3c/report.json`。是原只读评测，不是动作模型评测 |
| 离线基线比较 | Comparable=True、NoRegression=True、CandidateGate=False；`comparison/20261007-022859-9769d61ec64b4f0fb39fd64587c5b0a3/comparison.json`。与 AIC-008 离线基线可比且无回归，人工黄金／事实审核及已确认人工基线缺失，发布门槛未通过 |
| 差异复核 | 分层、身份／租户、逐动作权限、缓存前复核、后台禁用及旧契约检查通过；`git diff --check` 通过 |

全量 Unit 失败与 AIC-008 的已记录环境问题一致，未改 ACL、关闭证书校验或弱化断言：

1. `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：PFX 导入拒绝访问。
2. `LogArchiveServiceTests.ArchiveAsync_ShouldDeleteExpiredCompressedArchive`：Temp 目录拒绝访问。
3. `LogArchiveServiceTests.ArchiveAsync_ShouldCompressExpiredActiveLogAndKeepArchiveWithinRetention`：Temp 目录拒绝访问。
4. `FileSecurityAndCompensationTests.FileService_ShouldStreamHashAndActivateAfterStorageSave`：Temp 文件拒绝访问。
5. `FileStorageConfigurationTests.LocalStorageHealthCheck_ShouldProbeConfiguredDirectory`：Temp 探测失败导致 Unhealthy。

默认 Release 输出曾出现评测 DLL 写入拒绝，改用隔离 `--artifacts-path` 完成构建与测试。后台重试测试初次错误共用了 scoped fence，已将测试夹具改为独立 DbContext／fence 模拟新请求作用域，生产 fence 未修改；最终专项通过，全量仅保留上述 5 项失败。

主要复现命令（仓库根目录；隔离 SQL 连接必须由负责人另行审核配置，不在命令或日志中输出）：

```powershell
dotnet build backend/PermissionSystem.sln -c Release --artifacts-path artifacts/aic-009/build
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj -c Release --artifacts-path artifacts/aic-009/build --no-build --no-restore --logger "trx;LogFileName=aic-009-unit-final.trx" --results-directory artifacts/aic-009/tests
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj -c Release --artifacts-path artifacts/aic-009/build --no-build --no-restore --filter "FullyQualifiedName~AiActionToolRegistryTests|FullyQualifiedName~Aic009ActionAuthorizationTests|FullyQualifiedName~DemoBusinessOrderDraftHandlerTests|FullyQualifiedName~AiDocumentExecutionServiceTests|FullyQualifiedName~AiConversationServiceTests|FullyQualifiedName~Aic008BackgroundRunTests" --logger "trx;LogFileName=aic-009-core-final.trx" --results-directory artifacts/aic-009/tests
dotnet test backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj -c Release --artifacts-path artifacts/aic-009/build --no-build --no-restore --logger "trx;LogFileName=aic-009-integration-final.trx" --results-directory artifacts/aic-009/tests
dotnet test backend/PermissionSystem.Tests/PermissionSystem.Tests.csproj -c Release --artifacts-path artifacts/aic-009/build --no-build --no-restore --logger "trx;LogFileName=aic-009-legacy-final.trx" --results-directory artifacts/aic-009/tests
dotnet artifacts/aic-009/build/bin/PermissionSystem.AiEvaluations/release/PermissionSystem.AiEvaluations.dll --mode offline --output artifacts/aic-009/evaluations-final
```

前端在 `frontend/permission-admin` 执行 `npm run test:unit`、`npm run type-check`、`npm run lint`、`npm run build`，以及 `npx prettier --check src/api/ai.ts src/components/AiChatDialog.vue src/components/AiChatDialog.test.ts src/components/AiDocumentDraftCard.vue src/components/AiDocumentDraftCard.test.ts`。没有运行目标浏览器端到端验收。

## 4. 复核结论与剩余验收 [Reviewer]

**首批代码与确定性专项复核：通过，无已知代码阻断项。完整环境／发布验收：未通过，保持待验收。** 尚需以下证据：

- 在经审核的隔离 SQL Server 执行并发、唯一约束、确认消费、正式业务写入、失败回滚和 Outbox 验证。本轮未配置 `PERMISSION_SYSTEM_SQLSERVER_TEST_CONNECTION`，28 个 SQL 用例跳过。
- 解决 5 项 Windows 权限环境失败后重新运行全量 Unit；不得将当前 814/819 写成全量通过。
- 在目标浏览器验证历史草稿、逐卡片按钮、二次验证期间预览变化、撤权后请求拒绝及兼容响应。组件测试不能替代实际浏览器验收。
- 完成适用的真实模型、业务黄金／事实审核和发布构建验收。原只读离线回归不能证明动作 Tool Calling 或真实单据效果。
- 提供真实单据业务规则后独立确认第二批；未来开放后台草稿时单独完成 AIC-008 兼容与故障证据。

本轮未部署、执行实际库迁移、调用付费模型、commit 或 push。
