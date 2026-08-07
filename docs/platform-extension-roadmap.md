# 企业级平台增强功能路线图

本文档基于当前 `PermissionSystem` 项目、AGENTS.md 约束和现有代码能力，规划下一阶段企业级通用平台能力。当前项目已具备 RBAC 权限、菜单、数据权限、多租户、字典、参数配置、文件、通知、审批流、缓存、RabbitMQ 可靠消息、Hangfire 任务、审计日志、登录日志、健康检查等基础能力。

本路线图只规划平台通用能力，不生成 WMS / ERP 具体业务代码，不修改现有功能，不要求大规模重构。后续实现时应继续遵循当前分层架构：

- `PermissionSystem.Api`：Controller、认证授权、中间件、Swagger、依赖注入。
- `PermissionSystem.Application`：应用服务、DTO、请求响应、用例编排。
- `PermissionSystem.Domain`：实体、值对象、领域规则、领域事件。
- `PermissionSystem.Infrastructure`：EF Core、缓存、消息、任务、仓储、外部服务。
- `PermissionSystem.Shared`：通用结果、常量、异常、工具模型。

## 总体原则

- 所有数据库实体继承 `BaseEntity`，保留 `TenantId`、审计字段和软删除能力。
- 所有接口返回 `ApiResult` 或 `PagedResult`，不直接暴露 Entity。
- 所有后台接口使用 `async/await` 和 `CancellationToken`。
- 所有功能接入 `PermissionAttribute`、菜单权限、数据权限和多租户隔离。
- 配置类能力优先复用 `DictionaryType` / `DictionaryItem` 与 `SystemConfig`。
- 异步任务优先复用 Hangfire，跨边界事件优先复用 Outbox / Inbox 与 RabbitMQ。
- 高频读取配置可接入 `ICacheService`，修改后按租户和功能维度失效缓存。
- 敏感配置必须加密存储，不在日志中输出密钥、Token、连接串、证书内容。

## 1. 编号规则引擎

当前实现状态：已落地通用编号规则底座，包含 `NumberRule`、`NumberRuleSegment`、`NumberSequence` 三类核心实体，后端生成服务、管理接口、权限码、系统菜单和前端管理页面。编号规则引擎仍保持平台通用能力，不绑定 WMS / ERP 具体业务模块。

### 目标

为平台内各类业务对象提供统一、可配置、可审计、并发安全的编号生成能力。编号规则引擎只提供通用编号能力，不绑定具体业务模块。

典型能力：

- 支持按业务类型、租户、日期、组织、用户、流水号生成编号。
- 支持前缀、日期格式、固定文本、变量占位符、流水段、校验位等片段组合。
- 支持日、月、年、永久等流水重置周期。
- 支持预览、模拟生成、启停、版本化和并发锁控制。
- 支持编号占用、回滚策略和生成日志追踪。

### 核心实体

- `NumberRule`
  - 编号规则主表。
  - 当前字段：`RuleCode`、`RuleName`、`BusinessType`、`Prefix`、`DateFormat`、`SequenceLength`、`ResetCycle`、`Separator`、`IsEnabled`、`Remark`。
- `NumberRuleSegment`
  - 规则片段表，当前由简化规则自动同步默认片段。
  - 当前字段：`RuleId`、`SegmentType`、`SegmentValue`、`Sort`。
- `NumberSequence`
  - 流水状态表。
  - 当前字段：`RuleCode`、`SequenceKey`、`CurrentValue`、`LastGeneratedAt`。
- `NumberGenerationLog`
  - 编号生成日志，暂未落地，后续可按审计要求补充。
  - 字段建议：`RuleId`、`RuleCode`、`BusinessType`、`GeneratedNo`、`SequenceKey`、`BusinessId`、`Source`、`Succeeded`、`ErrorMessage`、`GeneratedAt`。
- `NumberReservation`
  - 可选，编号预占表，暂未落地。
  - 字段建议：`RuleId`、`GeneratedNo`、`BusinessId`、`Status`、`ExpiresAt`、`ConfirmedAt`。

### 后端接口

- `GET /api/system/number-rules`
  - 分页查询编号规则。
- `GET /api/system/number-rules/{id}`
  - 查询规则详情。
- `POST /api/system/number-rules`
  - 创建编号规则。
- `PUT /api/system/number-rules/{id}`
  - 更新编号规则。
- `DELETE /api/system/number-rules/{id}`
  - 删除编号规则。
- `POST /api/system/number-rules/{id}/enable`
  - 启用规则。
- `POST /api/system/number-rules/{id}/disable`
  - 禁用规则。
- `POST /api/system/number-rules/preview`
  - 按输入上下文预览编号。
- `POST /api/system/number-rules/{ruleCode}/generate`
  - 手动生成一个测试编号。
- `POST /api/system/number-rules/{ruleCode}/reset-sequence`
  - 重置流水号。
- `POST /api/number-rules/reserve`
  - 可选，预占编号，暂未落地。
- `POST /api/number-rules/confirm-reservation`
  - 可选，确认预占编号，暂未落地。
- `GET /api/number-generation-logs`
  - 查询编号生成日志，暂未落地。

建议 Application 服务：

- `NumberRuleService`
- `NumberGenerator`
- `NumberSequenceService`

关键实现要求：

- 使用分布式锁保护同一 `TenantId + RuleCode + SequenceKey` 的并发生成。
- 数据库对 `TenantId + RuleCode + SequenceKey` 建立唯一索引，作为并发重复的兜底约束。
- 规则读取可缓存，更新、启停后按租户和规则编码失效。
- 生成失败必须返回明确错误，不允许静默降级为随机编号。

当前支持的 `ResetCycle`：

- `None`：不按日期重置。
- `Daily`：按日重置流水。
- `Monthly`：按月重置流水。
- `Yearly`：按年重置流水。

当前支持的 `SegmentType`：

- `FixedText`
- `Date`
- `Sequence`
- `TenantCode`
- `DepartmentCode`
- `Custom`

当前简化配置示例：

