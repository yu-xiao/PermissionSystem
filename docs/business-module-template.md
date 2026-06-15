# 业务模块接入模板

本文档用于后续 WMS / ERP 等业务模块接入 PermissionSystem 平台能力。当前仓库只提供 `DemoBusinessOrder` 最小示例，不代表真实 WMS / ERP 业务规则。

## 接入目标

标准业务模块应复用平台能力，而不是在业务模块内重复实现基础设施：

- 菜单权限：通过 `Menu` 种子数据或后台菜单管理配置。
- 按钮权限：后端使用 `[Permission("module:resource:action")]`，前端使用 `v-permission`。
- 数据权限：业务列表查询使用 `IDataScopeService` + `IDataPermissionFilter`。
- 编号规则：业务单据调用 `INumberGenerator.GenerateAsync(ruleCode)`。
- 状态机：业务状态变更通过 `IStateTransitionExecutor`。
- 审批流：业务单据实现 `IWorkflowBusinessHandler`，流程定义和业务绑定由平台工作流模块维护。
- 附件：复用 `IFileService`，统一写入 `BusinessType` + `BusinessId`。
- Excel 导入导出：复用 `IExcelService` 和 `[ExcelColumn]`。
- 打印模板：复用 `IPrintTemplateService`，模板按 `BusinessType` 查询。
- 操作日志：由 `OperationLogMiddleware` 自动记录 API 操作，业务页按模块/路径查询。
- 变更历史：业务模块记录关键字段变化，建议最小可用时存 JSON，复杂模块再拆独立表。
- 通知：复用 `INotificationService`，通过 Outbox/消息消费发送站内通知。

## DemoBusinessOrder 文件清单

后端：

- `backend/PermissionSystem.Domain/Entities/DemoBusinessOrder.cs`
- `backend/PermissionSystem.Infrastructure/Configurations/DemoBusinessOrderConfiguration.cs`
- `backend/PermissionSystem.Infrastructure/Data/Migrations/20260611090000_AddDemoBusinessOrder.cs`
- `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderModels.cs`
- `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderService.cs`
- `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderWorkflowHandler.cs`
- `backend/PermissionSystem.Application/DemoBusinessOrders/DemoBusinessOrderStateTransitionHandler.cs`
- `backend/PermissionSystem.Api/Controllers/DemoBusinessOrderController.cs`

前端：

- `frontend/permission-admin/src/api/demoBusinessOrder.ts`
- `frontend/permission-admin/src/views/demo/business-order/index.vue`
- `frontend/permission-admin/src/stores/permission.ts`

平台种子：

- `SeedDataInitializer` 中新增 `demo-business-order:*` 权限、菜单、编号规则、状态机、默认已发布审批流和 `DemoBusinessOrder` 业务流程绑定。

## 命名规范

假设业务模块名为 `XxxOrder`：

- BusinessType：`XxxOrder`
- 编号规则 Code：`XxxOrder`
- 后端路由：`api/xxx-orders`
- 权限前缀：`xxx-order`
- 前端 API：`src/api/xxxOrder.ts`
- 前端页面：`src/views/<domain>/xxx-order/index.vue`
- 实体表：建议 `snake_case`，例如 `xxx_order`

## 后端接入步骤

1. 新增 Domain Entity

实体必须继承 `BaseEntity`。需要审批时实现 `IApprovalBusinessEntity`：

```csharp
public sealed class XxxOrder : BaseEntity, IApprovalBusinessEntity
{
    public string OrderNo { get; set; } = string.Empty;
    public ApprovalStatus ApprovalStatus { get; set; } = ApprovalStatus.Draft;
    public Guid? WorkflowInstanceId { get; set; }
}
```

2. 新增 EF Configuration 和 DbSet

配置表名、字段长度、金额精度、唯一单号索引、状态/部门/负责人查询索引。

3. 新增 Application Models

至少包含：

- QueryRequest
- CreateRequest
- UpdateRequest
- Response
- Excel ExportRow / ImportRow
- Service Interface

4. 新增 Application Service

服务层负责业务编排：

