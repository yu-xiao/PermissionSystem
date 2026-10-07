# AIC-012 方向 5 第二批方案：本人导出凭据查询与本地完整性核对（已确认、已实现，待验收）

> 日期：2026-10-07
> 用户选择第二批“导出凭据查询与完整性核对，沿用技术白名单、不改留存”，随后回复“确认”批准本方案，中断后要求继续。已按确认范围实现，待目标环境验收；下文保留原提案及规划历史，实际实施见第 10 节和[第二批验收记录](ai-center-aic-012-export-batch2-acceptance.md)。
> 依据：[AGENTS.md](../AGENTS.md)、[后续计划](ai-center-next-development-plan.md)、[方向 5 首批方案](ai-center-aic-012-export-implementation-plan.md)、[首批验收记录](ai-center-aic-012-export-acceptance.md)。首批继续待验收，其原证据不作为第二批测试结果。

## 1. 目标与范围 [Architect]

建议第二批让获准导出者在当前明确活动租户中查看**自己发起的技术导出凭据**，选择已有 Prepared 凭据后，在浏览器本地计算所选文件完整原字节的 SHA-256，并与服务端当前可见凭据中的 FileSha256／Bytes 核对。

这是首批准备凭据的可读与可核对能力。结果只表达“所选文件与当前可见准备凭据的字节摘要／长度一致”或“不一致／无法核对”，不表达响应最终获准、浏览器接收成功、数据真实、源记录未变化或法律认证。文件仍沿用首批技术白名单，不增加导出正文、用户／会话明细或模型配置。

本批拟不建设全租户导出审计台、管理员代查他人记录、再次下载／回放、导出文件服务端存储、异步导出、客户端确认送达、文件上传解析、归档介质、法律保留或清理策略。跨操作者审计如后续需要，须单独定义人员范围和专用权限，不能由本批“本人凭据”隐式放开。

## 2. 已查证的基础与缺口 [Architect] [DBA]

| 证据                                                                                                                                                                                       | 已有能力                                                                                                                                      | 第二批设计依据                                                                           |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------- |
| [首批导出服务](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportService.cs)                                                                                               | 专用 Module=`AiTechnicalExport`、Method=`ai:operations:export`、固定 Route；RequestBody 为 v1 最小 JSON，Requested／Prepared／Failed 追加记录 | 只读取这些专用凭据，不把普通 HTTP 操作日志当成导出准备凭据                               |
| [导出 DTO](../backend/PermissionSystem.Application/AiCenter/AiTechnicalExportModels.cs)                                                                                                    | 文件 SchemaVersion=1；最终字节最多 16 MiB；PayloadSha256 与文件 Hash 语义不同                                                                 | 本批与 Prepared 的完整文件 Hash 核对，无需重新序列化 payload 或改变 v1 文件              |
| [OperationLog](../backend/PermissionSystem.Domain/Entities/OperationLog.cs)                                                                                                                | BaseEntity、TenantId、UserId、CreatedAt、软删除、rowversion                                                                                   | 本人及目标租户过滤有既有字段，不新增领域实体                                             |
| [OperationLog 映射](../backend/PermissionSystem.Infrastructure/Configurations/OperationLogConfiguration.cs)                                                                                | RequestBody 上限 4,000 字符；已有 `(TenantId, CreatedAt)` 和 `(TenantId, UserId, CreatedAt)` 索引                                             | 有界按操作者／凭据创建时间读取；ExportId 只在 JSON 中，没有可索引独立列                  |
| [通用日志服务](../backend/PermissionSystem.Application/OperationLogs/OperationLogService.cs)、[日志入口](../backend/PermissionSystem.Api/Controllers/OperationLogController.cs)            | 通用查询使用 `system:operation-log:view`，详情可返回 RequestBody、IP、UserAgent、TraceId 等                                                   | 不代理通用日志接口、不开放其原始详情；另建 AI 本人凭据固定 DTO，原通用日志权限及契约不变 |
| [操作日志中间件](../backend/PermissionSystem.Api/Middlewares/OperationLogMiddleware.cs)                                                                                                    | `/api` 的 GET 也尽力记日志；AI 路径不捕获正文                                                                                                 | 本批读操作沿用该机制，不额外写“Hash 校验成功”或“送达成功”凭据                            |
| [AI 留存](../backend/PermissionSystem.Infrastructure/Ai/AiRetentionHostedService.cs)、[配置类型](../backend/PermissionSystem.Infrastructure/Options/AiCenterOptions.cs)                    | 清理 AI 内容及终态 Run／关联数据，代码默认 AI 审计 180 天；该清理未处理 OperationLogs                                                         | AI Run 留存不能当作凭据的承诺期限；本批不定义或延长 OperationLogs 留存                   |
| [文件存储抽象](../backend/PermissionSystem.Application/Abstractions/IFileStorageService.cs)、[日志归档服务](../backend/PermissionSystem.Infrastructure/Observability/LogArchiveService.cs) | 普通文件保存／读取／删除；Serilog 文件压缩归档                                                                                                | 不具备本批所需的法律保留／AI 关系归档契约，也不需要用于浏览器本地核对                    |

