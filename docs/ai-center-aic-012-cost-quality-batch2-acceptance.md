# AIC-012 方向 3 第二批：只读 UTC 日质量趋势与分币种差异分布实施／验收记录

> 日期：2026-10-07
> 状态：用户回复“确认”批准[具体第二批方案](ai-center-aic-012-cost-quality-batch2-implementation-plan.md)，第二批已实现、待环境验收。首批与方向 4、5 已有批次保持待验收；方向 3、AIC-012 整体未完成。
> 依据：[AGENTS.md](../AGENTS.md)、[后续计划](ai-center-next-development-plan.md)、[分批登记](ai-center-aic-012-implementation-plan.md)、[首批验收](ai-center-aic-012-cost-quality-acceptance.md)。本记录独立登记第二批证据，不覆盖首批历史结果；代码复核通过不等于环境、实际账单或发布验收通过。

## 1. 改了什么 [Architect] [Developer]

原“估算质量核验”默认窗口摘要保留，新增主动选择的“按日趋势”。UTC 日表展示同一观察内的参与、终态／未决／未知、费用可比／一致／不一致／不可比和 Token 样本；展开查看日桶范围、Token 依据、成对差异／加权比／零基线、TotalTokens 与固定重叠诊断。全窗口币种表展示存储高于／等于／低于重算的方向分布，以及原四金额字段。

| 层次        | 实际实现                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Application | [趋势 DTO](../backend/PermissionSystem.Application/AiCenter/AiCostQualityTrendModels.cs)、[UTC 分桶](../backend/PermissionSystem.Application/AiCenter/AiCostQualityTrendCalculator.cs)；[原计算器](../backend/PermissionSystem.Application/AiCenter/AiCostQualityCalculator.cs)同一 CostCounter 增加方向计数；[原服务](../backend/PermissionSystem.Application/AiCenter/AiCostQualityService.cs)局部共用读取／复核与分页，[原服务接口](../backend/PermissionSystem.Application/AiCenter/AiCostQualityModels.cs)增加趋势方法，无新增 DI 服务／依赖 |
| API         | [AiOperationsController](../backend/PermissionSystem.Api/Controllers/AiOperationsController.cs) 增加单一 NoStore view GET `/api/ai/operations/cost-quality/trends`，复用原构造依赖、ApiResult、CancellationToken                                                                                                                                                                                                                                                                                                                                  |
| 前端        | [API／DTO](../frontend/permission-admin/src/api/ai.ts)、[原质量 dialog](../frontend/permission-admin/src/views/ai/operations/AiCostQuality.vue)、[独立展示组件](../frontend/permission-admin/src/views/ai/operations/AiCostQualityTrends.vue)；沿用 Axios／Pinia／Element Plus／主题，无新路由／图表库                                                                                                                                                                                                                                            |
| 后端验证    | [31 个核心案例](../backend/PermissionSystem.UnitTests/AiCenter/Aic012CostQualityTrendTests.cs)、[离线 SQL 翻译](../backend/PermissionSystem.UnitTests/AiCenter/Aic012CostQualityTrendSqlTranslationTests.cs)、[7 个 HTTP 案例](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012CostQualityTrendApiTests.cs)、[3 个条件 SQL 案例](../backend/PermissionSystem.IntegrationTests/AiCenter/Aic012CostQualityTrendSqlTests.cs)；原离线查询捕获器／HTTP stub 最小复用                                                                       |
| 前端验证    | [24 个趋势组件案例](../frontend/permission-admin/src/views/ai/operations/AiCostQualityTrend.test.ts)、[原摘要回归](../frontend/permission-admin/src/views/ai/operations/AiCostQuality.test.ts)、[新增 API GET 契约](../frontend/permission-admin/src/api/ai-operations.test.ts)                                                                                                                                                                                                                                                                   |