- `PurchaseOrder`：`Prefix = PO`，`DateFormat = yyyyMMdd`，`SequenceLength = 4`，生成 `PO202605260001`。
- `InboundOrder`：`Prefix = IN`，`DateFormat = yyyyMMdd`，`SequenceLength = 4`，生成 `IN202605260001`。
- `DemoApprovalOrder`：`Prefix = DAO`，`DateFormat = yyyyMMdd`，`SequenceLength = 4`，生成 `DAO202605260001`。

### 前端页面

- `src/views/system/number-rule/index.vue`
  - 搜索：规则编码、名称、业务类型、启用状态。
  - 表格：规则编码、规则名称、业务类型、表达式、重置周期、启用状态、更新时间。
  - 操作：新增、编辑、启用、禁用、预览、查看日志。
- `src/views/platform/number-rule/components/RuleForm.vue`
  - 片段化配置：固定文本、日期、变量、流水号。
  - 支持排序、预览、校验。
- `src/views/platform/number-rule/log.vue`
  - 编号生成日志查询。

### 与现有能力关系

- 审批流：审批单、流程实例、任务等平台对象可使用编号规则生成通用单号；审批流本身不依赖具体业务编号。
- 权限：新增编号规则菜单和按钮权限，如 `system:number-rule:view`、`system:number-rule:create`、`system:number-rule:update`、`system:number-rule:generate`。
- 字典：编号片段类型、重置周期、预占状态可由字典维护。
- 参数配置：默认日期格式、最大流水长度、预占过期时间可放入参数配置。
- 通知：规则异常、流水耗尽、并发冲突持续失败时可通知平台管理员。
- Hangfire：定期清理过期预占编号、归档历史生成日志、检测流水接近上限。

### 验收标准

- 能创建、编辑、启停编号规则。
- 能基于上下文预览编号，并展示最终解析过程。
- 并发调用同一规则生成编号不重复、不跳回。
- 编号生成日志可分页查询。当前版本暂未落地独立日志表，可先通过操作日志追踪管理操作，后续补充生成日志。
- 禁用规则后生成接口返回业务错误。
- 多租户之间规则和流水互不影响。

### 风险和规避

- 风险：高并发下编号重复。
  - 规避：数据库唯一索引 + 分布式锁 + 事务内更新流水。
- 风险：规则表达式过于灵活导致不可维护。
  - 规避：采用受控片段配置，不开放任意脚本执行。
- 风险：预占编号导致大量空洞。
  - 规避：预占设置过期时间，并在业务侧明确是否允许空洞。

## 2. 业务状态机

当前实现状态：已落地通用业务状态机底座，包含 `StateMachineDefinition`、`StateDefinition`、`StateTransition`、`StateTransitionLog` 四类核心实体，后端配置管理接口、运行时流转执行器、业务回调 Handler、权限码、系统菜单和前端列表/设计器页面。状态机只负责通用状态流转，不承载 WMS / ERP 具体业务逻辑。

### 目标

为平台对象提供统一状态流转建模能力，解决“草稿、提交、审批中、已完成、作废、关闭”等通用状态管理问题。状态机负责状态定义、动作、条件、权限、事件和审计，不承载具体业务规则。

典型能力：

- 支持状态定义、动作定义、状态迁移规则和版本管理。
- 支持动作权限控制、前置条件、后置事件、操作确认。
- 支持与审批流联动：提交后进入审批中，审批通过后进入目标状态，审批拒绝后回退。
- 支持状态变更日志和状态图查看。

### 核心实体

- `StateMachineDefinition`
  - 状态机定义。
  - 当前字段：`BusinessType`、`Name`、`Description`、`IsEnabled`。
- `StateDefinition`
  - 状态定义。
  - 当前字段：`MachineId`、`StateCode`、`StateName`、`StateType`、`Color`、`Sort`、`IsInitial`、`IsFinal`。
- `StateTransition`
  - 状态迁移规则。
  - 当前字段：`MachineId`、`FromState`、`ToState`、`ActionCode`、`ActionName`、`RequiredPermission`、`ConditionJson`、`IsEnabled`、`Sort`。
- `StateTransitionLog`
  - 状态变更日志。
  - 当前字段：`BusinessType`、`BusinessId`、`FromState`、`ToState`、`ActionCode`、`ActionName`、`OperatorUserId`、`OperatorUserName`、`Comment`、`CreatedAt`。

### 后端接口

- `GET /api/system/state-machines`
  - 分页查询状态机。
- `POST /api/system/state-machines`
  - 创建状态机。
- `PUT /api/system/state-machines/{id}`
  - 更新状态机。
- `DELETE /api/system/state-machines/{id}`
  - 删除状态机。
- `GET /api/system/state-machines/{id}/states`
  - 查询状态定义。
- `POST /api/system/state-machines/{id}/states`
  - 创建状态定义。
- `PUT /api/system/state-machines/{id}/states/{stateId}`
  - 更新状态定义。
- `DELETE /api/system/state-machines/{id}/states/{stateId}`
  - 删除状态定义。
- `GET /api/system/state-machines/{id}/transitions`
  - 查询状态流转。
- `POST /api/system/state-machines/{id}/transitions`
  - 创建状态流转。
- `PUT /api/system/state-machines/{id}/transitions/{transitionId}`
  - 更新状态流转。
- `DELETE /api/system/state-machines/{id}/transitions/{transitionId}`
  - 删除状态流转。
- `POST /api/system/state-machines/transition`
  - 执行状态迁移。
- `GET /api/system/state-machines/logs`
  - 查询状态变更日志。

建议 Application 服务：

- `StateMachineDefinitionService`
- `StateMachineService`
- `StateTransitionExecutor`
- `IStateTransitionHandler`
- `IStateTransitionHandlerResolver`

当前 Demo 示例：

- `DemoApprovalOrder` 状态：`Draft`、`Pending`、`Approved`、`Rejected`、`Withdrawn`、`Cancelled`。
- `DemoApprovalOrder` 动作：`Submit`、`Approve`、`Reject`、`Withdraw`、`Cancel`。
- 审批流发起、审批通过、审批拒绝、撤回时通过 `DemoApprovalOrderStateTransitionHandler` 执行状态流转并记录日志。

### 前端页面

- `src/views/system/state-machine/index.vue`
  - 状态机列表、启停、发布、版本查看。
