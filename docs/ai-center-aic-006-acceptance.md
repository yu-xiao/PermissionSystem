# AIC-006：受控用户指标实现与验收记录

> 日期：2026-10-06
>
> 状态：首批代码与本地确定性验证完成，待验收。
>
> 确认依据：用户先选择“平台用户目录（推荐）”，随后对 [实施方案](ai-center-aic-006-implementation-plan.md) 回复“确认”。

## 1. 实际实现 [Architect] [Developer]

首批数据集为 `system-users-scoped`，固定能力 `UserDirectoryV1`，契约版本 `1.0`；对应新增安全视图 `reporting.AiSystemUsers`。AI 工具继续使用 `permission.reports.query_dataset`／`query_approved_report_dataset`，工具版本升级到 `2.0`。工具和 SQL 报表开关没有自动打开，批准键保持原配置为空；没有自动创建租户报表。

- 用户明细与完整用户总数、启用／停用人数，按无分组、启用状态或部门 ID 统计。按用户 ID 计数、不联角色，含内置用户；启停与部门均是当前值，单位“人”。空部门保留单独分组。
- 关键词包含匹配、启用状态、明确部门、本部门及创建时间过滤。新数据集创建起点含、终点不含；要求显式偏移，归一为 UTC；允许单边日期，不猜默认历史时间。相对自然月采用明确用户偏移与排他月末。
- 固定排序和展示上限；完整匹配总数、返回行数、总分组与展示分组分别表达。`sourceRowCount` 保持“报表服务返回明细行数”，不再冒充完整总体；指标总数由 `totalCount`／`metrics.totals` 提供。
- Application 用例生成服务端执行上下文，验证当前身份／租户与有效权限，调用原数据范围解析服务；执行结束再次验证权限及范围是否变化。用户数据集要求 `report:view` 和 `system:user:view`；导出还要求 `report:export`，AI 另要求原两项工具／报表数据集权限。
- 本人及允许部门为并集，然后与租户及业务过滤取交集；部门树仅由原范围服务解析。空有效范围为零，不支持的范围拒绝；使用 JSON 参数化部门集合，最多 10000 个范围 ID，超过上限明确拒绝。
- SQL 使用原独立只读连接、超时、取消及并发门限；固定字段投影与租户／范围过滤在完整聚合前应用。新数据集用单条参数化 SQL 返回完整计数与受限 JSON 明细／分组，避免应用层截断样本聚合。旧明细额外读取一行判断截断，完整总量未知时返回 NULL。
- `Rows`／`Metrics`、维度、固定排序及参数在服务端校验。HTTP DTO 和工具拒绝未知／重复大小写字段、非法类型与无偏移日期；可编辑 ColumnsJson 不控制新用户投影，模型不能传 SQL、列名、身份或租户替代隔离。
- 成功报表审计保存模式及实际生效筛选；拒绝／失败仅记录输入摘要，避免把未知参数原文写入审计。取消、超时与失败仍保留原审计路径。
- 新增 `controlled-report` 结构化载荷及指标卡片；复用原用户表格、引用和封套限制。明确数据时间限制，无独立水位不填 `AsOf`；统计不是过去月份的人员状态快照。
- 历史卡片重载及追问重新检查身份、权限、批准键、报表启停／删除／换绑、契约及定义指纹、当前范围指纹；明细另检查每行当前可见性。报表变化和撤批／撤权不能复用旧聚合卡片。
- 追问只合并实际条件中的明确嵌套筛选变更，可清除允许的单个过滤；换报表必须新查。“只看本部门”“再看上个月”不得静默改变其他筛选。切换明细模式清除不适用的统计维度／排序，重新执行服务端校验。
- 封套仍为 64 KiB，上下文 4 KiB，读取响应 256 KiB；裁剪只减少展示明细／组，保持完整总数，并校准引用行数。旧四个工具的 `1.0` 结果读取保留，未知版本拒绝。
- 报表查看页分开显示总量／展示量／截断，说明导出也有上限；新用户启停过滤可选择全部／启用／停用。

