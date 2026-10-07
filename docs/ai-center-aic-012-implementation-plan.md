# AIC-012 实施方案：运营指标细分首批（已实现，待验收）

> 日期：2026-10-07。用户已确认分批方案、方向 4“运营指标细分”及第 8 节具体指标、容量和页面边界；首批已实现、待环境与业务验收。
> 依据：[项目规则](../AGENTS.md)、[后续开发计划](ai-center-next-development-plan.md)第 8 节、[总体实施计划](ai-center-mcp-implementation-plan.md) P5-C／P5-D 及风险登记。
> 第 1～7 节保留调研阶段记录，第 8 节保留已批准的具体提案，第 9 节记录实施闭环；最新证据与限制见 [验收记录](ai-center-aic-012-acceptance.md)。其余方向不进入首批。

## 1. 任务判断 [Architect]

AIC-012 是按实际需求独立立项的六个方向，不是一次全部实现的既定任务。此次“处理 AIC-012”启动调研和方案阶段；不能据此推定真实 ERP／WMS 对象、查询字段、审批规则、供应商账单或合规期限。

用户已确认分批方案，并选择“运营指标细分”。尚未提供业务价值／人工纠错的采集规则及 Owner／正式容量目标，首批按已确认第 8 节实现现有数据可以核验的技术运营指标。AIC-007／009／010／011 的 Demo 或合成批次确认不覆盖本任务真实业务，也不授权扩大外部 MCP、后台写入或数据导出范围。

每批只处理已选择的方向。真实外部查询、写入、账单对账、归档导出和复杂工作流保留第 3 节启动条件，不在此次运营细分中实现。

## 2. 已查证的复用基础 [Architect]

以下结论来自本轮静态阅读，不代表真实环境验收通过。

| 能力 | 当前证据 | 扩展限制 |
| --- | --- | --- |
| 内部只读工具 | `AiTools/AiReadOnlyToolRegistry.cs`、`AiToolModels.cs`：受控注册、权限过滤、当前租户／用户上下文、超时和引用 | 继续 Application 直接编排；新增工具必须显式纳入场景白名单及版本评测，不自动发布全部工具 |
| 模型出站治理 | Infrastructure 的 `Ai/AiHttpTransport.cs`：DNS／IP 校验后固定连接目标、禁用重定向／代理／Cookie；`OpenAiCompatibleEndpointValidator.cs` | 当前实现服务于模型网关，不能直接视为通用 REST／MCP 客户端；仅复用经评审的模式，不先抽取通用框架 |
| 开放集成 | `Integration/OpenIntegrationService.cs`；设计文档第 12 节 | 入站客户端／Webhook 治理不等于主动出站连接器；不得将 `ApiClientSecret` 当作外部系统凭据 |
| 草稿确认和执行 | `AiActions/AiDocumentExecutionService.cs`：本人／租户归属、确认版本／Hash、并发、幂等及 Outbox；实际执行调用 Demo 业务服务 | 内部数据库事务不能覆盖外部系统写入；不承诺分布式恰好一次，不自动重试结果不明的写请求 |
| 费用估算 | Domain 的 `Entities/AiUsageLog.cs`：usage、估算 Token、价格快照、币种、ProviderRequestId、预留及估算结算 | 不是供应商账单事实；缺少匹配契约前，不假设请求 ID 足以逐笔对账，不自动换汇 |
| 运营查询 | `AiCenter/AiOperationsService.cs`／`AiOperationsModels.cs`、API 的 `AiOperationsController.cs`、前端 `views/ai/operations/index.vue` | 已有运行／供应商／UTC 日期汇总，默认 30 天、最大 90 天；Run 有场景／版本引用，但当前 DTO 未提供场景细分；当前全量载入窗口数据，扩展前评估规模 |
| 到期清理 | Infrastructure 的 `Ai/AiRetentionHostedService.cs`：内容脱敏及到期审计硬删除 | 不等同于分层归档、法律保留或合规导出；本轮不改变现有留存策略 |
| 验证基础 | UnitTests 的 `AiCenter/`、IntegrationTests 的 `AiCenter/`、AIC-004 评测、前端既有 Vitest | 现有 `AiOperationsServiceTests.cs` 两项覆盖反馈，不足以作为新运营汇总或对账验收证据；按首批范围补测试 |

上表 Application 相对路径位于 `backend/PermissionSystem.Application/`，Domain、Infrastructure、API 分别位于对应后端工程；前端路径位于 `frontend/permission-admin/src/`。