- 创建时生成单号。
- 查询时套数据权限。
- 更新时写变更历史。
- 提交时启动审批流。
- 附件/打印/通知/日志调用平台服务。

5. 新增状态机处理器

实现 `IStateTransitionHandler`，状态机只决定“能不能从 A 到 B”，业务处理器负责把目标状态写回实体。

6. 新增审批流处理器

实现 `IWorkflowBusinessHandler`，在流程启动、通过、拒绝、撤回、取消时调用状态机动作。

7. 新增 Controller

每个端点必须加权限：

```csharp
[Permission("xxx-order:view")]
[Permission("xxx-order:create")]
[Permission("xxx-order:submit")]
```

写操作建议加：

```csharp
[IdempotencyKey]
[PreventDuplicateSubmit]
```

8. 注册 DI

在 `PermissionSystem.Application/DependencyInjection.cs` 注册业务服务。工作流/状态机处理器会通过接口扫描自动注册。

9. 新增迁移

模板示例已提供迁移文件。正式模块应使用 EF Core 迁移命令生成迁移，并检查 Up/Down 只包含本次业务表变更。

## 前端接入步骤

1. 新增 API 文件

所有请求走 `utils/request.ts`，保持自动 Token 刷新和幂等 Key 行为。

2. 新增页面

默认页面结构：

- 搜索表单
- 表格
- 分页
- Modal 表单
- 行操作按钮
- 接入点抽屉或详情页

3. 使用按钮权限

```vue
<el-button v-permission="'xxx-order:create'">新增</el-button>
```

4. 接入动态菜单

在 `stores/permission.ts` 增加菜单 component 到页面组件的映射和 cacheName。

## 平台配置清单

正式业务模块上线前，应确认以下平台基础资料：

- 菜单：路径、组件、图标、可见性、权限码。
- 权限：每个按钮和 API 动作都有权限码。
- 角色权限：目标角色已分配菜单和按钮权限。
- 角色数据权限：按全部、本人、本部门、本部门及下级、自定义部门配置。
- 编号规则：RuleCode 与 BusinessType 保持一致。
- 状态机：状态、动作、动作权限完整。
- 审批流：发布流程定义，并创建业务流程绑定。
- 打印模板：按 BusinessType 创建模板。
- 通知模板：如需模板化文案，创建通知模板并在业务服务中渲染。

## DemoBusinessOrder 覆盖能力

| 能力 | 示例位置 |
| --- | --- |
| 菜单权限 | `SeedDataInitializer` 菜单 `Demo 业务单据` |
| 按钮权限 | Controller `[Permission]` + 前端 `v-permission` |
| 数据权限 | `DemoBusinessOrderService.BuildVisibleQueryAsync` |
| 编号规则 | `DemoBusinessOrderConstants.NumberRuleCode` |
| 状态机 | `DemoBusinessOrderStateTransitionHandler` |
| 审批流 | `DemoBusinessOrderWorkflowHandler` + 默认种子 `DemoBusinessOrderDefaultApproval` |
| 附件 | `/attachments` API + `IFileService` |
| Excel 导入导出 | `/import-template`、`/import`、`/export` |
| 打印模板 | `/print-templates`、`/print/{templateId}` |
| 操作日志 | `/operation-logs` |
| 变更历史 | `ChangeHistoryJson` + `/change-histories` |
| 通知 | `/notify` + `INotificationService` |

## 注意事项

- `DemoBusinessOrder` 是模板，不应演化为真实 WMS / ERP 模块。
- 默认开发环境会自动创建并启用 `DemoBusinessOrderDefaultApproval`，新库可直接从创建单据提交到待办；如果手工禁用或改坏绑定，提交仍会返回工作流绑定或已发布定义相关错误。
- 打印预览依赖已配置 `BusinessType = DemoBusinessOrder` 的打印模板；未配置时页面会提示先配置模板。
- 变更历史当前为最小 JSON 存储。高并发、审计强要求场景应拆成独立历史表。
- 正式模块不得绕过权限、数据权限、审计日志、租户隔离。