没有账单导入、业务阈值、校准系数、Provider／模型／用户细分、调用明细、原单价、正文、上传／下载、自动轮询或模型请求；不改预留／结算／配置／历史／预算／留存。原运营页入口、方向 4 汇总、方向 5 文件 v1／本人凭据与工作区已有改动保留。

## 2. 数据与安全契约 [Architect] [DBA] [Reviewer]

**无数据库结构、迁移、权限／菜单种子或历史回填变更。** 复用 usage／Run 的 QueryForTenant、非删除及原索引；GET 沿用既有普通请求日志，不写新的“对账通过”事实。回退撤销趋势入口／方法，不清表或重写历史。

API／Application 继续 `ai:operations:view` 双门禁，无需 export；入口、首次读取后、返回前 fresh 身份／角色／权限／安全戳／活动目标复核。普通身份同租户，超管也须 Header／Request 明确活动单租户，不开放系统范围，不接收 actor／tenant／Provider 或任意字段／外部目标。返回只包含安全聚合，不包含内部 Id／RunId／单价或原记录自由字符串。

usage.CreatedAt 左闭右开，默认 30 天、最长 90 天、结束最多未来 5 分钟。每条仍可读关联调用只归属一个 UTC 日期，Run 日期不限制；90 天时长最多跨 91 个日桶，桶为 UTC 日与实际窗口交集，IsPartialDay 明确部分日。UTC 午夜结束不生成下一日空桶；窗口内空日正常返回，计数 0、无样本比例 null，不能据此推断供应商无调用或零费用。

窗口与每日 Population／Basis／17 个 Issues／两项 Token 比较／TotalTokens 都由同一观察和首批计算器产生；可加字段日和守恒，窗口比例重新加权，不平均日比例。终态含完成／失败／取消，未决和未知状态不参加终态比较；零基线另计，诊断可重叠，没有“全部问题相加＝异常调用”的总数。

同币种方向只比较同一终态样本的非负存储费用与历史快照／分项有效 Token 所得六位 AwayFromZero 重算值。高于＋等于＋低于＝ComparableCostCount，前两种不一致方向合计＝DifferentCostCount；不可比另计，不改容差。缺币种不造分组，三位大写格式不是 ISO／计费证明。日表没有货币总和或日 × 币种矩阵，不换汇或跨币种相加。

CurrencyDistribution 封装原 CurrencySummary，四金额后台仍是 `decimal?`、JSON 精确十进制字符串或 null，前端不转 Number。单条溢出不可比；窗口／日聚合溢出整体拒绝，不返回部分结果。合法零和缺样本严格区分。

每次仍最多两次 usage／Run 群体标量读取，稳定排序、Take(50001)、50,000 条超限整体拒绝；内存分桶不追加逐日／逐币种 SQL，不读 Provider。摘要与趋势共用 actor 6/min、tenant 12/min，重查／翻页也消耗同一额度；10 秒读取／计算截止和取消贯穿，无新锁、成功缓存或导出额度占用。两次群体比较不是事务历史快照，返回后仍可能结算／清理；原索引／Take 也不等于扫描、内存或 SLA 已验收。

新响应独立 MetricsVersion=1、Grouping=UsageCreatedAtUtcDay、BucketTimezone=UTC、固定 Scope／CostBasis；最多 91 日 × 固定诊断、币种页默认 20／最大 50，页码 long 运算。首批 GET v1 不增加日或方向字段。页面只请求当前主动视图，清理旧结果，不拼不同观察来声称守恒；UTC Date 字符串直接展示，每次响应标观察时间，翻页重新观察可能改变值。

身份／权限／角色／超管／目标／窗口／视图变化、关闭、KeepAlive 停用和卸载均 abort、清理并丢弃迟到响应；校验目标、实际窗口、分页、版本、Scope／CostBasis／Grouping／BucketTimezone。无 DTO 本地持久化、后台轮询或自动恢复查询。

## 3. 验证了什么 [Developer] [Reviewer]