## 3. 首批选择及必须补齐的信息 [Architect]

| 方向 | 启动输入 | 首批交付边界 | 预计影响位置（具体文件待契约确定） |
| --- | --- | --- | --- |
| 外部只读 MCP／REST 连接器 | 一个稳定查询需求、Owner、协议／版本／测试环境、正式请求响应样例、字段分级、租户／组织／仓库／账套映射、凭据类型、时效／限流／超时要求；不适用维度须注明 | 单一系统、单一查询；服务端身份映射、显式字段白名单、响应契约验证、来源／时效／截断、故障拒绝；内部或外部公开另行选择 | Application 的 AiTools／接口定义；Infrastructure 的单一协议适配及 DI；必要的配置、结构化结果、场景版本与测试 |
| 外部写入动作 | 单据及 Owner、必填字段／状态机／审批、正式接口、幂等键有效期与作用域、结果查询能力、超时后人工处置／补偿契约、审计期限 | 单一经批准动作，人工确认、逐次授权、持久化执行状态、结果不明与失败区分；后台兼容单独评审 | Application 的 AiActions；Infrastructure 的外部适配及恢复存储；Domain／映射／迁移视契约评审；必要的 API、人工处置界面及测试 |
| 成本对账与估算校准 | 供应商账单脱敏样例、账期／时区／币种、税费／折扣／退款口径、usage 与账单匹配规则、审核人／容差／留存 | 单一供应商／币种的可核验对账；实际账单、usage、估算与未知分开；校准不得重写历史价格快照 | AiCenter 的预算／运营、AiUsageLog 相关读取、必要对账实体及导入入口、运营页与测试 |
| 运营指标细分 | 首批场景、业务 Owner、完成率分母、取消／超时／失败归类、反馈覆盖／人工纠错定义、时间区间／时区、历史未绑定场景口径、性能目标 | 经确认的少量场景指标；保留旧汇总兼容和未知桶，按币种展示，聚合不暴露会话正文或用户明细 | AiOperationsModels／Service、AiOperationsController、前端 api/ai.ts 和运营页、后端与前端测试；是否新增索引由查询计划决定 |
| 分层归档与合规导出 | 数据规模、Owner／合规责任人、留存／法律保留期限、导出用途／字段／接收人／格式、脱敏／删除及访问审计规则 | 单一受控导出或归档范围；独立授权和审计，下载再次鉴权、租户隔离，完成归档验证后才能按批准策略清理 | Application 的受控用例／接口、Infrastructure 的归档／存储、AiRetentionHostedService 协调、必要 API／下载入口及测试 |
| 复杂工作流或多 Agent | 可复现的复杂流程、现有单助手不足证据、步骤／人工节点／失败边界、收益／延迟／预算目标 | 先比较既有编排、后台 Run、调度和工具能力；确有证据再单独评审框架及多 Agent | 先做对比方案与评测；当前不预定代码目录、不新增依赖或并行权限体系 |

凭据只通过既有受保护配置或确认的秘密管理机制配置；需求材料不提供密码、Token、Cookie、证书或连接串。

## 4. 分层、权限与异常语义 [Architect]

1. 保持 Api/Worker → Application → Domain；协议适配放 Infrastructure，Controller 仅处理入口与 DTO。异步 I/O 传递 CancellationToken。
2. 延续 OpenIddict 和现有权限策略。首批明确操作及业务权限矩阵；缺少适用权限时提出专用权限种子方案，不借用宽泛管理权限。模型参数不能选择租户、外部账套、服务凭据或任意 URL。
3. 外部结果先按当前租户、服务身份与调用者数据范围校验，再做字段筛选和结果封套；映射缺失或冲突时拒绝。外部描述、内容、链接均不成为新指令或外联授权。
4. 外部连接先核对 EA-026 的适用验收，叠加目标域名／端口及网络出站策略、TLS、DNS/IP 一致性、响应大小与并发限制。总体文档仍将 EA-026 列为未完成，不能据模型 transport 推定全平台已完成。开放平台自身外部 MCP 时还须完成 AIC-001 对应验收。
5. 查询分别表达无数据、无权、超时、协议异常及来源不可用。写入超时／连接断开后的结果不明保持独立状态，通过目标系统正式查询或人工处置核对，不当作确定失败后自动再写。
6. 运营接口只提供已授权聚合；估算、实际账单和未知值保持可识别。人工纠错率不能直接用差评率替代。新增结果类型、工具与场景版本沿用 AIC-003／004／005 的契约、评测和发布机制。

