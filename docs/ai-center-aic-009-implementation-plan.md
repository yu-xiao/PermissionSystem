# AIC-009：动作权限解耦及真实单据草稿实施方案

> 日期：2026-10-07
>
> 状态：用户已确认首批，动作权限解耦已实现、待验收；真实单据批次待业务规则及独立确认，AIC-009 整体未完成。
>
> 依据：`AGENTS.md`、[后续开发计划](ai-center-next-development-plan.md)、[AIC-004 验收记录](ai-center-aic-004-acceptance.md)、[AIC-008 实施方案](ai-center-aic-008-implementation-plan.md)及当前代码。

## 1. 现状与依赖 [Architect]

工作区调研开始时 `git status --short` 为空。当前注册的动作处理器只有 `DemoBusinessOrderDraftHandler`，真实 ERP／WMS 单据类型、Owner 和业务契约尚未提供；已检索到的订单模块为 `DemoBusinessOrders`、`DemoApprovalOrders`，不能当作已确认的真实业务。

| 调研起点文件（相对仓库根目录） | 实施前已核实的行为 |
| --- | --- |
| `backend/PermissionSystem.Application/AiActions/AiActionToolRegistry.cs` | 对所有动作统一要求 `demo-business-order:create`，没有按处理器的 `ToolDefinition.RequiredPermissions` 逐项检查 |
| `backend/PermissionSystem.Application/AiActions/DemoBusinessOrderDraftHandler.cs` | 已声明 AI 草稿和 Demo 创建权限，并在用例入口复核；草稿读取、更新、取消及参数验证均为 Demo 实现 |
| `backend/PermissionSystem.Application/AiCenter/AiConversationService.cs` | `CanReadDocumentDrafts` 含 Demo 创建权限；后台 Run 的工具列表排除全部动作工具 |
| `backend/PermissionSystem.Application/AiActions/AiDocumentDraftModels.cs` | 通用命名的响应和更新请求使用强类型 Demo Payload，接口 Schema 入口也为 Demo 专用 |
| `backend/PermissionSystem.Application/AiActions/AiDocumentExecutionService.cs` | 确认／执行要求 AI 草稿、AI 执行、Demo 创建权限；只接受 Demo 类型及版本，直接调用现有 Demo 业务服务 |
| `frontend/permission-admin/src/components/AiChatDialog.vue`、`AiDocumentDraftCard.vue` | 全局执行按钮能力依赖 Demo 创建权限；卡片字段和确认文案为 Demo 专用 |
| `backend/PermissionSystem.Application/DependencyInjection.cs` | 草稿 Reader／Service 直接绑定 Demo 处理器，动作注册器通过现有 DI 注册 |

现有人工确认绑定草稿版本、PayloadHash、HandlerVersion、确认版本和并发令牌，执行消费确认并在事务内创建业务单据、执行记录及 Outbox。以上机制保留。

AIC-004 代码及离线闭环可复用，但黄金案例和真实模型尚未验收；现有评测不注册动作处理器，不能将它的通过结果记为动作验收。AIC-008 已实现后台执行，但隔离 SQL、多进程和环境验收待完成，且明确采用“后台不提供草稿动作”的兼容分支。

## 2. 批次与确认范围 [Architect]

按用户已确认方案分两批实施，确认首批不等于确认真实单据规则或后台动作开放。

1. **首批：动作权限解耦与兼容验证。** 复用当前 Demo 行为，建立按处理器声明的授权路径，移除通用层和聊天组件中的 Demo 全局权限耦合；增加安全测试与验收记录。生产中仍只有现有 Demo 动作，不新增其他业务动作。
2. **真实单据批次：待业务信息后独立确认。** 决定单据类型、字段、业务服务、状态机和审批，再评审 DTO、前端展示、事务及数据影响。缺少规则时暂停该批实现，不创建 AI 专属业务单据表。

首批保持后台 Run 不提供任何草稿动作，正式单据仍由原人工确认／执行 API 创建。当前配置了持久化执行的正常聊天不能通过本批获得草稿生成能力；这是已知兼容限制，不能宣称完整用户场景已恢复。若需要在当前后台聊天生成草稿，须增加独立兼容方案和 SQL／故障证据后重新确认。

## 3. 权限与职责 [Architect]

### 3.1 处理器声明与统一校验

复用 `AiToolDefinition.RequiredPermissions` 声明草稿准备所需权限；在 Application 增加动作能力声明和共用访问策略，按受控处理器定义匹配业务类型、处理器版本及人工确认／执行所需权限。声明来自服务端注册代码，不接受模型或客户端提交权限、业务类型映射或处理器版本。

注册时参照只读注册器的已有模式校验工具定义、权限集合及唯一性：工具代码、函数名和可解析的业务类型／版本不得冲突。缺失业务权限、版本不一致或未知动作默认拒绝；不能用空权限集合意外开放动作。动作与只读工具的合并目录继续由现有会话目录校验，发布场景继续执行工具白名单及版本兼容检查。