本轮仅静态查证，不读取真实日志、文件、数据库或敏感配置。Owner／正式规模、SQL／Redis／浏览器、下载副本要求及 AIC-005 发布资格仍是首批和实际环境开放条件；选择第二批不代表这些条件已通过。

## 3. 可见范围、权限与异常 [Architect]

### 3.1 本人范围

服务端固定 `OperationLog.UserId == 当前有效调用者 UserId`，并通过 QueryForTenant 限定当前活动目标租户及非删除。请求不接受 TenantId、UserId、UserName、任意 Module／Action／JSON 路径、SQL、排序表达式或外部目标。

拟沿用 `ai:operations:view` + `ai:operations:export` 双权限，不新增权限种子或把通用日志查看权限作为捷径。曾经能导出但现在已撤权的用户不能继续查看凭据。普通身份必须与目标同租户；超管同样只能查看自己在一个 Header／Request 明确活动目标内的凭据，不因超管身份枚举其他操作者或系统作用域。

API 与 Application 双门禁；入口、读取后及返回前复核 fresh 身份／角色／权限／安全戳及活动目标租户。建议小范围提取首批导出访问校验为 `AiTechnicalExportAccessPolicy`，仅由原导出服务和新凭据服务复用，保持首批授权、准入、审计和文件行为不变；不扩展为通用权限框架，不改运营统计服务的其他授权规则。

无效身份 401、无权／停用／系统作用域 403；已获准查询中的不存在／不可见 ExportId 统一 404，不提示其他租户或他人记录是否存在。参数或容量错误 400、源凭据变化 409、限流 429，基础设施故障沿用原异常映射。错误文案固定，不回显 JSON 或异常原文。

### 3.2 固定 DTO

| 响应             | 拟允许的字段                                                                                                                                                                                                                     |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 列表封套         | 当前目标 TenantId、固定 `Scope=CurrentCaller`、实际 ReceiptFrom／ReceiptTo、ObservedFrom／ObservedTo、原始匹配凭据数、不可解析／不支持记录数、核对可用性提示、PagedResult                                                        |
| 每个 Export 摘要 | ExportId、窗口内最早／最近凭据时间、在窗口内观察到的 Requested／Prepared／Failed 记录数、冲突／完整性提示；不得生成“已下载成功”状态                                                                                              |
| 详情             | 仅窗口内该 Export 的安全凭据：不透明 ReceiptId、记录时间、SchemaVersion、固定阶段、From／To、ObservedFrom／ObservedTo、RunCount／UsageCount／Bytes／FileSha256（存在且有效时）、固定 FailureCode；是否可用于本地核对及固定原因码 |

不输出 UserId／UserName、原始 RequestBody／ResponseBody、IP、UserAgent、TraceId、原始 HTTP 日志内容或 Run／usage 明细。JSON 仅用于服务端解析，不能通过展开详情取回原摘要字符串。失败码只显示首批 ErrorCode 和 Cancelled／PreparationTimeout／InfrastructureFailure 固定集合；未知自由字符串不输出。

