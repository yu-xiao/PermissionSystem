# AIC-007：本地 Demo 只读查询实现与验收记录

> 日期：2026-10-07
>
> 状态：Demo 代码实现与专项验证完成，待环境及人工验收；首个真实业务数据集仍待确认。
>
> 确认依据：用户选择“先使用本地的 demo”“仅内部 AI 查询”，随后对 [实施方案](ai-center-aic-007-implementation-plan.md) 回复“确认”。

## 1. 实际实现 [Architect] [Developer]

首批查询本地平台已有 `DemoBusinessOrder`，数据集 `demo-business-orders-readonly`、工具 `business.demo_business_order.query`、函数 `query_demo_business_orders`，契约与工具版本均为 `1.0`。只读工具直接调用 Demo 模块公开 Application 用例，不绕行自身 HTTP/MCP，不调用创建、审批或草稿动作，也不生成示例数据。

- 查询返回 `id/orderNo/title/approvalStatus/departmentId/createdAt`、完整匹配单数、展示行数及截断标记。客户、金额、负责人和审批明细不返回；关键词仅搜索单号或标题。
- 支持关键词（最多 100 字符）、六种当前审批状态、明确部门 ID、授权范围或当前部门、展示上限。默认 20 行，最多 `min(MaxToolRows, 200)`；固定创建时间降序、ID 升序。未知／重复大小写参数、非法类型、状态和范围拒绝。
- 本人规则沿用 `CreatedBy`，部门沿用 `DepartmentId`。使用 `IDataPermissionRepository`、既有规范与过滤器，再与新解析范围、当前租户和非删除条件相交。超级管理员也限当前租户，空范围返回零，不回退全量。
- 业务公开用例自行检查有效身份、租户与 `demo-business-order:view`；AI 工具另要求 `ai:tool:query` 及原批准配置。查询结束复核身份、权限、开关和范围。查看权限足够，不要求创建权限。
- 新增专用单据卡片，明确“单”与“展示行”，展示口径、过滤、来源版本、查询时间和限制。业务文本使用 Vue 插值转义，无单据写动作或跳转。
- 历史读取沿用同用户／租户／会话、成功调用及摘要绑定，额外检查版本、字段、开关、当前权限与范围，并经 Demo 公开用例逐行核对可见性及安全字段。删除、修改、撤权、变更范围或关闭工具后隐藏旧卡片并要求重查。
- 追问继承服务器实际条件，支持本部门、审批状态等修改及允许字段的清除；重新执行查询。时间筛选、金额和分组统计不支持，不能静默忽略。“再看上个月”要求澄清。
- 封套 64 KiB、上下文 4 KiB、读取响应 256 KiB 限制及历史保留沿用原机制。封套裁剪同步减少展示行与引用行数，不改变完整计数。
- `AsOf` 留空，不伪造历史状态快照；普通读隔离下计数和明细之间可能变化。新增 30 秒工具超时，取消传递给异步查询，限流／预算／审计沿用原运行链路。
- `Ai.EnableDemoBusinessOrderQueryTool` 默认关闭。只有内部 Application 注册选择加入 Handler；外部 MCP 的默认 `AddAiCenterCore()` 不注册它。权限助手四工具白名单保持原定义。
- 安全提示词版本由 `2.2` 更新为 `2.3`，区分查询已有单据与准备草稿。构建、提示词与 Schema 变化仍受 AIC-005 的冻结／评测／发布兼容规则约束。

没有新增框架或依赖，没有修改普通 Demo 写入、审批、Controller、外部 MCP 契约或报表查询实现。

## 2. 数据影响与启用 [DBA]

**无数据库变更。** 沿用现有表、租户、审计、软删除与数据权限；无新实体、EF 映射、迁移、视图、索引、权限种子或数据回填。未执行数据库迁移或业务数据写入。

经环境负责人批准后，本地内部入口可设置 `Ai__EnableDemoBusinessOrderQueryTool=true`。这只是配置说明，本轮没有实际开启环境。原 `Ai.Enabled`、批准租户、供应商合规配置、`ai:chat:use`、`ai:tool:query`、`demo-business-order:view` 等检查继续生效；历史读取还需 `ai:conversation:view`。权限及业务范围由原系统配置，不自动授予。

没有匹配数据时返回零单与空列表；通过正常业务入口准备或选择 Demo 数据，AI 查询不负责建数。真实 SQL Server 翻译、查询计划和规模性能未经验证，不能从内存夹具推断生产表现；如需索引，须另行提交 DBA 方案。

## 3. 实际验证 [Developer] [Reviewer]

所有路径相对仓库根目录。测试产物与临时验证工程位于 Git 忽略的 `obj/`，不进入提交。