- `src/views/system/state-machine/designer.vue`
  - 状态节点、动作边、条件、权限、审批联动配置。
- 流转日志已集成在 `src/views/system/state-machine/index.vue` 的“流转日志”页签。
  - 状态变更日志。
- 通用组件：
  - `StateTag`
  - `StateActionButtons`
  - `StateTimeline`

### 与现有能力关系

- 审批流：状态迁移可声明 `RequiresApproval`，由审批流完成后回调状态机；状态机不直接实现审批节点。
- 权限：迁移动作绑定权限码，前端按钮和后端接口双重校验。
- 字典：状态类型、动作类型、终态类型、颜色标识可接入字典。
- 参数配置：是否允许终态回退、默认日志保留天数、是否强制填写原因可通过参数控制。
- 通知：状态变更、审批触发、异常回退可发送通知。
- Hangfire：处理超时状态自动关闭、状态一致性巡检、历史日志归档。

### 验收标准

- 能配置状态、动作和迁移规则。
- 能查询某业务对象当前可执行动作。
- 无权限用户无法执行受控迁移动作。
- 审批通过或拒绝后能按配置触发状态迁移。
- 状态变更日志完整记录操作人、前后状态、原因和 TraceId。

### 风险和规避

- 风险：状态机和审批流边界混乱。
  - 规避：状态机只负责状态变更，审批流只负责审批过程，通过 Application 服务编排。
- 风险：表达式条件不可控。
  - 规避：初期只支持字段比较、字典枚举、用户上下文等白名单条件。
- 风险：历史版本变更影响运行中实例。
  - 规避：发布后生成不可变版本，运行时绑定版本号。

## 3. 表单设计器

### 目标

提供低代码动态表单定义能力，用于配置平台通用表单结构、字段、校验、布局和权限。表单设计器只提供表单定义与数据承载能力，不内置具体业务流程。

典型能力：

- 支持文本、数字、日期、下拉、单选、多选、用户选择、部门选择、文件上传、明细表格等控件。
- 支持字段校验、默认值、显隐条件、只读条件、字典绑定。
- 支持表单版本、发布、预览、复制。
- 支持与审批流、状态机、打印模板、报表中心复用表单元数据。

### 核心实体

- `FormDefinition`
  - 表单定义。
  - 字段建议：`Code`、`Name`、`BusinessType`、`Description`、`Version`、`SchemaJson`、`IsPublished`、`IsEnabled`。
- `FormFieldDefinition`
  - 字段定义，可选拆表。
  - 字段建议：`FormId`、`FieldCode`、`FieldName`、`FieldType`、`DataType`、`Required`、`DictionaryTypeCode`、`SortOrder`、`ConfigJson`。
- `FormLayoutDefinition`
  - 布局定义，可选。
  - 字段建议：`FormId`、`LayoutJson`。
- `FormData`
  - 动态表单数据主表。
  - 字段建议：`FormCode`、`FormVersion`、`BusinessType`、`BusinessId`、`DataJson`、`Status`。
- `FormDataAttachment`
  - 动态表单附件关联。
  - 字段建议：`FormDataId`、`FieldCode`、`FileResourceId`。

### 后端接口

- `GET /api/forms`
  - 分页查询表单定义。
- `GET /api/forms/{id}`
  - 查询表单定义详情。
- `POST /api/forms`
  - 创建表单定义。
- `PUT /api/forms/{id}`
  - 更新表单定义。
- `POST /api/forms/{id}/publish`
  - 发布表单版本。
- `POST /api/forms/{id}/copy`
  - 复制表单。
- `POST /api/forms/{id}/preview`
  - 表单预览数据校验。
- `GET /api/forms/{code}/schema`
  - 按编码获取已发布表单 Schema。
- `POST /api/form-data`
  - 保存动态表单数据。
- `GET /api/form-data/{id}`
  - 查询动态表单数据。
- `PUT /api/form-data/{id}`
  - 更新动态表单数据。
- `POST /api/form-data/{id}/validate`
  - 校验表单数据。

建议 Application 服务：

- `FormDefinitionService`
- `FormSchemaValidator`
- `FormDataService`

### 前端页面

- `src/views/platform/form/index.vue`
  - 表单列表、发布、复制、预览。
- `src/views/platform/form/designer.vue`
  - 拖拽控件区、画布区、属性配置区。
- `src/views/platform/form/preview.vue`
  - 运行时预览。
- 通用组件：
  - `DynamicFormRenderer`
  - `FormFieldPropertyPanel`
  - `FormSchemaPreview`

### 与现有能力关系

- 审批流：流程发起节点可绑定表单定义；审批节点可配置字段只读、可编辑、隐藏。
- 权限：表单设计、发布、数据查看、字段编辑可绑定权限码；字段级权限可结合现有角色权限矩阵扩展。
- 字典：下拉、单选、多选控件优先绑定字典项。
- 参数配置：文件大小、附件数量、默认布局列数、字段数量上限可放入参数配置。
- 通知：表单提交、退回补充材料、字段异常校验失败可触发通知。
- Hangfire：清理草稿、归档历史表单数据、定期校验孤立附件。

### 验收标准

- 能创建表单并配置基础字段、校验和布局。
- 能发布表单版本，已发布版本不可被直接覆盖。
- 能通过运行时组件渲染表单并提交数据。
- 字典字段能正确加载启用状态的字典项。
- 表单数据按租户隔离，附件复用现有文件能力。

### 风险和规避

- 风险：动态表单 JSON 过度复杂，后续难以升级。
  - 规避：定义稳定 Schema 版本，保留迁移器接口。
- 风险：字段权限和审批节点权限冲突。
  - 规避：按“后端最终裁决、最小权限优先”合并权限。
- 风险：任意脚本表达式带来安全问题。
  - 规避：不执行用户脚本，使用白名单表达式 DSL。

## 4. 代码生成器

### 目标

提供面向平台内部开发的代码生成能力，根据受控元数据生成基础 CRUD、DTO、Controller、Application Service、前端 API 和标准列表页，提高一致性和开发效率。

代码生成器只生成通用平台脚手架，不生成 WMS / ERP 具体业务逻辑，不覆盖用户已有改动。