Requested／Prepared／Failed 展示为分别观察到的事实，不以最高阶段、最新行或 StatusCode=200 推定成功。不按 CreatedAt 相同记录的 Guid 顺序推断业务先后；排序只为稳定展示。Prepared 与 Failed 可同时显示，缺少 Failed 不表示响应成功。

## 4. 查询与解析契约 [Architect]

### 4.1 拟新增接口

| API                                                                | 输入／输出                                                                                                   |
| ------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------ |
| `GET /api/ai/operations/technical-export-receipts`                 | From／To 为凭据记录时间窗口，PageIndex／PageSize；返回 ApiResult 包含范围／诊断字段及 PagedResult 的摘要封套 |
| `GET /api/ai/operations/technical-export-receipts/{exportId:guid}` | From／To 为同一凭据窗口；返回 ApiResult 的安全详情；没有文件或重新导出响应                                   |

From／To 按 **OperationLog.CreatedAt**，左闭右开，默认过去 30 天、最多 90 天，To 最多未来 5 分钟。它不同于首批文件内的 Run 创建时间范围；页面和 DTO 用独立“凭据记录时间”说明，不能套用运营页原“运行创建时间”筛选。

列表返回实际采用的固定窗口；详情和核对重读传回该 From／To，不能每次重新采用默认 now 导致群体悄悄变化。分页建议默认 20、最大 50；DTO 只接受上述分页字段，不开放任意排序，页码计算使用受检或 long 运算避免溢出。摘要按窗口内最近记录时间降序／ExportId 固定排序。

### 4.2 有界读及历史兼容

建议一次查询最多读取 **1,000 条匹配的专用凭据**，Take(1001)，越界整体拒绝并要求缩短区间，不先截断再声称完整总数。SQL 先限定 TenantId、当前 actor、CreatedAt、固定 Module／Method／Route／RequestMethod，且不预先过滤 Action，以便发现同一专用来源中的未知阶段。标量投影只包含解析／输出及复核所需字段。

在 Application 内逐条解析长度 ≤ 4,000 的摘要；JSON 深度建议上限 8。仅支持已定义的 v1 固定字段和语义，核验 ExportId 非空、JSON Outcome 与行 Action 一致、Purpose／Recipient 固定值、时间顺序、首批数量／字节上限以及 Prepared SHA-256 为 64 位十六进制。重复属性、类型不符、未知版本／阶段、无效 Hash、冲突字段均不得用于核对；额外字段不回显或执行。历史合法 v1 记录兼容读取，不回填旧行。

合法记录按 ExportId 归组后分页，TotalCount 仅为窗口内**可解析 Export 分组数**。不支持／损坏行另返回计数和固定警告；窗口内有无法归属的行时，不能把零分组解释成“从未导出”，建议保守地禁用本窗口所有本地凭据核对，要求缩短窗口或由原审计流程复核，不自动修正历史审计。

同 Export 在窗口内的摘要范围／固定语义冲突、Prepared 缺失或 Prepared 不止一条，均提示不可核对，不按“最新一条”挑选。核对要求本窗口可解释、目标 Export 有唯一有效 Prepared；Requested 或 Failed 位于窗口外仍可能存在，缺失只表示当前窗口未观察到。页面固定提示“只展示该时间窗口内仍可读的凭据，非完整历史”。

读取及返回前对有界群体再核查租户、actor、非删除、记录标识及投影字段；发现删除、修改、迁移或窗口群体变化时拒绝响应并要求重查。不承诺事务历史快照或抗数据库管理员篡改。已有索引可支持租户／actor／时间筛选，但 Take 只限制结果量，不证明扫描／排序成本已达标。