| 阶段 | 当前 Demo 的权限要求 | 检查位置 |
| --- | --- | --- |
| 准备、读取、编辑、取消草稿 | `ai:document:draft` + `demo-business-order:create` | 处理器声明、注册器／草稿用例共用访问策略；租户、归属及状态继续由用例检查 |
| 人工确认、执行正式单据 | 上述权限 + `ai:document:execute` | 执行服务先验证公共身份及归属，再按草稿实际业务类型／版本复核；不根据客户端选择处理器 |
| 部门引用 | 沿用 `system:department:view` 及现有有效部门验证 | 当前 Demo 业务校验，不加入所有动作的公共权限 |
| 二次验证 | 沿用现有强制敏感操作校验 | 确认入口；前端继续检查验证码发送、验证权限 |

不新增权限体系、不改权限种子、不扩大 Demo 用户原有权限。复用当前 AI 开关、租户允许列表、当前用户服务、租户上下文和权限计算；当前身份与有效租户一致才可调用。

### 3.2 工具和历史草稿

- 工具展示按每个处理器的声明过滤；调用入口再次检查同一声明，并校验上下文的用户、租户及调用所属 Run／会话。
- 会话层仅判断公共草稿能力，Reader 对每份草稿按实际业务类型／版本、租户及调用人过滤；无权或无法识别的类型不返回 Payload、候选项或执行结果。直接读取、更新、取消仍拒绝无权访问。
- 当前 Reader／Service 仍只投影受支持的 Demo Payload；其他类型默认不投影，不能把其他业务 JSON 反序列化为 Demo。真实单据批次才扩展结果契约和处理器分派。
- 重复调用命中已有草稿时，返回前验证调用人、会话、Run、业务类型和版本一致；冲突拒绝，不能因仅匹配租户和调用标识返回他人的草稿。保留现有调用唯一约束，不放宽幂等键范围。

### 3.3 人工确认、执行与前端

执行服务的公共授权去掉全局 Demo 创建判断，改为根据已加载草稿的服务端声明校验；本批业务创建仍只接受现有 Demo 类型和版本并调用 `IDemoBusinessOrderService`。没有受支持执行实现的动作不得确认或执行，也不得因获得 Demo 权限被放行。成功重放路径同样复核当前授权、业务类型、租户、调用人及草稿绑定，不能通过旧成功记录绕过撤权。

保留版本／Hash／并发令牌、确认期限、强制二次验证、确认消费、事务、业务幂等键、失败恢复及 Outbox。确认后业务关联状态变化仍由现有业务服务校验；超时或结果不明不得自动再创建单据，只有已证明成功且匹配的执行记录可返回历史结果。

草稿响应增加服务端计算的编辑、取消、确认／执行能力字段，前端按当前卡片能力和已有敏感验证权限控制按钮，移除聊天组件全局 Demo 权限判断。字段仅用于体验，API 每次重新授权。新前端对缺少能力字段采用保守禁用，保留旧 Payload 和现有 Demo 卡片；旧前端可忽略新增字段。未知类型不展示 Demo 表单或配置入口。

实施复核补充：取消、确认及执行入口增加专用 `AiDocumentDraftAccessAttribute` 资源过滤器，顺序为 `-2100`，在通用幂等过滤器 `-2000` 返回缓存前检查当前草稿归属及动作权限；确认／执行通过 Application 的 `EnsureAccessAsync` 复核。通用幂等过滤器保持原实现，用例入口继续再次授权。该补充用于落实撤权后不能重放旧成功响应的原方案边界，不扩展业务动作。

## 4. AIC-008 兼容结论 [Architect]

本批保留后台 Run 动作禁用，测试覆盖后台模型未收到动作定义、提出未提供动作时不能触发处理器、人工重试不会自动确认或执行。历史草稿仍可在当前授权范围读取并通过原人工入口处理。

不能仅删除后台工具列表限制就开放草稿。后续开放前至少需证明：可信后台身份复核、取消／租约丢失／结算路径不能留下新草稿写入、同次调用重复投递只生成一份草稿、草稿与校验及调用状态写入边界一致、旧进程迟到不能提交、已开始 Run 不自动重放。现有持久化 fence 的结算清理主要处理消息和工具记录，不能直接视为草稿兼容证明。

## 5. 数据影响及回退 [DBA]

**首批无数据库变更。** 复用现有 `AiDocumentDraft`、Validation、Confirmation、Execution、Run、Invocation 和业务表，保留 BaseEntity 租户、审计、软删除和并发机制。能力字段只在响应计算，不持久化。

保留现有草稿 `(TenantId, SourceInvocationId)`、调用 `(TenantId, InvocationId)`、执行 `(TenantId, ConfirmationId, ConfirmationVersion)` 及业务幂等键的过滤唯一索引。首批不修改索引，不生成或执行迁移，不清理或改写历史数据。现有唯一索引仍须隔离 SQL 验证，不能用 InMemory 证明并发和事务。

真实单据若涉及新增字段、关联、索引或兼容契约，由 DBA 在第二批另评审。需要修改幂等键或数据库约束、开放后台写入时也必须重新评审确认。

