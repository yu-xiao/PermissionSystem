# AIC-012 方向 3 首批：只读估算质量核验实施／验收记录

> 日期：2026-10-07
> 状态：用户回复“确认”批准[具体方案](ai-center-aic-012-cost-quality-implementation-plan.md)，首批已实现、待环境验收。方向 4、5 的已有批次保持待验收；方向 3、AIC-012 整体未完成。
> 依据：[AGENTS.md](../AGENTS.md)、[后续计划](ai-center-next-development-plan.md)、[分批登记](ai-center-aic-012-implementation-plan.md)。本记录独立登记本批证据；代码复核通过不等于环境、财务对账或发布验收通过。

## 1. 改了什么 [Architect] [Developer]

在原运营页新增主动打开的“估算质量核验”，提供历史单价快照质量、已记录 Token 与输入估算／输出上限的成对比较，以及分币种、同一可比样本的存储费用与历史快照重算差异。查询时间按 usage 创建时间独立选择，不沿用 Run 窗口，首次进入运营页不额外发起质量查询。

| 层次        | 实际实现                                                                                                                                                                                                                                                                                                                                                  |
| ----------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application | [安全 DTO／契约](../backend/PermissionSystem.Application/AiCenter/AiCostQualityModels.cs)、[纯计算](../backend/PermissionSystem.Application/AiCenter/AiCostQualityCalculator.cs)、[查询服务](../backend/PermissionSystem.Application/AiCenter/AiCostQualityService.cs)及 [DI](../backend/PermissionSystem.Application/DependencyInjection.cs)；无新增依赖 |
| API         | [AiOperationsController](../backend/PermissionSystem.Api/Controllers/AiOperationsController.cs) 新增 `GET /api/ai/operations/cost-quality`，view 策略、NoStore、ApiResult、安全 DTO、CancellationToken；不直接访问数据库                                                                                                                                  |
| 前端        | [API](../frontend/permission-admin/src/api/ai.ts)、[原运营页](../frontend/permission-admin/src/views/ai/operations/index.vue)、[独立质量组件](../frontend/permission-admin/src/views/ai/operations/AiCostQuality.vue)；复用 Axios／Pinia／Element Plus／主题                                                                                              |
| 核心验证    | [48 个核心案例](../backend/PermissionSystem.UnitTests/AiCenter/Aic012CostQualityTests.cs)、[离线 SQL 翻译](../backend/PermissionSystem.UnitTests/AiCenter/Aic012CostQualitySqlTranslationTests.cs)                                                                                                                                                        |
| HTTP／SQL   | [6 个 HTTP 案例](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012CostQualityApiTests.cs)、[3 个条件 SQL 案例](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012CostQualitySqlTests.cs)；[原 HTTP 夹具](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012OperationsApiTests.cs)仅补 DI／stub                             |
| 前端验证    | [19 个组件案例](../frontend/permission-admin/src/views/ai/operations/AiCostQuality.test.ts)、[API 契约](../frontend/permission-admin/src/api/ai-operations.test.ts)、[原页面入口回归](../frontend/permission-admin/src/views/ai/operations/index.test.ts)                                                                                                 |

本批不导入账单、不调用模型、不自动调价，不展示原单价、调用明细、模型／供应商名称或正文，不修改 usage／Run／历史价格／预算／预留／结算／留存。原方向 4 汇总、方向 5 导出文件与本人凭据契约保留。

## 2. 授权、读取与计算边界 [Architect] [Reviewer]

仅复用 `ai:operations:view`，无需 export 权限，API／Application 双门禁。入口、首次读取后、返回前复核当前有效身份、角色／权限、安全戳和活动目标；超管也须 Header／Request 明确单租户，拒绝系统作用域。范围是获准运营聚合的当前租户，不是本人导出凭据的 actor 范围。

usage 与关联 Run 均使用 QueryForTenant／非删除过滤，按 usage.CreatedAt 左闭右开，默认 30 天、最长 90 天、结束最多未来 5 分钟。跨窗创建但仍可读的 Run 可以关联；不可读 Run 的 usage 排除，失败、取消和重试分别计数，不表示完整已发生请求或已清理历史。

最多 50,000 条，稳定 CreatedAt／Id 排序、固定标量投影、Take(50001)，超限整体拒绝。内部 Id／RunId／历史单价不进入响应。独立限流 actor 6/min、tenant 12/min；应用读取／计算截止 10 秒，无新锁、成功缓存或导出额度占用。计算后重新构造查询，完整投影群体变化则 409；两次观察不是事务或不可变历史快照，返回后仍可能结算或清理。