建议凭据只读接口复用既有限流抽象：每 actor 每分钟 30 次、每目标租户每分钟 60 次，应用读取截止 10 秒，CancellationToken 贯穿。两接口共用凭据读取限流，不消耗首批导出 2／10 次额度，不持有导出准备锁、不缓存敏感成功 DTO；这些是待确认保护值，不是正式吞吐或延迟目标。API 返回 NoStore。

## 5. 本地文件核对与页面 [Architect]

在原运营页新增“我的导出凭据”入口，建议独立子组件承载列表／独立凭据时间筛选／分页／详情和核对，避免继续扩大原运营指标实现。入口及请求要求 view + export 双权限；不为只具备 view 的用户自动加载凭据。

1. 用户主动选定凭据，只有唯一有效 Prepared 时启用“核对本地文件”。选择控件一次只接收一个文件，最多 16 MiB，与首批限制一致；拒绝空文件及超限，`.json` 仅为选择提示。
2. 通过浏览器 `File.arrayBuffer()` 与 `crypto.subtle.digest('SHA-256', 原字节)` 计算完整文件 Hash，不解析 JSON／payload、不解释 HTML、不根据文件名或文件内容决定租户／ExportId，不重新序列化，不计算新的业务结算。
3. Hash 完成后使用同一 ExportId 和实际凭据窗口重新获取详情并复核当前可核对性。对比所选 ReceiptId、SchemaVersion、Bytes、FileSha256 和范围等相关事实；变化、删除、撤权或窗口不可解释时清除结果，要求重新选择。
4. 字节数及 Hash 均相同显示“与当前可见准备凭据一致”；否则显示“不一致”。同时保留 Prepared 非送达、Hash 非签名及当前窗口非完整历史说明；既有 Failed 事实仍单独展示，Hash 匹配不能覆盖它。
5. 身份／权限／目标租户／凭据时间／选定记录／文件变化，KeepAlive 停用、关闭及卸载时取消请求并清理文件、缓冲区引用、详情及核对结果。Web Crypto 无取消 API，以代次和身份键丢弃迟到结果；不声称能立即清零浏览器内部内存。

文件原字节和本地计算 Hash 均不上传；只发生凭据 GET 请求。无 FileStorage、第三方、模型调用、localStorage／IndexedDB／Pinia 内容持久化或 Hash 结果服务端凭据。HTTPS 安全上下文且 Web Crypto 可用是正式使用前提；不可用时明确提示“当前环境无法进行本地核对”，不增加不可信 CDN 或非标准哈希降级。

HTTP GET 的既有中间件日志只证明凭据读取请求结果，不能证明用户计算 Hash、文件匹配或接收成功。本批不引入客户端“已收文件”回执，更不将其升级为法律证明。

## 6. 数据影响、性能与回退 [DBA]

**建议无数据库结构或迁移变更。** 本轮规划无数据库变更；实施拟复用 OperationLogs、原租户／actor／时间索引和既有凭据，不新增表、列、计算列、索引、回填、权限／菜单种子、文件存储或保留规则。读操作仍会通过既有中间件尽力新增普通操作日志，其存储和权限保持原机制。

ExportId 位于 JSON 内，所以本批接受显式有界窗口读取／解析；不编造已有 JSON 索引或跨窗口直接定位能力。若实际查询计划／规模证明不能满足目标，返回 Architect／DBA 独立提出最小索引或规范凭据实体方案，再确认迁移，不临时改生产库或扩大读取上限。

无历史摘要批量修补、硬删除或重写。OperationLogs 软删除、修改或留存导致凭据缺失时显示不可用，不延长留存、不从 Run 重造“原文件”或以重新导出替代原字节。回退应用撤销新入口即可，原导出和已保存准备凭据继续保持；不执行清表或结构 Down。

首批已确认的数据库写保护、单租户授权及文件契约不得改变。提取访问策略属于 Application 编译变化，实施后继续 AIC-005 BuildIdentity／冻结／评测／资格门禁；不得绕过发布限制。

## 7. 预计文件与实施顺序 [Developer]

以下路径为**方案确认时预计新增／修改**；确认后已按此范围实施，实际证据见第 10 节。

