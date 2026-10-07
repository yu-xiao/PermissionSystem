# AIC-012 方向 3 首批方案：只读估算质量核验（已确认、已实现，待验收）

> 日期：2026-10-07
> 用户要求继续方向 3，选择“先规划只读估算质量核验（价格快照、Token、费用差异），不导入账单、不自动改价”，随后回复“确认”批准本具体方案。首批已实现、待环境验收；第 1～8 节保留确认时的提案口径，第 9 节登记确认，第 10 节登记实际实施与验证，证据见[独立验收记录](ai-center-aic-012-cost-quality-acceptance.md)。
> 依据：[AGENTS.md](../AGENTS.md)、[后续计划](ai-center-next-development-plan.md)、[AIC-012 分批方案](ai-center-aic-012-implementation-plan.md)、[总体计划](ai-center-mcp-implementation-plan.md)。方向 4、5 原实现及验收证据保留，真实供应商账单对账和自动校准未启动。

## 1. 目标与本批边界 [Architect]

建议在现有运营页新增主动打开的“估算质量核验”，对当前明确活动单租户、调用记录创建时间窗口内仍可读的 usage，提供三种只读证据：历史单价快照是否具备计算条件；已记录 Token 与输入估算／输出上限的差异；存储的 EstimatedCost 与按历史快照及既有有效 Token 选择规则重算的差异。

核验是现有估算数据的质量诊断，结果不能表达供应商实际收费、财务对账通过或价格应当调整。没有供应商账单事实，也没有每个 Token 值的来源证据；符合内部计算规则只证明存储数据在本次观察内可以相互解释。

本批不导入／上传／抓取账单，不新增供应商适配、当前配置单价比较、模型／供应商细分或调用明细，不展示原单价，不写回 usage／Run／预算／供应商配置，不调用 SettleCost／SettleInvocationAsync，不给自动调价系数，不改输入估算算法、输出上限、预留和结算行为，不改留存或技术导出 v1。真实对账及校准后续仍须账单契约、审核人与变更规则。

## 2. 已查证事实与限制 [Architect] [DBA]

| 现有证据                                                                                                                                                                       | 查证事实                                                                                                                                                                               | 本批设计依据                                                                                 |
| ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------- |
| [AiUsageLog](../backend/PermissionSystem.Domain/Entities/AiUsageLog.cs)                                                                                                        | 保存调用 Token、估算 Token、EstimatedCost、ReservedCost、历史输入／输出单价和币种；SettleCost 分项优先选择有效非负 InputTokens／OutputTokens，否则选择估算；金额保留六位、AwayFromZero | 只读重算复用这套选择与舍入语义；不把 ReservedCost 当已结算值或重新执行结算                   |
| [预算服务](../backend/PermissionSystem.Application/AiCenter/AiBudgetService.cs)                                                                                                | 调用前复制供应商单价和币种到 usage；输出估算来自 maxOutputTokens；缺计算字段时结算可能保留既有费用或原预留                                                                             | 单价必须取 usage 历史快照；不能用现在的配置单价补历史空值，无法重算不等于零费用              |
| [会话服务](../backend/PermissionSystem.Application/AiCenter/AiConversationService.cs)                                                                                          | 输入估算使用上下文内容／工具调用字符数，输出估算是本次请求 MaxTokens；保存网关返回的 Token；部分后台 rate limit 失败写入本地零 Token                                                   | 页面称“已记录 Token”，不对记录值宣称全部来自供应商；输出上限不是输出预测，不能当普通预测误差 |
| [模型网关](../backend/PermissionSystem.Infrastructure/Ai/OpenAiCompatibleModelGateway.cs)                                                                                      | 读取响应中的 prompt／completion／total tokens，字段可缺失                                                                                                                              | 回报不是账单；不能依据调用成功或非空 RequestId 推定已计费、完整计费或精确计费                |
| [结算测试](../backend/PermissionSystem.UnitTests/AiCenter/AiBudgetServiceTests.cs)                                                                                             | 覆盖混合 Token、缺 usage、历史快照、重复结算和稍后有效 Token 替换估算                                                                                                                  | 新诊断必须与既有正常结算样本相容；不修正历史记录来让新测试通过                               |
| [usage 映射](../backend/PermissionSystem.Infrastructure/Configurations/AiUsageLogConfiguration.cs)                                                                             | 金额／单价 decimal(18,6)、币种长度 3；既有租户／Provider／时间、租户／Status／时间、租户／Currency／时间索引                                                                           | 有界投影可复用；不声称已有纯租户／时间覆盖索引或跨租户组合外键，实际计划仍需 DBA 验收        |
| [场景运营服务](../backend/PermissionSystem.Application/AiCenter/AiScenarioOperationsService.cs)、[权限常量](../backend/PermissionSystem.Shared/Constants/AiCenterConstants.cs) | 已授权租户级聚合复用 ai:operations:view、fresh 身份／角色／权限／活动目标和白名单标量                                                                                                  | 本批只输出质量聚合，不隐式开放单价配置或记录明细，不借用 export 权限                         |