旧 `system-users`、`system-login-logs`、`system-operation-logs` 和其他未声明能力的数据集仍为 `AllOnly`。普通报表查询及导出现在也拒绝局部范围身份，这是已确认的访问收紧。AI 仅允许有代码安全投影的新用户数据集；旧视图即使加入批准键，也不会把联系方式、IP 或失败详情提供给模型。

权限助手的四工具绑定、已发布快照及 AIC-005 门槛没有改变。内置兼容入口可以在显式批准后使用新工具，但不代表正式受控场景已通过发布。构建变化仍要求相关受控版本按原规则重新冻结、评测与发布。

## 2. 数据影响 [DBA]

**无业务表、实体、EF 映射、迁移或数据回填变更；有新增视图和专用只读授权的部署脚本，尚未执行。**

[部署审核脚本](sql/ai-center-aic-006-reporting.sql) 先检查已有 reporting schema、专用只读角色、Users 必需列及 SQL Server 兼容级别至少 130，再在事务内创建／更新安全视图和对象级 SELECT 授权。只投影 ID、租户、部门、用户名、显示名、启停与创建时间，源视图过滤软删除；AI 输出不投影 TenantId。

没有修改三个旧视图、实际数据库登录／角色、真实连接串或业务记录。没有自动新增索引；租户／部门／时间的查询计划、读取量和规模性能仍由 DBA 在批准的隔离环境评审。若需要表索引迁移或新的隔离配置，另行确认方案。

## 3. 本轮实际验证 [Developer] [Reviewer]

| 验证 | 结果及证据 |
| --- | --- |
| Release 全解决方案构建 | 通过，0 错误；既有 WebHostBuilder／TestServer 过时警告 2 个 |
| AIC-006 核心确定性用例 | 58/58；从最终全量 `aic-006-unit-final.trx` 按测试类核对，覆盖范围、批准、安全投影、完整总量、时间、撤权、审计、追问与封套 |
| UnitTests 全量 | 661 项，660 通过、1 失败；失败为既有 `AiHttpTransportTests.SendAsync_RejectsUntrustedHttpsCertificate` 的 Windows PFX 导入拒绝访问 |
| Legacy 全量 | 45/45；`artifacts/aic-006/aic-006-legacy-final.trx` |
| Integration 全量 | 70 项，45 通过、25 SQL 环境测试跳过；`artifacts/aic-006/aic-006-integration-final.trx` |
| 新 HTTP 契约／权限策略 | 8/8；采用 TestServer 与契约服务，证明 DTO／原 API 权限策略行为；真实身份与范围逻辑由上述 58 项验证，不冒充真实 OAuth／SQL 链路验收 |
| 新 SQL 语义测试 | 1 项跳过；没有配置明确隔离的 `PERMISSION_SYSTEM_AIC006_SQL_TEST_CONNECTION`，未选择本地业务库。测试就绪后仅使用连接内临时表，不自动迁移／部署视图 |
| 前端全量单测 | 40/40，11 个文件；`artifacts/aic-006/frontend-tests.json`，新增 6 项指标／兼容／转义用例 |
| 前端类型、lint、format、build／包体预算 | 通过；lint 保留 UploadFile 两个已有警告，Element Plus 大块提示保留；本轮涉及文件另单独执行 Prettier 检查 |
| 原 AIC-004 离线回归 | 41 个变体全部通过，发布门槛仍 pending/failed；`artifacts/aic-006/offline/20261006-100759-6d8ac244a2ce44248665eae2ceea6a1a/report.json` |
| 差异与新增文件空白 | 已执行检查；不借用历史 AIC-002～005 的结果作为本轮证据 |

原离线评测用于检查权限助手兼容，不扩充为未经人工审核的用户指标黄金集。AIC-006 的新增确定性回归进入现有 UnitTests／IntegrationTests／前端 CI 流程，不能以合成执行器的数字证明 SQL Server 聚合已通过。

最终后端命令在仓库根目录执行：