| 范围             | 预计变更                                                                                                                                                                                                                  |
| ---------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application      | 新增 `AiCenter/AiTechnicalExportReceiptModels.cs`、`AiTechnicalExportReceiptService.cs`、`AiTechnicalExportAccessPolicy.cs`；最小修改原 `AiTechnicalExportService.cs` 的访问校验复用与 DI；不改文件 SchemaVersion／白名单 |
| API              | 在 `Controllers/AiOperationsController.cs` 增加两条双权限 GET、NoStore、DTO 和 CancellationToken；Controller 不查询数据库                                                                                                 |
| 前端             | 修改 `src/api/ai.ts`、`src/views/ai/operations/index.vue`；新增 `src/views/ai/operations/AiTechnicalExportReceipts.vue`、`src/utils/aiTechnicalExportIntegrity.ts`，复用原 UI／请求／状态模式，无新依赖                   |
| UnitTests        | 新增 `AiCenter/Aic012TechnicalExportReceiptTests.cs`、`AiCenter/Aic012TechnicalExportReceiptSqlTranslationTests.cs`；补访问策略测试，原首批测试夹具适配新构造依赖                                                         |
| IntegrationTests | 新增 `AiCenter/Aic012TechnicalExportReceiptApiTests.cs`、`AiCenter/Aic012TechnicalExportReceiptSqlTests.cs`；复用现有合成／显式隔离夹具，原 HTTP／SQL 导出构造依赖最小适配                                                |
| 前端测试         | 补 `src/api/ai-operations.test.ts`、原页面测试；新增子组件及本地 Hash 测试                                                                                                                                                |
| 文档             | 本方案、后续／总体计划、AIC-012 批次登记；实施后新增第二批验收记录，保留首批证据                                                                                                                                          |

后端路径分别位于既有 `backend/PermissionSystem.Application`／`PermissionSystem.Api`／`PermissionSystem.UnitTests`／`PermissionSystem.IntegrationTests`；前端路径位于 `frontend/permission-admin`。

顺序：确认本方案 → 小范围复用访问校验并实现有界凭据解析／查询 → HTTP 契约 → 子组件及本地 Hash → 核心／跨租户／异常测试 → Reviewer 与文档闭环。没有后台 Worker、消息队列、新通用日志框架或新的认证权限体系。

## 8. 测试与验收 [Reviewer]

| 验证层               | 必须覆盖／退出条件                                                                                                                                                                                                                     |
| -------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 核心语义             | 合法历史 v1、空数据、Requested／Prepared／Failed 并存、窗口边界、不完整历史、重复／冲突 Prepared、损坏／未知 schema／阶段／失败字符串／重复 JSON 属性、不可解释窗口、计数／Hash／时间字段验证、稳定分组分页、1,000／1,001 边界及无截断 |
| 权限／来源变化       | 只有 view／只有 export、撤权／安全戳／停用、他人／跨租户／软删除、超管明确目标／本人限制／系统拒绝、返回前删除或摘要更改、两个 GET 的 404 不泄漏其他记录存在                                                                           |
| SQL provider／隔离库 | 离线翻译证明 TenantId／UserId／CreatedAt／固定来源／Take／标量投影；显式隔离 SQL 合成凭据验证实际结果、actor 限制、修改／软删除、原索引查询计划、边界规模；缺环境跳过不能记通过，不自动迁移                                            |
| HTTP                 | 双权限、日期／分页 DTO、NoStore、固定安全 DTO、不输出原 RequestBody、无文件上传或下载响应，首批 POST 保持原契约                                                                                                                        |
| 前端／Hash           | 对确定字节使用标准 SHA-256 向量、不同字节及空／超限；文件不解析／上传、不可信文件名不选 ExportId；核对后重新读取凭据、权限／租户／文件／窗口变化及迟到结果丢弃、Web Crypto 不可用、KeepAlive／关闭／卸载清理、失败可恢复               |
| 回归                 | 首批导出 42 核心／翻译与 6 HTTP 及方向 4 原回归必须继续满足；全量后端、Legacy、前端 Vitest／类型／lint／production build／格式及 AIC-004 Offline，报告使用独立新产物；不弱化既有 Windows 证书失败                                      |
| 目标环境             | 首批 SQL／Redis／浏览器／正式规模／Owner 条件与第二批索引成本／HTTPS Web Crypto／真实认证浏览器核对分别签认；Offline 不能代替 AIC-005 发布资格                                                                                         |