本轮只静态阅读代码与文档，没有读取真实 usage、账单、数据库、供应商配置或敏感值。未找到真实账单导入／匹配契约，不假定 ProviderRequestId 能逐笔匹配。

## 3. 人员范围、窗口与读取保护 [Architect]

### 3.1 授权和固定数据群体

建议复用 `ai:operations:view`：API 与 Application 双门禁，返回的是当前活动目标租户全体获准运营聚合，**不是方向 5 的本人凭据范围**。不新增权限／菜单种子，不复用要求 export 的导出访问策略；沿用既有 fresh 认证／授权模式，仅在新服务内完成必要校验，不重构原服务。

入口、第一次读取后与返回前核验当前有效 actor、身份租户、安全戳、fresh 角色与权限、目标租户仍活动；当前超管声明与 fresh 角色一致。普通身份须与目标同租户；超管须 Header／Request 明确选择单个活动目标，不读系统作用域。请求不接收租户／actor／Provider ID、SQL、任意字段或外部目标。

时间按 **AiUsageLog.CreatedAt** 左闭右开，默认最近 30 天、最长 90 天、结束最多未来 5 分钟。它不同于原场景统计／技术导出的 Run 创建时间；页面使用独立“调用记录时间”，明确两页合计可能不同，不把该窗口称供应商账期。DateTimeOffset 接收明确时间偏移，未传值由 TimeProvider 确定实际窗口，返回实际 From／To 与观察起止时间。

查询两侧均 QueryForTenant 限定目标与非删除：窗口内 usage 仅在其 Run 仍属本租户且非删除时参与。不要求 Run 的创建时间也落在窗口，不根据 Run 状态筛掉失败、取消或重试调用；每条 usage 单独计数，不按 Run 去重。缺失／不可读关联 Run 的行排除，输出明确 Scope 为“当前租户内仍可读关联 Run 的调用”，不能声称覆盖所有已发生供应商请求或已清理历史。供应商配置是否后来改价／停用／软删除不参与判断，不读取配置实体。

### 3.2 有界读取与一致性

建议最多 **50,000 条**匹配 usage，SQL 先限定双侧租户／软删除和 usage 时间，再按 CreatedAt／Id 固定排序及标量投影，Take(50001)，超限整体 400 并要求缩短窗口，不截断后返回“全量”金额。投影仅包括 Id／RunId／CreatedAt／Status、三项已记录 Token、两项估算 Token、历史两项单价／币种及 EstimatedCost；内部标识／单价不进入响应。

读取、计算后重新构造相同有界查询，比较整组投影记录，检查新增／修改／软删除／租户或关联变化，变化则固定 409、要求重查。复核结束再次 fresh 授权并检查截止；无成功结果缓存，不采用“只有条数相同即视为未变”。多次观察仍不是事务历史快照或不可变审计，返回后可能继续结算或清理。

两个保护值拟为每 actor 每分钟 **6 次**、每目标租户每分钟 **12 次**，通过既有限流抽象、独立 `ai-cost-quality` 策略限制；应用读取／计算截止 **10 秒**、CancellationToken 贯穿。只读，无新锁、不占导出或凭据读取额度；这些是待确认保护值，不是正式 SLA 或扫描／内存成本证明。缺真实 Redis 不将替身限流标为多实例验收通过。

## 4. 计算契约：未知、样本及差异 [Architect]

### 4.1 人口与价格质量