### 核心实体

- `CodeGenerationProject`
  - 生成项目配置。
  - 字段建议：`Code`、`Name`、`ModuleName`、`EntityName`、`TableName`、`Description`。
- `CodeGenerationEntity`
  - 实体元数据。
  - 字段建议：`ProjectId`、`EntityName`、`DisplayName`、`Namespace`、`InheritsBaseEntity`、`IsTenantScoped`。
- `CodeGenerationField`
  - 字段元数据。
  - 字段建议：`EntityId`、`FieldName`、`DisplayName`、`DataType`、`MaxLength`、`Required`、`IsSearchable`、`IsTableColumn`、`DictionaryTypeCode`。
- `CodeGenerationTemplate`
  - 模板定义。
  - 字段建议：`Code`、`Name`、`TemplateType`、`Content`、`IsBuiltin`、`IsEnabled`。
- `CodeGenerationTask`
  - 生成任务。
  - 字段建议：`ProjectId`、`Status`、`TargetPath`、`OverwritePolicy`、`PreviewResult`、`ErrorMessage`。
- `CodeGenerationFile`
  - 生成文件记录。
  - 字段建议：`TaskId`、`FilePath`、`FileType`、`ContentHash`、`ActionType`。

### 后端接口

- `GET /api/codegen/projects`
  - 分页查询生成项目。
- `GET /api/codegen/projects/{id}`
  - 查询项目详情。
- `POST /api/codegen/projects`
  - 创建生成项目。
- `PUT /api/codegen/projects/{id}`
  - 更新生成项目。
- `POST /api/codegen/projects/{id}/preview`
  - 预览生成结果。
- `POST /api/codegen/projects/{id}/generate`
  - 创建生成任务。
- `GET /api/codegen/tasks`
  - 查询生成任务。
- `GET /api/codegen/tasks/{id}/files`
  - 查看生成文件列表。
- `GET /api/codegen/templates`
  - 查询模板。
- `PUT /api/codegen/templates/{id}`
  - 更新非内置模板。

建议 Application 服务：

- `CodeGenerationProjectService`
- `CodeGenerationTemplateService`
- `CodeGenerationTaskService`

关键实现要求：

- 默认只预览，不直接写入工作区。
- 真正落盘需要明确选择目标路径和覆盖策略。
- 覆盖策略默认 `SkipIfExists`，不得静默覆盖已有文件。
- 生成任务记录文件 Hash，便于审计和回溯。

### 前端页面

- `src/views/platform/codegen/project/index.vue`
  - 生成项目列表。
- `src/views/platform/codegen/project/edit.vue`
  - 实体、字段、查询条件、表格列、表单项配置。
- `src/views/platform/codegen/preview.vue`
  - 文件树预览、Diff 预览。
- `src/views/platform/codegen/template/index.vue`
  - 模板管理。
- `src/views/platform/codegen/task/index.vue`
  - 生成任务记录。

### 与现有能力关系

- 审批流：可为生成模块预留审批接入点，但不生成具体审批业务逻辑。
- 权限：为生成的菜单和按钮生成权限码建议，由管理员确认后写入权限体系。
- 字典：字段可绑定字典类型，生成前端下拉和后端字典校验。
- 参数配置：默认命名空间、默认分页大小、默认覆盖策略可配置。
- 通知：生成任务完成、失败、存在冲突时通知发起人。
- Hangfire：大批量生成、模板预编译、历史任务清理可异步执行。

### 验收标准

- 能配置实体和字段元数据。
- 能预览后端和前端生成文件。
- 默认不会覆盖已有文件。
- 生成任务有状态、有日志、有文件清单。
- 生成代码符合当前项目分层、命名、返回值和权限规范。

### 风险和规避

- 风险：误覆盖已有代码。
  - 规避：默认预览和跳过已有文件，落盘前展示 Diff。
- 风险：模板失控导致生成代码不符合架构。
  - 规避：内置模板只允许管理员维护，模板变更记录审计。
- 风险：业务人员误用生成器生成不适合的模块。
  - 规避：代码生成器仅对开发/平台管理员开放。

## 5. 打印模板设计

### 目标

提供统一打印模板设计和渲染能力，支持平台单据、审批记录、动态表单数据的打印、导出和归档。打印模板只处理展示模板，不承载业务计算。

典型能力：

- 支持模板设计、版本发布、预览、复制。
- 支持绑定数据源字段、动态表单字段、审批轨迹、二维码、条码、图片、附件。
- 支持 HTML/PDF 渲染，后续可扩展套打。
- 支持打印日志和导出日志。

当前实现状态：

- 已落地通用打印模板底座：`PrintTemplate`、`PrintRecord`。
- 已支持 HTML 模板、基础变量替换、`items` 明细循环、模板预览、渲染记录。
- 已接入系统权限、系统菜单和前端基础设计器。
- 暂不引入具体 WMS / ERP 业务模块，PDF 渲染、模板发布版本、复杂数据源绑定保留为后续增强。

### 核心实体

- `PrintTemplate`
  - 打印模板主表。
  - 已实现字段：`TemplateCode`、`TemplateName`、`BusinessType`、`TemplateType`、`ContentHtml`、`ContentJson`、`PaperSize`、`Orientation`、`IsDefault`、`IsEnabled`、`Version`、`Remark`。
- `PrintRecord`
  - 打印日志。
  - 已实现字段：`TemplateId`、`BusinessType`、`BusinessId`、`PrintUserId`、`PrintUserName`、`PrintedAt`、`PrintCount`。
- `PrintTemplateDataSource`
  - 模板数据源定义，后续增强。
- `PrintRenderTask`
  - 异步渲染任务，后续增强。

### 后端接口

- `GET /api/system/print-templates`
  - 分页查询打印模板。
- `GET /api/system/print-templates/{id}`
  - 查询模板详情。
- `POST /api/system/print-templates`
  - 创建模板。
- `PUT /api/system/print-templates/{id}`
  - 更新模板。
- `DELETE /api/system/print-templates/{id}`
  - 删除模板。
- `GET /api/system/print-templates/by-business-type/{businessType}`
  - 按业务类型查询启用模板。
- `POST /api/system/print-templates/{id}/set-default`
  - 设置默认模板。
