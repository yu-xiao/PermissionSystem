# AIC-007：本地 Demo 只读数据集实施方案

> 日期：2026-10-07
>
> 状态：已确认并实施，Demo 批次待验收。用户已选择“先使用本地的 demo”“仅内部 AI 查询”，随后回复“确认”，批准本方案；实际实现与验证见 [验收记录](ai-center-aic-007-acceptance.md)。
>
> 依据：`AGENTS.md`、[后续开发计划](ai-center-next-development-plan.md)、[总体实施计划](ai-center-mcp-implementation-plan.md)及本次代码调研。
>
> 本轮完成 Demo 验证批次的代码实现，不将 Demo 作为首个真实 ERP／WMS 业务数据集。即使 Demo 验收通过，AIC-007 的真实来源、业务 Owner 和业务范围验收仍需后续单独确认。

## 1. 首批范围 [Architect]

用户已确认本地 Demo、仅内部 AI 查询，并批准选择现有 `DemoBusinessOrder`（业务示例单据）：它已经接入 AI 草稿／正式单据链路，有完整租户、软删除、审批状态及数据权限规则。仓库另有 `DemoApprovalOrder`（审批示例单据），本方案不同时接入两个对象；如用户希望选用后者，应先调整方案。

首批用途：查询当前授权范围内的 Demo 单据明细，回答“有哪些单据”“待审批单据有多少”“只看本部门”“只看某状态”等受控问题。

正式来源为本地平台现有 `DemoBusinessOrder` 数据，通过 Application 模块公开只读用例访问。AI 不调用自身 HTTP／MCP 服务，不使用仓库的交期回复 SQL，不建设外部连接器或开放外部 MCP。查询无匹配数据时返回零与空列表，不自动生成业务记录。

本方案的字段和限制是已确认的 Demo 技术范围，不能冒充业务 Owner 已批准的真实业务口径。真实业务 Owner、接口、租户／组织／仓库／账套映射和外部服务范围留在后续真实接入阶段。

## 2. 已查证现状 [Architect]

下列路径均相对于仓库根目录，均为当前存在的文件。

| 文件 | 现状与复用依据 |
| --- | --- |
| `backend/PermissionSystem.Domain/Entities/DemoBusinessOrder.cs` | 继承 BaseEntity，实现数据权限接口；有单号、标题、客户、金额、部门、负责人、审批状态及审计字段 |
| `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderModels.cs` | 已有模块公开服务与查询 DTO；普通列表支持关键词、审批状态、部门 ID |
| `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderService.cs` | 通过 `IDataPermissionRepository` 查询可见记录；现有列表关键词还匹配客户名称和负责人用户名，计数与列表物化目前为同步调用 |
| `backend/PermissionSystem.Application/DataPermissions/BusinessDataPermissionSpecifications.cs` | DemoBusinessOrder 的“本人”键是 `CreatedBy`，部门键是 `DepartmentId`；不是 `OwnerUserId` |
| `backend/PermissionSystem.Application/DataPermissions/DataPermissionRepository.cs`、`DataPermissionFilter.cs` | 复用当前范围解析及本人／部门并集；空有效范围不返回数据；All 不额外限制行级范围 |
| `backend/PermissionSystem.Api/Controllers/DemoBusinessOrderController.cs` | 列表／详情权限为现有 `demo-business-order:view`；创建等写权限独立 |
| `backend/PermissionSystem.Application/Abstractions/IAsyncQueryExecutor.cs` | 已提供异步计数、列表、Any 和单条读取，可传 CancellationToken |
| `backend/PermissionSystem.Application/AiTools/AiReadOnlyToolRegistry.cs`、`AiReadOnlyToolHandlerBase.cs` | 已有只读注册、权限过滤、JSON 参数校验、限时取消、执行上下文和结果引用 |
| `backend/PermissionSystem.Application/AiCenter/AiQueryAccessGuard.cs` | 逐次复核身份、租户、权限及身份是否过期，工具白名单需补新只读工具 |
| `backend/PermissionSystem.Application/AiCenter/AiStructuredResultModels.cs`、`AiStructuredResultReader.cs`、`AiFollowUpContextService.cs` | 封套／引用／历史重载／追问可复用；用户表格和报表契约不能直接套用到业务单据 |
| `backend/PermissionSystem.Application/AiCenter/AiScenarioCatalog.cs` | 权限助手仅绑定四个工具；当前安全提示对 DemoBusinessOrder 主要描述草稿，需区分查单据与准备草稿 |
| `backend/PermissionSystem.Application/Reports/ReportDatasetCapabilities.cs`、`backend/PermissionSystem.Infrastructure/Reports/ReportDatasetCatalog.cs` | 受控报表为用户专用指标与安全视图；不能复用用户数据集键或“人”单位承载 Demo 单据 |