| 分类／计数   | 固定规则                                                                                                                              |
| ------------ | ------------------------------------------------------------------------------------------------------------------------------------- |
| 参与调用数   | 有界查询完整匹配群体，按 usage 计数                                                                                                   |
| 已终结       | Completed、Failed、Cancelled；失败和取消不视为零费用或不收费                                                                          |
| 未决         | Pending、Running；只计未决数量，不参与历史费用重算／差异或终态 Token 比较                                                             |
| 未知状态     | 未定义枚举；独立计数，不推断终态或已结算                                                                                              |
| 有效价格快照 | 输入／输出单价均存在且非负，0 合法；币种为三位大写 ASCII 字母，与原新运营口径一致；格式合格不证明是真实 ISO 币种或能换汇              |
| 缺失／无效   | 分别统计单价缺失、负单价、币种缺失／格式无效、费用缺失／负费用、各 Token 缺失／负值及算术溢出；固定标记计数可重叠，不相加冒充互斥类别 |

总体状态计数互斥，参与调用数 = 已终结 + 未决 + 未知状态。价格／Token／费用问题计数针对已终结调用；零与 null 区分，不为负值取绝对值、不改币种大小写后偷偷归组、不把未知值填为零。

### 4.2 Token 依据与比较

有效已记录 Token 指该分项存在且非负。按现有 SettleCost 选择规则：有效已记录值优先；缺失或负已记录值时，仅在对应估算值存在且非负时回退，否则该分项不可用。未采用的负估算仍保留独立无效字段诊断，但不推翻另一项已记录有效值。

终态费用依据分四个互斥类别：**两项已记录**、**一项已记录／一项估算回退**、**两项估算回退**、**无法形成有效 Token 对**。四者之和等于终态调用数。名称描述存储字段的选择，不称“实际账单”或“供应商实测”；本地写入的零 Token 无来源字段可供区分。

输入比较仅在终态、InputTokens ≥ 0、EstimatedInputTokens > 0 的成对样本上计算：样本数、已记录与估算的各自合计、差值合计（已记录减估算）、逐条绝对差值之和、加权记录／估算比 `sum(InputTokens) / sum(EstimatedInputTokens) × 100%`。零估算且记录有效的成对数量另计、不进入比例分母；无样本比例为 null，不显示 0%。“加权比”不是逐条百分比平均或可直接应用的校准系数。

输出比较采用同样成对条件（OutputTokens ≥ 0、EstimatedOutputTokens > 0），名称为**输出上限使用情况**：样本数、已记录输出合计、配置上限合计、记录减上限差值与逐条绝对差值之和、加权上限使用比例，以及记录输出大于上限的样本数。比例不裁剪到 100%，超过仅作数据质量提示；不据此断言供应商协议违规。零上限另计、无样本比例 null，不能把上限余额称“预测不准”或模型费用节省。

TotalTokens 一致性仅在终态三项已记录值均非负时以 long 比较 `TotalTokens == InputTokens + OutputTokens`，输出可比／不一致数量；缺字段或负值不伪造 TotalTokens，不用它替代缺失分项。累计与差值以 long／decimal 受检计算，百分比保留两位、AwayFromZero。

### 4.3 历史快照费用一致性

满足有效价格快照与有效选定 Token 对时，只在独立投影上计算：

`RecomputedCost = Round(InputSelected × InputSnapshotPrice / 1,000,000 + OutputSelected × OutputSnapshotPrice / 1,000,000, 6, AwayFromZero)`

不加载或改动跟踪实体，不调用现有写方法。原 SettleCost 在字段缺失时可能保留既有 EstimatedCost／原 ReservedCost，而历史预留已清空，因此这类行只能显示**无法按当前可见快照重算**，不猜测它的原结算来源。

重算可完成且 EstimatedCost 存在／非负时才进入费用可比样本；按六位既有精度直接比较，不引入账单容差。可比样本分一致／不一致，其他终态记不可比：终态数 = 可比 + 不可比，可比 = 一致 + 不一致。费用不一致只提示字段质量或数据变化原因待复核，不自动断定算费 bug、坏账或篡改。

金额仅按有效历史币种分组。每币种返回同一可比样本的存储费用合计、重算费用合计、差额合计（存储减重算）和逐条绝对差额之和；同时返回样本数／一致／不一致／不可比数量和 Token 依据计数。**不同分母的金额不得直接相减**，绝对差额防止正负互相抵消。没有可比样本时金额为 null；真实有效零样本金额为 0。无效／缺失币种只返回诊断数量，不归并为任一币种或全币种金额；全局不提供跨币种金额总和。

