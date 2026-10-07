# AIC-012 运营指标细分首批实施与验收记录

> 日期：2026-10-07
> 状态：用户已确认具体方案；运营首批已实现、待验收，其余方向暂缓／待启动。
> 依据：[AGENTS.md](../AGENTS.md)、[后续开发计划](ai-center-next-development-plan.md)、[已确认方案第 8 节](ai-center-aic-012-implementation-plan.md#8-运营指标细分首批具体提案-architect)。代码复核通过不等于真实 SQL、目标环境、业务或发布验收通过。

## 1. 改了什么 [Architect] [Developer]

本批在既有运营页增加“场景统计”，只读取已有 Run、usage、feedback 和可读场景元数据。按当前活动单租户、运行创建时间的左闭右开窗口跨场景版本汇总，默认 30 天、最多 90 天。没有 Run 的场景不额外枚举；历史 NULL 引用为“未绑定场景”，不可读或删除引用为“场景不可用”，停用场景仍统计，名称表示当前名称。

| 模块 | 实际变更 |
| --- | --- |
| Application | [DTO／契约](../backend/PermissionSystem.Application/AiCenter/AiScenarioOperationsModels.cs)、[查询服务](../backend/PermissionSystem.Application/AiCenter/AiScenarioOperationsService.cs)及 DI 注册；复用仓储、IAsyncQueryExecutor 和当前认证／租户／权限服务 |
| API | [AiOperationsController](../backend/PermissionSystem.Api/Controllers/AiOperationsController.cs) 新增 `GET /api/ai/operations/scenarios`，继续 `ai:operations:view`、ApiResult／PagedResult、NoStore 和 CancellationToken |
| 前端 | [API](../frontend/permission-admin/src/api/ai.ts)及[原运营页](../frontend/permission-admin/src/views/ai/operations/index.vue)新增标签、分页、展开明细、口径／观察窗口／容量说明，复用 Axios／Pinia／Element Plus |
| 验证 | 新增核心统计、SQL Server 离线翻译、HTTP 契约、显式隔离 SQL、Vue 页面和 API 测试；不新增运行依赖 |
| 文档与产物 | 同步实施方案、后续计划和总体计划；`.gitignore` 精确忽略 `artifacts/aic-012/` 验证产物 |

旧 summary／feedback DTO、公式和 URL 保持兼容。新技术完成率名称及终态分母与旧成功率区分；原质量趋势仍按 UTC 日期汇总，页面查询范围和观测时间按本机时区显示。

## 2. 指标与查询复核 [Reviewer]

- 每个 Run 是一次运行尝试，重试新 Run 独立计数，不能视为去重业务任务。
- 技术完成率为 Completed／(Completed + Failed + Cancelled)，Pending／Running／未知状态单列；零分母返回 null。百分数由服务端按两位小数返回，并返回计数。
- 超时只计 Failed 且 ErrorCode 为 `run_timeout` 或 `run_queue_timeout`，属于失败子集，不推定 `run_orphaned`／`run_interrupted` 为已证明超时。
- 反馈资格固定于最初读取的 Completed 且有 ResponseMessageId 群体。反馈须匹配租户、Run、原运行 owner 和响应消息，正／负评价分别计数；覆盖率与好评率分别显示。评价为当前读取值，不宣称历史快照；不读取备注。
- 人工纠错率、业务完成率明确“未采集”，不以差评或 `incorrect` 原因替代采集事实。
- P95 仅取已有非负终态耗时的最近秩 `ceil(N × 0.95)`，返回样本数；无样本 null，不补算缺失耗时，不统一解释为模型延迟或含排队耗时。
- 汇总非负实际输入／输出 Token，不用估算 Token 补实际 usage；已终结调用实际 usage 不完整单列。未知调用状态与 Pending／Running 未决调用单列。
- 已终结调用的有效非负 EstimatedCost 按三位大写币种分开展示，0 与未知区分，不换汇、不计预留、不汇总 Run.EstimatedCost，不称为财务账单。
- Run／usage／有效 feedback 分别最多 10,000／50,000／10,000 条，标量投影采用 Take(上限 + 1)；任一越界整体拒绝，要求缩短范围。分页只分页场景组，不从截断样本推算全量。
- 多次读取保留 ObservedFrom／ObservedTo，期间状态、评价或用量可能变化，未使用事务同一时点快照；返回前检查原 Run 未删除／迁出租户及已读取场景元数据仍可读，变化时拒绝并要求重新查询。

## 3. 授权与前端数据清理 [Reviewer]

API 延续 OpenIddict 和原 Permission 策略。Application 入口及返回前均重新读取有效身份、角色、权限、安全戳和活动目标租户；普通身份租户必须与上下文一致。超管必须由当前身份及 fresh role 同时确认，才能读取平台已解析的一个明确目标租户；系统作用域、缺失目标或停用目标均拒绝，不开放全租户聚合或 query 参数选租户。

Run／usage／feedback／scenario 均通过 QueryForTenant 显式约束 TenantId 和非删除条件，关联两侧校验同一租户。响应只包含场景引用、可读名称及聚合，不含用户／Run ID、正文、备注、Prompt、请求响应 JSON、原始使用记录、凭据或错误详情。Controller 不承载业务或数据库查询。

前端身份、权限、目标租户和时间变化清除全部运营数据；请求代次和 identityKey 丢弃迟到响应，KeepAlive 停用及卸载清理、恢复重新请求。原 summary 和新 statistics 的错误独立显示，失败清除本次数据并保留重试；场景名称按纯文本显示，不解释 HTML。

## 4. 数据影响、兼容与回退 [DBA]

**无数据库变更。** 未新增或修改实体、映射、迁移、索引、回填、留存规则或真实配置；没有连接或操作实际业务库。新查询复用现有表和租户／创建时间／关联索引，但是否覆盖、读放大、执行计划及请求性能尚未实测，不能据此宣称正式规模通过。

回退应用只失去新增统计入口，旧汇总／反馈读取可继续使用，不需要结构回退、清表或数据恢复。Application 编译变更可能影响 AIC-005 BuildIdentity／发布资格；正式场景继续遵守冻结、重新评测、版本兼容及原发布门禁，不修改 Hash 或绕过校验。

## 5. 验证了什么 [Developer] [Reviewer]

最终后端构建使用新的独立 `obj/aic012-final` 输出；复用旧输出遇 Windows DLL 写入拒绝后未修改 ACL、删除目录或弱化测试。全量 Unit 使用仅测试进程内的工作区 TEMP／TMP 并恢复变量。报告在忽略目录 `artifacts/aic-012/`。

| 验证 | 实际结果 | 证据／边界 |
| --- | --- | --- |
| 后端解决方案 Release build | 成功，0 error，2 条既有 IntegrationTests ASPDEPR004／008 warning | `dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic012-final -p:UseSharedCompilation=false --nologo` |
| AIC-012 核心／离线 SQL 翻译 | 46/46 通过 | `tests/aic012-core-final.trx`；真实 SQL Server provider ToQueryString，不连接 SQL |
| 全量 UnitTests | 962 项：961 通过、1 失败、0 跳过 | `tests/unit-final.trx`；既有环境失败见下文 |
| 全量 IntegrationTests | 129 项：89 通过、40 环境跳过、0 失败 | `tests/integration-final.trx`；本批 HTTP 7/7、SQL 3 项跳过 |
| Legacy Tests | 45/45 通过 | `tests/legacy-final.trx` |
| 前端全量 Vitest | 18 文件、86/86 通过 | `npm run test:unit`，含本批页面 11 项及 API 1 项 |
| 最后页面口径文案复核 | 本批 2 文件、12/12 通过 | `npm run test:unit -- src/views/ai/operations/index.test.ts src/api/ai-operations.test.ts` |
| 前端类型、lint、production build／包体预算 | 通过，lint 0 error | `npm run build` 包含 vue-tsc；UploadFile.vue 两条既有 default-prop warning、Element Plus 大 chunk 提示保留；预算 `total=1813074`、最大 chunk `910023` bytes |
| 修改前端文件格式 | Prettier check 通过 | API、API test、运营页、页面 test 四文件 |
| AIC-004 原 Offline 回归 | 41 个变体，确定性检查通过 | `evaluations/20261007-070508-77dc59d67954406da72ef66c50fa0b80/report.json`；release gate 仍 pending／failed |
| 文档引用及工作区检查 | 相对文件引用有效，`git diff --check` 通过 | 检查限本批文件，未提交／推送 |

上表报告路径相对 `artifacts/aic-012/`。Offline 直接运行最终构建的 AiEvaluations DLL，以避免原脚本重新构建旧输出；使用原 cases.json，不执行 Live 或付费模型请求。

核心覆盖状态／终态分母、重试、超时子集、历史场景、时间边界、空／缺失值、最近秩、反馈资格冻结／归属、实际和估算分离、多币种、三个容量上限、跨租户／软删除／撤权／安全戳及返回 DTO。HTTP 测试为 TestServer 契约验证，不等于真实 OAuth 端到端。SQL 翻译证据不等于真实约束、并发或执行计划。

唯一全量 Unit 失败为既有 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：在 `X509CertificateLoader.LoadPkcs12` 导入测试证书时 Windows 返回“拒绝访问”，尚未进入 TLS 拒绝断言。工作区 TEMP／TMP 下仍复现；未跳过、修改旧测试或系统权限，不能声称全量 Unit 全绿。

本批三个 SQL 用例仅同时具备 `PERMISSION_SYSTEM_AIC012_SQL_TEST_CONNECTION` 和 `PERMISSION_SYSTEM_AIC012_SQL_TEST_ISOLATED=1` 时运行，要求隔离库迁移已审核应用；夹具使用事务回滚，不自动迁移、不打印连接值。本轮缺条件，全部跳过，不能记为通过。

## 6. 还有什么风险与退出条件 [Reviewer]

| 未完成事项 | 后续责任／条件 |
| --- | --- |
| 隔离 SQL Server 结果、权限变化、软删除与关联验证 | 开发／DBA 在明确隔离库运行本批 3 项及相关回归；补容量边界、并发变化、实际查询计划，不以离线 provider 或 InMemory 代替 |
| Windows 证书导入环境失败 | 环境维护人员查明测试证书导入权限后重跑原测试；未解决前保留失败记录 |
| 真实认证和浏览器体验 | 前端／验收人员验证真实登录、租户切换、撤权、KeepAlive、宽窄屏、空态／失败恢复；本轮仅组件／HTTP 替身验证 |
| 正式数据规模与并发 | Owner／DBA／运维签认容量和延迟目标，测 SQL 扫描／排序／读放大、并发、P95 请求耗时；上限不是压测证明 |
| 原 summary 全量读取 | 本批保持兼容，没有解决旧最多 90 天全量载入的规模风险；需另行评审有界或服务端聚合方案 |
| 业务完成／人工纠错及真实场景价值 | 业务 Owner 提供采集规则、分母、时效和签认；首批继续显示未采集 |
| 同一时点一致性与财务用途 | 当前观测窗口允许期间更新，不用于历史快照或财务对账；真实账单匹配及币种规则另行立项 |
| AIC-005 正式发布兼容 | 发布负责人复核新 BuildIdentity、冻结／评测和资格门禁；Offline 不代表真实模型或人工黄金审核通过 |

Reviewer 对已确认范围代码复核结论：**通过，首批待验收。** 本批未新增外部连接器、外部写入、对账、归档导出或多 Agent；没有模型调用、实际迁移、部署、commit 或 push。真实业务、目标环境和适用发布条件满足前，不将 AIC-012 整体或 P5-C／P5-D 标为完成。