## 5. 数据影响、兼容及回退 [DBA]

**结论：无数据库变更。** 调研阶段只修改文档；后续运营首批复用现有实体和索引，未修改实体、迁移、实际配置或数据库。

后续批次不能统一承诺无迁移：运营细分可能复用现有 Run／Usage／Feedback；外部连接映射、写入恢复、对账与归档可能需要持久化结构。首批确定后逐项评审 BaseEntity、TenantId、审计、软删除、rowversion、租户组合约束、索引、数据容量和留存，不能为未知业务先建通用表。

优先兼容新增字段／DTO；历史缺少场景、请求匹配键或幂等键时明确为未知，不伪造事实或授权。存在实际迁移时先生成增量审核 SQL 和备份／恢复方案，在明确的隔离环境验证；未经另行授权不执行实际迁移。

回退先停用新增能力及有关后台任务，保留审计、执行／对账／归档记录，继续旧兼容读取；不执行清表或破坏性 Down。外部已发生的业务效果不能靠本地事务回滚撤销。

## 6. 确认后的实施与验收步骤 [Developer]

1. 根据首批选择核验正式契约和业务口径，补齐本方案的具体 DTO、接口、权限矩阵、预计文件、异常／恢复状态及 DBA 结论，提交用户确认。
2. 按确认范围实现单一用例，复用现有 DI、Axios、Pinia、路由和权限控制；不先抽取通用连接器或工作流框架。
3. 补充允许／拒绝／跨租户／撤权／缺失映射与契约异常测试；只读验证完整性和业务结果一致，写入验证确认后变化、重复效果及结果不明；运营验证分母、空样本／未知桶、多币种、时间边界、历史兼容和规模限制。
4. 执行实际涉及的 build／test／类型／lint；确定性测试、隔离 SQL、真实供应商／目标接口、浏览器和性能证据分别记录。跳过的 SQL 或缺少真实接口不得计为通过，不使用历史结果代替本轮结果。
5. 由 Reviewer 核查边界、数据和验收证据，更新对应方案、后续开发计划及总体实施计划。真实业务 Owner 签认和适用目标环境退出条件满足前，不将 AIC-012 整体标为完成。

## 7. 调研阶段复核记录 [Reviewer]

调研阶段方案通过，当时业务实现尚未启动，未运行应用、调用外部系统、读取使用记录／账单、执行数据库清理／迁移或运行构建／测试。确认后的实际验证另见第 9 节及验收记录。

第 8 节具体提案随后已获用户确认并实施。缺少业务纠错、真实外部契约或合规规则时，暂停对应业务实现，不以差评、虚构接口或 Demo 替代真实验收。

## 8. 运营指标细分首批具体提案 [Architect]

### 8.1 目标及业务缺口

在现有 AI 运营页增加“场景统计”，按已有 `AiRun.ScenarioId` 聚合运行、反馈、耗时和模型使用记录，不新增模型调用、通知、外部系统访问或业务写入。首批提供技术运行完成情况；业务任务是否真正完成、人工是否纠错，当前没有可核验采集记录，页面明确显示“未采集”，不返回伪造的 0% 指标。

首批按场景汇总已实际存在的运行，跨该场景已有发布版本合计，不先做版本对比或自动运营判定。当前目录只有权限助手，历史未绑定场景仍可能包含其他内部工具用例；不能从用户问题、AgentCode 或工具名称反推并回填场景。

用户确认本节技术口径后可以实施；业务完成率／人工纠错率、真实场景价值评价、Owner 与正式容量目标仍需后续签认。以下容量参数是拟定保护上限，不是已有性能实测结论。

### 8.2 拟定指标契约

统计群体为当前明确租户、非删除、`Run.CreatedAt >= From && Run.CreatedAt < To` 的运行。默认过去 30 天，最多 90 天，保留现有结束时间不得超过当前时间 5 分钟的校验；实际时间边界以响应的带偏移时间为准。时间窗口按运行创建时间选取，不按完成时间或反馈提交时间选取；反馈表示读取时的当前评价，不宣称历史时点快照。