Completed／Failed／Cancelled 为终态，Pending／Running 仅计未决，未知状态单列。重算分项优先选非负已记录 Token，否则非负估算回退；双记录／混合／双回退／不可用互斥计数，17 个固定诊断码允许重叠。输入成对比较、输出上限使用率只取已记录非负且估算／上限为正的样本；零基线另计，加权比不是逐条比率均值。TotalTokens 使用 long 与输入＋输出比较，不补缺值。

费用只用历史快照，六位 `AwayFromZero`，不读现在的 Provider、不调用结算方法。每币种金额来自同一可比样本，带符号差与逐条绝对差分别累加，不跨币种合计；三位大写格式合格不证明真实 ISO 币种。缺可比样本金额为 null，合法零为 0；单条溢出不可重算、聚合溢出整体拒绝，不返回部分总数。

四金额字段 `StoredCost`／`RecomputedCost`／`DifferenceCost`／`AbsoluteDifferenceCost` 在后台仍为 `decimal?`，JSON 通过 `JsonNumberHandling.WriteAsString` 输出精确十进制字符串或 null。前端使用 `string | null` 并补足小数位，**不转 Number**，避免合法大额聚合丢失小数精度；不改变数据库 decimal(18,6) 或舍入规则。

页面校验 MetricsVersion／Scope／CostBasis／TenantId／实际窗口／币种分页。身份／角色／权限／超管声明／目标／窗口变化、关闭、KeepAlive 停用或卸载时 abort、清理结果，丢弃迟到响应；不本地持久化质量 DTO。文案明确“已记录 Token”无来源认证、输出是请求上限、历史估算一致性不是供应商实际收费或财务对账。

## 3. 数据影响与兼容 [DBA]

**无数据库结构、迁移、权限／菜单种子或历史回填变更。** 复用现有 AiUsageLog／AiRun、租户／审计／软删除及查询抽象；GET 沿用原中间件普通操作日志，不新增“校准通过”或“对账通过”审计事实。

Run 关联不是租户组合外键，因此查询显式双侧隔离；现有索引不等于纯租户／时间覆盖索引，Take 也不是 SQL 扫描／排序成本保证。50,000 行两次投影的实际查询计划、内存和截止需隔离环境验收，不直接增索引、提高容量或修改历史以满足页面。

回退撤销本批入口与服务，不清表、不改历史、不执行 Down。保留原预算／会话／网关／watchdog／结算及方向 4、5 未提交改动。未启动真实宿主、初始化或迁移数据库、清理、部署、真实模型调用或真实业务数据读取，未 commit／push。

## 4. 验证了什么 [Developer] [Reviewer]

最终后端使用独立 `obj/aic012-cost-quality-final2` 产物，测试 `--no-build`；未改 DLL ACL 或删旧产物。全量 Unit 测试进程 TEMP／TMP 仅指向工作区 `obj/aic012-cost-quality-temp` 并在 finally 恢复，保留实际退出码。下表报告相对 `artifacts/aic-012/cost-quality/`，本地报告不是正式部署或真实模型证据。

| 验证                             | 实际结果                                        | 证据／限制                                                                                                                                                                                                  |
| -------------------------------- | ----------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 后端 Release build               | 成功，0 error、2 条既有 ASPDEPR004／008 warning | `dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic012-cost-quality-final2 -p:UseSharedCompilation=false --nologo`                                                               |
| 本批核心／离线 SQL 翻译          | 49/49 通过                                      | [quality-core-final.trx](../artifacts/aic-012/cost-quality/tests/quality-core-final.trx)，核心 48 + 翻译 1，无 SQL 连接                                                                                     |
| AIC-012 四批核心回归             | 186/186 通过                                    | 从最终全量 Unit TRX 核实 Aic012 命名案例，未借用旧批报告                                                                                                                                                    |
| 全量 UnitTests                   | 1102 项：1101 通过、1 失败、0 跳过              | [unit-final.trx](../artifacts/aic-012/cost-quality/tests/unit-final.trx)，既有证书导入失败见下文                                                                                                            |
| 全量 IntegrationTests            | 156 项：107 通过、49 环境跳过、0 失败           | [integration-final.trx](../artifacts/aic-012/cost-quality/tests/integration-final.trx)，本批 HTTP 6/6；AIC-012 HTTP 共 25 项通过，四批 SQL 共 12 项跳过                                                     |
| Legacy Tests                     | 45/45 通过                                      | [legacy-final.trx](../artifacts/aic-012/cost-quality/tests/legacy-final.trx)                                                                                                                                |
| 前端全量 Vitest                  | 21 文件、148/148 通过                           | `npm run test:unit`，含本批 19 个组件案例及新增 API／原页入口契约                                                                                                                                           |
| 前端类型／production build／预算 | 通过                                            | `npm run build` 含 vue-tsc；budget total `1836338`、最大 chunk `910023` bytes，保留既有 Element Plus 大 chunk 提示                                                                                          |
| 前端 lint                        | 0 error、4 条既有 warning                       | `npm run lint`；UploadFile 两条 default-prop、index.test KeepAlive 宿主两条 one-component-per-file                                                                                                          |
| 修改前端文件格式                 | 六文件 Prettier check 通过                      | API／API test、新组件／test、原页／test                                                                                                                                                                     |
| AIC-004 Offline 回归             | 41 变体确定性检查通过                           | [report.json](../artifacts/aic-012/cost-quality/evaluations/20261007-100849-7b04cc970e9b46fbb49e6d2fef05ce24/report.json)，`AutomaticChecksPassed=true`；人工黄金审核和可比／已审核基线缺失，发布门槛未满足 |

