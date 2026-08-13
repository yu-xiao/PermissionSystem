# API 版本治理与模块化单体边界

## API 版本

- 当前稳定版本为 `v1`，业务 API 的规范路径是 `/api/v1/...`。
- 现有 `/api/...` 路径保留为兼容入口，不新增业务能力；响应带 `Deprecation: true` 和 successor `Link` 响应头。
- 新客户端必须使用 `/api/v1/...`。旧路径在至少一个完整发布周期内保持可用，移除前必须单独发布弃用通知和迁移说明。
- `/connect/...`、`/api/sso/oidc/...`、`/health...` 和 SignalR Hub 属于协议或运维端点，不纳入业务 API 版本号。
- 破坏性变更包括删除或重命名路径/HTTP 方法、删除必填请求字段、改变既有响应字段语义或改变认证/授权要求；新增可选字段和新端点属于兼容变更。
- 使用 `scripts/check-openapi-breaking.ps1 -Baseline <baseline.json> -Current <current.json>` 检查 OpenAPI 路径和 HTTP 方法是否被删除。发布流水线必须将该检查作为合并门禁。

## 模块清单与边界

当前模块按 Application 命名空间组织：

- Platform：`Abstractions`、`Common`、`Security`、`UserSessions`、`Tenants`
- Identity：`Users`、`Roles`、`Permissions`、`Menus`、`Departments`
- Workflow：`Workflows`、`StateMachines`、`DemoApprovalOrders`
- Integration：`Sso`、`Integration`、`Notifications`、`Messaging`、`Files`
- Operations：`Reports`、`ScheduledTasks`、`Jobs`、`OperationLogs`、`LoginLogs`
- Demo：`DemoBusinessOrders`、`Dictionaries`、`SystemConfigs`、`PrintTemplates`、`NumberRules`

模块必须遵循以下规则：

1. 每个模块公开 DTO、用例接口和领域事件等 Contracts；实体、仓储实现和基础设施适配器属于内部实现。
2. Api/Worker 只能依赖 Application 暴露的用例和 Contracts；Controller 不直接访问 `AppDbContext`。
3. Application 可以依赖 Domain 和抽象接口；Domain 不依赖 Api、Infrastructure 或外部传输技术。
4. 模块不得引用其他模块的内部服务、实体或仓储。跨模块查询通过公开 Contracts；跨模块副作用优先使用领域事件/Outbox。
5. 新模块使用独立命名空间、注册入口和迁移目录；未来 ERP/WMS 模块不得把实现代码放入 Platform 或现有业务模块内部。
6. 本项目保持模块化单体，不因边界规则立即拆分微服务。需要独立部署时，先以 Contracts 和事件契约为迁移边界单独评审。

数据库短期不按模块拆 Schema，也不新增迁移；未来 Schema 或迁移目录调整必须单独经过 DBA 评审。

