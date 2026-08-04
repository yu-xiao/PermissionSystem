# 数据库索引与性能优化检查

检查日期：2026-06-11

## 范围

本次检查基于当前 EF Core 实体配置、`AppDbContext` 全局查询过滤器、主要 Application Service 查询路径和现有迁移快照，覆盖：

- 用户、角色、菜单、权限相关表
- 审批流实例、任务、记录、抄送、业务绑定表
- 操作日志、登录日志、安全相关日志
- 通知、在线用户、会话表
- Outbox / Inbox
- 文件资源表
- SSO Provider、SSO 用户绑定、SSO 登录日志
- 报表执行日志
- API 调用日志

## 已落地的最小修改

已补充迁移：`20260611023144_AddDatabasePerformanceIndexes`。

### 基础实体配置一致性

以下配置类原本没有显式调用 `ConfigureBaseEntity()`，但实体均继承 `BaseEntity`，历史迁移中也已经存在 `TenantId`、`IsDeleted` 基础列和基础索引。本次补回配置，避免后续生成迁移时误判基础索引或默认值为需要删除：

- `ApiClientConfiguration`
- `ApiClientSecretConfiguration`
- `ExternalApiCallLogConfiguration`
- `IpAccessRuleConfiguration`
- `LoginFailureRecordConfiguration`
- `ReportDefinitionConfiguration`
- `ReportExecutionLogConfiguration`
- `ReportQueryParamConfiguration`
- `SecurityPolicyConfiguration`
- `SensitiveOperationVerificationConfiguration`
- `WebhookDeliveryLogConfiguration`
- `WebhookSubscriptionConfiguration`

迁移文件已手动修剪：仅执行本次新增/替换的性能索引，不重复创建历史迁移中已存在的基础索引。

### 新增或调整索引

| 表 | 索引 | 目的 |
| --- | --- | --- |
| `Users` | `(TenantId, Email)`、`(TenantId, PhoneNumber)` | 支撑 SSO 自动绑定时按邮箱/手机号匹配本地用户 |
| `Roles` | `(TenantId, IsEnabled, Sort)` | 支撑角色启停筛选和排序 |
| `Menus` | `(TenantId, ParentId, Sort)` | 支撑菜单树按父级加载和排序 |
| `Permissions` | `(TenantId, Group)` | 支撑权限按分组查询 |
| `OperationLogs` | `(TenantId, UserId, CreatedAt)`、`(TenantId, TraceId)` | 支撑租户内用户日志、TraceId 查询 |
| `LoginLogs` | `(TenantId, UserId, CreatedAt)`、`(TenantId, TraceId)` | 支撑租户内用户登录日志、TraceId 查询 |
| `UserNotifications` | 唯一索引由 `(UserId, NotificationId)` 调整为 `(TenantId, UserId, NotificationId)` | 与多租户模型保持一致 |
| `OutboxMessages` | `(TenantId, Status, CreatedAt)`、`(Status, NextRetryAt, CreatedAt)` | 支撑管理端分页和后台跨租户待投递扫描 |
| `InboxMessages` | `(TenantId, Status, CreatedAt)` | 支撑 Inbox 管理端按状态分页 |
| `FileResources` | `(TenantId, BusinessType, BusinessId, CreatedAt)` | 支撑业务对象附件列表按创建时间排序 |
| `SsoLoginLogs` | `(TenantId, LocalUserId, CreatedAt)`、`(TenantId, ProviderCode, ExternalUserId, CreatedAt)`、`(TenantId, TraceId)` | 支撑 SSO 用户、Provider、TraceId 维度查询 |
| `WorkflowTasks` | `(TenantId, ApproverUserId, Status, AssignedAt)`、`(TenantId, InstanceId, ApproverUserId)` | 支撑待办/已办按分配时间排序和实例权限判断 |
| `ReportExecutionLogs` | `(TenantId, CreatedAt)` | 支撑报表执行日志默认分页 |
| `ExternalApiCallLogs` | `(TenantId, CreatedAt)` | 支撑 API 调用日志默认分页 |

## 索引覆盖结论

### 用户、角色、菜单、权限

已有唯一索引：

- `Users`: `(TenantId, NormalizedUserName)`
- `Roles`: `(TenantId, Code)`
- `Permissions`: `(TenantId, Code)`
- `UserRoles`: `(TenantId, UserId, RoleId)`
- `RoleMenus`: `(TenantId, RoleId, MenuId)`
- `RolePermissions`: `(TenantId, RoleId, PermissionId)`

本次补充了邮箱、手机号、角色启停排序、菜单父级排序、权限分组索引。当前索引基本覆盖认证、授权、菜单构建和 SSO 自动绑定的高频查询。

### 审批流

已有核心索引：

- `wf_instance`: `(TenantId, BusinessType, BusinessId)`、`(TenantId, StarterUserId, Status, CreatedAt)`、`(TenantId, Status, CreatedAt)`
- `wf_task`: `(TenantId, InstanceId, NodeKey)`
- `wf_record`: `(TenantId, InstanceId, OperatedAt)`、`(TenantId, OperatorUserId, OperatedAt)`
- `wf_cc`: `(TenantId, CcUserId, IsRead, CreatedAt)`、`(TenantId, InstanceId, CcUserId)`
- `wf_business_binding`: `(TenantId, BusinessType, IsDeleted)` 唯一、`(TenantId, DefinitionId, IsEnabled)`