后端独立产物 `obj/aic012-cost-trends-check2`，测试 `--no-build`，没有改 DLL ACL 或删除旧产物。全量 Unit 的测试进程 TEMP／TMP 仅设工作区 `obj/aic012-cost-trends-temp` 并 finally 恢复，保留实际退出码。以下报告路径相对 `artifacts/aic-012/cost-quality-batch2/`，均是本批实际运行证据。

| 验证                             | 实际结果                                        | 证据／限制                                                                                                                                                       |
| -------------------------------- | ----------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 后端 Release build               | 成功，0 error、2 条既有 ASPDEPR004／008 warning | `dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic012-cost-trends-check2 -p:UseSharedCompilation=false --nologo`                     |
| 本批核心／离线 SQL 翻译          | 32/32 通过                                      | [trends-core-check.trx](../artifacts/aic-012/cost-quality-batch2/tests/trends-core-check.trx)，31 核心 + 1 翻译，无实际 SQL 连接                                 |
| 本批 HTTP／条件 SQL              | HTTP 7/7；SQL 3 项环境跳过                      | [trends-integration-check.trx](../artifacts/aic-012/cost-quality-batch2/tests/trends-integration-check.trx)；GET／NoStore／单 view／DTO 绑定及 GET-only          |
| 全量 Unit                        | 1134 项：1133 通过、1 失败、0 跳过              | [unit-final.trx](../artifacts/aic-012/cost-quality-batch2/tests/unit-final.trx)；从实际报告核实 AIC-012 五批核心 218/218                                         |
| 全量 Integration                 | 166 项：114 通过、52 环境跳过、0 失败           | [integration-final.trx](../artifacts/aic-012/cost-quality-batch2/tests/integration-final.trx)；AIC-012 HTTP 共 32 项通过、五批 SQL 共 15 项跳过                  |
| Legacy                           | 45/45 通过                                      | [legacy-final.trx](../artifacts/aic-012/cost-quality-batch2/tests/legacy-final.trx)                                                                              |
| 前端全量 Vitest                  | 22 文件、173/173 通过                           | `npm run test:unit`；新增趋势 24 个组件案例和 API 1 项，原摘要等回归保留                                                                                         |
| 前端类型／production build／预算 | 通过                                            | `npm run build` 含 vue-tsc；bundle total `1843020`、最大 chunk `910023` bytes；保留既有大 chunk 提示                                                             |
| 前端 lint                        | 0 error、4 条既有 warning                       | `npm run lint`；UploadFile 两条 default-prop、原 index.test 两条 one-component-per-file                                                                          |
| 六个修改前端文件格式             | Prettier check 通过                             | API／API test、原 dialog／test、新展示组件／趋势 test                                                                                                            |
| AIC-004 Offline                  | 41 个变体确定性检查通过                         | [report.json](../artifacts/aic-012/cost-quality-batch2/evaluations/20261007-113754-cd2df465fdf54bbdb6df45b2223d9a78/report.json)；release gate 仍 pending/failed |

核心覆盖等价偏移／UTC 午夜／半开边界／跨窗 Run、90 天最多 91 桶、空日／部分日／null 比率、人口／依据／诊断／Token／TotalTokens 日守恒与重新加权；六位中点／有效零／未知／方向互斥分母／相反差额绝对合计、精确金额序列化和不改来源；双侧隔离／关联不可读、入口／返回前 fresh 授权、来源修改／日期变化／新增／软删除、超管明确目标、50,000／50,001、共同准入、分页／大页码、取消／截止／整体溢出。离线 SQL 捕获证明两次相同有界白名单查询，没有 SQL 日期分组或 N+1；不是实际计划验证。