| 检查 | 结果及证据边界 |
| --- | --- |
| 最终专项／架构验证 | 74/74：60 个 AIC-007 用例及 14 个 EA020 数据权限架构用例。`obj/aic007-validation/aic007-core-isolated.trx` |
| HTTP 集成全量 | 58 通过、25 个 SQL 环境用例跳过、0 失败；新增 AIC-007 HTTP 13/13。`obj/aic007-validation/integration-full.trx` |
| Legacy 测试 | 45/45。`obj/aic007-validation/legacy-full.trx` |
| 前端单元测试 | 46/46，含新增卡片 6 个用例 |
| 前端类型／lint／格式／构建 | 类型检查、任务文件 Prettier、构建与包体预算通过；lint 无错误，`UploadFile.vue` 有两条既有默认值警告 |
| 原离线评测回归 | 41/41 变体确定性检查通过；人工发布门槛未通过。`obj/aic007-validation/evaluations/20261007-005220-ce3452952f184fc5acffd8508b40e107/report.json` |
| 解决方案构建 | 前期独立产物目录 Release 构建通过（两条既有 ASP 弃用警告）；最终重建其余项目编译成功，但复制 `PermissionSystem.UnitTests.dll` 时 MSB3021“拒绝访问”，最终解决方案构建未通过 |
| 全量 Unit | 首次 706 通过／9 失败／715 总计；修复本任务新增原始仓储架构问题后，最终全量运行受 UnitTests.dll 访问／不可用错误阻塞，不能记为通过。最终 TRX `obj/aic007-validation/unit-full-final.trx` 为零执行 |
| 静态复核 | 依赖方向、默认关闭、内部注册、字段投影、授权与历史校验、只读边界及差异空白检查通过 |

首次 Unit 失败明细保存在 `backend/obj/aic007-validation/unit-full.trx`：1 个本任务仓储架构问题（已修正并经 14 个 EA020 用例复核）、4 个文件／临时目录相关失败、1 个既有 PFX 导入拒绝访问、3 个既有评测测试在独立产物目录下定位成 `backend/backend/...` 的路径失败。未为通过而修改这些无关测试或系统安全配置。

为验证最终代码，在 `obj/aic007-core-tests/Aic007Validation.csproj` 创建临时工程，链接正式 AIC-007 测试、夹具、EA020 与 TestDoubles，引用 Application／Infrastructure，使用仓库已有测试包版本。它验证实际最终代码的任务子集，不能替代主 Unit 程序集全量回归。

主要复现命令：

```powershell
dotnet test obj/aic007-core-tests/Aic007Validation.csproj -c Release --artifacts-path obj/aic007-core-build --logger "trx;LogFileName=aic007-core-isolated.trx" --results-directory obj/aic007-validation
dotnet build backend/PermissionSystem.sln -c Release --artifacts-path obj/aic007-build
dotnet test backend/PermissionSystem.IntegrationTests/PermissionSystem.IntegrationTests.csproj -c Release --artifacts-path obj/aic007-build --no-build --no-restore --logger "trx;LogFileName=integration-full.trx" --results-directory obj/aic007-validation
dotnet test backend/PermissionSystem.Tests/PermissionSystem.Tests.csproj -c Release --artifacts-path obj/aic007-build --no-build --no-restore --logger "trx;LogFileName=legacy-full.trx" --results-directory obj/aic007-validation
```

前端在 `frontend/permission-admin` 执行 `npm run test:unit`、`npm run type-check`、`npm run lint`、`npm run build`，对四个本任务前端文件执行 `npx prettier --check`。核心夹具采用合成数据／身份；新增 HTTP 用例使用真实 Controller／授权／Registry／Handler／只读业务查询，但会话编排为替身。专项会话测试另用实际 `AiConversationService` 和合成模型网关验证查询、审计摘要、重载、引用追问、无草稿与无业务写入，不是完整真实模型 HTTP 验收。

原离线 41 个变体属于已有权限助手回归，不是新增 Demo 的真实模型评测。发布门槛仍缺人工黄金案例／事实审核、可比较基线及已审核基线，不能用自动通过绕过。

## 4. 复核结论与剩余风险 [Reviewer]

**代码与本轮专项复核通过；Demo 环境验收及最终全量回归未完成，不能作为发布通过结论。**

- 未提供指定隔离 SQL Server、目标浏览器和真实供应商验收环境；数据库语义、真实模型工具选择、浏览器体验、规模性能及并发运行仍需验收。30 秒超时和运行限制沿用既有机制，本轮不声称完成真实模型超时或压测。
- 标题与单号仍是机密业务文本；允许投影不代表无敏感内容，业务数据质量与人工口径需核对。
- 主 Unit 文件访问和既有环境问题需解决后重新完成全量回归；当前 74 个隔离用例仅覆盖所列子集。
- 构建和安全提示词变化须按 AIC-005 重新冻结对应版本并完成绑定评测，未部署或发布。
- Demo 不是首个真实 ERP/WMS 数据集，真实来源、Owner／验收人、租户／组织／仓库／账套与外部服务范围仍待单独确认，P4-B2 不记为完成。

## 5. 回退与阶段状态

回退先将 Demo 工具开关设为 false；保留已有会话和业务数据，历史 Demo 卡片因关闭而不可继续读取或追问。不执行数据库回退、清表或恢复宽松授权。旧前端对未知结构化结果保留不支持提示。

本轮未实际数据库执行、付费模型调用、部署、commit 或 push。后续先补全环境／人工验收及全量回归，再决定 Demo 批次退出；真实业务批次单独确认，AIC-007 整体不标记已完成。
