# AIC-012 方向 3 第二批方案：只读按日质量趋势与分币种差异分布（已确认、已实现，待验收）

> 日期：2026-10-07
> 用户要求“继续方向 3 第二批”并要求继续处理，随后回复“确认”批准本具体方案。第二批已实现、待环境验收；第 1～8 节保留确认时提案，第 9 节登记确认，第 10 节登记实际实施，证据见[独立第二批验收记录](ai-center-aic-012-cost-quality-batch2-acceptance.md)。
> 依据：[AGENTS.md](../AGENTS.md)、[后续计划](ai-center-next-development-plan.md)、[批次登记](ai-center-aic-012-implementation-plan.md)、[首批方案](ai-center-aic-012-cost-quality-implementation-plan.md)、[首批验收记录](ai-center-aic-012-cost-quality-acceptance.md)。首批环境验收与真实成本对账／自动校准保持未完成。

## 1. 目标与边界 [Architect]

建议在已有“估算质量核验”中增加主动选择的趋势视图，回答两个只读问题：窗口内哪些 UTC 日观察到较多缺失／不可比或费用不一致样本；同一币种内存储费用高于、等于或低于历史快照重算值的样本分布如何。继续使用首批已有数据和诊断，不新增业务阈值、严重级别或校准系数。

本批仅包含 UTC 按日人口／质量／Token 聚合与全窗口分币种费用差异方向分布，不按 Provider／模型／用户／场景拆分，不返回任何调用明细、原单价、正文或供应商请求标识。没有账单导入、外部请求、真实模型执行、自动通知、定时轮询、导出、新留存或历史写回。

真实账单格式、逐笔匹配、Token 来源、税费／折扣／币种及审核 Owner 仍未确定；本批不能表示实际支出趋势、对账通过、计费错误或应当改价。首批对来源和未知历史的限制继续适用。

## 2. 已查证依据与待验收事实 [Architect] [DBA]

| 依据                                                                                                                                                                                                        | 已查证事实                                                                                                                    | 第二批设计依据                                                                     |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------- |
| [既有运营服务](../backend/PermissionSystem.Application/AiCenter/AiOperationsService.cs)                                                                                                                     | Daily 按 `CreatedAt.UtcDateTime` 的 DateOnly 聚合，但其时间来源是 Run                                                         | 沿用 UTC 日的既有展示惯例；新趋势明确按 usage.CreatedAt，不能混同原 Daily          |
| [首批服务](../backend/PermissionSystem.Application/AiCenter/AiCostQualityService.cs)                                                                                                                        | 使用 usage 时间窗口、双侧 QueryForTenant、50,000 行上限、两次完整投影比较和 fresh 授权；actor 6/min、tenant 12/min，10 秒截止 | 不扩大可见范围或读取容量；趋势与摘要建议共享同一限流策略，不能通过新增入口翻倍额度 |
| [首批计算](../backend/PermissionSystem.Application/AiCenter/AiCostQualityCalculator.cs)                                                                                                                     | 已有终态／未决／未知状态、17 个固定诊断、Token 分项回退与成对摘要、分币种同样本费用                                           | 复用相同计算语义；日分组不重新定义终态或未知处理，费用方向只用相同可比样本         |
| [首批 DTO](../backend/PermissionSystem.Application/AiCenter/AiCostQualityModels.cs)                                                                                                                         | 四金额字段使用 `decimal?` 和精确 JSON 字符串；币种排序分页，Scope／CostBasis 固定                                             | 保持金额精度和白名单；单独趋势契约，保留首批 GET 的响应形状和 MetricsVersion       |
| [usage 映射](../backend/PermissionSystem.Infrastructure/Configurations/AiUsageLogConfiguration.cs)                                                                                                          | 现有价格／费用 decimal(18,6)、币种长度 3；租户／Provider／时间、租户／Status／时间、租户／Currency／时间索引，Run 为普通外键  | 不新增实体或迁移；继续双侧租户过滤，不假定有纯租户／时间覆盖索引或租户组合外键     |
| [首批 SQL 案例](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012CostQualitySqlTests.cs)／[离线翻译案例](../backend/PermissionSystem.UnitTests/AiCenter/Aic012CostQualitySqlTranslationTests.cs) | 真实 SQL 3 项缺隔离环境跳过；离线翻译证明有界白名单双侧查询，不能证明实际成本或性能                                           | 第二批独立补日边界／方向分布／变化案例；不能把首批跳过项或替身标为环境通过         |
| [首批组件](../frontend/permission-admin/src/views/ai/operations/AiCostQuality.vue)                                                                                                                          | 主动打开、独立窗口、精确金额字符串、响应范围校验、abort／代次／身份键清理                                                     | 沿用生命周期及请求模式；不同时拼接两次观察的摘要与趋势，不增加图表依赖             |