规划阶段未执行 build／test，不使用首批 42／6／101 通过证明新能力已通过。确认后已生成第二批独立证据，见第 10 节。整批 SQL／Redis 和真实浏览器条件仍需明确受控环境，不提供或打印凭据／连接串。

## 9. 原提案确认项与规划历史 [Architect] [DBA] [Reviewer]

已确认具体范围：**只查当前目标租户内本人凭据；沿用 view + export；固定安全 DTO；独立凭据时间窗口；最多 1,000 行／分页 20 至 50／读取限流 30 与 60／截止 10 秒；浏览器本地完整文件 SHA-256 与唯一有效 Prepared 核对，不上传文件；不加表／迁移／权限种子、不改留存。**损坏／未知记录导致窗口不可解释时禁用核对，不按最新 Prepared 猜测、不宣称送达或法律证明。

最初的方向选择只确定规划主题，当时未视为新增可见范围和保护参数的实施确认。用户随后回复“确认”批准具体方案，按 [AGENTS.md](../AGENTS.md) 工作流进入实现；同一范围未重复询问。

原规划轮改动：新增本规划及任务／总体状态登记，保留首批和方向 4 所有已有改动。原规划验证：静态读取已有凭据写入、通用日志权限、映射／索引、留存／文件存储和页面模式；当时四份文档的 102 个相对文件引用有效，新增方案格式及 `git diff --check` 通过，保留原文档合法 Markdown 硬换行。原规划风险：具体范围当时待确认；首批环境验收、Owner、下载副本要求及正式发布资格未完成；归档／法律保留仍需介质、期限、删除／恢复规则和责任人，未进入本批。

Reviewer 原规划复核结论：**通过，可提交确认；不是功能／环境验收通过。** 当时状态为“运营首批待验收；导出首批待验收；凭据第二批待确认”，后续状态见下节，AIC-012 及方向 5 整体未完成。

## 10. 确认后实施与复核 [Architect] [DBA] [Developer] [Reviewer]

已完成共用导出访问策略、本人凭据固定 DTO／两条 GET、有界窗口双读及严格 v1 解析、原运营页独立凭据组件与浏览器本地完整原字节 SHA-256 核对。Hash 后重读同 Export／同窗口并比较安全凭据和阶段事实；关闭、权限／目标／窗口／文件变化及 KeepAlive／卸载清理状态。没有上传文件／Hash、再次下载、模型调用或留存变更。

DBA 结论：**无数据库结构、迁移或新增权限／菜单种子变更**；读取沿用中间件普通日志，没有执行实际宿主初始化、真实业务导出、迁移或清理。

第二批核心及离线 SQL 翻译 49/49，AIC-012 三批核心 137/137、HTTP 共 19 项通过；全量 Unit 1052 通过、1 项既有 Windows 证书导入失败，全量集成 101 通过、46 环境跳过（本批 SQL 3 项）。Legacy 45/45、前端全量 127/127、类型／production build／预算／格式通过，lint 0 error、4 warning；Offline 41 个变体确定性检查通过、发布门禁仍 pending/failed。

Reviewer 已确认代码范围复核：**通过，第二批待验收**。完整证据和 SQL／Redis／真实认证浏览器／规模／Owner／证书失败／AIC-005 发布资格退出条件见[独立第二批验收记录](ai-center-aic-012-export-batch2-acceptance.md)。当前状态为“运营首批待验收；导出首批待验收；凭据第二批待验收；其余方向暂缓／待启动”；不将方向 5 或整个 AIC-012 标为完成。
