# AIC-002：权限排障助手实施方案

> 日期：2026-10-06
>
> 状态：2026-10-06 用户已确认并完成首批实现；环境待验收，见 [验收记录](ai-center-aic-002-acceptance.md)。
>
> 依据：`AGENTS.md`、`ai-center-next-development-plan.md` 的 AIC-002。

## 1. 现状与影响 [Architect]

已核对的计算链路：

| 对象 | 当前真实链路 | 诊断口径 |
| --- | --- | --- |
| 菜单 | `CurrentUserAppService.GetCurrentUserMenusAsync`：启用角色 → RoleMenu → Visible 菜单 → 补可见祖先 → BuildTree；超级管理员读取租户可见菜单 | 返回菜单树是否包含目标；不能把取得角色菜单关系等同于页面一定可访问 |
| 页面/按钮 | 前端 `auth`/`permission` store、路由守卫、权限指令；普通用户使用大小写敏感的权限码匹配 | 单独展示前端静态判断及限制；本轮不修改既有匹配语义 |
| API 权限 | `PermissionRequirement` 拆分 `|`/`,` 后任一满足；`CurrentUserService.HasPermission` 支持超级管理员、`*`、大小写不敏感 | 自查以当前服务端身份为准；仅说明指定权限要求，不声称完整 API 请求一定成功 |
| 他人身份配置 | `IUserCredentialValidator.GetAuthenticationStateAsync`：活动租户、启用且未删除的用户、同租户启用角色及有效权限关系 | 复用认证状态读取；标明是当前配置推算，不代表目标用户某个浏览器或 Token 的状态 |
| 数据范围 | `DataScopeService`：超级管理员 All → 用户覆盖 → 启用角色范围合并 → 无配置默认 CurrentUser；`DataPermissionFilter` 将本人和部门条件取并集 | 解释实际有效规则；不从角色配置读取接口的展示默认值推断有效范围 |
| AI 结果 | 工具结果已存于 `AiMessage.Content`；调用引用存于 `AiToolInvocation.CitationJson`；会话接口隐藏 Tool 消息 | 增加专用诊断 DTO 投影，前端不解析助手文字作为证据 |

当前无法直接观测的条件：目标用户客户端缓存、真实请求的全部授权策略、会话撤销/过期状态、业务资源归属、业务状态校验和前端组件实际运行状态。必须在结果中列出，不能由模型补全。

影响范围为 Application、API、相关身份/菜单共用判断及聊天组件，属于中大型需求。仅提取诊断所需共用计算，不建设另一套授权系统。

## 2. 建议首批范围及授权边界 [Architect]

首批支持三种请求：`Menu`、`Permission`、`DataScope`。每次只诊断一个目标；默认查询自己。菜单以明确菜单 ID 定位，权限以明确权限码或现有组合要求定位，数据范围不接收业务数据 ID。名称歧义时先澄清，不能猜选用户或菜单。

调用者身份、租户、权限来自服务端；请求只允许可选 `targetUserId`、诊断类型及对应资源参数，不允许提交 actor、租户、角色、权限集合或超级管理员标记。服务层再次校验身份和租户，不能仅依赖工具列表隐藏或 Controller 的授权属性。

已确认复用现有权限，不新增权限种子。授权边界如下：

| 请求 | 必需权限与范围 | 证据限制 |
| --- | --- | --- |
| 查询自己 | `ai:tool:query`；通过聊天调用还需现有聊天权限 | 仅输出自己的判断和必要证据，不枚举全租户用户/菜单/权限目录 |
| 查询他人的共同前提 | 上述权限，加 `ai:tool:user-query`、`system:user:view`、`system:role:view`；目标必须在同租户且通过调用者现有用户数据范围过滤 | 不存在、跨租户、软删除或范围外目标使用相同不可查询结果，不泄露存在性 |
| 查询他人菜单 | 共同前提，加 `system:menu:view` | 才能读取目标菜单配置和角色菜单关系 |
| 查询他人权限 | 共同前提，加 `system:permission:view` | 仅返回请求权限的命中/缺失及必要角色来源，不输出全部权限 |
| 查询他人数据范围 | 共同前提，加 `system:role:data-scope` | 返回有效规则和已授权配置证据，不披露无权读取的部门名称 |

自己查询菜单时，若没有 `system:menu:view`，只基于当前可见菜单树判断；目标未在树中时可说明“不在当前可见菜单树中”，但隐藏、未配置、未分配等原因属于证据不足，不能返回隐藏菜单详情。角色配置细节需相应角色查看权限；数据范围无额外管理权限时仅显示本人有效规则。