本轮只静态查证项目代码与文档，未读取真实 usage、数据库、账单、供应商配置或敏感值。首批 SQL／Redis／浏览器／规模／Owner 和 AIC-005 发布资格缺口不因进入第二批而关闭。

## 3. 时间、群体与计算契约 [Architect]

### 3.1 UTC 日与空日

时间输入继续为带明确偏移的 From／To：usage.CreatedAt 左闭右开，默认最近 30 天、最长 90 天、结束最多未来 5 分钟。把输入转换为同一 UTC 时间轴，再按 usage.CreatedAt.UtcDateTime 的日期分桶；不根据浏览器时区或输入偏移改变归属，不接收任意时区参数。

每个日桶返回 `Date`（`yyyy-MM-dd`）、`BucketFrom`／`BucketTo`，后两者是该 UTC 日与实际查询窗口的交集；`IsPartialDay` 明确标注桶是否覆盖完整 UTC 日。桶按日期升序，最多 **91 个**：90 天时长可以跨越 91 个 UTC 日期。首尾部分日不作为完整自然日比较。开始包含，结束不包含；结束恰为 UTC 午夜时不额外生成下一日桶，只有满足 BucketFrom < BucketTo 的桶才返回。

窗口内没有匹配记录的日也返回，人口／诊断／Token 合计为 0、无样本比例为 null；含义仅为“本次仍可读范围未观察到”，不是供应商当天无请求、零收费或完整历史。日结果没有货币金额总和，避免跨币种合计。

### 3.2 固定参与群体与日摘要

继续首批当前活动单租户的租户级 view 聚合，usage 和关联 Run 均非删除且属于目标租户；Run 创建日期不限制。每条 usage 只归入一个日桶，失败、取消及重试不去重；不可读关联 Run 的 usage 排除。

每个日桶返回首批相同的 Population、Basis、17 个 Issues、InputComparison、OutputLimitComparison 和 TotalTokens。Completed／Failed／Cancelled 是终态；未决和未知状态不进入终态 Token／费用比较。问题可重叠，不提供把全部问题相加得到“异常调用数”的总数。

同一响应同时提供完整窗口摘要，完全来自同一批观察。各日人口、依据、诊断计数、成对样本／Token 合计／带符号和绝对 Token 差、零基线对／超基线计数、TotalTokens 比较数，应分别与窗口摘要守恒；比例不能求和或取日均值，窗口加权比按原同一样本的 Token 合计重新计算。所有零／未知与舍入沿用首批。

### 3.3 全窗口分币种差异方向

币种只用首批三位大写格式校验，排序／分页保持 Ordinal；格式合格不等于真实 ISO 币种，不换汇、不跨币种合计。缺失／无效币种仅进入质量诊断，不归并为合法币种或产生金额。

每个有效币种分组沿用首批 CurrencySummary，增加两个方向计数：`StoredAboveRecomputedCount`（存储大于重算）和 `StoredBelowRecomputedCount`（存储小于重算）。已有 ConsistentCostCount 表示相等；三者合计必须等于 ComparableCostCount，前两者合计等于 DifferentCostCount。UncomparableCostCount 单列，不纳入差异方向样本。

比较必须使用同一终态记录的非负存储费用、历史快照与有效 Token 选择所得六位 AwayFromZero 重算值。零单价／零 Token 的有效零仍是合法样本。相等使用既有 decimal 精确比较，不新增误差容忍阈值、金额区间或未经确认的显著性规则。单条溢出不可比，聚合溢出整体拒绝，不给部分趋势。

四金额字段仍为精确十进制字符串或 null，前端不转 Number。币种表是**全窗口**方向分布，日表是**全币种安全数量／Token**趋势，两者不构造日 × 币种金额矩阵、不暗示有逐日财务对账。

## 4. 接口、授权与保护 [Architect]

### 4.1 拟新增只读接口

`GET /api/ai/operations/cost-quality/trends`，输入仅 From／To／PageIndex／PageSize；PageIndex 默认 1，PageSize 默认 20、最大 50，页码运算使用 long，大合法页码返回空币种页。TotalCount 是窗口有效币种组数，不是日期或调用数。