- `POST /api/system/print-templates/{id}/preview`
  - 预览模板。
- `POST /api/system/print-templates/{id}/render`
  - 渲染模板并记录打印日志。
- `GET /api/system/print-records`
  - 查询打印日志。

建议 Application 服务：

- `PrintTemplateService`
- `PrintTemplateRenderer`
- `PrintRenderService`（后续增强）
- `PrintDataSourceResolver`（后续增强）

### 前端页面

- `src/views/system/print-template/index.vue`
  - 模板列表、新增、编辑、删除、设置默认、预览、测试渲染、打印记录。
- `src/views/system/print-template/designer.vue`
  - 左侧变量列表、中间 HTML textarea、右侧模板属性、iframe 预览。

### 与现有能力关系

- 审批流：可打印审批单、审批轨迹、任务处理记录、抄送记录。
- 权限：已接入 `system:print-template:*`、`system:print-record:view` 权限码。
- 字典：打印模板类型、纸张类型、渲染模式可使用字典。
- 参数配置：默认纸张、PDF 渲染超时时间、导出文件保留天数可配置。
- 通知：异步渲染完成后通知发起人下载。
- Hangfire：PDF 渲染、批量导出、历史文件清理使用后台任务。

### 验收标准

- 能创建模板并维护模板 HTML、纸张、方向、默认模板和启用状态。
- 能使用测试数据预览模板。
- 能按业务对象渲染 HTML。
- 渲染行为有 `PrintRecord` 日志。
- PDF 渲染、导出日志和模板发布不可变版本作为后续验收项。

### 风险和规避

- 风险：HTML 到 PDF 渲染依赖外部组件，部署复杂。
  - 规避：先支持 HTML 预览，PDF 渲染作为可配置基础设施能力。
- 风险：模板中注入危险脚本。
  - 规避：清洗 HTML，禁止执行用户脚本，字段输出默认转义。
- 风险：大批量导出占用资源。
  - 规避：走 Hangfire 队列，限制并发和单次导出数量。

## 6. 报表中心

### 目标

提供平台级报表定义、查询、图表展示、导出和订阅能力。报表中心应面向通用统计和运营分析，不直接内置具体业务报表。

典型能力：

- 支持报表分类、数据集定义、查询参数、表格列、图表配置。
- 支持 SQL 受控查询或服务数据源查询。
- 支持权限控制、数据权限、多租户隔离。
- 支持导出、订阅、定时推送和缓存。

当前实现状态：

- 已落地通用报表中心基础能力：`ReportDefinition`、`ReportQueryParam`、`ReportExecutionLog`。
- 已支持 SQL 数据源查询、参数化执行、Excel 导出、执行日志。
- 已接入系统菜单、权限码和前端基础管理/查看页面。
- 已内置系统示例报表：用户列表、登录日志、操作日志。
- `ApiUrl` 字段作为服务数据源预留，图表、订阅、异步导出和缓存作为后续增强。

### 核心实体

- `ReportDefinition`
  - 报表定义。
  - 已实现字段：`ReportCode`、`ReportName`、`Category`、`DataSourceType`、`SqlText`、`ApiUrl`、`ColumnsJson`、`ParamsJson`、`IsEnabled`、`Remark`。
- `ReportQueryParam`
  - 查询参数定义。
  - 已实现字段：`ReportId`、`ParamCode`、`ParamName`、`ParamType`、`DefaultValue`、`Required`、`Sort`。
- `ReportExecutionLog`
  - 报表执行日志。
  - 已实现字段：`ReportId`、`ReportCode`、`ExecuteUserId`、`ExecuteUserName`、`ParamsJson`、`ElapsedMilliseconds`、`RowCount`、`CreatedAt`。
- `ReportCategory`
  - 报表分类，当前使用 `ReportDefinition.Category` 字段，独立分类表后续增强。
- `ReportSubscription`
  - 报表订阅。
  - 后续增强：`ReportId`、`SubscriberUserId`、`CronExpression`、`ExportFormat`、`NotifyChannel`、`IsEnabled`。

### 后端接口

- `GET /api/reports`
  - 分页查询报表定义。
- `GET /api/reports/{id}`
  - 查询报表定义详情。
- `POST /api/reports`
  - 创建报表。
- `PUT /api/reports/{id}`
  - 更新报表。
- `DELETE /api/reports/{id}`
  - 删除报表。
- `POST /api/reports/{id}/query`
  - 执行报表查询。
- `POST /api/reports/{id}/export`
  - 导出报表。
- `GET /api/reports/execution-logs`
  - 查询执行日志。

建议 Application 服务：

- `ReportDefinitionService`
- `ReportQueryService`
- `ReportExportService`
- `ReportSubscriptionService`（后续增强）

### 前端页面

- `src/views/report/definition/index.vue`
  - 报表定义管理、查询参数配置、执行日志。
- `src/views/report/viewer/index.vue`
  - 报表选择、参数输入、表格查询、Excel 导出。

### 与现有能力关系

- 审批流：可基于审批定义、实例、任务、处理耗时提供通用流程统计。
- 权限：已接入 `report:definition:*`、`report:view`、`report:export`、`report:log:view`。
- 字典：报表分类、参数选项、图表类型、导出格式可接入字典。
- 参数配置：最大查询时间、最大导出行数、缓存 TTL、订阅频率上限可配置。
- 通知：订阅报表生成后推送通知。
- Hangfire：定时报表订阅、异步导出、缓存预热、执行日志归档。

### 验收标准

- 能配置报表定义、查询参数和展示列。
- 能执行分页查询并导出。
- 无权限用户无法查看或导出报表。
- 报表查询受多租户和数据权限约束。
- 订阅报表能按计划生成并通知用户。

### 风险和规避

- 风险：开放 SQL 带来注入和越权风险。
  - 规避：优先服务数据源；如支持 SQL，必须只读、参数化、白名单数据源、强制租户条件。
- 风险：报表查询影响主库性能。
  - 规避：限制超时和行数，支持缓存、异步导出，后续可接只读库。
- 风险：导出敏感数据。
  - 规避：导出权限独立控制，字段脱敏，记录执行日志。