单条计算溢出标记不可重算及固定溢出数量，不取截断值；聚合／比例溢出则整体拒绝并返回固定参数／容量错误，不返回部分总数。该保护必须测试，即使真实 SQL decimal(18,6) 的常规范围很难触发。没有预算上界、校准建议或供应商账单匹配结果。

## 5. 接口、DTO 与页面 [Architect]

### 5.1 拟新增接口

`GET /api/ai/operations/cost-quality`，输入仅 From／To／PageIndex／PageSize；页码默认 1、每页默认 20、最大 50。对合法历史币种排序／分组后分页，防止大量格式合格但非标准币种造成响应无限增大；TotalCount 是完整有效币种分组数，不是调用数。页码使用 long 运算，超大合法页码返回空页，不整数溢出。

ApiResult 的固定安全响应拟包括 MetricsVersion=1、目标 TenantId、固定 Scope／CostBasis、实际 From／To／ObservedFrom／ObservedTo、Limits、完整全窗口的状态／依据／质量计数、两项 Token 比较摘要、TotalTokens 比较摘要，以及 `PagedResult<AiCostQualityCurrencySummary>`。CostBasis 固定为“历史快照估算一致性，不是供应商账单”。窗口全局摘要不随币种页变化，币种页摘要只对各自同币种样本计算，不外推到窗口外或其他页。

实施补充：四项金额 StoredCost／RecomputedCost／DifferenceCost／AbsoluteDifferenceCost 在后台仍为 `decimal?`，JSON 使用精确十进制字符串或 null；前端类型为 `string | null`，显示时补足六位小数，不转为 JavaScript Number。该契约防止合法大额聚合在浏览器丢失小数精度，不更改数据库或舍入语义。

不输出用户／会话／Run／usage／Provider 引用、供应商请求 ID、模型／供应商名称、原单价、URL、正文、Prompt／配置／快照 JSON、错误自由字符串或 TraceId。Controller 仅调用 Application，响应 NoStore。GET 沿用原中间件普通日志，不额外写“校准通过”或“对账通过”的审计事实。

错误采用既有 ApiResult／异常机制：失效身份 401、无权／无明确活动目标／系统范围 403；窗口、分页、容量或聚合溢出 400；观察群体变化 409；准入限流 429。取消／截止沿用既有取消和基础设施异常映射，不在本批改全局中间件，也不回显值或异常原文。空数据为合法 200，计数 0、无样本金额／比例 null、币种空页。

### 5.2 页面

建议原运营页增加仅 view 权限可见的“估算质量核验”入口，用独立子组件承载主动 GET、独立调用时间／分页、质量计数、Token 成对样本与分币种费用一致性表，继续 Element Plus、原 Axios／Pinia／权限及主题，不引入图表依赖或扩大原 index.vue。首次进入运营页不自动多读 50,000 行；未主动打开或无权时不请求。

固定说明调用创建窗口与 Run 窗口不同、终态含失败／取消、数值为已记录而非来源认证、输出是上限、费用是历史估算一致性、问题计数可重叠。显示每项样本数，缺样本用“—”，合法零显示 0；不提供“已对账／建议改价／修复历史费用”按钮。

身份／权限／目标／调用窗口变化、关闭、KeepAlive 停用或卸载时 abort、清理摘要和分页结果，并通过代次／身份键丢弃迟到响应。再次加载不复用上一窗口金额；Scope／TenantId／实际窗口不符则丢弃。失败保留固定重试入口，容量提示缩短窗口；不本地持久化质量 DTO，不发送文件或模型请求。

## 6. 数据影响、兼容与回退 [DBA]

**建议无数据库结构、迁移、权限／菜单种子或历史回填变更。** 本轮仅文档，无数据库变更；实施拟复用 AiUsageLog／AiRun、原索引、软删除和查询抽象，不新增对账实体、不更改 decimal 精度、不启用 Snapshot isolation、不改数据保留。GET 仍可能通过原中间件尽力新增普通请求日志。

现有 usage 的 Run 关联不是租户组合外键，新查询须显式双侧租户过滤及二次群体复核。按 usage 创建时间、稳定排序和两次宽标量读取的原索引覆盖／内存／10 秒成本仍需实际隔离 SQL 计划及受控规模验证；Take 不是性能保证。若不满足，返回 Architect／DBA 另提最小索引及迁移方案，不直接改库、提高上限或删记录。