首批坚持当前 AI 工具注册器的身份租户与租户上下文一致约束；不扩展超级管理员跨租户代查能力。当前身份停用或租户停用应被既有认证链拒绝；授权范围内查询同租户停用目标时，可给出身份停用结论，不能将其仍有角色配置解释为可访问。

## 3. 实施设计 [Architect]

### 3.1 诊断用例与共用规则

新增 `IPermissionDiagnosticService` 及请求/响应 DTO，位于 Application/Permissions。服务负责校验调用者、定位已授权目标、调用共用计算并裁剪证据。Controller 仅接收 DTO、传递 CancellationToken 和包装 ApiResult。

- 权限：自查复用 `ICurrentUserService.HasPermission`；配置代查复用 `IUserCredentialValidator.GetAuthenticationStateAsync`。将组合权限要求解析与身份权限匹配提取为小型共用函数，让现有 API 判断与诊断使用同一规则，并补现有授权回归测试。保留前端匹配差异，不顺手修复。
- 菜单：从 `CurrentUserAppService` 提取按服务端身份计算可见菜单的共用 resolver，保留 Visible、启用角色、祖先展开和树构建语义。诊断调用同一 resolver；同时记录必要来源。无权读取的配置不进入模型结果。
- 数据范围：从 `DataScopeService` 提取接受明确服务端身份快照的只读 resolver，当前用户入口和诊断共用它；不改写请求身份，不用“冒充当前用户”实现代查。保留覆盖优先级、范围合并及空范围行为，所有目标读取显式限定租户并沿用软删除。
- 调用者用户可见范围继续复用 `IDataScopeService` 和 `IDataPermissionFilter`，在读取目标认证配置及角色证据之前完成检查。

DTO 包含契约版本、目标引用、评估口径、评估时间、检查项及状态、服务端结论、证据来源、未覆盖条件、受控建议入口。结论使用明确枚举，例如允许、拒绝、受限、证据不足；检查项保留未评估状态，不能用“未检查”代替“已通过”。

### 3.2 工具与 API

新增只读工具 `permission.diagnose`，函数名 `diagnose_permission`，复用现有注册、超时、取消、调用审计、引用和失败流程。严格校验类型、必要参数、GUID、权限要求长度及组合项数量，拒绝未知身份字段。一次调用返回一个诊断对象，证据列表设置上限并显式标记截断。

新增 `POST /api/ai/permission-diagnostics`，作为确定性诊断入口，复用相同 Application 用例及权限检查。工具上下文与服务端当前身份必须一致，不能信任外部直接构造的执行上下文。

提示词仅指导模型使用该工具解释服务端结果；权限结论和证据卡片直接来自服务端 DTO，模型文字不能覆盖 DTO。

### 3.3 聊天证据与兼容

为 `AiToolExecutionResult` 增加可选的专用诊断结果。会话服务仅对这个已注册诊断工具生成服务端结果封套：类型、版本、RunId、诊断 DTO；沿用现有 Tool 消息 Content 存储，不再保存第二份完整查询结果。发送给模型的内容仍为裁剪后的工具结果。

运行响应和会话详情增加可选诊断列表；读取时校验封套类型/版本、所属租户、会话、运行及调用归属，对诊断证据重新执行当前权限和目标可见范围检查。撤权后隐藏对应卡片，不重新执行历史诊断；评估时间保留为历史时间。旧消息没有封套时继续使用纯文本路径，不把任意 Tool JSON 或助手文字解释为诊断。

新增 `AiPermissionDiagnosticCard.vue`，展示目标、结论、检查项、来源、评估时间、范围限制和截断提示，并接入 `AiChatDialog.vue`。建议入口使用服务端固定入口标识，前端映射白名单路由，再检查当前权限和路由可用性；禁止使用模型或数据库任意 URL。只提供授权允许的配置页面入口，不直接变更角色、菜单、权限或数据范围。

历史助手文字继续沿用现有会话行为，本轮不承诺撤权后追溯清除所有历史自然语言；新增加的诊断卡片和重新查询必须重新鉴权。该限制须在验收记录明确。

## 4. 数据评审 [DBA]

结论：**无数据库结构变更、无新增迁移、无权限种子变更**。