| 指标 | 具体提案 |
| --- | --- |
| 运行次数 | 所选创建时间窗口内每个 Run 计一次；重试创建的新 Run 也独立计数，不冒充去重业务任务数 |
| 状态数量 | 单独统计 Pending、Running、Completed、Failed、Cancelled；未知枚举值单独记录，不计为成功或已终结 |
| 终态技术完成率 | `Completed / (Completed + Failed + Cancelled)`；排除 Pending／Running／未知状态；分母为 0 时返回 null，页面显示“—” |
| 超时失败数量 | Failed 中 ErrorCode 精确为 `run_timeout` 或 `run_queue_timeout` 的数量；是失败数量子集，不重复计入终态分母；`run_orphaned`／`run_interrupted` 等不推定为已证明超时 |
| 反馈可评价次数 | Completed 且 ResponseMessageId 非空的 Run 数；符合现有反馈提交条件 |
| 反馈覆盖率 | 带有效本人当前反馈的可评价 Run 数／反馈可评价次数；反馈同时匹配 TenantId、RunId、Run.ActorUserId 与 ResponseMessageId，仅接受现有正／负评价，不读取备注；分母 0 时为 null |
| 反馈好评率 | 正评价数／有效正负评价总数；与反馈覆盖率分别展示，不称为人工纠错率；无评价时为 null |
| 人工纠错率／业务完成率 | 首批标记“未采集”；差评的 `incorrect` 原因仅表示用户反馈，不证明人工已经改正或业务完成 |
| P95 运行耗时 | 仅终态且 DurationMilliseconds 非负的已有记录，按升序取最近秩 `ceil(N × 0.95)`；返回有效样本数，无样本为 null；不重算缺失耗时，也不宣称统一为模型延迟或统一包含排队 |
| Token | 汇总 usage 中非负的实际 InputTokens／OutputTokens，分别展示；估算 Token 不补成实际 usage；已终结且实际任一 Token 缺失／无效的调用数单独展示 |
| 分币种估算费用 | 仅汇总已终结调用的非负 EstimatedCost 且有效三位大写币种；USD／CNY 等分别返回，不换汇、不使用 Run.EstimatedCost 跨币种合计、不计仍预留金额；0 费用与未知分开 |
| 未知费用／未决调用 | 已终结调用缺金额／币种或值无效时计入“估算费用未知”；Pending／Running 单列未决，不当作费用为 0 或已经结算 |

比率由服务端返回百分数，保留两位小数；同时返回分子／分母数量，页面不重复定义统计公式。取消进入终态技术完成率分母，其数量可见。现有首页汇总和 UTC 日趋势继续使用原契约及公式；新增指标名称明确为“终态技术完成率”，不把两种分母混为同一指标。

历史 ScenarioId 为 NULL 的运行进入独立“未绑定场景”组。非空引用的场景已删除或元数据不可读时保留原场景 ID 单独聚合，显示“场景不可用”，不读取其他租户或被删除的名称；停用但未删除的场景仍统计。场景改名后显示当前可读名称，不宣称是运行时名称；不读取 SnapshotJson／Prompt 来恢复显示名称。没有 Run 的场景不额外枚举。

### 8.3 API、分层及授权

拟新增只读 `GET /api/ai/operations/scenarios`，继续使用现有 `ai:operations:view`、ApiResult 和 PagedResult；请求包含 From、To、PageIndex、PageSize，服务端固定按运行次数降序、场景 ID 排序，默认每页 20、最大 50，不开放任意排序表达式、用户 ID、外部租户选择或导出参数。

拟新增 `IAiScenarioOperationsService` 及其 Application 实现，独立负责场景汇总；复用已有仓储、IAsyncQueryExecutor、ICurrentUserService、ITenantContext、IUserCredentialValidator 和 PermissionEvaluation。旧 IAiOperationsService 的反馈／汇总公式保持兼容，不在本批重构其查询流程。

Application 入口及返回前复核有效身份、当前权限、租户上下文和目标租户状态：普通用户必须当前身份租户与上下文一致；超级管理员只有当前身份和重新读取的有效角色均支持原超级管理员权限，才按平台已解析的明确目标租户读取，仍仅一个租户，不提供全租户汇总。系统作用域或缺少有效目标租户时拒绝；客户端普通 query 参数不能切换租户。复用 GetAuthenticationStateAsync／ResolveActiveTenantIdAsync 校验，不能仅靠历史 claims 或菜单可见性。