## 7. 安全策略中心

当前实现状态：已落地安全策略中心基础能力，包含 `SecurityPolicy`、`LoginFailureRecord`、`SensitiveOperationVerification`、`IpAccessRule` 四类核心实体，后端策略服务、登录失败锁定、IP 黑白名单中间件、敏感操作验证码接口、权限码、系统菜单和前端管理页面。当前版本聚焦平台通用安全底座，不替换 OpenIddict，也不生成具体业务模块。

### 目标

统一管理平台安全策略，包括密码策略、登录策略、会话策略、Token 策略、IP 访问策略、MFA 预留、API 限流策略和敏感操作策略。该中心不绕过 OpenIddict，不实现自定义 JWT 服务。

典型能力：

- 支持密码复杂度、过期周期、失败锁定、历史密码限制。
- 支持登录 IP 白名单/黑名单、异常登录提醒。
- 支持会话并发限制、强制下线、空闲超时。
- 支持敏感操作二次确认或二次认证预留。
- 支持 API 限流策略可视化配置。

### 核心实体

- `SecurityPolicy`
  - 安全策略主表。
  - 当前字段：`PasswordMinLength`、`RequireDigit`、`RequireUppercase`、`RequireLowercase`、`RequireSpecialChar`、`PasswordExpireDays`、`LoginFailureLockThreshold`、`LoginFailureLockMinutes`、`EnableMfa`、`EnableSensitiveOperationVerify`、`EnableIpWhitelist`、`EnableIpBlacklist`。
- `LoginFailureRecord`
  - 登录失败记录和锁定状态。
  - 当前字段：`UserName`、`IpAddress`、`FailureCount`、`LockedUntil`、`LastFailureAt`。
- `SensitiveOperationVerification`
  - 敏感操作验证码。
  - 当前字段：`UserId`、`OperationCode`、`VerifyCode`、`ExpiresAt`、`UsedAt`。
- `IpAccessRule`
  - IP 访问规则。
  - 当前字段：`RuleType`、`IpPattern`、`Description`、`IsEnabled`。
- `SensitiveOperationPolicy`
  - 敏感操作策略。
  - 当前未独立建表，先由 `SecurityPolicy.EnableSensitiveOperationVerify` 控制全局二次验证，业务入口通过 `OperationCode` 接入。
- `SecurityEventLog`
  - 安全事件日志。
  - 暂未落地独立表，当前优先复用登录日志和操作日志；后续可补充字段：`EventType`、`UserId`、`UserName`、`IpAddress`、`UserAgent`、`RiskLevel`、`ActionTaken`、`TraceId`、`OccurredAt`。

### 后端接口

- `GET /api/security/policy`
  - 查询当前租户安全策略。
- `PUT /api/security/policy`
  - 更新当前租户安全策略，启用二次验证后需要验证码。
- `POST /api/security/verification/send`
  - 创建绑定当前用户、租户、操作码和登录会话的 Step-up 挑战。
- `POST /api/security/verification/verify`
  - 使用当前密码完成 Step-up 验证并返回短期一次性 Ticket；数据库仅保存 Ticket 哈希。
- `GET /api/security/ip-rules`
  - 分页查询 IP 黑白名单。
- `POST /api/security/ip-rules`
  - 新增 IP 规则。
- `PUT /api/security/ip-rules/{id}`
  - 编辑 IP 规则。
- `DELETE /api/security/ip-rules/{id}`
  - 删除 IP 规则。
- `GET /api/security/login-failures`
  - 查询登录失败和锁定记录。

建议 Application 服务：

- `SecurityPolicyService`
- `IpAccessMiddleware`
- `ISensitiveOperationCodeProvider`

### 前端页面

- `src/views/security/policy/index.vue`
  - 密码策略、登录失败锁定、二次验证、IP 黑白名单开关。
- `src/views/security/ip-rule/index.vue`
  - IP 访问规则。
- `src/views/security/login-failure/index.vue`
  - 登录失败和锁定记录。
- `src/components/SensitiveVerificationDialog/index.vue`
  - 敏感操作二次验证弹窗。

### 与现有能力关系

- 审批流：可对流程发布、审批转交、管理员代办等敏感操作配置二次确认；高风险策略变更可要求审批。
- 权限：安全策略中心只开放给安全管理员或系统管理员，策略启停、编辑、事件处理分离权限。
- 字典：策略类型、风险等级、处置动作、事件类型可使用字典。
- 参数配置：全局默认安全阈值可保留在参数配置，策略中心提供更细粒度覆盖。
- 通知：异常登录、账号锁定、策略变更、安全事件升级通知管理员或用户。
- Hangfire：定期检测长期未改密账号、清理过期会话、汇总安全事件、生成安全日报。

### 验收标准

- 能配置密码复杂度、登录失败锁定阈值、锁定分钟数、敏感操作验证开关、IP 黑白名单开关。
- 创建用户、修改密码、重置密码时按密码策略校验。
- 登录失败会累计记录，达到阈值后锁定，登录成功后清理失败记录。
- 请求进入后按 IP 黑白名单中间件拦截。
- 删除用户、重置密码、分配 SuperAdmin、修改 SuperAdmin 权限、修改安全策略支持 Step-up Ticket。
- 登录失败记录可查询。
- 策略变更记录审计日志。
- MFA、密码过期强制改密、独立安全事件表和策略缓存为后续增强项。

### 风险和规避

- 风险：策略配置错误导致管理员无法登录。
  - 规避：保留安全兜底账号策略、配置前测试、关键策略变更二次确认。
- 风险：与 OpenIddict 行为冲突。
  - 规避：只在认证前后做策略校验和事件记录，不替换 OpenIddict token 机制。
- 风险：策略过多导致判定复杂。
  - 规避：明确优先级、作用域和冲突解决规则，并提供生效策略预览。

## 8. 开放集成中心

当前实现状态：已落地开放集成中心基础能力，包含 `ApiClient`、`ApiClientSecret`、`WebhookSubscription`、`WebhookDeliveryLog`、`ExternalApiCallLog` 五类核心实体，后端管理接口、API Key 中间件、Webhook HMAC 签名投递、Hangfire 失败重试、权限码、系统菜单和前端管理页面。当前版本提供通用集成底座，不开放具体 WMS / ERP 业务接口。