本次将待办任务索引从 `CreatedAt` 调整为 `AssignedAt`，并增加实例 + 审批人索引，以匹配当前服务层查询。

### 日志表

日志类表普遍具备 `(TenantId, CreatedAt)` 或同类时间索引。本次补充了操作日志、登录日志、SSO 登录日志、报表执行日志、API 调用日志的租户前缀查询索引。

建议后续按数据量规划归档：

- 高频访问日志：`OperationLogs`、`LoginLogs`、`SsoLoginLogs`、`ExternalApiCallLogs`
- 执行类日志：`ReportExecutionLogs`、`JobExecutionLogs`、`ScheduledTaskExecutionLogs`、`WebhookDeliveryLogs`
- 建议按 `TenantId + CreatedAt` 做归档条件，冷热分离或分区表策略可在数据量达到千万级前评估。

### Outbox / Inbox

已有消息唯一约束：

- `OutboxMessages`: `(TenantId, MessageId)` 唯一
- `InboxMessages`: `(TenantId, MessageId, Consumer)` 唯一

本次补充 Outbox 跨租户后台扫描索引 `(Status, NextRetryAt, CreatedAt)`，避免后台任务在无租户上下文时无法利用以 `TenantId` 开头的索引。

### SSO

已有唯一约束：

- `sso_provider`: `(TenantId, ProviderCode)` 唯一
- `sso_user_binding`: `(TenantId, ProviderId, ExternalUserId)` 唯一
- `sso_user_binding`: `(TenantId, ProviderId, LocalUserId)` 唯一

本次补充 SSO 登录日志的租户前缀用户、外部用户、TraceId 查询索引。SSO 用户绑定的 `ClaimsJson` 为 `nvarchar(max)`，应避免在列表页频繁读取。

## 唯一索引与软删除风险

当前 `AppDbContext` 对所有 `BaseEntity` 应用全局过滤：

```csharp
!entity.IsDeleted && (IsSystemTenantScopeActive || entity.TenantId == CurrentTenantId)
```

租户上下文缺失且未进入显式系统作用域时，租户条件恒不匹配，查询采用 fail-closed 行为。整体设计是合理的，但唯一索引与软删除存在以下业务语义风险：

- `Users`、`Roles`、`Permissions`、`SsoProvider` 等唯一索引不包含 `IsDeleted`，软删除后仍会占用唯一值。如果业务要求删除后允许重建同名编码，需要改为过滤唯一索引或显式包含 `IsDeleted`。
- `WorkflowBusinessBinding` 使用 `(TenantId, BusinessType, IsDeleted)` 唯一，只允许同一业务类型存在一条已删除历史记录。若同一业务类型多次删除/重建，可能触发唯一约束冲突。
- `DemoApprovalOrder` 使用 `(TenantId, OrderNo, IsDeleted)` 唯一，也有同类多次删除历史冲突风险。

本次未修改这些唯一约束，因为这会改变“软删除后是否允许复用编码/单号”的业务规则。

## N+1 与查询形态风险

检查到的主要风险：

- `ReportService.GetPagedAsync` 在列表结果中逐条调用 `GetParams(entity.Id)`，存在 N+1 查询。建议后续按当前页 `ReportId` 批量加载参数后分组。
- `WorkflowTaskService.BuildTaskPagedResult` 和 `GetMyCcAsync` 先加载全部任务/抄送，再在内存中匹配关键字和分页。当前通过 `LoadInstances` 批量取实例，避免了逐条 N+1，但数据量增大后会产生内存分页问题。
- `OpenIntegrationService.GetApiCallLogsAsync` 先加载全部 API Client 到字典，再分页 API 调用日志。Client 数量较大时应改为只加载当前页涉及的 ClientId。
- 多个日志列表先 `ToList()` 后映射 DTO，会读取实体中的大字段。建议后续改为查询端 `Select` 投影。

## 大字段查询建议

以下字段不建议在列表页或高频查询中默认读取：

- `OperationLogs.RequestBody`、`OperationLogs.ResponseBody`
- `OutboxMessages.Payload`、`OutboxMessages.Headers`
- `Notification.Content`、`Notification.Payload`
- `WorkflowInstance.FormDataJson`
- `SsoUserBinding.ClaimsJson`
- `ReportDefinition.SqlText`、`ReportDefinition.ColumnsJson`、`ReportDefinition.ParamsJson`
- `ReportExecutionLogs.ParamsJson`
- `WebhookDeliveryLogs.Payload`、`WebhookDeliveryLogs.ResponseBody`

建议列表查询统一使用 DTO 投影，只在详情接口读取大字段。

## 后续建议

1. 为日志表制定归档策略：按 `CreatedAt` 归档，保留近期热数据，历史数据转归档表或低频存储。
2. 评估唯一索引的软删除语义：明确“删除后是否可复用编码/单号/绑定关系”。
3. 对报表、工作流任务、API 调用日志列表做查询投影和批量加载优化。
4. 对包含 `Contains` 的关键字查询保持谨慎：普通 B-tree 索引无法有效支持 `%keyword%`，大数据量场景应考虑全文索引或更明确的查询条件。
