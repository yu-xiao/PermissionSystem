# AIC-012 方向 5 第二批：本人导出凭据与本地完整性核对实施／验收记录

> 后续规划（2026-10-07）：用户选择先规划本批与导出首批的[联合环境验收](ai-center-aic-012-export-environment-acceptance-plan.md)。规划已完成，实测未执行；下文原验证结果、失败／跳过证据及待验收状态保留。

> 日期：2026-10-07
> 状态：用户已回复“确认”批准[第二批具体方案](ai-center-aic-012-export-batch2-implementation-plan.md)，中断后要求继续；第二批已实现、待验收。运营首批、导出首批仍待验收，AIC-012 整体未完成。
> 依据：[AGENTS.md](../AGENTS.md)、[后续计划](ai-center-next-development-plan.md)、[导出首批验收](ai-center-aic-012-export-acceptance.md)。本记录独立登记第二批证据，不覆盖首批历史结果；代码复核通过不等于目标环境或发布验收通过。

## 1. 改了什么 [Architect] [Developer]

在原运营页增加主动打开的“我的导出凭据”。只读取当前明确活动单租户内**当前调用者本人**的专用导出凭据，分别展示 Requested／Prepared／Failed 事实。唯一有效 Prepared 可以与用户选择的本地文件原字节 SHA-256 和长度核对；Hash 完成后重读同一 ExportId、同一凭据窗口，凭据或阶段事实变化即丢弃结果。

| 模块        | 实际实现                                                                                                                                                                                                                                                                                                                                                                                                                                                         |
| ----------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application | [共用访问策略](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportAccessPolicy.cs)、[安全 DTO](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportReceiptModels.cs)、[凭据服务](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportReceiptService.cs)及 DI；原导出服务复用策略，保留首批文件、审计和准入流程                                                                                                        |
| API         | [AiOperationsController](../backend/PermissionSystem.Api/Controllers/AiOperationsController.cs) 新增列表／详情两个 GET，双权限、NoStore、ApiResult、安全固定 DTO、CancellationToken；Controller 不查数据库                                                                                                                                                                                                                                                       |
| 前端        | [API](../frontend/permission-admin/src/api/ai.ts)、[运营页](../frontend/permission-admin/src/views/ai/operations/index.vue)、[独立凭据组件](../frontend/permission-admin/src/views/ai/operations/AiTechnicalExportReceipts.vue)、[本地 Hash](../frontend/permission-admin/src/utils/aiTechnicalExportIntegrity.ts)；沿用原 Axios、Pinia、Element Plus 和主题，无新依赖                                                                                           |
| 核心验证    | [核心测试](../backend/PermissionSystem.UnitTests/AiCenter/Aic012TechnicalExportReceiptTests.cs)、[SQL 离线翻译](../backend/PermissionSystem.UnitTests/AiCenter/Aic012TechnicalExportReceiptSqlTranslationTests.cs)、[HTTP](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012TechnicalExportReceiptApiTests.cs)、[条件隔离 SQL](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012TechnicalExportReceiptSqlTests.cs)；原批次构造夹具最小适配 |
| 前端验证    | [真实 Web Crypto 测试](../frontend/permission-admin/src/utils/aiTechnicalExportIntegrity.test.ts)、[组件测试](../frontend/permission-admin/src/views/ai/operations/AiTechnicalExportReceipts.test.ts)、原页面入口和 API 回归                                                                                                                                                                                                                                     |

## 2. 授权、查询与核对语义 [Reviewer]

复用 `ai:operations:view` + `ai:operations:export`，API 与 Application 双门禁；入口、读取后及返回前 fresh 核验身份、角色、权限、安全戳和活动目标。普通身份与目标同租户，超管也只能在 Header／Request 明确指定的活动目标内查本人；拒绝系统作用域、停用目标或失效身份。不存在、他人或跨租户 ExportId 均为固定 404，不提示其存在。

QueryForTenant 限制租户与非删除，再限定当前 UserId、CreatedAt、固定 Module／Method／Route／POST。两次独立构造有界查询，比较匹配群体的记录 ID、记录时间、阶段和原摘要，发现删除、修改或新增即拒绝返回。只投影必要标量，不加载／输出完整实体；不输出用户信息、原 RequestBody、ResponseBody、IP、UserAgent 或 TraceId。

凭据时间按 OperationLog.CreatedAt 左闭右开，默认 30 天、最多 90 天、结束最多未来 5 分钟，与 Run 创建时间独立。每次 Take(1001)，超过 1,000 条整体拒绝；合法 Export 分组后分页，默认 20、最大 50。两接口共用 actor 30/min、tenant 60/min 读取限流及 10 秒截止，不占首批导出额度或准备锁。