所有 Run／Usage／Feedback／Scenario 查询显式匹配同一 TenantId 和非删除条件；usage／feedback 的 Run 关联同时核对租户，避免依赖只有 RunId 的外键作为跨租户保护。结果只返回场景引用、可读名称和聚合，绝不返回用户、会话／消息正文、反馈备注、Prompt、请求／响应 JSON、凭据、运行错误详情或原始用量记录。新 API 采用 NoStore 并传递 CancellationToken。

响应拟为 `AiScenarioOperationsResponse`：口径版本、From／To、观测起止时间、容量上限和 `PagedResult<AiScenarioOperationsItemResponse>`。每个条目包含场景引用／名称可用性、上述计数／比率／P95 样本／实际 Token／分币种估算／未知与未决数量；不返回原始 Run ID 或 User ID。

### 8.4 查询容量及一致性限制 [DBA]

**首批结论：无数据库变更。** 不新增实体、迁移、索引、回填或物化汇总；复用现有 TenantId＋CreatedAt 相关索引和 RunId 关联。索引是否覆盖、实际排序及读放大须看隔离 SQL 查询计划后再评审，不能声称现有索引已经满足正式规模。

为保持最小实现并避免无界加载完整实体，新查询拟投影必要标量并设置硬上限：Run 最多 10,000，关联 usage 最多 50,000，关联有效 feedback 最多 10,000。分别采用 Take(上限＋1) 探测越界；任一超限则整体拒绝，要求缩短时间窗口，不返回前若干记录算出的不完整百分位／总量。后续分页仅分页场景组，不能绕过样本上限。投影不包含消息／备注／Prompt；场景名称仅查询已出现的当前页引用。

先固定本次 Run 集合、状态和反馈可评价集合，再读取其关联 usage／feedback 并在容量内聚合；反馈只匹配固定集合中可评价的 Run，避免后续新完成的运行造成分子／分母错位。保留观测起止时间并说明多次读取可能期间发生评价或用量更新，首批不承诺金融对账或事务性同一时点快照，也不对现有数据库启用 Snapshot isolation。运行集合在查询后发生软删除／租户变化时不复用缓存；返回前再次检查对应引用仍属当前租户并非删除，发生变化则本次拒绝并提示重新查询。

不新增正文缓存或永久指标快照。容量上限只约束返回到 Application 的记录规模，不代替数据库扫描、排序、并发及 P95 请求耗时的实测。页面继续使用旧 summary 接口，其既有最多 90 天全量读取风险另列，不把本次有界场景查询当作已解决所有运营查询规模问题。

回退应用后只失去新增统计入口，旧 summary／feedback 契约可继续使用；没有结构回退、清表或数据恢复动作。Application 编译变更仍可能影响 AIC-005 构建身份／发布资格；正式场景须按既有冻结、评测和发布门禁复核，不改 Hash 或绕过版本一致性校验。

### 8.5 现有页面的最小调整

复用 Vue 3、Pinia／Axios、Element Plus 和当前运营页，增加“场景统计”标签页及分页，不新建页面／菜单／权限点／图表依赖。与原汇总共用所选时间区间，新标签显示响应实际边界、观测时间和口径说明。

主表显示场景、运行数量、终态技术完成率（带分子／分母）、反馈覆盖率／好评率、P95 与样本数、分币种估算费用；可展开当前场景的状态、Token、未知费用／usage／未决调用和“未采集”说明。窄屏横向滚动限表格容器，保持页面／工具栏正常布局，不以颜色代替状态文本。

查询按钮有明确加载状态和日期标签；首次加载、无数据、零分母、无耗时、无已知估算和超容量失败分别呈现。失败清除本次表格并保留重试入口，不把上次结果冒充本次。采用既有页面的请求代次模式，切换时间／分页、身份／目标租户变化、撤权、离开页面或 KeepAlive 停用时丢弃迟到响应并清理全部运营数据；复用 auth Store，不建立第二套权限状态。

设计依据：ui-ux-pro-max 的 Vue 查询命中 setup／onMounted 获取数据及既有 Pinia 模式；quick-reference 的表单可见标签、loading、empty-states、error-recovery 和 progressive-disclosure。沿用项目布局、组件与语义颜色，不另生成全局设计系统。

### 8.6 预计文件 [Developer]

以下新增名称为拟定文件，不表示已实现：