新用例在 DemoBusinessOrders 模块内部使用已有仓储、数据权限规范和异步执行器，AI Handler 仅调用公开只读契约。不为本任务改造现有列表、写入、审批、草稿或动作权限，也不把 Demo 演化成真实 ERP／WMS 模块。

## 3. 数据集、字段和查询契约 [Architect]

以下标识和契约已按本方案实现：

- 数据集代码：`demo-business-orders-readonly`，契约版本 `1.0`。
- 工具代码：`business.demo_business_order.query`，函数名 `query_demo_business_orders`，版本 `1.0`，分类 `Confidential`。
- 仅明细查询与准确匹配总量，单位为“单”。当前租户、非软删除、当前授权范围及业务筛选应用后，一条实体 ID 计一单，包含所有审批状态。不按标题或客户去重。
- 返回字段固定为 `id`、`orderNo`、`title`、`approvalStatus`、`departmentId`、`createdAt`。ID／部门 ID 是受控引用，不展示或枚举额外部门目录。
- 不返回或筛选客户名称、金额、负责人姓名、操作人、审批明细、附件、变更历史、原始请求或工作流内部字段。金额缺少币种口径，首批不做金额汇总；标题和单号仍按机密业务文本处理，不声称完全无敏感内容，文本不得当作指令执行。

允许参数：

| 参数 | 定义与校验 |
| --- | --- |
| `keyword` | 单号或标题包含匹配，去首尾空白，最多 100 字符；不从被排除字段匹配，以免通过命中结果推断客户或负责人 |
| `approvalStatus` | 可选字符串枚举：Draft、Pending、Approved、Rejected、Withdrawn、Cancelled；无值表示所有状态。中文显示映射沿用项目已有约定，未知状态拒绝 |
| `departmentId` | 可选非空 Guid，永远与授权范围相交；越界无匹配返回零，不额外泄露部门存在性 |
| `departmentScope` | Authorized（默认）或 CurrentDepartment；本部门不包含下级，当前部门不存在／停用时要求澄清 |
| `limit` | 默认 20；1 至 `min(MaxToolRows, 200)`，超限拒绝，不静默扩大或降为全量 |

不接受租户、用户、SQL、列名、表达式、自由排序、金额条件或分页游标。固定按 `CreatedAt` 降序、`Id` 升序稳定排序，取前 limit 条；未显示的单据以截断标记表达。

首批不增加时间筛选、状态分组或历史状态快照。“再看上个月”等未支持口径明确澄清，不能悄悄忽略或根据创建时间猜测历史审批状态。

完整匹配总量使用异步数据库计数，不能从已截断明细计算。查询时间表示实际查询发生时间；无独立数据水位，不声称历史时点一致。普通读隔离下计数与读取之间可能变化，返回时应保留这一限制；不为本批新增快照表或修改数据库隔离级别。

## 4. 授权与范围 [Architect]

工具要求现有 `AiCenterConstants.ToolQueryPermission` 和 `demo-business-order:view`；聊天入口继续要求原聊天权限及租户批准配置。创建权限不是查询的前提，不新增权限体系或权限种子。

逐次校验当前用户、租户有效性、上下文一致性、当前业务查看权限及 AI 配置；不能仅信任历史 claims、模型参数或会话旧结果。只读业务公开用例也在自身入口验证身份、租户与业务查看权限，避免被其他内部入口直接调用后绕过业务授权。复用现有身份校验、范围解析、`IDataPermissionFilter` 与 `DemoBusinessOrderDataPermissionSpecification`，不复制数据权限规则。

无论是否超级管理员，AI 查询都显式限定服务端当前租户与 `IsDeleted = false`，不提供跨租户查询参数；无有效租户时拒绝。