摘要限 4,000 字符、JSON 深度 8，只支持合法 v1。验证重复属性、类型、固定用途／接收方、阶段与 Action、时间、数量、字节数及 Hash。未知字段不回显；损坏／未知记录计入不可解析数并禁用整个窗口核对。同一 Export 的范围冲突、Prepared 缺失或重复均不能核对，不选择“最新一条”。窗口缺失阶段只表示未观察到；Prepared 和 Failed 可同时展示。

本地核对要求 HTTPS 安全上下文和 Web Crypto，文件非空且最多 16 MiB。仅使用 `File.arrayBuffer()` 的完整原字节，既不解析 JSON，也不根据文件名推断 ExportId；**文件和本地 Hash 均不上传**。Hash 完成后的 GET 对比目标租户、实际凭据窗口、Export 摘要、完整安全阶段事实和选定 Prepared。身份／权限／租户／窗口／文件／记录变化、关闭、KeepAlive 停用及卸载均取消读取并清理状态，代次检查丢弃迟到结果。

匹配文案只表达“与当前可见准备凭据一致”。Prepared 不证明最终响应获准或浏览器收到，Hash 不是签名或法律认证，匹配不能覆盖失败事实。查询不是事务历史快照，也不防数据库管理员篡改；Web Crypto 无取消 API，清理引用不保证立即清零浏览器内部内存。本批没有文件服务端存储、再次下载、上传、模型请求或客户端送达回执。

## 3. 数据影响与回退 [DBA]

**无数据库结构、迁移或新增权限／菜单种子变更。** 复用 OperationLogs 及原租户／actor／时间索引，不新增表、列、索引、计算列、回填、文件介质或留存规则。GET 沿用既有中间件普通操作日志，不新增“Hash 已验证”或“文件已送达”专用凭据。

ExportId 仍位于 JSON，接受有界时间窗口解析；Take 只限制返回量，不能证明 SQL 扫描／排序成本合格。OperationLogs 缺失、软删除或留存变化不能从 Run 重造历史准备凭据。回退撤销本批应用入口，不清表、不重写审计。首批文件 v1／白名单及审计语义不变；未实际迁移、初始化宿主、导出业务数据、清理或部署。

## 4. 验证了什么 [Developer] [Reviewer]

后端使用独立 `obj/aic012-receipts-check2` 产物，测试 `--no-build`；没有改 DLL 权限／ACL 或删除旧目录。全量 Unit 的 TEMP／TMP 仅对测试进程设为工作区路径并在 finally 恢复，保留实际退出码。后端报告相对 `artifacts/aic-012/receipts/`。

| 验证                             | 实际结果                                        | 证据／限制                                                                                                                                |
| -------------------------------- | ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- |
| 后端 Release build               | 成功，0 error、2 条既有 ASPDEPR004／008 warning | `dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic012-receipts-check2 -p:UseSharedCompilation=false --nologo` |
| 第二批核心／离线 SQL 翻译        | 49/49 通过                                      | `tests/receipts-core-final.trx`；核心 48 + 翻译 1，未连接 SQL                                                                             |
| AIC-012 三批核心回归             | 137/137 通过                                    | `tests/aic012-core-check2.trx`；运营 46、导出 42、凭据 49                                                                                 |
| 全量 UnitTests                   | 1053 项：1052 通过、1 失败、0 跳过              | `tests/unit-final.trx`；既有 Windows 证书导入失败见下文                                                                                   |
| 全量 IntegrationTests            | 147 项：101 通过、46 环境跳过、0 失败           | `tests/integration-final.trx`；第二批 HTTP 6/6，本批 SQL 3 项跳过；三批 HTTP 共 19 项通过                                                 |
| Legacy Tests                     | 45/45 通过                                      | `tests/legacy-final.trx`                                                                                                                  |
| 前端全量 Vitest                  | 20 文件、127/127 通过                           | `npm run test:unit`；Hash 6 项使用真实 Node Web Crypto，组件 18 项，另含 API／原页面回归                                                  |
| 前端类型／production build／预算 | 通过                                            | `npm run build` 含 vue-tsc；budget total `1826441`、最大 chunk `910023` bytes，保留既有 Element Plus 大 chunk 提示                        |
| 前端 lint                        | 0 error、4 warning                              | `npm run lint`；UploadFile.vue 原两条 default-prop、运营测试内 KeepAlive 宿主两条 one-component-per-file                                  |
| 修改前端文件格式                 | 八文件 Prettier check 通过                      | API、API test、Hash、Hash test、凭据组件／test、原页面／test                                                                              |
| AIC-004 Offline 回归             | 41 个变体确定性检查通过                         | `evaluations/20261007-090818-b9288e23957f48ad8ef89c13c8ade156/report.json`；release gate 仍 pending/failed                                |