### 目标

提供面向外部系统的统一集成能力，包括应用接入、API 凭证、Webhook、事件订阅、回调日志、签名验签、限流和集成审计。开放集成中心应复用 OpenIddict 和现有消息基础设施，不自行实现不受控认证体系。

典型能力：

- 支持外部应用登记、密钥管理、授权范围、启停。
- 支持 Webhook 订阅平台事件。
- 支持事件投递重试、签名、幂等、死信记录。
- 支持 API 调用审计、限流、IP 白名单。
- 支持集成调试和投递日志追踪。

### 核心实体

- `ApiClient`
  - API 客户端。
  - 当前字段：`ClientCode`、`ClientName`、`Description`、`IsEnabled`、`AllowedScopes`、`AllowedIpList`、`RateLimitPerMinute`。
- `ApiClientSecret`
  - API 客户端密钥。
  - 当前字段：`ClientId`、`SecretHash`、`ExpiresAt`、`LastUsedAt`。Secret 只在生成时返回一次，数据库只保存哈希。
- `WebhookSubscription`
  - Webhook 订阅。
  - 当前字段：`EventType`、`TargetUrl`、`Secret`、`IsEnabled`、`RetryCount`。Secret 使用现有配置加密器保护，接口脱敏显示。
- `WebhookDeliveryLog`
  - Webhook 投递日志。
  - 当前字段：`SubscriptionId`、`EventType`、`Payload`、`Status`、`ResponseStatusCode`、`ResponseBody`、`RetryCount`、`CreatedAt`。
- `ExternalApiCallLog`
  - 集成 API 调用日志。
  - 当前字段：`ClientId`、`Path`、`Method`、`IpAddress`、`StatusCode`、`ElapsedMilliseconds`、`CreatedAt`。
- `IntegrationEvent`
  - 集成事件定义。
  - 暂未独立建表，当前预留事件类型：`user.created`、`workflow.approved`、`workflow.rejected`、`notification.created`。

### 后端接口

- `GET /api/integration/clients`
  - 分页查询 API 客户端。
- `POST /api/integration/clients`
  - 创建 API 客户端。
- `PUT /api/integration/clients/{id}`
  - 更新 API 客户端。
- `DELETE /api/integration/clients/{id}`
  - 删除 API 客户端。
- `POST /api/integration/clients/{id}/generate-secret`
  - 生成 API Secret，只返回一次。
- `POST /api/integration/clients/{id}/enable`
  - 启用客户端。
- `POST /api/integration/clients/{id}/disable`
  - 禁用客户端。
- `GET /api/integration/webhooks`
  - 查询 Webhook 订阅。
- `POST /api/integration/webhooks`
  - 创建 Webhook 订阅。
- `PUT /api/integration/webhooks/{id}`
  - 更新 Webhook 订阅。
- `POST /api/integration/webhooks/{id}/test`
  - 测试 Webhook。
- `GET /api/integration/webhook-logs`
  - 查询投递日志。
- `GET /api/integration/api-call-logs`
  - 查询 API 调用日志。

建议 Application 服务：

- `OpenIntegrationService`
- `ApiKeyAuthenticationMiddleware`
- `WebhookDeliveryJob`
- `WebhookHttpSender`

### 前端页面

- `src/views/integration/client/index.vue`
  - API 客户端列表、启停、生成 Secret、Scope、IP 白名单、限流配置。
- `src/views/integration/webhook/index.vue`
  - Webhook 订阅管理。
- `src/views/integration/log/index.vue`
  - API 调用日志和 Webhook 投递日志。

### 与现有能力关系

- 审批流：流程发起、任务完成、流程完成、流程拒绝等可作为开放事件；外部系统可订阅但不能绕过审批权限。
- 权限：外部应用通过授权范围控制 API 能力，后台管理页面通过 RBAC 控制。
- 字典：事件分类、投递状态、应用类型、签名算法可使用字典。
- 参数配置：Webhook 超时时间、最大重试次数、签名算法、投递并发数可配置。
- 通知：Webhook 连续失败、密钥轮换、应用禁用通知应用负责人和管理员。
- Hangfire：Webhook 重试、死信扫描、调用日志归档、密钥过期提醒使用后台任务。

### 验收标准

- 能创建 API 客户端，配置 Scope、IP 白名单、每分钟限流并启停。
- 能生成密钥，Secret 只显示一次，数据库只保存哈希。
- 能配置 Webhook 订阅并测试回调。
- Webhook 使用 `X-Webhook-Signature` HMAC-SHA256 签名，投递失败按 `RetryCount` 通过 Hangfire 重试。
- API Key 调用记录包含客户端、路径、方法、IP、状态码和耗时。
- 禁用客户端后无法继续通过 API Key 调用。

### 风险和规避

- 风险：开放接口越权访问租户数据。
  - 规避：应用绑定租户和授权范围，所有开放接口继续走租户隔离和权限校验。
- 风险：Webhook 密钥泄露。
  - 规避：密钥加密存储，只在创建或轮换时展示一次，日志脱敏。
- 风险：外部回调不稳定拖慢主流程。
  - 规避：事件进入 Outbox 后异步投递，失败按策略重试，不阻塞主事务。

## 推荐实现顺序

### 第一阶段：元数据基础能力

优先实现：

1. 编号规则引擎
2. 业务状态机
3. 表单设计器基础版

原因：

- 编号、状态、表单是后续打印、报表、审批扩展和代码生成的底层元数据。
- 三者与现有审批流、权限、字典、参数配置的集成价值最高。
- 先完成稳定元模型，可以减少后续功能返工。

阶段验收标准：

- 编号规则支持配置、预览、并发生成和日志。
- 状态机支持定义、发布、动作权限、状态流转和日志。
- 表单设计器支持基础字段、字典绑定、发布、运行时渲染和数据保存。
- 三个模块均完成菜单、权限码、多租户隔离、审计日志。

### 第二阶段：输出与开发效率能力

优先实现：

1. 打印模板设计
2. 代码生成器

原因：