拟新增独立 `AiCostQualityTrendResponse`：MetricsVersion=1、固定 Scope／CostBasis、`Grouping=UsageCreatedAtUtcDay`、`BucketTimezone=UTC`、目标 TenantId、实际 From／To 和 ObservedFrom／ObservedTo、原 Limits、窗口 Population／Basis／Issues／两项 Token 比较／TotalTokens、最多 91 项 Daily，以及 `PagedResult<AiCostQualityCurrencyDistribution>`。每个分布项封装原 CurrencySummary 与两个新方向计数。

这只是拟新增契约，当前不存在该接口或 DTO。保留首批 `/cost-quality` GET、v1 DTO 和原默认视图，不把增加 Daily 字段直接塞进旧响应或更改其计算规则。新接口 NoStore、ApiResult、CancellationToken，Controller 仅调用 Application。

### 4.2 安全与读取成本

沿用首批 API／Application view 双门禁，入口、首次读取后、返回前 fresh 身份／角色／权限／安全戳／活动目标复核。普通身份同租户，超管须 Header／Request 明确活动单租户，不开放系统作用域，不接收 actor／tenant／Provider ID、任意排序／字段／查询或外部目标。

建议在现有 AiCostQualityService 内增加趋势方法，只对必要的有界读取／复核流程作局部复用，不重构其他运营、导出或凭据服务。每次查询仍最多两次 SQL 有界白名单读取、Take(50001)、完整群体比较；趋势在内存对同一观察分桶，不按日／币种追加查询，不调用原 HTTP 摘要端点或并发查询拼结果。

按日可以对已分桶行复用原纯计算器，所有日合计处理每条行一次；新的方向计数须复用相同单条重算语义，不复制一套可能漂移的费用公式。若需要提取单条纯计算结果，仅限首批计算器内，必须用原测试和新增守恒测试证明旧行为不变。额外分桶／结果构建仍属于原 10 秒截止，贯穿取消检查；计算／排序／分页完成后返回前再次检查。

摘要与趋势**共用**首批 `ai-cost-quality:actor` 6/min 和 `ai-cost-quality:tenant` 12/min；打开趋势、重查及翻页均消耗同一额度，不增加独立额外额度、锁或成功缓存。窗口全部匹配行超过 50,000 则整体拒绝，不截断后绘制完整趋势。

响应严格最多 91 日、每日至多固定 17 诊断、币种页最多 50 组；无日 × 币种嵌套、自由字符串或调用明细。两次来源观察不是事务快照，不能保证返回后不再结算或清理。401／403／400／409／429 与取消／基础设施映射沿用首批，不修改全局异常中间件或回显异常原文。GET 保留原普通请求日志，不写“对账通过”的新审计事实。

## 5. 页面与交互 [Architect]

建议在现有质量 dialog 内增加“窗口摘要／按日趋势”切换，默认窗口摘要不变；用户主动选择趋势才发起新 GET，每次只加载当前视图。趋势显示独立调用记录窗口、UTC／首尾部分日说明、按日质量和成对 Token 表，以及全窗口分币种差异方向表，不新增图表库或独立路由。

切换视图、时间或币种分页时清理旧结果并 abort，不把上一响应窗口金额／日期留在新视图；每个响应独立展示观察时间，页面不拼接不同观察的首批摘要与趋势来声称守恒。翻页只改变全窗口币种表的分页范围，日期表和完整窗口摘要不按页缩小分母；每次翻页仍重新观察，来源变化时数值可能改变，不承诺跨响应相同。

沿用身份键、代次和 active 检查；撤权、actor／角色／超管／目标变化、关闭、KeepAlive 停用和卸载时取消和清理。校验新 MetricsVersion／Scope／CostBasis／Grouping／BucketTimezone／TenantId／实际窗口／分页；UTC 日期作为字符串显示，不交给本地 Date 转换而移动到前一天。缺样本为“—”，有效零照常显示，精确金额只作字符串格式化。

采用已有 Element Plus 表格、分页、主题 tokens 和窄屏容器内滚动。日数量只描述可读样本，不标“当天无调用”；费用方向文案为“存储高于／等于／低于重算”，不用“多收／少收／省钱／需调价”。无默认后台轮询、本地 DTO 持久化、下载或上传。

## 6. 数据影响、兼容与回退 [DBA]

