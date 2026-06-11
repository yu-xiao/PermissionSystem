# 工作流指南

## 当前能力

当前项目包含工作流定义、流程设计、业务绑定、实例、待办、已办、抄送和审批操作等能力。后端模块位于：

- `backend/PermissionSystem.Application/Workflows`
- `backend/PermissionSystem.Domain/Entities/Workflow*.cs`
- `backend/PermissionSystem.Infrastructure/Configurations/Workflow*.cs`
- `backend/PermissionSystem.Api/Controllers/Workflow*.cs`

前端页面位于：

- `frontend/permission-admin/src/views/workflow/definition/index.vue`
- `frontend/permission-admin/src/views/workflow/designer/index.vue`
- `frontend/permission-admin/src/views/workflow/business-binding/index.vue`
- `frontend/permission-admin/src/views/workflow/task/todo.vue`
- `frontend/permission-admin/src/views/workflow/task/done.vue`
- `frontend/permission-admin/src/views/workflow/instance/my-started.vue`
- `frontend/permission-admin/src/views/workflow/instance/detail.vue`
- `frontend/permission-admin/src/views/workflow/cc/index.vue`

更详细设计可参考 `docs/workflow-design.md`。

## 主要接口

流程定义：

- `GET /api/workflow/definitions`
- `GET /api/workflow/definitions/{id}`
- `POST /api/workflow/definitions`
- `PUT /api/workflow/definitions/{id}`
- `DELETE /api/workflow/definitions/{id}`
- `GET /api/workflow/definitions/{id}/designer`
- `PUT /api/workflow/definitions/{id}/designer`
- `POST /api/workflow/definitions/{id}/publish`
- `POST /api/workflow/definitions/{id}/disable`
- `POST /api/workflow/definitions/{id}/copy`

业务绑定：

- `GET /api/workflow/business-bindings`
- `GET /api/workflow/business-bindings/by-business-type/{businessType}`
- `POST /api/workflow/business-bindings`
- `PUT /api/workflow/business-bindings/{id}`
- `DELETE /api/workflow/business-bindings/{id}`
- `POST /api/workflow/business-bindings/{id}/enable`
- `POST /api/workflow/business-bindings/{id}/disable`

流程实例和任务：

- `POST /api/workflow/instances/start`
- `GET /api/workflow/instances/my-started`
- `GET /api/workflow/instances/{instanceId}`
- `GET /api/workflow/instances/{instanceId}/records`
- `POST /api/workflow/instances/{instanceId}/withdraw`
- `GET /api/workflow/tasks/todo`
- `GET /api/workflow/tasks/done`
- `POST /api/workflow/tasks/{taskId}/approve`
- `POST /api/workflow/tasks/{taskId}/reject`
- `POST /api/workflow/tasks/{taskId}/transfer`
- `POST /api/workflow/tasks/{taskId}/add-sign`

抄送：

- `GET /api/workflow/cc/my`
- `POST /api/workflow/cc/{ccId}/read`

## 典型使用流程

1. 在流程定义页面创建流程。
2. 打开设计器维护节点、连线、条件和审批人规则。
3. 发布流程定义。
4. 在业务绑定页面将业务类型绑定到已发布流程。
5. 业务单据调用流程启动接口或业务服务内触发启动。
6. 审批人进入待办列表处理任务。
7. 发起人查看我发起的流程和详情。
8. 相关人员查看抄送。

## 开发接入方式

新增业务模块接入工作流时，后端通常需要：

- 业务实体实现或符合项目中审批业务实体约定。
- 在 Application 中实现业务服务。
- 实现工作流业务处理器，参与流程启动、通过、拒绝、撤回等回调。
- 如需状态流转，配合 `StateMachines` 模块实现状态变更。
- 在 `DependencyInjection` 中通过扫描注册 `IWorkflowBusinessHandler` 实现。
- 增加菜单、权限和前端页面。

当前项目已有演示审批相关代码，可参考：

- `backend/PermissionSystem.Application/DemoApprovalOrders`
- `backend/PermissionSystem.Application/DemoBusinessOrders`
- `frontend/permission-admin/src/views/demo/approval-order`
- `frontend/permission-admin/src/views/demo/business-order`

## 权限码

常见工作流权限码：

- `workflow:definition:view`
- `workflow:definition:create`
- `workflow:definition:update`
- `workflow:definition:delete`
- `workflow:definition:design`
- `workflow:definition:publish`
- `workflow:business-binding:view`
- `workflow:business-binding:create`
- `workflow:business-binding:update`
- `workflow:business-binding:delete`
- `workflow:instance:start`
- `workflow:instance:view`
- `workflow:task:todo`
- `workflow:task:approve`
- `workflow:task:reject`
- `workflow:task:transfer`
- `workflow:task:add-sign`
- `workflow:cc:view`

权限码必须同时匹配后端 `[Permission]`、种子数据、角色授权和前端按钮控制。

## 本地、Docker、生产差异

本地：

- API Development 启动会执行迁移和种子数据。
- 可用 Swagger 和前端页面调试。
- Worker 未启动时，涉及后台任务的通知或异步处理可能不执行。

Docker：

- API、Worker、SQL Server、Redis 默认运行。
- RabbitMQ 默认关闭，通知 Outbox 可记录但不发布到队列。
- 启用 RabbitMQ 后可测试异步通知消费。

生产：

- 需要保证 API 与 Worker 使用同一数据库和一致配置。
- 发布流程定义前应在预发布环境验证。
- 流程定义、业务绑定和角色权限调整应纳入变更记录。

## 常见问题

### 发起流程失败

检查业务类型是否有启用的业务绑定，流程定义是否已发布，当前用户是否有 `workflow:instance:start` 权限。

### 待办列表没有任务

检查审批人规则是否解析到当前用户，任务是否已被他人处理，当前用户权限是否包含 `workflow:task:todo`。

### 审批通过后业务状态没变

检查业务模块是否实现对应的工作流业务处理器或状态流转处理器，服务是否被依赖注入扫描到。

### 通知没有实时出现

检查 SignalR 连接、通知权限、Outbox 状态、RabbitMQ 是否启用以及消费者服务是否启动。

### Docker 中流程相关后台行为不一致

确认 `permission-system-worker` 容器是否健康，API 与 Worker 的环境变量是否一致。