本批不改变 AiUsageLog.SettleCost、AiBudgetService、AiConversationService、网关、后台 watchdog 或 Run 的历史聚合；已有结算回归必须保留。现有方向 4 汇总、方向 5 文件／凭据及权限契约不变。回退撤销新入口／服务即可，不修改已存金额、价格、预留或审计，不执行清表或结构 Down。

新 Application 编译影响 AIC-005 BuildIdentity，后续发布仍须按新构建冻结／评测及兼容核验；Offline 不代替正式资格。

## 7. 预计文件与实施顺序 [Developer]

下表是确认后预计变更，文件尚未实现；不把规划路径误当已有接口。

| 范围             | 预计文件／改动                                                                                                                                           |
| ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application      | 新增 `AiCenter/AiCostQualityModels.cs`、`AiCostQualityService.cs`、内部纯计算 `AiCostQualityCalculator.cs`；DI 注册，复用抽象与 fresh 授权模式，不新增包 |
| API              | `Controllers/AiOperationsController.cs` 新增单 GET／构造依赖／NoStore，原入口保持兼容                                                                    |
| 前端             | `src/api/ai.ts`、`src/views/ai/operations/index.vue` 最小修改；新增 `AiCostQuality.vue` 独立子组件                                                       |
| UnitTests        | 新增 `AiCenter/Aic012CostQualityTests.cs`、`Aic012CostQualitySqlTranslationTests.cs`；正常历史结算以现有领域方法生成合成事实，再独立期望比对             |
| IntegrationTests | 新增 `AiCenter/Aic012CostQualityApiTests.cs`、`Aic012CostQualitySqlTests.cs`；原 Aic012OperationsApiTests 的 DI／stub 最小适配                           |
| 前端测试         | 新增 `src/views/ai/operations/AiCostQuality.test.ts`；补原 `index.test.ts`、`src/api/ai-operations.test.ts`                                              |
| 文档             | 本方案、AIC-012 批次登记、后续／总体计划；实施后新增 `docs/ai-center-aic-012-cost-quality-acceptance.md` 独立记录                                        |

后端路径位于既有 `backend/PermissionSystem.Application`／`PermissionSystem.Api`／`PermissionSystem.UnitTests`／`PermissionSystem.IntegrationTests`；前端位于 `frontend/permission-admin`，子组件位于 `src/views/ai/operations`。

顺序：确认具体范围与参数 → 纯计算／DTO及核心测试 → 有界双侧查询／fresh 授权／限流及截止 → HTTP → 前端主动核验与清理 → 回归及 Reviewer／文档闭环。没有后台任务、文件导入、新框架或真实模型执行。

## 8. 测试与验收 [Reviewer]

| 层次       | 必须覆盖                                                                                                                                                                               |
| ---------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 人口与选择 | 空数据／合法零、三种终态／两种未决／未知状态；按 usage 时间而非 Run 时间、跨窗 Run、失败／取消／重试各自计数；Token 依据四类与计数守恒                                                 |
| 算术与诊断 | 正常领域结算历史、当前配置改价不影响历史、双记录／混合／双回退、缺失／负值／零单价、缺失／无效币种、舍入中点六位、存储／重算差异、同币种相反差额不能抵消绝对差额、未知费用不补零、溢出 |
| Token 样本 | 已记录／估算成对分母、无样本 null、零估算另计、long 总数不溢出、输出为上限而非预测、超过 100% 保留提示、TotalTokens 合计不一致，标记可以重叠                                           |
| 来源与权限 | 双侧租户／非删除／不可读 Run 排除、不读配置、普通／超管明确活动目标、仅 view 无需 export、无权／撤权／停用／安全戳、入口与返回前变化、第二次查询新增／更改／删除／关联变化、无成功缓存 |
| 容量／保护 | 50,000／50,001 整体拒绝、稳定币种排序／分页／巨大页码、30／90 天与未来窗口限制、独立 actor／tenant 限流及取消／10 秒截止，无导出锁或模型调用                                           |
| SQL        | 离线翻译证明双侧 TenantId／IsDeleted／usage CreatedAt／固定排序／Take／白名单投影；显式隔离库验证实际结果、关联隔离、来源变化／撤权、边界规模及查询计划，缺环境跳过不能记通过          |
| HTTP／前端 | DTO 只允许窗口／分页、NoStore、安全聚合／无原单价／明细；主动打开、独立时间、样本与未知／0、取消／身份／权限／租户／窗口／分页变化、关闭／KeepAlive／卸载及迟到丢弃、失败恢复          |
| 回归       | 原预算／结算／会话／后台执行及 AIC-012 三批回归；全量后端／Legacy、前端 Vitest／类型／build／预算／lint／格式、原 AIC-004 Offline；独立产物与报告，不弱化原 Windows 证书失败           |