```powershell
dotnet build backend/PermissionSystem.sln -c Release --artifacts-path E:/Projects/PermissionSystem/.build/aic-006-final -p:UseSharedCompilation=false -m:1 -v minimal
dotnet test backend/PermissionSystem.UnitTests/PermissionSystem.UnitTests.csproj -c Release --no-build --no-restore --artifacts-path E:/Projects/PermissionSystem/.build/aic-006-final --logger 'trx;LogFileName=aic-006-unit-final.trx' --results-directory artifacts/aic-006 -v minimal
dotnet test backend/PermissionSystem.Tests/PermissionSystem.Tests.csproj -c Release --no-build --no-restore --artifacts-path E:/Projects/PermissionSystem/.build/aic-006-final --logger 'trx;LogFileName=aic-006-legacy-final.trx' --results-directory artifacts/aic-006 -v minimal
dotnet test backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj -c Release --no-build --no-restore --artifacts-path E:/Projects/PermissionSystem/.build/aic-006-final --logger 'trx;LogFileName=aic-006-integration-final.trx' --results-directory artifacts/aic-006 -v minimal
```

默认／初次增量构建遇到 AiEvaluations `refint` 写入拒绝，最终采用独立目录、关闭共享编译、串行构建通过；没有变更产品安全设置或文件 ACL。Unit 与离线评测仅将各自进程的 TEMP／TMP 指向 `artifacts/aic-006/temp-unit`／`temp-evaluation`，没有修改全局环境。PFX 导入失败仍保留，未弱化 TLS、删除或跳过该测试。

## 4. 剩余验收与风险 [Reviewer]

代码、分层、数据边界和本地回归复核：**通过**。完整环境／业务验收：**未通过，待验收**。

1. 业务 Owner／验收人尚未提供。须确认当前值、内置用户、空部门、创建半开时间及启停筛选后总体口径，按同条件可信业务查询签认结果。
2. DBA 指定隔离 SQL Server，审核脚本后另行授权部署，验证真实视图列／软删除、只读角色不得读业务表、JSON 集合语义、范围矩阵、空集、边界与截断规模；参数化 SQL 文本测试不代替这些证据。
3. 新可选 SQL 测试仅证明临时表上的查询语义，不证明真实安全视图、凭据、EF／权限链路或部署成功。环境测试开启时不得把连接指向生产。
4. 目标数据规模和延迟 SLA 尚未提供；需要记录查询计划、读取量、耗时、超时／取消／并发限制、10000 部门边界和请求成本后判断是否需要索引。普通读隔离下同一语句不同扫描可能受并发写影响，不承诺历史快照。
5. 当前部署的局部范围报表用户将被旧 AllOnly 数据集拒绝；仅有 `report:export` 而无 `report:view` 的身份不能导出。AI 旧含敏感字段的数据集拒绝返回，须在发布说明和角色验收中明确这些已确认收紧。
6. Windows 证书导入环境仍需管理员修复并重跑失败测试；全量 UnitTests 不能记为全通过。真实供应商 Tool Calling、用户明细／指标／追问、报表参数和导出的浏览器体验尚未验收。
7. AIC-005 构建兼容与正式评测门槛保持原规则，应用升级后旧受控版本可能不可执行；不原地改快照或跳过评测。首批指标未开放外部 MCP 或新受控场景。

部署先完成视图／只读授权与目标身份验证，再通过既有报表管理入口创建绑定新数据集的定义，向使用者提供明确报表 ID，最后显式配置批准键与工具开关。工具没有报表 ID 猜测／自动发现能力，不能编造记录。凭据只通过部署密钥配置，不写入文档、日志或 Git。

回退首先禁用新工具／批准键并结束后续新查询，再评估应用与历史契约兼容。默认保留新视图和消息历史，不自动 DROP、Down、删除数据或恢复旧宽松范围。未进行实际迁移、数据库脚本执行、付费模型调用、部署、commit 或 push。

## 5. 后续用户指令（2026-10-07）

用户要求先提交 AIC-006 改动，后续统一验收。本次仅授权本地 Git 提交；任务继续保持“待验收”，提交不表示实际数据库、业务签认、真实模型或正式发布门槛已通过。