核心覆盖正常历史结算、分项回退、状态划分、未知／合法零、价格／币种诊断、成对分母／加权比、long 总数、六位舍入、相反差额的绝对合计、单条／整体溢出和精确金额序列化；同时覆盖双侧租户／软删除、usage 窗口与跨窗 Run、来源变化、fresh 撤权／安全戳／目标停用、超管明确目标、50,000／50,001、有界分页／大页码、独立限流／取消／截止。

前端覆盖主动入口、独立窗口、空数据／缺样本／合法零、精确大额金额字符串、响应范围／分页校验、撤权／身份／租户／窗口变化、重查与迟到结果、关闭／KeepAlive／卸载以及失败恢复。实现中编译缺 using／LINQ 范围变量冲突、组件重查测试点击加载中禁用按钮的问题已修正；取消测试改用真实可触发重查的日期变化，未弱化断言。

全量 Unit 唯一失败仍为 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：Windows `X509CertificateLoader.LoadPkcs12` 导入合成证书时“拒绝访问”，尚未进入 TLS 拒绝断言；工作区 TEMP／TMP 下仍失败。未跳过、改 ACL 或弱化原测试，不能宣称全量全绿。

本批 SQL 3 项复用显式隔离条件 `PERMISSION_SYSTEM_AIC012_SQL_TEST_CONNECTION` 和 `PERMISSION_SYSTEM_AIC012_SQL_TEST_ISOLATED=1`，使用已审核迁移的隔离库、合成数据及外层事务回滚，不自动迁移、不打印连接值。缺环境全部跳过；离线翻译和 TestServer 不替代实际数据库、真实认证或 Redis 多实例。Vitest／jsdom 组件测试不等于真实浏览器视觉或端到端，本批没有截图验收。

Offline 使用原案例和最终 DLL，未运行 Live 或真实模型。新 Application 构建影响 AIC-005 BuildIdentity，后续必须按新构建重新冻结／评测／兼容核验及获取发布资格，不能用本次 Offline 绕过。

已同步本方案、AIC-012 分批登记、后续计划与总体计划；11 份相关规则／文档的 212 个相对文件引用有效。本批方案与验收记录 Prettier check、`git diff --check` 通过，旧总体文档仅修改本批必要段落，未全篇格式化。

## 5. 还有什么风险与退出条件 [Reviewer]

| 未完成事项               | 责任／退出条件                                                                                                                                          |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Token 与实际收费证据     | 业务／供应商明确 Token 来源、账单、税费／折扣／币种、匹配及审核契约后另行立项；本地失败零 Token 不是供应商实测，不能从 RequestId 或成功状态推定真实计费 |
| 不可重算历史与观察一致性 | 业务理解缺字段可能保留原预留／既有费用，不能补零或重写；两次观察不保证事务历史快照，页面返回后仍可结算／清理                                            |
| 隔离 SQL／正式规模       | 开发／DBA 运行本批及四批 SQL 回归，核实真实双侧隔离、撤权／来源变化、50,000／50,001、查询计划／内存及截止；现有 3 项 SQL 不替代全部规模验收             |
| Redis 与故障行为         | 运维／开发验证独立 actor／tenant 限流的跨实例一致性、故障／取消／截止，确认导出及凭据准入仍独立                                                         |
| 真实 OpenIddict 浏览器   | 前端／验收核实普通与超管明确目标、view 单权限、撤权／租户切换／KeepAlive／取消、宽窄屏和主题、响应精确金额及原页面兼容                                  |
| Owner 与口径签认         | 业务／安全／运维签认租户级运营可见人员、用途、独立 usage 窗口、终态含失败／取消、输出上限及可接受规模／延迟                                             |
| 原证书失败与发布资格     | 环境维护人员修复原合成证书导入拒绝并重跑；发布负责人完成新 BuildIdentity 的重新冻结／评测和人工／基线审核，保留前批环境验收                             |

Reviewer 对已确认代码范围结论：**通过，估算质量首批待环境验收。** 无新增结构或历史写入；实际验证和限制如上。方向 3 的真实成本对账／自动校准、方向 5 的归档／法律保留、AIC-012 和 P5-C／P5-D／P5-E 整体均未完成。