真实隔离 SQL、Redis、HTTPS 浏览器、实际规模和业务 Owner 对口径与使用权限签认仍是目标环境退出条件；需要合法明确范围，不能用替身／离线翻译替代。复用 AIC-012 SQL 合成夹具的显式隔离条件、外层事务回滚，不自动迁移或读取生产 usage，不要求在聊天提供连接串或秘密。

## 9. 方案确认与规划结论 [Architect] [DBA] [Reviewer]

用户已确认本批：**当前活动单租户租户级安全聚合；复用 view 权限；独立 usage 创建窗口；50,000 行／页 20 至 50／actor 6 与 tenant 12 每分钟／10 秒截止；历史快照只读重算、分项有效 Token 选择与未知诊断、输入估算成对差异及输出上限使用情况、分币种同样本费用差异；不导入账单、不展示原单价／调用明细、不改价／结算／历史／留存、不增加结构或种子。**

首批主题选择之后，本具体方案的统计口径、可见范围与保护参数已通过用户“确认”获得实施授权。按 [AGENTS.md](../AGENTS.md) 第 4／5 节进入 Developer；同一已确认范围不重复询问。

规划阶段改动为新增方向 3 首批方案与计划登记；当时仅静态核查 usage、历史价格／舍入／混合回退、输入字符估算／输出上限、网关／本地零 Token、索引／现有权限及运营模式，未运行本批 build／test。确认后的实际实施与独立验证另见第 10 节，不借用方向 4、5 历史测试作本批证据。无 Token 来源／实际计费证据，历史预留或费用可能无法重算；真实对账、Owner、目标环境及发布资格缺口保留。

Reviewer 规划结论：**通过，方案已获确认；不替代功能或环境验收。** 当前方向 3 为“估算质量核验首批已实现、待验收”；方向 4／5 各已实现批次仍待验收，AIC-012、成本对账与自动校准整体未完成。

## 10. 实际实施与复核 [Developer] [Reviewer]

已新增纯计算／安全 DTO／有界查询服务、DI 和 NoStore GET；原运营页主动打开独立组件，支持调用时间、币种分页、缺值与零区分，以及身份／权限／租户／窗口变化、关闭、KeepAlive 停用及卸载时取消和丢弃迟到结果。计算沿用历史快照与分项有效 Token 选择，六位 AwayFromZero；金额以精确十进制字符串传输和显示。原方向 4、5 契约与已有未提交改动保留，无新增依赖、数据库结构、迁移、种子或历史写回。

最终独立产物 `obj/aic012-cost-quality-final2`：Release build 成功；本批核心／离线 SQL 翻译 49/49、HTTP 6/6，通过最终全量报告核实 AIC-012 四批核心 186/186、HTTP 共 25 项通过。全量 Unit 1101 通过、1 项既有 Windows 证书导入失败；集成 107 通过、49 环境跳过（本批 SQL 3 项），Legacy 45/45。前端 148/148、类型／build／预算／格式通过，lint 0 error、4 条既有 warning；Offline 41 变体确定性检查通过，正式发布门槛仍未满足。

Reviewer 对已确认代码范围结论：**通过，首批待环境验收。** 真实 SQL／查询计划／正式规模、Redis 跨实例、真实 OpenIddict 浏览器／宽窄屏／主题、Owner 签认和新 Application BuildIdentity 的重新冻结／评测仍待完成；双次观察不是事务历史快照，质量差异不是实际账单或自动改价依据。报告路径、验证限制和退出条件见[独立验收记录](ai-center-aic-012-cost-quality-acceptance.md)。未启动真实宿主、迁移、清理、部署、真实模型或业务数据读取，未 commit／push。