回退保持后台禁用并关闭受影响动作／场景入口，保留草稿、确认、执行、费用及审计记录；构建变更继续受 AIC-005 兼容规则约束，不静默迁移旧发布版本。不自动执行数据库回滚或删除记录。

## 6. 实际改动文件 [Developer]

用户回复“确认”后已实施以下最小必要变更，测试与证据详见 [验收记录](ai-center-aic-009-acceptance.md)。Application 文件路径均以 `backend/PermissionSystem.Application/` 为根。

| 范围 | 文件与职责 |
| --- | --- |
| Application 动作 | 修改 `AiActions/AiDocumentDraftModels.cs`、`AiDocumentExecutionModels.cs`、`AiActionToolRegistry.cs`、`DemoBusinessOrderDraftHandler.cs`、`AiDocumentExecutionService.cs`；新增 `AiActions/AiBusinessActionAccessPolicy.cs`，集中能力声明解析和阶段权限检查 |
| Application 会话／DI | 修改 `AiCenter/AiConversationService.cs`、`DependencyInjection.cs`；去掉公共 Demo 门槛并注册访问策略，保留后台动作限制 |
| API | 修改 `backend/PermissionSystem.Api/Controllers/AiDocumentDraftController.cs`；新增 `Authorization/AiDocumentDraftAccessAttribute.cs`，在幂等缓存返回前复核草稿访问权限 |
| 前端 | 修改 `frontend/permission-admin/src/api/ai.ts`、`src/components/AiChatDialog.vue`、`AiDocumentDraftCard.vue` 及对应测试；使用逐草稿能力 |
| 后端测试 | 扩展 `backend/PermissionSystem.UnitTests/AiCenter/AiActionToolRegistryTests.cs`、`AiConversationServiceTests.cs`、`AiDocumentExecutionServiceTests.cs`、`DemoBusinessOrderDraftHandlerTests.cs`、`Aic008BackgroundRunTests.cs`；新增 `Aic009ActionAuthorizationTests.cs`、`Aic009ActionTestSupport.cs` 和 `backend/PermissionSystem.IntegrationTests/AiCenter/Aic009ActionApiTests.cs` |
| 文档／产物 | 本方案、后续开发计划、总体实施计划、新增 `docs/ai-center-aic-009-acceptance.md`；`.gitignore` 忽略本地 `artifacts/aic-009/` 验证产物 |

不新增依赖，不重构真实业务模块，不先泛化所有 Payload、控制器或前端表单。

## 7. 验证与退出条件 [Reviewer]

首批已完成下列确定性专项及适用回归；SQL、真实模型、浏览器和目标环境验证仍待验收，实际结果及证据边界见 [验收记录](ai-center-aic-009-acceptance.md)。

1. 用两个合成动作定义验证不同业务权限的允许／拒绝矩阵：只有 Demo 创建权限不能使用另一个动作；仅有另一个动作权限不需要 Demo 权限。测试夹具不注册生产新动作或创建真实业务数据。
2. 展示与调用授权一致；目录生成后撤权拒绝执行；无效定义、重复注册、未知类型／版本、禁用 AI、无效身份及租户上下文拒绝。
3. 会话／Run 历史按业务类型过滤；跨租户、跨用户和混合草稿不泄露；调用标识冲突不返回不属于当前上下文的草稿。
4. 草稿编辑与确认后修改、过期确认、重复确认、撤权后的成功重放、关联数据变化、并发令牌冲突及事务失败；现有正式单据和 Outbox 不产生半完成结果。
5. 后台运行和后台重试仍无动作能力；超时、取消、未知执行结果不能自动再创建或自动确认正式单据。
6. 前端逐卡片按钮、旧响应兼容、未知类型、服务端再次拒绝、敏感验证、修改后确认失效与历史重载。
7. 后端 Release build、专项及适用全量测试、AIC-004 原离线回归；前端单元测试、类型检查、lint 和 build。动作授权新增确定性专项，原只读评测不被冒充为动作模型评测。
8. 隔离 SQL Server 复核执行并发／事务；真实模型、浏览器及目标环境条件不足时明确待验收，不沿用其他任务的通过证据。

首批实现和确定性检查通过后可记“首批待验收”，不能将整个 AIC-009 标为已完成。真实单据规则与业务验收、AIC-004 适用质量证据，以及未来后台动作兼容仍分别跟踪。

## 8. 确认记录与后续依赖 [Architect][DBA]

- 用户已回复“确认”，批准先实施动作权限解耦，保留当前 Demo 契约及后台动作禁用；该首批已实施，不再重复请求确认。未来恢复后台草稿需要独立兼容方案及确认。
- 真实单据批次需要提供：首个类型和业务 Owner；已有模块／服务接口；必填字段、关联主数据和金额口径；创建后状态、状态机及审批规则；角色权限、租户和组织／仓库／账套范围；幂等、事务及验收案例。尚未提供，不能推定。

本轮已按 `AGENTS.md` 完成调研、数据评审、用户确认、首批开发及复核。代码与确定性专项复核通过，完整环境及发布验收未通过，首批保持待验收；真实单据批次继续等待上述关键业务信息。
