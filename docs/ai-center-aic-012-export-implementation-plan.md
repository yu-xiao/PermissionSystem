# AIC-012 方向 5 首批方案：AI 运行技术元数据受控导出（已实现，待验收）

> 日期：2026-10-07
> 用户已选择首批“AI 运行技术元数据受控导出，不含正文、不改留存”，并回复“确认”批准本方案；首批已实现、待验收。实际实现、验证与剩余条件见 [导出验收记录](ai-center-aic-012-export-acceptance.md)。下文保留确认时的具体提案，实施记录见第 9 节。
> 依据：[AGENTS.md](../AGENTS.md)、[后续开发计划第 8 节](ai-center-next-development-plan.md#8-下一步及长期规划aic-012)、[AIC-012 分批方案](ai-center-aic-012-implementation-plan.md)。方向 4 的已实现代码和验证记录保留，方向 5 单独确认、实现与验收。

## 1. 首批目标与尚缺业务信息 [Architect]

首批用途固定为内部技术复核，接收人固定为当前获准调用者；导出当前明确活动租户中、选定创建时间窗口内仍可读取的 Run 及关联模型 usage 技术元数据。文件为 UTF-8 JSON，由本次已授权请求直接下载；不通过聊天、公开链接、邮件、Webhook 或第三方传输。

没有指定正式合规责任人、法规／法律保留期限、接收方保存及删除要求、正式数据规模和性能目标。这些缺口不以代码默认值补齐；本批只交付受控技术导出，不宣称法律合规认证或长期归档完成。实际环境开放前由 Owner／安全／运维签认用途、授权人员和下载副本处理要求。

分层归档后续须另行明确归档对象／介质／冷热期限／加密与访问、法律保留优先级、校验及恢复、到期删除和证明。在这些规则确定前，本批不修改现有到期清理，不通过先备份再删库隐式实施归档。

## 2. 已查证的复用基础 [Architect] [DBA]

| 现有能力 | 代码证据 | 本批适用边界 |
| --- | --- | --- |
| AI 到期处理 | [AiRetentionHostedService](../backend/PermissionSystem.Infrastructure/Ai/AiRetentionHostedService.cs) | 现为每日事务性清理；旧内容置为 expired，审计到期后删除终结 Run 及关联数据。不是归档或法律保留机制，不运行／改动该服务 |
| 留存配置 | [AiCenterOptions](../backend/PermissionSystem.Infrastructure/Options/AiCenterOptions.cs) | 代码默认会话 30 天、审计 180 天、草稿 30 天；不是实际部署配置或法定期限，也不等于 OperationLogs 的留存规则 |
| 日志文件归档 | [LogArchiveService](../backend/PermissionSystem.Infrastructure/Observability/LogArchiveService.cs) | 处理 Serilog .log/.gz 文件，不具备 AI 关系数据、法律保留、受控恢复契约，不直接套用 |
| 单租户运营授权 | [AiScenarioOperationsService](../backend/PermissionSystem.Application/AiCenter/AiScenarioOperationsService.cs) | 可复用当前身份／fresh role／权限／安全戳／活动目标租户复核模式；新导出另加专用权限，不重定义 OpenIddict 或权限计算 |
| 通用审计 | [OperationLog](../backend/PermissionSystem.Domain/Entities/OperationLog.cs)、[OperationLogService](../backend/PermissionSystem.Application/OperationLogs/OperationLogService.cs)、[OperationLogMiddleware](../backend/PermissionSystem.Api/Middlewares/OperationLogMiddleware.cs) | OperationLogs 可保存最小凭据；中间件在响应后尽力写入且 AI 路径不捕获正文，不足以作为导出前审计成功的保证 |
| 报表与文件下载 | [ReportController](../backend/PermissionSystem.Api/Controllers/ReportController.cs)、[ReportService](../backend/PermissionSystem.Application/Reports/ReportService.cs)、[FileController](../backend/PermissionSystem.Api/Controllers/FileController.cs)、[IFileStorageService](../backend/PermissionSystem.Application/Abstractions/IFileStorageService.cs) | 已有权限和下载模式；报表仍为有界同步导出，文件存储没有归档保留契约，不伪称已有异步导出治理 |
| 容量治理 | [IDistributedRateLimitService](../backend/PermissionSystem.Application/Abstractions/IDistributedRateLimitService.cs)、[IDistributedLock](../backend/PermissionSystem.Application/Abstractions/IDistributedLock.cs) | 复用既有实现；Memory 不保证多实例隔离，多实例环境需既有 Redis 配置与验证，不新增框架 |
| 权限种子 | [SeedDataInitializer](../backend/PermissionSystem.Infrastructure/SeedData/SeedDataInitializer.cs)、[TenantInitializationJob](../backend/PermissionSystem.Application/Tenants/TenantInitializationJob.cs) | 默认平台租户及新租户初始化分别维护权限清单，必须同步；已有活动租户不会因修改新租户清单自动获权 |

本轮只静态查证代码，不读取真实会话、usage、日志记录、敏感配置或连接串，不启动宿主、下载数据或执行清理／迁移。

## 3. 导出数据契约 [Architect]

### 3.1 请求与统计群体

拟新增 `POST /api/ai/operations/technical-export`。请求 DTO 只接受 From、To；用途和接收人由服务端固定，不接收用户／租户 ID、任意字段、SQL、排序表达式、路径、存储目标、邮箱或外部 URL。默认过去 30 天、最大 90 天，结束不超过当前时间 5 分钟，左闭右开，以 `Run.CreatedAt` 选群体；输入带偏移时间，JSON 时间保持明确偏移语义。

读取当前租户非删除 Run；跨版本保留场景及版本不透明引用，不从正文推断归属。关联 usage 同时约束两侧 TenantId、非删除和固定 Run 群体。包含 Pending／Running，以状态表达未决，不宣称所有记录已结算。首批不读 feedback、tool invocation、知识文档引用或单据执行记录。

### 3.2 固定字段白名单

以下为拟批准字段，投影只读取必要标量，不加载完整实体或关联内容。

| 部分 | 字段 |
| --- | --- |
| Manifest | SchemaVersion、ExportId、目标 TenantId、固定 Purpose=`TechnicalReview`、Recipient=`CurrentCaller`、From／To、ObservedFrom／ObservedTo、RunCount／UsageCount、上限、PayloadSha256；注明观察窗口、未知值与估算性质 |
| Run | Id、ScenarioId／ScenarioVersionId（可空）、CreatedAt、StartedAt／CompletedAt（可空）、Status、DurationMilliseconds（可空）、FallbackCount |
| Usage | Id、RunId、CreatedAt、Sequence、Round、Attempt、RouteRole、Status、StartedAt／CompletedAt／DurationMilliseconds（可空）、InputTokens／OutputTokens、EstimatedInputTokens／EstimatedOutputTokens、已终结且有效的 EstimatedCost／PricingCurrency，以及固定未知／未决标记 |

不导出运行 owner／用户名／用户 ID、会话／消息 ID或正文、标题、反馈内容、Prompt、工具参数／结果／摘要／引用、配置快照、请求响应 JSON、供应商请求 ID、TraceId、会话／安全戳／租约字段、地址、凭据或运行错误详情。Agent／Model／Provider 名称及任意错误字符串不在本批白名单。场景只导出引用，不额外枚举名称或版本快照。

状态／路由只用代码固定名称，未知值独立标记，不把数据库自由字符串写入固定语义字段。负耗时／负 Token 等无效量表示 null 并标记，不补为实际 0。实际与估算 Token 分开；费用只按调用自身币种表达，金额／币种无效为未知，未决调用不输出预留或已结算费用。不使用 Run.EstimatedCost 或 Run Token 替代调用记录，不换汇，也不计算供应商真实账单。

Run 固定按 CreatedAt／Id 排序，usage 按 RunId／Sequence／Id 排序；文件没有客户端任意分页或隐含截断。无记录返回有效空数组和零计数。Manifest 的目标租户引用用于副本范围识别；导出操作者只保存在服务端审计，文件不含其用户 ID。

### 3.3 完整性与一致性

Manifest 的 PayloadSha256 为固定 JSON 序列化契约下、紧凑 UTF-8 `payload` 对象字节的 SHA-256；文件保持同一段 payload 字节，避免 Hash 自引用或重新序列化漂移。专用审计保存最终完整文件的 SHA-256、字节数、ExportId 和数量，不保存文件原文。

Hash 仅校验字节完整性，不是签名、来源认证、不可篡改审计或法律证明。JSON Manifest 与 payload SchemaVersion 同步升级，不暴露可自定义序列化选项。

先固定 Run 集合，再读取关联 usage，记录观测开始／结束；数据期间可能更新，不承诺 SQL 同一时点快照。准备完毕及返回前检查已选 Run／usage 仍属目标租户、非删除且关联仍有效；发生变化拒绝本次下载，不返回半个文件。首次读取已不存在的历史数据不能恢复，不保证导出覆盖既有清理已经删除的记录。

## 4. 授权、审计及异常 [Architect]

### 4.1 权限与下载

新增专用 `ai:operations:export`，同时要求原 `ai:operations:view`。API 和 Application 均检查；Application 在入口、等待锁后、源数据复核及返回前重新核验当前有效身份、fresh 权限／角色、安全戳和活动目标租户。拥有运营查看、报表导出或通用文件下载权限不能替代此专用权限。

普通用户上下文必须与身份租户一致。超管保留原 fresh role 验证，只能在平台已明确解析的一个活动目标租户内导出；系统作用域、缺失目标或未明确选择的写入上下文拒绝。审计写入沿用 AppDbContext 的租户写保护，不以系统作用域绕过审计目标租户校验。

响应采用固定 `application/json; charset=utf-8`、安全固定文件名、Content-Disposition attachment、NoStore 及 nosniff；错误仍走既有 ApiResult／异常映射。首批无服务端持久化文件、公开链接或可重放的内容缓存；每次下载就是新一次导出请求，必须再次授权和审计。不为此入口启用缓存成功正文的 IdempotencyKeyAttribute。

文件返回后无法撤回客户端已保存副本；下载字节开始传输后也不能保证瞬时撤权能收回已发出的数据。前端丢弃迟到响应只保护当前页面，不是服务端访问控制替代方案。

### 4.2 专用准备凭据

复用现有 OperationLog 实体／仓储／IUnitOfWork，在明确目标租户内写最小版本化凭据；不修改通用操作日志查询或把文件塞进 RequestBody／ResponseBody。

1. 有效身份／权限／参数及容量治理准入后，生成 ExportId，持久化 `Requested` 凭据；保存目标租户、操作者、固定用途／接收语义、时间区间和版本，不保存业务数据。写入失败即拒绝，尚未读取导出群体。
2. 查询、验证、生成有界 JSON；返回前同步持久化 `Prepared` 凭据，保存相同 ExportId、观察时间、实际数量、最终文件 Hash／长度和契约版本。准备凭据写入失败时不返回文件。
3. 后续取消／容量／来源变化等失败可追加固定代码的 Failed／Rejected 凭据；数据库故障可能使最终状态缺失，已保存 Requested 仍表示尝试，不能推定完成。原 HTTP 中间件继续尽力记录实际响应结果，不捕获 AI 正文。

Requested／Prepared 是追加记录，不以更新旧记录伪造历史；单条摘要限制在现有 4,000 字符列容量内。Prepared 只证明服务器在当时授权下已生成文件并保存准备凭据，不证明最终响应获准或浏览器已接收／保存成功。准备后最终授权失败仍拒绝文件，审计的准备事实不改写为接收成功。

复用 OperationLogs 的审计用户／租户、软删除及现有读取权限。其现有存储不是 WORM、不可变法律保留或长期归档，首批不擅自改变保留／删除策略。跨租户操作者身份只作为本条审计的执行者事实记录，不扩大其对原 Run owner 的可见性。

### 4.3 容量和异常

拟定保护参数：Run 最多 10,000、关联 usage 最多 50,000、最终 JSON 最多 16 MiB；必要标量 Take(上限 + 1)，记录或字节任一超限整体拒绝，要求缩短区间。序列化使用有界内存写入，不先生成无界大字符串再检查，不使用临时文件或落盘绕开上限。

复用既有分布式限流：每个调用者每分钟最多 2 次准入、每个目标租户每分钟最多 10 次；失败或取消不返还额度。复用既有锁限制同一目标租户同时准备 1 份，忙时立即拒绝，不排队。应用准备截止为 10 秒、锁期限为 30 秒，CancellationToken 贯穿读取／审计／序列化，锁到期或准备截止后不能继续返回；这些是拟定保护值，不是压测结果或严格恰好一次保证。

仍沿用 API 全局限流和 Axios 的既有请求模式；网络传输不计为准备过程，下载完成不是锁语义。Memory 提供者只约束单实例；多实例需 Redis 与真实环境复核，不把单元测试当成跨实例容量证明。

错误分别表达参数无效、无权／失效身份、目标停用、容量超限、限流／忙、源数据变化、取消／准备超时及审计／基础设施失败；不泄露原始数据库、路径、连接或记录细节。不会把失败当空数据，任何拒绝路径不返回文件正文。

## 5. 数据影响、兼容与回退 [DBA]

**本轮仅方案文档，无数据库变更。拟定实现无数据库结构变更，但有受控数据写入。** 不新增实体、表、列、索引、迁移或留存参数；新增权限／菜单按钮种子，并在现有 OperationLogs 中追加导出凭据。审计数据量由准入上限约束，正式索引／写入压力及保留规则仍待测量与签认。

默认平台超管及新租户初始管理员沿用原种子授权模式；不批量自动给已有普通角色授予导出权限。默认租户种子与 TenantInitializationJob 清单同步。已有活动非默认租户的权限目录及角色授权须经管理员受控补齐，不为此次上线重跑租户初始化或悄然扩权；缺权限时拒绝。

Run／usage 外键目前主要按 RunId 关联，新读路径必须显式核对两侧租户；不借此任务进行历史结构重构。参数、DTO 和投影不影响旧 summary／feedback／scenarios。方向 4 的工作区变更与测试保留，不覆盖或还原。

回退应用关闭新增入口，不执行 Down、清表、删除权限历史或审计记录。Application 编译变化仍按 AIC-005 复核 BuildIdentity／场景资格，不能改 Hash 或跳过冻结／评测门禁。现有清理照旧，导出副本不会因此自动清除或成为档案。

## 6. 页面与预计文件 [Developer]

原 AI 运营页新增“导出技术元数据”按钮，复用已选日期范围；导出未查询／未保存的参数时，在确认对话框显示实际待提交区间、固定用途、当前目标租户、仅 Run／usage 白名单、接收人和上限。按钮要求 view + export 权限，提交中禁用；下载需用户主动触发，不在页面加载时自动导出。

沿用 Axios Blob 与现有下载模式；成功前检查状态、Content-Type 与安全文件名，JSON 错误 Blob 不下载为业务文件。身份／目标租户／权限／时间变化、离开或 KeepAlive 停用时取消请求、丢弃迟到 Blob、撤销 Object URL，不持久化到 Pinia、localStorage、IndexedDB 或永久缓存。提示只说明技术复核与容量，不宣称法律认证。

| 工程 | 拟新增／修改文件 |
| --- | --- |
| Shared | 修改 `Constants/AiCenterConstants.cs`：专用导出权限常量 |
| Application | 新增 `AiCenter/AiTechnicalExportModels.cs`、`AiCenter/AiTechnicalExportService.cs`；修改 `DependencyInjection.cs`；明确 DTO、有界投影／序列化、现有身份权限复核、限流／锁和审计编排 |
| 种子 | 修改 `Application/Tenants/TenantInitializationJob.cs`、`Infrastructure/SeedData/SeedDataInitializer.cs`：权限及运营页导出按钮，沿用原管理员初始化边界 |
| API | 修改 `Controllers/AiOperationsController.cs`：DTO 入口、权限、NoStore 与文件响应，不访问 DbContext 或承载导出逻辑 |
| 前端 | 修改 `src/api/ai.ts`、`src/views/ai/operations/index.vue` 及其既有测试，保留方向 4 的展示／清理逻辑 |
| 后端测试 | 新增 `AiCenter/Aic012TechnicalExportTests.cs`、`AiCenter/Aic012TechnicalExportSqlTranslationTests.cs`、`AiCenter/Aic012TechnicalExportApiTests.cs`、`AiCenter/Aic012TechnicalExportSqlTests.cs`，补权限初始化与审计断言；名称位于对应 UnitTests／IntegrationTests 工程 |
| 文档 | 本方案、后续／总体计划；实现后新增 `docs/ai-center-aic-012-export-acceptance.md`，不覆写方向 4 验收 |

上表为预计文件，不表示已经新增业务实现。后端按现有 `backend/PermissionSystem.*` 工程定位，前端路径位于 `frontend/permission-admin/`。不增加新运行依赖、通用归档框架或大型异步任务系统。

## 7. 验证与退出条件 [Reviewer]

- 核心：固定白名单、群体左闭右开、默认／最大时间、空数据、全量及稳定排序、NULL 场景、状态／无效量／实际和估算分离、文件及 payload Hash、三个容量边界，拒绝不能输出截断文件。
- 授权：只有 view／只有 export、匿名、旧 claims／撤权、停用身份／租户、安全戳变化、普通跨租户、超管明确目标／系统作用域、锁等待后和审计提交后变化、源数据删除／关联变化。
- 审计：Requested／Prepared 同 ExportId、正确目标租户与 actor、摘要有界且不含正文、准备前／准备后写失败均无文件、失败状态缺失不冒充成功；既有 middleware 不捕获 JSON 文件。
- 容量：调用者／租户限流、同租户并发准备拒绝、取消／截止／锁失效、资源释放；Memory 与 Redis 分别记录真实能力边界。
- SQL：真实 SQL Server provider 离线翻译证明投影、两侧租户／软删除、时间／关联条件及 Take；显式隔离 SQL 合成夹具回滚验证读取、审计事务、权限变化和容量／计划，缺环境跳过不记通过，不自动迁移。
- HTTP／前端：view + export 双门禁、NoStore／attachment／nosniff、安全文件名、错误为 ApiResult；Blob 错误、主动确认、无权限不发送、参数／租户变化及迟到响应不触发下载、Object URL 释放，旧运营指标不退步。
- 实现后执行后端解决方案 Release build、本批专项／全量相关回归、方向 4 回归；前端 Vitest／类型／lint／production build／格式检查及 AIC-004 Offline。复用新的独立构建输出，保留既有 Windows 证书导入失败的真实记录，不弱化旧测试。

实际隔离 SQL／Redis、真实认证浏览器、正式规模、Owner／下载副本处理和适用发布条件未验证前，标为“导出首批待验收”；分层归档、法律保留和方向 5 整体仍未完成。

## 8. 提交确认时的内容与结论 [Architect] [DBA] [Reviewer]

请确认第 3～6 节的具体白名单、UTF-8 JSON、内部技术复核／当前调用者、view + 新 export 双权限、专用 Requested／Prepared 凭据、10,000／50,000／16 MiB、准入／并发／准备截止及原页面入口。确认后按本方案实施，不重复询问同一范围；不涉及真实导出、迁移、留存或外部传输授权。

本轮改动仅为本方案和任务状态文档；静态调研通过，未运行功能构建或测试，不能给出功能验收通过结论。真实 Owner／合规规则／环境条件仍未提供，明确保留为开放与整体验收条件。

## 9. 确认后的实施记录 [Developer] [DBA] [Reviewer]

2026-10-07：用户回复“确认”批准第 3～6 节，已实现 Application 固定标量白名单导出、双权限及多次 fresh 身份／租户校验、Requested／Prepared 最小审计、有界 JSON／双 Hash、既有限流和同租户准备锁。API 使用 NoStore／nosniff／attachment 和固定文件名，并仅在此入口开放 Content-Disposition 响应头读取，支持既有允许来源的跨域浏览器下载；没有放宽 CORS 来源策略。原运营页增加主动确认和 Blob 下载，取消及迟到响应保护、Object URL 释放复用已有状态及请求模式。

无数据库结构或迁移变更；有权限／按钮种子变更和现有 OperationLogs 的凭据写入。默认平台租户与新租户初始化种子已同步，已有活动非默认租户须由管理员按已批准人员范围补权限／菜单关系，不重跑租户初始化、不自动扩权普通角色。没有执行真实迁移、数据导出、清理或部署，留存规则未变。

最终 Release build 成功，核心及离线 SQL 翻译 42/42、HTTP 6/6、Legacy 45/45 通过；全量 Unit 1003 通过、1 项既有 Windows 证书导入失败；集成 95 通过、43 环境跳过，包含导出 SQL 3 项。前端全量 101/101、类型／production build／预算／lint／四文件格式检查通过；lint 保留 4 条 warning。AIC-004 Offline 41 个变体确定性检查通过，release gate 仍 pending／failed。

Reviewer 已确认范围代码复核：**通过，导出首批待验收。** 隔离 SQL／Redis／真实浏览器／正式规模／Owner 及下载副本要求、AIC-005 发布资格仍待验证；分层归档、法律保留和方向 5 整体未完成。详见 [实际证据与退出条件](ai-center-aic-012-export-acceptance.md)。