| 服务端范围 | Demo 实际过滤语义 |
| --- | --- |
| All | 当前租户非删除 Demo 单据 |
| CurrentUser | `CreatedBy` 等于当前身份；不改成负责人 |
| 部门、下级或自定义部门 | 由原范围解析器确定的 DepartmentId 集合 |
| 本人与部门合并 | CreatedBy 本人或允许部门，再与租户及业务筛选相交 |
| 空有效范围 | 零条，不回退租户全量 |
| CurrentDepartment 追问 | 原授权结果再限定当前有效部门，不包含下级 |

查询结束、敏感结果输出前再次验证身份、权限、配置和范围指纹；发生变化要求重新查询。历史卡片重载与追问同样重新检查：若任一历史行删除、变成不可见或部门／状态等事实发生变化，隐藏旧卡片并要求重查；历史结果不是授权凭据。

## 5. 分层实现、结果与追问 [Architect]

1. 在 DemoBusinessOrders 新增只读 DTO、公开接口与查询服务，复用领域数据权限规范、原过滤器和异步执行器；源数据只读，不生成示例记录。查询、当前历史行校验共用模块范围规则，AI 模块不直接读取 Demo 仓储。
2. 新增只读 Handler，注册到已有只读工具体系，校验参数后直接调用公开用例；用服务器实际生效参数形成最小上下文及范围指纹。设置 30 秒工具超时，传递取消；继续受现有 Run／用户／租户限流及预算控制，不新增独立后台运行机制。
3. 增加独立的 `demo-business-orders` 结构化载荷和卡片，显示总量、展示量、截断、状态、口径、过滤、来源／版本及查询时间。不能把单据塞进 AiUserTableData 或复用人数卡片。
4. 扩展工具／数据集版本白名单、结果封套、大小裁剪和引用校验，保留 64 KiB 封套、4 KiB 上下文、256 KiB 单次读取与会话保留期限；裁剪只减少展示行，不能改变完整计数。无独立水位时 `AsOf` 不伪造历史快照。
5. 历史读取核对同租户、同用户、同会话真实成功调用、字段／版本、批准配置、当前范围与行可见性／事实。权限撤销、工具关闭、范围或记录变化后不显示旧敏感卡片；全部校验经业务公开用例完成。
6. 追问只继承实际 keyword、状态、部门、范围选择与 limit；支持“本部门”“只看待审批”等修改，重新执行查询。未知字段、换错工具、跨会话引用、过期或歧义均拒绝或澄清，不把助手文字当事实来源。
7. 最小调整 Demo 提示词，明确“查询已存在单据”调用只读工具，“准备草稿”走已有动作，避免查询意图触发草稿。保持原动作授权、人工确认和写入链路。
8. 新工具默认关闭，增加明确 Demo 开关；默认配置不开放，不启用 SQL 报表或改已有批准键。仅已有内置兼容入口获批后可用，不把工具加入 AIC-005 权限助手四工具白名单；构建／安全提示／Schema 变化遵守原冻结、评测和发布兼容规则。

本批只更新内部工具 Schema、版本及数据集引用；外部 MCP 不新增 Handler、字段授权、Hash 或发布记录。未来外部暴露时另行评审服务身份业务范围并更新 MCP Schema／Hash，新增字段不能自动开放。

## 6. 预计改动文件 [Architect]

| 范围 | 文件（仓库相对路径） |
| --- | --- |
| 拟新增只读契约与实现 | `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderReadOnlyModels.cs`、`DemoBusinessOrderReadOnlyQueryService.cs` |
| 拟新增工具 | `backend/PermissionSystem.Application/AiTools/DemoBusinessOrderQueryAiToolHandler.cs` |
| 注册与开关 | `backend/PermissionSystem.Application/DependencyInjection.cs`、`AiTools/AiToolModels.cs`、`AiCenter/AiCenterDependencyInjection.cs`；`backend/PermissionSystem.Infrastructure/Options/AiCenterOptions.cs`、`DependencyInjection.cs` 及必要配置说明 |
| 服务端授权、封套与追问 | `backend/PermissionSystem.Application/AiCenter/AiQueryAccessGuard.cs`、`AiStructuredResultModels.cs`、`AiStructuredResultReader.cs`、`AiFollowUpContextService.cs`、`AiScenarioCatalog.cs`；`AiTools/AiReadOnlyToolHandlerBase.cs` 仅补载荷与真实水位限制 |
| 前端 DTO／卡片 | `frontend/permission-admin/src/api/ai.ts`、`components/AiStructuredResultCard.vue`；拟新增 `components/AiDemoBusinessOrderTableCard.vue` 及测试 |
| 核心回归 | 拟新增 `backend/PermissionSystem.UnitTests/AiCenter/Aic007DemoReadOnlyTests.cs`、`backend/PermissionSystem.IntegrationTests/AiCenter/Aic007DemoReadOnlyApiTests.cs`；补现有注册／封套／追问／场景／会话回归 |
| 文档 | 本方案、后续计划、总体实施计划及实现后新增 `docs/ai-center-aic-007-acceptance.md` |