| 工程 | 预计文件及用途 |
| --- | --- |
| Application | 新增 `AiCenter/AiScenarioOperationsModels.cs`、`AiCenter/AiScenarioOperationsService.cs`；修改 `DependencyInjection.cs` 注册专用查询服务 |
| API | 修改 `Controllers/AiOperationsController.cs` 增加 DTO／ApiResult 入口和原运营权限／NoStore |
| 前端 | 修改 `src/api/ai.ts` 和 `src/views/ai/operations/index.vue`；新增该页面 `index.test.ts`，必要的 API 契约测试 |
| UnitTests | 新增 `AiCenter/Aic012ScenarioOperationsTests.cs`、`AiCenter/Aic012SqlTranslationTests.cs`，复用已有测试替身 |
| IntegrationTests | 新增 `AiCenter/Aic012OperationsApiTests.cs`、`AiCenter/Aic012OperationsSqlTests.cs`；SQL 仅显式隔离环境开启、使用回滚合成夹具，不自动迁移 |
| 文档 | 本方案、后续计划／总体实施计划；实现后新增 `docs/ai-center-aic-012-acceptance.md` 记录实际证据与限制 |

后端工程目录以既有 `backend/PermissionSystem.*` 为准；前端路径位于 `frontend/permission-admin/`。本批不修改 Domain、Infrastructure 生产实现、迁移、真实配置或敏感数据。

### 8.7 验证与退出条件 [Reviewer]

- 确定性用例：五种状态及未知值、超时子集、重试独立计数、NULL／不可用／停用／改名场景、空样本、零分母、P95 最近秩、缺失／负耗时、反馈归属／响应不匹配、差评不能证明纠错、实际／估算／未知 Token、多币种／缺金额／零费用、未决调用、左闭右开、30／90 天与非法时间／分页、三个容量上限及软删除变化。
- 授权：匿名／缺权限、失效用户／租户、请求前后撤权、租户缺失／普通用户上下文不一致、超级管理员明确单目标、系统作用域拒绝、关联数据跨租户、软删除及响应无敏感字段。
- SQL：使用真实 SQL Server provider 离线 ToQueryString 检查投影、租户／软删除／关联／时间条件及 Take 上限；隔离 SQL 验证实际结果与合成可信基线、查询计划／容量边界和并发变化，缺环境跳过不计通过。
- HTTP／前端：原权限策略／DTO／NoStore、旧 summary 兼容，新表分页／百分比／缺失／估算说明、错误重试和容量提示、权限／租户切换与迟到请求清理，组件测试和浏览器体验分别记录。
- 实现后运行后端解决方案 Release build、专项及相关回归，前端 `npm run test:unit`／`npm run type-check`／`npm run lint`／`npm run build`，修改前端文件另做 Prettier check；沿用 AIC-004 Offline 回归，不执行 Live／付费请求。SQL、真实模型／业务 Owner 和正式容量未验收时保持适用批次待验收。

用户已回复“确认”，批准本节指标、10,000／50,000／10,000 容量上限和“人工纠错／业务完成未采集”的首批边界；实施与验证结果如下，不重复请求同一范围的许可。

## 9. 已确认首批实施闭环 [Developer] [Reviewer]

- 新增 Application 场景统计 DTO／服务及 DI；API 增加原 `ai:operations:view` 权限下的 NoStore 只读入口，Controller 不访问 DbContext。
- 有界标量读取、固定 Run 群体和反馈资格、终态分母、精确超时子集、P95 最近秩、实际 Token 与分币种估算／未知／未决分类按第 8 节实现；保留旧 summary／feedback 契约。
- 当前活动单租户，入口和返回前复核当前身份／权限／租户，返回前检查 Run 及已读取场景元数据仍可读；不暴露原始 Run／用户／正文／备注。
- 原运营页新增场景标签、分页和展开明细；复用 Axios／Pinia，身份／目标租户／权限／日期变化和 KeepAlive 停用清理数据并丢弃迟到响应。
- 无数据库变更、新依赖、模型调用、实际迁移、部署或 Git 提交／推送。报告目录 `artifacts/aic-012/` 精确加入忽略规则。
- 后端 Release build、AIC-012 核心 46 项、HTTP 7 项、Legacy 45 项通过；前端全量 86 项及类型／lint／production build 通过，AIC-004 Offline 41 个变体确定性检查通过。
- 全量 UnitTests 为 961 通过、1 项既有 Windows 证书导入失败；IntegrationTests 为 89 通过、40 环境跳过，其中本批 SQL 3 项全部跳过，不能记为通过。