诊断只读取现有用户、租户、角色、菜单、权限和数据范围。最小诊断证据复用现有 Tool 消息 Content，不新增证据表或字段；与现有消息内容相同的保留/清理机制，不能放入保留时间更长的审计 CitationJson。调用审计继续保存摘要和现有引用信息。

不保存 Token、密码、SecurityStamp、会话标识、完整身份对象或完整权限清单；认证快照必须投影后才能进入 DTO。结果需要限制证据项数和序列化大小；超限明确裁剪，不静默改变结论。

现有代码回退后忽略专用 Tool 消息封套即可，不需要 Down 或数据清理。若实施中需要表结构、种子或新的证据持久化方案，必须重新提交 DBA 评审和用户确认。

## 5. 方案阶段候选文件 [Developer]

以下为确认时的候选路径；实际实现采用既有服务同时实现 resolver 接口，另新增专用结果读取器及测试，具体见验收记录：

- `backend/PermissionSystem.Application/Permissions/PermissionDiagnosticModels.cs`、`PermissionDiagnosticService.cs`、`PermissionEvaluation.cs`。
- `backend/PermissionSystem.Application/Users/CurrentUserAppService.cs` 及新增共用菜单 resolver。
- `backend/PermissionSystem.Application/DataPermissions/DataScopeService.cs` 及新增共用数据范围 resolver/接口。
- `backend/PermissionSystem.Application/AiTools/PermissionDiagnosticAiToolHandler.cs`、`AiToolModels.cs`。
- `backend/PermissionSystem.Application/AiCenter/AiCenterDependencyInjection.cs`、`AiConversationModels.cs`、`AiConversationService.cs` 和 Application 注册入口。
- `backend/PermissionSystem.Api/Controllers/AiPermissionDiagnosticController.cs`、现有授权要求/当前用户匹配实现的最小共用规则调整。
- `frontend/permission-admin/src/api/ai.ts`、`components/AiPermissionDiagnosticCard.vue`、`components/AiChatDialog.vue` 及对应测试。
- 对应后端单元/集成测试；本方案、后续开发计划、原 AI 中心实施计划的状态和验收记录。

不改 Domain 实体/EF 映射，不增加依赖，不实施 AIC-003 通用结构化追问、AIC-004 真实模型评测系统或 AIC-005 发布治理。

## 6. 验证与复核 [Reviewer]

确认后需完成：

1. 诊断和实际权限链一致：普通用户、超级管理员、通配符、大小写、组合权限；菜单直接分配/祖先展开/隐藏父节点/停用角色；数据范围覆盖、角色并集、无配置、无部门、空自定义范围。
2. 安全：未认证、租户上下文不匹配、跨租户、范围外目标、无查看他人权限、软删除、停用目标、停用调用者、伪造身份参数、超量/非法参数及取消。失败不得返回身份或配置证据。
3. 结果：服务端结论不受模型解释改变；失败或部分结果不被展示为通过；专用封套不能由助手文字伪造；会话重载、旧会话、撤权后的卡片隐藏、受控建议入口和截断。
4. 复用现有 xUnit、EF InMemory 和 Vitest；按需补 API/SQL Server 集成测试，只有明确隔离的数据库环境才执行，不能自动沿用 AIC-001 遗留库。模型替身用于确定性测试；真实供应商及浏览器人工验收单独记录。
5. 实施后执行后端相关测试和构建，前端 `test:unit`、`type-check`、`lint`、`build`。命令结果以当次实际执行为准，未覆盖环境列入待验收，不提前标记已完成。

本轮复核：方案与 AIC-002 的只读、安全、证据和最小变更要求一致；2026-10-06 用户确认授权边界后已实施，本地验证及未完成的环境验收见验收记录。

## 7. 确认与实施补充

用户已在本次会话回复“确认”，同意第 2 节三类诊断范围及保守授权矩阵、第 3 节共用计算与专用证据卡片方案。若需要新建诊断专用权限或扩大代查范围，应先调整方案并重新评审数据影响。

实现时补充宿主注册边界：主应用显式启用新增诊断工具，外部 MCP 的默认六个工具不变。代查数据范围由共用 resolver 使用调用者最新认证快照计算；过期超级管理员/部门身份拒绝继续诊断。

任务开始时已存在未跟踪目录 `artifacts/`，本任务未修改或清理其内容。已执行本地确定性测试和构建；实现与本地验证阶段未执行真实数据库迁移、部署或 Git 提交。随后用户明确授权先提交并推送 Git，后续统一验收。