`AiCenterOptions` 已直接实现 `IAiToolConfiguration`，配置注册沿用 Infrastructure 原入口，不新增平行配置体系。预计不修改实体、普通业务 Controller／列表／写服务、MCP Server 或用户报表查询实现；不新增依赖。

## 7. 数据影响与回退 [DBA]

**结论：无数据库变更。** 本地 Demo 批次复用既有业务表、租户／审计／软删除／数据范围，不新增实体、视图、索引、EF 迁移、权限种子或数据库授权，也不执行迁移和业务数据写入。

异步查询投影仅取批准字段，显式过滤租户和软删除；现有数据量和性能未知，SQL Server 语义／查询计划／规模性能须在指定隔离环境验证，不凭 InMemory 结果声称通过。不根据假设自动增加索引；若实测需要索引或结构变化，重新提交 DBA 方案。

结果与上下文沿用已有 Tool 消息存储和保留机制。回退先关闭 Demo 工具开关，保留会话历史，历史结果因开关关闭不可继续读取／追问；不执行 EF Down、清理表或恢复宽松授权。前端旧版本保留未知结果类型的提示路径，不改旧用户卡片语义。

## 8. 验证与阶段退出 [Reviewer]

| 验证类型 | 核心覆盖 |
| --- | --- |
| 业务查询 | 各审批状态、无匹配、稳定排序、完整计数与展示截断、仅允许字段；客户／负责人文本不参与关键词，金额不输出 |
| 范围与身份 | All、CreatedBy 本人、各部门范围、本人／部门并集、空范围、跨租户、软删除、禁用身份、撤权、本部门缺失；Owner 与 CreatedBy 不同的夹具防止规则漂移 |
| 参数安全 | 未知／重复键、非法 Guid／状态／范围、关键词长度、超限行数、伪造身份／租户、SQL／任意字段、提示注入文本 |
| 历史与追问 | 引用归属、保留期、上下文歧义、字段／版本／开关变化、撤权、对象删除／修改、范围变化；只修改预期条件，时间追问明确不支持 |
| 写入边界 | 查看权限而无创建权限仍能查询；只读查询不注册动作、不调用创建／修改／审批接口；查询意图不生成草稿 |
| 运行与 UI | 超时、取消、既有限流和审计、封套大小裁剪；单据卡片、状态文案、过滤／总量／截断、旧会话兼容 |
| 环境与模型 | 指定本地 SQL Server 的查询语义和规模性能，目标权限身份的 HTTP／浏览器体验；真实供应商与离线评测分开记录 |

实现后按实际工程运行构建、核心专项／全量回归及前端测试、类型检查、lint、构建；源环境、浏览器、人工口径及真实模型不足项保持待验收。运行命令读取真实工程配置后确定，不把历史 AIC-006 测试作为本轮证据。

Demo 批次退出条件：只读查询、授权、字段、结构化重载／追问及必要验证通过，实际覆盖和缺口记录明确。真实业务接入退出条件仍是原 AIC-007 的 Owner 签认、正式来源、业务范围和目标调用者端到端验收，本批不能替代。

实施复核：使用 `IDataPermissionRepository<DemoBusinessOrder>` 沿用可见行过滤，再与新解析的当前范围相交，符合 EA020 架构约束；基础设施配置注册无需改动。无新增依赖或数据库变更。代码与安全边界复核通过，专项测试 74/74（AIC-007 60 个、EA020 14 个），HTTP 集成全量 58 个通过、25 个 SQL 环境用例跳过，前端 46/46。最终全量 Unit 与解决方案构建受 UnitTests.dll 文件访问错误阻塞，不能记为最终全绿；实际 SQL Server、浏览器和真实模型仍待验收。完整证据、限制及回退见 [验收记录](ai-center-aic-007-acceptance.md)。