Reviewer 对已确认代码范围复核通过；整体状态为“运营首批待验收；其余方向暂缓／待启动”。隔离 SQL 结果／查询计划／并发、真实认证浏览器、正式容量与 Owner、业务指标采集及 AIC-005 发布兼容要求见 [验收记录](ai-center-aic-012-acceptance.md)。这不是发布或整个 AIC-012 的完成结论。

## 10. 方向 5 首批实施记录 [Architect] [DBA] [Developer] [Reviewer]

用户随后要求继续方向 5，选择先做“AI 运行技术元数据受控导出，不含正文、不改留存”，并回复“确认”批准 [独立导出方案](ai-center-aic-012-export-implementation-plan.md)。固定 Run／usage 白名单、单活动租户双权限、多次 fresh 复核、Requested／Prepared 凭据、有界 JSON／双 Hash、限流／锁和原页面主动确认下载已实现。无数据库结构或迁移变更，有权限／按钮种子和现有审计凭据写入；留存未改。

导出核心／离线 SQL 翻译 42/42、HTTP 6/6 通过；全量 Unit 1003 通过、1 项既有 Windows 证书导入失败，集成 95 通过、43 环境跳过（本批 SQL 3 项），前端全量 101/101、Release build 和 Offline 确定性检查通过。Reviewer 已确认范围复核通过，证据与目标环境条件见 [导出验收记录](ai-center-aic-012-export-acceptance.md)。本节登记独立批次，不覆盖第 9 节方向 4 的原验证记录；当前为“运营首批待验收；导出首批待验收；其余方向暂缓／待启动”，归档介质、期限、法律保留和副本处理等尚需业务签认，AIC-012 整体未完成。

## 11. 方向 5 第二批实施记录 [Architect] [DBA] [Developer] [Reviewer]

用户选择“导出凭据查询与完整性核对，沿用技术白名单、不改留存”，随后回复“确认”批准 [独立第二批具体方案](ai-center-aic-012-export-batch2-implementation-plan.md)。已实现本人／当前明确活动目标租户的安全凭据 GET、有界独立记录时间窗口、原双权限／fresh 复核、严格 v1 解析和浏览器本地完整文件 Hash 核对；Hash 后重读并比较凭据／阶段事实，不上传文件或 Hash、不增加表／迁移／权限种子、不改留存。损坏或未知窗口、重复／冲突 Prepared 不核对，Prepared／Hash 不等于送达或法律证明。

第二批核心／离线 SQL 翻译 49/49、HTTP 6/6，AIC-012 三批核心 137/137、HTTP 共 19 项通过；全量 Unit 1052 通过、1 项既有 Windows 证书导入失败，集成 101 通过、46 环境跳过（本批 SQL 3 项）。前端全量 127/127、类型／build／预算／格式、Legacy 45/45 和 Offline 41 个变体确定性检查通过；lint 0 error、4 warning，发布门禁仍 pending/failed。Reviewer 已确认代码范围复核通过，证据及环境退出条件见 [独立第二批验收记录](ai-center-aic-012-export-batch2-acceptance.md)，保留第 9、10 节原批次证据。当前为“运营首批待验收；导出首批待验收；凭据第二批待验收；其余方向暂缓／待启动”，SQL／Redis／真实浏览器／规模及 Owner 仍待验收，分层归档、法律保留和整个 AIC-012 未完成。

## 12. 方向 3 首批实施记录 [Architect] [DBA] [Developer] [Reviewer]

用户要求继续方向 3，选择“先规划只读估算质量核验（价格快照、Token、费用差异），不导入账单、不自动改价”，随后回复“确认”批准[独立具体方案](ai-center-aic-012-cost-quality-implementation-plan.md)。已实现当前活动单租户 view 权限安全聚合、独立 usage 创建窗口、有界双侧租户查询／fresh 复核；历史单价快照诊断、输入估算成对差异、输出上限使用情况、既有有效 Token 选择与分币种同样本费用一致性，以及原页面主动独立组件。四金额字段输出／显示为精确十进制字符串、不转 Number；不展示原单价或调用明细、不改结算／历史／配置／留存。

输入估算为字符数、输出为请求上限，部分后台失败在本地写零 Token；无 Token 来源／供应商计费证明，不能把有效记录全部称实测。重算只用历史快照，缺字段不从现配置补值，不将预留或估算当真实账单。无数据库结构／迁移／种子变更；实施沿用已确认的 50,000 行、actor 6／tenant 12 每分钟、10 秒截止及币种分页。真实账单对账／自动校准仍缺供应商契约、匹配／税费／币种口径和审核规则。