- 打印模板依赖表单元数据、审批轨迹和文件能力。
- 代码生成器依赖前一阶段形成的命名、权限、字典、页面规范。
- 这两个模块能提升平台交付效率，但不应早于元数据稳定。

阶段验收标准：

- 打印模板支持字段绑定、预览、HTML 渲染、打印日志。
- 异步 PDF 渲染具备任务状态和失败重试能力。
- 代码生成器支持元数据配置、文件预览、Diff 查看、生成任务记录。
- 代码生成默认不覆盖已有文件，生成内容符合当前分层架构。

### 第三阶段：运营分析与安全治理

优先实现：

1. 报表中心
2. 安全策略中心

原因：

- 报表中心需要前期积累的日志、审批、状态、表单数据作为分析基础。
- 安全策略中心影响认证、会话、限流、审计等核心路径，适合在平台能力稳定后谨慎接入。

阶段验收标准：

- 报表中心支持报表定义、参数查询、权限控制、导出和订阅。
- 报表查询受租户和数据权限限制，有执行日志。
- 安全策略中心支持密码、登录、会话、IP 和敏感操作策略。
- 安全事件可记录、查询、处置和通知。

### 第四阶段：生态开放能力

优先实现：

1. 开放集成中心

原因：

- 开放集成涉及认证、授权、限流、审计、消息、重试和安全策略，需要前面平台治理能力支撑。
- 最后实现可以最大化复用已有稳定能力，降低外部集成风险。

阶段验收标准：

- 能创建外部应用、分配授权范围、轮换密钥和禁用应用。
- Webhook 支持订阅、签名、测试、异步投递、失败重试和日志。
- 开放 API 调用有审计日志、限流和租户隔离。
- 连续失败和密钥过期可通知负责人。

## 跨模块共性设计建议

### 权限码建议

建议按模块统一命名：

- `system:number-rule:*`
- `system:state-machine:*`
- `platform:form:*`
- `platform:codegen:*`
- `system:print-template:*`
- `system:print-record:*`
- `report:*`
- `security:policy:*`
- `integration:*`

常用动作后缀：

- `list`
- `detail`
- `create`
- `update`
- `delete`
- `enable`
- `disable`
- `publish`
- `export`
- `log`

### 菜单建议

建议新增顶级或二级菜单：

- 平台扩展
  - 编号规则
  - 状态机
  - 表单设计
  - 打印模板
  - 报表中心
  - 代码生成
- 安全中心
  - 安全策略
  - 安全事件
- 开放集成
  - 接入应用
  - Webhook
  - 事件目录
  - 调用日志

### 缓存建议

适合缓存：

- 编号规则定义
- 状态机发布版本
- 表单发布 Schema
- 打印模板发布版本
- 报表定义
- 安全策略生效结果
- 集成应用授权范围

缓存 Key 建议包含租户：

- `ps:{tenantId}:number-rule:{code}`
- `ps:{tenantId}:state-machine:{code}:{version}`
- `ps:{tenantId}:form:{code}:{version}`
- `ps:{tenantId}:security-policy:effective:{scope}`

### 审计建议

必须记录审计的操作：

- 发布、启用、禁用、删除平台元数据。
- 修改安全策略、开放应用、密钥轮换、Webhook 配置。
- 执行代码生成落盘。
- 导出报表、打印敏感数据。
- 手动重试 Webhook 或后台任务。

### Hangfire 任务建议

建议统一任务命名和日志：

- `NumberReservationCleanupJob`
- `NumberSequenceHealthCheckJob`
- `StateTimeoutTransitionJob`
- `FormDraftCleanupJob`
- `PrintRenderJob`
- `ReportExportJob`
- `ReportSubscriptionJob`
- `SecurityEventSummaryJob`
- `WebhookDeliveryRetryJob`
- `IntegrationLogArchiveJob`

所有任务应记录执行日志，并在失败时关联 TraceId 或任务 Id。

## 总体风险和规避方案

### 元数据模型膨胀

风险：编号、状态、表单、打印、报表都依赖元数据，若边界不清晰会导致模型膨胀。

规避：

- 每个模块只维护自己的元数据，不跨模块直接写入。
- 跨模块通过编码、版本号、业务类型和 Application 服务编排。
- 发布后的版本不可变，草稿和发布版本分离。

### 表达式安全风险

风险：状态条件、表单显隐、报表参数、编号变量若支持任意表达式，可能引入安全漏洞。

规避：

- 初期仅支持白名单字段、白名单操作符和受控函数。
- 禁止执行用户输入脚本。
- 表达式解析和执行集中封装，并加入单元测试。

### 多租户隔离风险

风险：平台级配置容易被误认为全局共享，导致租户间数据串用。

规避：

- 默认所有实体继承 `BaseEntity` 并带 `TenantId`。
- 只有明确标记为系统内置的数据才允许跨租户共享。
- 缓存 Key、唯一索引、查询条件均包含 `TenantId`。

### 性能风险

风险：报表查询、打印渲染、批量导出、Webhook 重试可能占用大量资源。

规避：

- 大任务进入 Hangfire。
- 限制查询时间、导出行数、并发数和重试次数。
- 高频元数据使用缓存。
- 执行日志用于后续性能分析和容量规划。

### 权限和按钮可见性不一致

风险：前端隐藏按钮但后端未校验，或后端拒绝但前端仍显示。

规避：

- 所有关键操作后端必须使用权限校验。
- 前端按钮使用同一权限码控制可见性。
- 菜单、按钮、接口权限统一维护。

### 版本兼容风险

风险：已发布表单、状态机、打印模板、报表定义变更后影响历史数据。

规避：

- 引入版本号。
- 运行中实例绑定发布版本。
- 新版本发布不修改历史版本。
- 必要时提供版本迁移工具，但不默认自动迁移。

## 当前阶段结论

建议下一阶段先从“编号规则引擎、业务状态机、表单设计器基础版”开始，形成稳定的平台元数据底座；随后建设打印模板与代码生成器提升交付效率；再推进报表中心和安全策略中心；最后开放集成中心对外提供受控、安全、可审计的生态能力。

整个路线图应以小步迭代方式落地，每个模块都先完成最小可用闭环，再逐步增强高级能力，避免一次性引入过大复杂度。