**建议无数据库结构、迁移、权限／菜单种子或历史回填变更。** 本轮规划无数据库变更；拟实施仍复用现有 usage／Run、软删除和索引，不新建趋势表、日快照、统计任务或对账实体，不改变 decimal 精度、结算、预留、配置、预算和留存。

UTC 分桶在已授权标量读取后进行，不新增依赖特定 SQL 日函数的查询；原 Take 和索引依旧不能证明扫描／排序／两次读取成本合格。正式规模和实际查询计划仍需 DBA 验收。若必须改索引、数据来源或保护参数，返回 Architect／DBA 独立方案，不直接改库或提高上限。

原接口、导出 v1／本人凭据、运营汇总及首批历史报告保留；回退撤销趋势入口／方法，不清表、不重写审计或历史费用。新 Application 构建仍影响 AIC-005 BuildIdentity，发布必须重新冻结／评测／兼容核验，不以 Offline 替代正式资格。

## 7. 预计文件与实施顺序 [Developer]

下表是确认后预计修改，新增业务／测试文件尚不存在，不能作为当前实现或通过证据。

| 范围             | 预计文件／最小改动                                                                                                                                                                                                   |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application      | 新 `AiCenter/AiCostQualityTrendModels.cs`、`AiCostQualityTrendCalculator.cs`；原 `AiCostQualityModels.cs` 服务接口加方法，`AiCostQualityService.cs` 局部共用读取；仅必要时提取原计算器单条结果，不新增 DI 服务或依赖 |
| API              | `Controllers/AiOperationsController.cs` 单趋势 GET；原构造依赖不增加，复用 IAiCostQualityService                                                                                                                     |
| 前端             | `src/api/ai.ts` 新趋势 DTO／GET，`src/views/ai/operations/AiCostQuality.vue` 主动视图切换；若表格过大仅拆同目录展示子组件，原运营页入口不扩展                                                                        |
| UnitTests        | 新 `AiCenter/Aic012CostQualityTrendTests.cs`、`Aic012CostQualityTrendSqlTranslationTests.cs`，保留首批全部回归                                                                                                       |
| IntegrationTests | 新 `AiCenter/Aic012CostQualityTrendApiTests.cs`、`Aic012CostQualityTrendSqlTests.cs`；共用 stub 最小补新方法                                                                                                         |
| 前端测试         | 原 `src/api/ai-operations.test.ts`、`src/views/ai/operations/AiCostQuality.test.ts`，必要时独立趋势展示测试                                                                                                          |
| 文档             | 本方案、分批／后续／总体登记；实施后新增 `docs/ai-center-aic-012-cost-quality-batch2-acceptance.md`，不覆盖首批证据                                                                                                  |

顺序：确认具体范围／UTC／空日／分母与共用限流 → 纯计算及守恒测试 → 局部共用有界读取／复核 → HTTP → 主动趋势视图／清理 → 首批及全量回归 → Reviewer 与独立验收登记。确认前不写业务代码、不运行迁移、真实模型或业务数据查询。

## 8. 测试与验收 [Reviewer]

| 层次          | 必须覆盖                                                                                                                                                                                          |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 时间与空日    | 等价时间偏移归入同一 UTC 日、跨 UTC 午夜、首尾部分日、恰午夜结束、空窗口群体、无记录日、30／90 天、最多 91 日、日期边界与不可用参数；不以 Run 日期分桶                                            |
| 守恒          | 每行一桶；人口／终态／未决／未知、四种依据、17 诊断、Token 样本／合计／差／零基线／超基线、TotalTokens 日和等于同观察窗口；窗口比例重新加权，不按日比率平均                                       |
| 方向与金额    | 高于／相等／低于互斥及分母守恒，不可比单列；缺币种、混合回退、零价格／Token、六位中点、正负差互抵仍保留绝对合计、单条／聚合溢出、精确大额字符串、合法零与 null                                    |
| 安全与读取    | 原双侧租户／软删除／fresh 撤权／安全戳／目标停用／超管明确目标，第二次新增／修改／删除／关联变化；两次读取、不按日 N+1、不读 Provider；50,000／50,001、取消／截止、摘要与趋势共用准入及无成功缓存 |
| 响应与分页    | 最多 91 日／固定诊断／最多 50 币种、稳定排序、大页码、空页；同一观察群体下翻页不改 Daily 或窗口摘要，跨观察变化不能拼接；首批 GET v1 不变、新 NoStore／安全白名单／未知参数不扩大来源             |
| 前端          | 默认仍首批摘要、主动趋势才 GET、不并发拼接；切换／日期／分页／身份／权限／目标变化清理，取消／迟到／失败恢复、关闭／KeepAlive／卸载；UTC 字符串、部分日／空日文案、精确金额和宽窄屏               |
| SQL／真实环境 | 条件隔离 SQL 核实 UTC 归属与实际比较结果、双侧隔离／fresh 撤权／来源变化；独立原查询计划和正式规模；Redis 共用跨实例限流；真实 OpenIddict 与浏览器主题／生命周期，不以替身代替                    |
| 回归          | 原首批与方向 4、5 各批核心／HTTP、预算／结算／会话／后台执行、全量 Unit／Integration／Legacy、前端测试／类型／build／预算／lint／格式、AIC-004 Offline；新产物及独立报告，既有证书失败仍真实记录  |