最终独立核心／离线 SQL 翻译 49/49、HTTP 6/6；从最终全量报告核实 AIC-012 四批核心 186/186、HTTP 共 25 项通过。全量 Unit 1101 通过、1 项既有 Windows 证书导入失败；集成 107 通过、49 环境跳过（本批 SQL 3 项），Legacy 45/45。前端 148/148、类型／build／预算／格式及 Release build 通过，lint 0 error、4 条既有 warning；Offline 41 变体确定性检查通过，发布门槛未满足。Reviewer 代码范围复核通过，报告和 SQL／Redis／真实浏览器／规模／Owner／新 BuildIdentity 退出条件见[独立验收记录](ai-center-aic-012-cost-quality-acceptance.md)。

当前状态为“运营首批待验收；导出首批待验收；凭据第二批待验收；估算质量首批待验收；其余方向暂缓／待启动”。本节不覆盖方向 4、5 的历史验收结果，不将 AIC-012、方向 3 或 P5-C／P5-D／P5-E 标为整体完成。

## 13. 方向 3 第二批实施记录 [Architect] [DBA] [Developer] [Reviewer]

用户要求继续方向 3 第二批并继续处理，随后回复“确认”批准[独立第二批方案](ai-center-aic-012-cost-quality-batch2-implementation-plan.md)。已实现只读 UTC usage 按日质量／Token 趋势和全窗口同币种存储高于／等于／低于重算分布，复用首批历史快照／Token 选择／舍入；日与窗口来自同一观察、可加字段守恒、比率重新加权，空日／部分日明确。方向统计在原计算器的同一 CostCounter，未重复创建费用公式。

原服务内局部共用双侧有界读取／完整群体比较／fresh 授权，原 GET v1 保留，新增 NoStore view 趋势 GET；最多 91 日、50,000 行、币种页 20 至 50、摘要／趋势共用 actor 6 与 tenant 12 每分钟及 10 秒截止。原 dialog 默认摘要，主动切换趋势；独立展示组件、取消／迟到丢弃、UTC 字符串和精确金额，无新依赖／路由。无数据库结构／迁移／种子变更，不增加账单导入、明细、原单价、模型／Provider 细分，不改结算／历史／配置／预算／留存。

第二批核心／离线 SQL 翻译 32/32、HTTP 7/7，本批 SQL 3 项环境跳过；最终全量报告核实 AIC-012 五批核心 218/218、HTTP 共 32 项通过。全量 Unit 1133 通过、1 项既有 Windows 证书导入失败；集成 114 通过、52 环境跳过，Legacy 45/45。前端 22 文件／173 项通过，类型／build／预算／格式及 Release build 通过，lint 0 error、4 条既有 warning；Offline 41 变体确定性检查通过，发布门槛仍 pending/failed。Reviewer 已确认代码范围复核通过，独立报告与 SQL／Redis／浏览器／规模／Owner／新 BuildIdentity 退出条件见[第二批验收记录](ai-center-aic-012-cost-quality-batch2-acceptance.md)，真实账单与首批环境缺口保留。

当前状态追加“只读趋势第二批待验收”，首批与方向 4、5 的待验收及原历史记录保持不变；方向 3、AIC-012 与 P5-C／P5-D／P5-E 整体未完成。未启动真实宿主、迁移、清理、部署、真实模型或业务数据读取，未 commit／push。

## 14. 方向 1 接入准备记录 [Architect] [DBA] [Reviewer]

用户要求“继续方向 1”。已静态核实只读工具注册、模型出站 transport、入站开放集成／MCP 准入及 EA-026 状态，形成[外部只读连接器接入准备方案](ai-center-aic-012-connector-preparation-plan.md)。尚未确定真实系统、Owner、单一查询、正式协议／字段／身份映射和目标环境；现有入站 MCP、ApiClientSecret、ExternalApiCallLog 及模型 transport 均不能直接视为已有业务出站连接器。

用户随后回复“确认”，已登记接入准备方案确认。当前仅文档准备，无数据库变更；方向 1 为“接入准备方案已确认、真实接入对象及查询契约待提供”，连接器未实现。真实业务实现须在契约补齐后提交具体方案并确认，真实连接须完成 EA-026 适用验收，对外公开另须 AIC-001 适用认证验收；不覆盖方向 3、4、5 的实现及待验收状态，不将 P5-C 或 AIC-012 标为完成。