前端覆盖默认摘要、主动趋势／返回摘要、UTC 日期／部分日／空日文案、方向分布／精确金额、翻页和窗口复位、Scope／CostBasis／Grouping／Timezone／版本／目标／窗口／分页校验、身份／撤权／角色／超管／目标／日期／视图／关闭／卸载变化取消、迟到旧观察丢弃、失败重试及 KeepAlive。

首轮后端测试构建遗漏命名空间引用，已修正后重新构建通过。前端首次全量有一项测试尝试操作无权时未渲染的控件；已改为明确验证切换控件不可见、摘要和趋势请求均未发生，取消／撤权断言保留。新展示组件格式经再次 write／check 已通过，无调试或临时代码。

全量 Unit 唯一失败仍为 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate`：Windows `X509CertificateLoader.LoadPkcs12` 导入合成证书“拒绝访问”，尚未到 TLS 拒绝断言。工作区 TEMP／TMP 下仍复现，未跳过、改 ACL 或弱化原测试，不能宣称全量全绿。

本批 SQL 3 项沿用 `PERMISSION_SYSTEM_AIC012_SQL_TEST_CONNECTION` 与 `PERMISSION_SYSTEM_AIC012_SQL_TEST_ISOLATED=1` 显式隔离条件，合成数据、已审核迁移和外层事务回滚，不自动迁移、不打印连接值；当前缺环境，全部跳过。HTTP 为 TestServer，限流替身不是 Redis 多实例；Vitest／jsdom 不是实际 OpenIddict、视觉或浏览器端到端，未做截图验收。

Offline 使用最终 DLL 和原案例，没有 Live、真实模型或外部业务请求。新的 Application BuildIdentity 必须在发布前重新冻结／评测／兼容核验并完成人工与基线审核，本次 Offline 不替代 AIC-005 资格。未启动真实宿主、读取真实业务数据／账单／敏感配置、迁移、清理、部署、commit 或 push。

已同步第二批方案、分批登记、后续任务表和总体计划；13 份相关规则／文档的 261 个相对文件引用有效。第二批方案／验收记录 Prettier check 与 `git diff --check` 通过，旧总体文档仅修改本批必要段落，已有未提交改动保留。

## 4. 还有什么风险与退出条件 [Reviewer]

| 未完成事项                 | 责任／退出条件                                                                                                                                             |
| -------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 首批与第二批真实 SQL／规模 | 开发／DBA 完成本批及五批回归，验收 UTC 与双侧租户／软删除／撤权／来源变化、50,000／50,001、查询计划／内存及 10 秒截止；现有 3 项 SQL 不覆盖全部环境验收    |
| Redis 共用准入／故障       | 运维／开发验证摘要与趋势共享 actor／tenant 额度、跨实例一致性、故障／取消／截止，导出和本人凭据额度仍独立                                                  |
| 真实浏览器与主题           | 前端／验收在真实 OpenIddict 下验证 view、超管明确目标、撤权／切换／KeepAlive／取消，宽窄屏／主题／键盘展开与焦点、UTC 日期、空日和精确金额，不拼跨观察结果 |
| Owner／未知历史            | 业务／安全签认当前租户运营可见范围、UTC usage 窗口、部分日和可比分母、失败／取消含义及可接受规模／延迟；空日与不可比不证明无调用或无费用                   |
| 真实账单与校准             | 另行明确账单／Token 来源、匹配／税费／折扣／币种及审核流程；本地零 Token、有效记录、费用方向不能证明供应商实际收费或调价依据                               |
| 原证书失败与发布资格       | 环境维护人员修复原合成证书导入并重跑；发布负责人完成新 BuildIdentity 的重新冻结／评测与人工／基线审核，不能绕过首批验收与 AIC-005                          |

Reviewer 对已确认代码范围结论：**通过，方向 3 第二批待环境验收。** 计划同步登记为“估算质量首批待验收；趋势第二批待验收”；方向 3 真实对账／自动校准、方向 5 归档／法律保留及 AIC-012、P5-C／P5-D／P5-E 整体未完成。