真实 SQL 沿用显式隔离配置、合成数据和外层事务回滚，不自动迁移或读取生产 usage，不在聊天索取连接串。测试通过数量只在实施后用实际报告登记，不沿用首批 49／6／148 作为第二批证据。真实规模、Owner、环境和发布门槛仍是开放退出条件。

## 9. 方案确认与规划结论 [Architect] [DBA] [Reviewer]

用户已确认范围：**当前活动单租户 view 安全聚合；UTC usage 按日、最多 91 桶、空日及首尾部分日明确；同观察窗口摘要及日守恒；全窗口分币种高于／等于／低于重算分布；精确字符串金额；保留首批 GET v1；50,000 行、页 20 至 50、摘要与趋势共用 actor 6／tenant 12 每分钟、10 秒截止；不扩明细、不导入账单、不改价／历史／结算／留存、不增加结构或种子。**

按 [AGENTS.md](../AGENTS.md) 第 4／5 节，具体方案提交后已获得用户“确认”，已进入 Developer 并完成实施；同一已确认范围不再请求实施许可。

规划 Reviewer 结论：**通过，方案已获确认；不替代实现或环境验收。** 规划阶段新增提案及计划登记，静态核对 UTC 原惯例、首批查询／计算／页面／索引及缺口，当时未运行新 build／test；确认后实施与验证另见第 10 节。无账单／Token 来源／Owner、首批真实环境缺口未关闭、两次观察非事务快照、日样本并非真实收费或供应商完整历史的风险保留。

当前状态：“估算质量首批待验收；只读趋势第二批已实现待验收”；不将方向 3、AIC-012 或 P5-C／P5-D／P5-E 整体标为完成，不覆盖方向 4、5 的已有实施和历史证据。

## 10. 实际实施与复核 [Developer] [Reviewer]

已新增趋势 DTO／纯计算／NoStore GET，原服务内局部共用有界读取与 fresh 复核，两个入口共用首批限流；分币种方向在原计算器同一 CostCounter 内统计，没有第二套重算公式。窗口与 UTC 日结果来自同一观察，最多 91 日桶；首批 GET v1 形状与公式保留。前端原 dialog 默认摘要，主动趋势才请求；独立展示组件负责日表／展开明细聚合和全窗口方向表，切换／身份／权限／窗口／关闭／KeepAlive／卸载均清理和取消，精确字符串金额不转 Number。无新依赖、结构、迁移、种子或历史写回。

独立 `obj/aic012-cost-trends-check2` Release build 成功，0 error、2 条既有 warning；本批核心 31 + 离线 SQL 翻译 1，共 32/32，HTTP 7/7，本批 SQL 3 项环境跳过。最终全量 Unit 1133 通过、1 项既有 Windows 证书导入失败，集成 114 通过、52 环境跳过，Legacy 45/45；最终报告核实 AIC-012 五批核心 218/218、HTTP 32 项通过。前端 22 文件／173 项全部通过，类型／build／预算／格式通过，lint 0 error、4 条既有 warning；Offline 41 变体确定性检查通过，发布门槛仍 pending/failed。

Reviewer 已确认代码范围复核结论：**通过，第二批待环境验收。** 实际报告、编译／测试修正和未验收条件见[独立第二批验收记录](ai-center-aic-012-cost-quality-batch2-acceptance.md)；真实 SQL／Redis／浏览器／规模／Owner、首批环境及新 BuildIdentity 发布资格仍未关闭。未启动真实宿主、迁移、清理、部署、真实模型或业务数据读取，未 commit／push。