前端测试夹具曾因 Node 类型全局引用影响应用 timer 类型、以及 Node／jsdom 的 ArrayBuffer 环境差异而失败，已限制为 Node 字节测试、移除全局 Node 类型引用并适配公共存储清理；标准 SHA-256 向量及精确 16 MiB 边界仍用真实 digest，无哈希替身或弱化断言。修复后全量 Vitest、类型／构建、lint 与格式通过。

核心覆盖本人／跨租户／软删除／固定来源过滤、真实首批 v1、三阶段并存、半开窗口、稳定分页／大页码、1,000／1,001 边界、损坏／未知版本／阶段／重复属性／无效字段、缺失／重复／冲突 Prepared、双权限／fresh 撤权／安全戳／活动目标、超管明确目标、读取期间来源变化、限流／取消／截止。前端覆盖原字节、文件边界、安全上下文、核对后重读、阶段变化、撤权／目标／文件／窗口变化、迟到结果、关闭／KeepAlive／卸载和失败恢复。

全量 Unit 唯一失败仍为 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：Windows `X509CertificateLoader.LoadPkcs12` 导入合成证书时“拒绝访问”，尚未执行 TLS 拒绝断言。工作区 TEMP／TMP 下仍复现；未跳过、改 ACL 或弱化测试，不能宣称全量全绿。

第二批 3 个 SQL 用例复用 `PERMISSION_SYSTEM_AIC012_SQL_TEST_CONNECTION` 与 `PERMISSION_SYSTEM_AIC012_SQL_TEST_ISOLATED=1` 显式条件，要求已审核迁移的隔离库、合成数据和外层事务回滚。本环境缺条件，全部跳过；不打印连接值、不自动迁移。三批 SQL 共 9 项跳过。HTTP 为 TestServer 契约；限流替身不等于 Redis 多实例验证，Node Web Crypto 与组件测试不等于真实浏览器端到端。

Offline 使用原 cases 和本批编译 DLL，没有 Live、真实模型或外部业务请求；确定性通过不替代 AIC-005 的人工审核、重新冻结／评测或发布资格。文档同步本方案、后续计划、总体计划与 AIC-012 批次登记；8 份相关文档的 165 个相对文件引用有效，第二批方案／验收 Prettier check 与 `git diff --check` 通过。未 commit 或 push。

## 5. 还有什么风险与退出条件 [Reviewer]

| 未完成事项                | 责任／退出条件                                                                                                                                        |
| ------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 隔离 SQL 与查询成本       | 开发／DBA 运行本批 3 项及三批回归，验收真实结果、租户／actor 限制、撤权／修改／软删除、1,000／1,001 规模与实际原索引查询计划；不能用离线翻译替代      |
| Redis 读取限流与故障      | 运维／开发验证两 GET 共用 actor／tenant 限流、跨实例一致性、故障／取消／10 秒截止，以及首批导出准入仍独立                                             |
| 真实认证浏览器            | 前端／验收在 HTTPS 和真实 OpenIddict 身份下验证双权限、本地真实 File Hash、同一凭据重读、撤权／租户切换／KeepAlive／取消、宽窄屏和确无文件／Hash 上传 |
| Owner／正式规模／副本规则 | 业务／安全／运维签认获权人员、用途、可接受规模／延迟、客户端下载副本及凭据仍可读期限；本批不定义 OperationLogs 保留承诺                               |
| 原证书测试失败            | 环境维护人员查明 Windows 证书导入拒绝原因并重跑原用例；解决前保留失败证据                                                                             |
| 首批与发布资格            | 首批 SQL／Redis 锁及浏览器下载验收继续独立完成；发布负责人按新 Application BuildIdentity 重新冻结／评测和兼容核验，不绕过 AIC-005                     |
| 归档／法律保留            | 独立明确介质、期限、删除／恢复、副本和责任人后再立项；当前日志不是 WORM，Hash 非签名，Prepared 非送达                                                 |

Reviewer 对已确认代码范围结论：**通过，凭据第二批待验收。** 当前整体登记为“运营首批待验收；导出首批待验收；凭据第二批待验收；其余方向暂缓／待启动”，不将 AIC-012、方向 5 或 P5-C／P5-D／P5-E 标为整体完成。
