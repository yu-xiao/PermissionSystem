# 矩阵式角色权限分配设计

本文基于当前项目代码、AGENTS.md 约束和现有 RBAC 实现，规划“矩阵式角色权限分配”功能。本文只做设计规划，不包含业务代码实现。

## 1. 当前实现分析

### 1.1 角色、菜单、权限关系

当前后端核心实体位于 `backend/PermissionSystem.Domain/Entities`：

- `Role`
  - 字段：`Code`、`Name`、`Description`、`IsEnabled`、`Sort`
  - 导航关系：`UserRoles`、`RoleMenus`、`RolePermissions`、`RoleDataScope`
- `Menu`
  - 字段：`ParentId`、`Name`、`Path`、`Component`、`Redirect`、`Icon`、`Sort`、`Visible`、`KeepAlive`、`MenuType`、`PermissionCode`
  - 支持父子结构，已有 `Parent` / `Children`
  - `PermissionCode` 可作为菜单访问权限码
- `Permission`
  - 字段：`Code`、`Name`、`Group`、`Description`、`Resource`、`Action`
  - 当前没有 `MenuId`，即权限与菜单没有数据库层面的直接外键关系
- `RoleMenu`
  - 角色和菜单的关联关系
- `RolePermission`
  - 角色和按钮/功能权限的关联关系

结论：

- 当前菜单结构已经支持矩阵 UI 所需的父级模块、排序、路由、图标、类型和页面访问权限码。
- 当前权限可以通过 `Group` / `Resource` / `Action` 归类，但不能可靠地直接定位到某个菜单。
- 当前角色授权分为菜单授权和权限授权两套接口，矩阵保存时需要统一协调 `RoleMenu` 与 `RolePermission`。

### 1.2 当前角色接口

当前 `RoleController` 已提供：

- `GET /api/roles`
- `POST /api/roles`
- `PUT /api/roles/{id}`
- `DELETE /api/roles/{id}`
- `POST /api/roles/{id}/menus`
- `POST /api/roles/{id}/permissions`
- `GET /api/roles/{id}/data-scope`
- `POST /api/roles/{id}/data-scope`

当前 `RoleService` 中：

- `AssignMenusAsync` 对 `RoleMenu` 做全量替换
- `AssignPermissionsAsync` 对 `RolePermission` 做全量替换
- 已校验菜单/权限是否存在且属于同租户

缺口：

- 没有读取某个角色已授权菜单 ID / 权限 ID 的接口。
- 没有一次性读取矩阵结构、勾选状态、数据范围摘要的接口。
- 没有一次性保存菜单和权限的矩阵接口。
- 授权变更后没有明确刷新权限 claims、清理缓存或踢出相关用户会话的策略。

### 1.3 当前前端角色管理页面

当前页面位于 `frontend/permission-admin/src/views/system/role/index.vue`。

现有交互是表格操作模式：

- 角色列表表格
- 菜单授权弹窗：使用 `el-tree-select`
- 权限授权弹窗：使用多选 `el-select`
- 数据范围弹窗：使用现有 `getRoleDataScope` / `setRoleDataScope`

当前限制：

- 菜单授权、权限授权、数据范围配置是分散入口，不适合大量角色和大量功能权限。
- 打开菜单/权限授权弹窗时当前选中值初始化为空，缺少已授权明细加载。
- 权限没有按菜单行组织，管理员难以看出“某个菜单下有哪些按钮权限”。
- 没有模块级全选、菜单行全选、半选状态和矩阵保存体验。

### 1.4 数据权限现状

当前已有角色级和用户级数据范围实体：

- `RoleDataScope`
- `UserDataScope`
- `DataScopeType`
  - `All`
  - `CurrentUser`
  - `CurrentDepartment`
  - `CurrentDepartmentAndChildren`
  - `CustomDepartments`
- `IDataScopeService`
- `DataScopeService`

当前 `RoleController` 已有角色数据范围接口，可在矩阵界面复用。

结论：

- 角色级数据范围能力已具备，可以作为矩阵行右侧“数据范围”的第一阶段弹窗能力。
- 当前数据范围是角色级，不是“菜单行级”或“权限级”。矩阵行右侧可以先展示角色数据范围摘要，并打开角色级数据范围弹窗。

### 1.5 字段授权现状

当前未发现明确的字段权限实体或接口，例如：

- `FieldPermission`
- `RoleFieldPermission`
- `UserFieldPermission`
- 字段授权 API

结论：

- 字段授权当前只能做 UI 预留。
- 后续如需实现，需要先补充字段资源定义、角色字段授权关系和授权校验策略。

### 1.6 权限缓存、Token 和会话现状

当前认证和授权链路：

- 登录时 `UserCredentialValidator` 从用户角色读取 `RolePermissions`，将权限码写入 access token claims。
- `CurrentUserService` 从 token claims 读取当前权限。
- `/api/me/permissions` 对普通用户直接返回 token 中的权限 claims。
- 当前用户菜单由 `CurrentUserAppService.GetCurrentUserMenusAsync` 从 `RoleMenu` 实时查询并补齐父级菜单。
- 已有 `UserSession`、`UserSessionService` 和 `ICacheService`。
- 修改密码、退出登录已经使用 `ITokenRevocationService` / `UserSessionService` 做 refresh token 或会话吊销。

风险：

- 角色权限变更后，已登录用户 access token 中的权限 claims 不会自动变化。
- 若只保存 `RoleMenu` / `RolePermission`，当前用户可能需要刷新 token 或重新登录才能获得新的按钮权限。
- 菜单接口实时查库，菜单变更可能更快体现；按钮权限 claims 则取决于 token 生命周期。

## 2. 目标 UI 设计

目标页面采用企业后台常见的左右分栏矩阵：

```text
+----------------------+---------------------------------------------------+
| 角色列表              | 角色：系统管理员                                 |
| - 系统管理员          | [保存] [刷新]                                     |
| - 租户管理员          |                                                   |
| - 普通用户            | 模块：系统管理  [全选] [展开/收起]                |
|                      | ------------------------------------------------ |
|                      | 菜单       全选   功能权限             扩展能力   |
|                      | 用户管理   [ ]    查看 新增 编辑 删除   数据范围 字段授权 |
|                      | 角色管理   [ ]    查看 新增 编辑 删除   数据范围 字段授权 |
|                      | 菜单管理   [ ]    查看 新增 编辑 删除   数据范围 字段授权 |
|                      |                                                   |
|                      | 模块：审计日志  [全选] [展开/收起]                |
+----------------------+---------------------------------------------------+
```

左侧角色列表：

- 支持按角色名称/编码搜索。
- 展示角色名称、编码、启用状态。
- 点击角色后加载右侧矩阵。
- 当前选中角色高亮。

右侧矩阵：

- 顶部展示当前角色名称、编码、状态。
- 顶部操作：保存、刷新。
- 按一级菜单或业务模块折叠分组。
- 每个模块支持展开/收起。
- 每个模块支持模块级全选和半选状态。
- 每个菜单行展示：
  - 菜单名称
  - 菜单访问权限 checkbox
  - 行级全选 checkbox
  - 该菜单下的功能权限 checkbox
  - 数据范围按钮或摘要
  - 字段授权按钮或预留状态

视觉和交互原则：

- 保持 Element Plus 企业级 UI 风格。
- 矩阵区域要支持横向信息密度，但避免过宽导致不可读；功能权限可使用紧凑 checkbox group。
- 保存前本地维护 dirty 状态；切换角色时如有未保存变更，需要二次确认。
- 个人权限或超级管理员角色可根据规则禁用部分危险操作。

## 3. 后端接口设计

建议在现有 `RoleController` 下新增矩阵接口，仍由 Application 层处理业务。

### 3.1 获取角色权限矩阵

```http
GET /api/roles/{id}/permission-matrix
```

权限要求：

- `system:role:view`

返回建议：

```csharp
public sealed class RolePermissionMatrixResponse
{
    public Guid RoleId { get; init; }
    public string RoleCode { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public bool IsEnabled { get; init; }
    public IReadOnlyCollection<Guid> CheckedMenuIds { get; init; } = [];
    public IReadOnlyCollection<Guid> CheckedPermissionIds { get; init; } = [];
    public IReadOnlyList<RolePermissionMatrixModuleResponse> Modules { get; init; } = [];
}

public sealed class RolePermissionMatrixModuleResponse
{
    public Guid? MenuId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Icon { get; init; }
    public int Sort { get; init; }
    public IReadOnlyList<RolePermissionMatrixMenuRowResponse> Menus { get; init; } = [];
}

public sealed class RolePermissionMatrixMenuRowResponse
{
    public Guid MenuId { get; init; }
    public Guid? ParentId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Path { get; init; }
    public string? Component { get; init; }
    public string MenuType { get; init; } = string.Empty;
    public string? PermissionCode { get; init; }
    public int Sort { get; init; }
    public bool IsMenuChecked { get; init; }
    public IReadOnlyList<RolePermissionMatrixActionResponse> Actions { get; init; } = [];
    public RoleDataScopeSummaryResponse? DataScope { get; init; }
    public FieldPermissionSummaryResponse? FieldPermission { get; init; }
}

public sealed class RolePermissionMatrixActionResponse
{
    public Guid PermissionId { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Group { get; init; }
    public string? Resource { get; init; }
    public string? Action { get; init; }
    public bool IsChecked { get; init; }
}
```

矩阵构建规则：

- 模块优先使用一级菜单。
- 菜单行使用模块下可授权的菜单节点。
- 菜单访问权限来自 `Menu.PermissionCode` 和 `RoleMenu`。
- 功能权限优先按 `Permission.Resource` 匹配菜单：
  - 菜单 `PermissionCode` 为 `system:role:view` 时，可推导资源前缀为 `system:role`。
  - 权限 `Resource == system:role` 的权限归入该菜单行。
- 若无法通过 `Resource` 匹配，可按 `Permission.Group` 做兜底分组。
- 无法匹配菜单的权限放入“未归类权限”模块，避免丢失。

### 3.2 保存角色权限矩阵

```http
PUT /api/roles/{id}/permission-matrix
```

权限要求：

- `system:role:update`

请求建议：

```csharp
public sealed class SaveRolePermissionMatrixRequest
{
    public IReadOnlyCollection<Guid> MenuIds { get; init; } = [];
    public IReadOnlyCollection<Guid> PermissionIds { get; init; } = [];
}
```

保存规则：

- 校验角色存在且属于当前租户可见范围。
- 校验菜单 ID 和权限 ID 均属于角色租户。
- 使用事务一次性全量替换 `RoleMenu` 和 `RolePermission`。
- 保存后记录操作日志。
- 保存后执行缓存/会话处理策略。

### 3.3 数据范围接口

第一阶段复用现有接口：

```http
GET /api/roles/{id}/data-scope
POST /api/roles/{id}/data-scope
```

权限要求：

- `system:role:data-scope`

后续若要支持菜单行级数据范围，可新增：

```http
GET /api/roles/{id}/menu-data-scopes
PUT /api/roles/{id}/menu-data-scopes
```

但当前不建议第一阶段实现菜单行级数据范围，避免超出已有数据权限模型。

### 3.4 字段授权接口

当前字段授权实体缺失，第一阶段只做按钮预留。

后续可规划：

```http
GET /api/roles/{id}/field-permissions?resource=system:user
PUT /api/roles/{id}/field-permissions
```

建议模型：

```csharp
public sealed class FieldPermissionDefinition
{
    public Guid Id { get; init; }
    public string Resource { get; init; } = string.Empty;
    public string FieldCode { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
}

public sealed class RoleFieldPermissionRequest
{
    public string Resource { get; init; } = string.Empty;
    public IReadOnlyCollection<string> ReadableFields { get; init; } = [];
    public IReadOnlyCollection<string> WritableFields { get; init; } = [];
}
```

## 4. 前端组件设计

建议在现有角色页面基础上演进，不替换整体系统架构。

### 4.1 页面入口

可选方案：

- 方案 A：改造 `frontend/permission-admin/src/views/system/role/index.vue`，在角色表格页新增“权限矩阵”操作，打开全屏抽屉或页面区域。
- 方案 B：新增隐藏路由 `frontend/permission-admin/src/views/system/role-permission-matrix/index.vue`，点击角色页“权限矩阵”进入独立页面。

建议方案 B：

- 页面信息密度高，独立页面更适合左右分栏。
- 不破坏现有角色 CRUD、菜单授权、权限授权弹窗，可平滑过渡。

### 4.2 组件拆分

建议组件：

- `RolePermissionMatrixPage`
  - 页面容器，负责角色选择、矩阵加载、保存、刷新、dirty 状态。
- `RoleListPane`
  - 左侧角色列表、搜索、状态展示。
- `PermissionMatrix`
  - 右侧矩阵整体布局。
- `PermissionModulePanel`
  - 单个模块折叠面板，包含模块全选、半选状态、展开/收起。
- `PermissionMenuRow`
  - 单个菜单行，包含菜单选择、行全选、功能权限组、数据范围入口、字段授权入口。
- `RoleDataScopeDialog`
  - 可复用当前角色页数据范围弹窗逻辑。
- `FieldPermissionDialog`
  - 第一阶段显示“字段授权待启用”或禁用态；后续接入字段授权接口。

### 4.3 API 封装

建议新增：

- `frontend/permission-admin/src/api/rolePermissionMatrix.ts`
- `frontend/permission-admin/src/api/types/rolePermissionMatrix.ts`

方法：

- `getRolePermissionMatrix(roleId: string)`
- `saveRolePermissionMatrix(roleId: string, data)`

继续使用现有 `request.ts`，不创建新的 axios 实例。

## 5. 权限勾选联动规则

### 5.1 菜单行规则

- 菜单行包含：
  - 菜单访问 checkbox
  - 行级全选 checkbox
  - 功能权限 checkbox group
- 勾选行级全选：
  - 勾选该菜单访问权限。
  - 勾选该菜单下全部功能权限。
- 取消行级全选：
  - 取消该菜单访问权限。
  - 取消该菜单下全部功能权限。
- 勾选任意功能权限：
  - 自动勾选该菜单访问权限。
  - 自动勾选祖先菜单，保证动态菜单可展示。
- 取消全部功能权限：
  - 不强制取消菜单访问权限，由用户决定该页面是否仅可访问但无按钮权限。

### 5.2 模块级规则

- 模块级全选勾选：
  - 勾选模块下所有菜单访问权限。
  - 勾选模块下所有功能权限。
- 模块级全选取消：
  - 取消模块下所有菜单访问权限。
  - 取消模块下所有功能权限。
- 模块半选：
  - 模块内任意菜单或功能权限被选中，但未全部选中时显示半选。

### 5.3 父子菜单规则

- 子菜单被勾选时，自动勾选所有祖先菜单。
- 父菜单取消时：
  - 若父菜单只是目录，可取消其全部子菜单。
  - 若父菜单本身也是页面，可提示“取消父级将同步取消子级授权”。
- 保存时后端也应补齐必要父级菜单，防止前端状态异常导致菜单树断裂。

### 5.4 菜单权限码与功能权限规则

- `Menu.PermissionCode` 表示页面访问权限或菜单可见权限。
- `Permission.Code` 表示按钮/接口级功能权限。
- 如果 `Menu.PermissionCode` 对应的 `Permission.Code` 存在，可在矩阵里作为“查看”动作展示，但保存时仍要同时维护 `RoleMenu` 和 `RolePermission`。
- 如果 `Menu.PermissionCode` 没有对应 `Permission`，仍可通过 `RoleMenu` 控制菜单显示。

## 6. 数据范围弹窗设计

第一阶段复用现有角色级数据范围：

- 点击菜单行右侧“数据范围”按钮，打开角色数据范围弹窗。
- 弹窗展示当前角色的数据范围，而不是菜单行独立数据范围。
- 保存后刷新矩阵里的数据范围摘要。

弹窗字段：

- 数据范围类型：
  - 全部数据
  - 仅本人
  - 本部门
  - 本部门及下级
  - 自定义部门
- 自定义部门：
  - 使用部门树多选。

矩阵行展示：

- 第一阶段所有行展示同一个角色级数据范围摘要，例如“本部门及下级”。
- 可在按钮 tooltip 中说明“当前为角色级数据范围”。

后续扩展：

- 如果需要菜单行级数据范围，需要新增实体，例如 `RoleMenuDataScope`。
- 查询业务数据时需要结合资源编码和当前接口权限决定使用哪条数据范围规则。

## 7. 字段授权弹窗设计

第一阶段：

- 保留“字段授权”按钮位置。
- 当前无字段授权实体时按钮可禁用，tooltip 显示“字段授权待启用”。
- 不保存任何字段授权数据。

后续完整方案：

- 新增字段定义，例如：
  - 资源编码：`system:user`
  - 字段编码：`phoneNumber`
  - 字段名称：`手机号`
  - 授权模式：可见、可编辑、脱敏
- 新增角色字段授权关系。
- 字段授权弹窗按资源显示字段列表：
  - 可见
  - 可编辑
  - 脱敏
- 后端查询和返回 DTO 时结合字段授权做裁剪或脱敏。

注意：

- 字段授权会影响数据返回形态，不能只在前端隐藏字段。
- 涉及敏感数据时必须以后端控制为准。

## 8. 缓存清理策略

### 8.1 当前风险

当前按钮权限写入 access token claims。角色权限保存后：

- 数据库中的 `RolePermission` 已更新。
- 已登录用户旧 access token 中的权限 claims 仍是旧值。
- 刷新 token 时当前实现会复用原 principal，旧权限仍可能继续存在。

### 8.2 最小可用策略

保存角色矩阵后：

- 清理可能新增的角色矩阵缓存，例如：
  - `ps:role-permission-matrix:{tenantId}:{roleId}`
  - `ps:user-menus:{tenantId}:{userId}`
  - `ps:user-permissions:{tenantId}:{userId}`
- 记录操作日志。
- 前端如果当前登录用户拥有被修改的角色：
  - 清空本地菜单、权限和动态路由。
  - 重新拉取当前用户菜单。
  - 提示用户重新登录以刷新按钮权限 claims。

### 8.3 推荐企业级策略

保存角色矩阵后：

- 查询拥有该角色的用户 ID。
- 对这些用户执行会话失效策略：
  - 使用 `UserSessionService.RevokeUserSessionsAsync(userId, "Role permissions changed.")`
  - 使用 OpenIddict 官方方式吊销对应 refresh token，或引入按用户吊销 refresh token 的应用服务。
- 前端收到 401 或刷新失败后自动清理：
  - access token
  - refresh token
  - user info
  - menus
  - permissions
  - dynamic routes
  - tabsView
- 用户重新登录后获得最新权限 claims。

### 8.4 更平滑的长期策略

可引入 `PermissionVersion`：

- 用户、角色或租户维护权限版本号。
- access token 中写入权限版本。
- 请求时校验 token 版本与服务端版本是否一致。
- 不一致时拒绝请求并要求刷新登录。

该方案用户体验更可控，但实现复杂度高，建议放在后续阶段。

## 9. 需要新增和修改的文件列表

### 9.1 后端建议新增

- `backend/PermissionSystem.Application/Roles/RolePermissionMatrixModels.cs`
  - 矩阵 DTO、保存请求。
- `backend/PermissionSystem.Application/Roles/IRolePermissionMatrixService.cs`
  - 如不想扩大 `IRoleService`，可单独抽服务。
- `backend/PermissionSystem.Application/Roles/RolePermissionMatrixService.cs`
  - 构建矩阵、保存矩阵、补齐父级菜单、触发缓存/会话策略。

### 9.2 后端建议修改

- `backend/PermissionSystem.Api/Controllers/RoleController.cs`
  - 新增矩阵读取和保存接口。
- `backend/PermissionSystem.Application/Roles/RoleModels.cs`
  - 如选择复用 `IRoleService`，可在这里增加方法定义。
- `backend/PermissionSystem.Infrastructure/DependencyInjection.cs`
  - 注册矩阵服务。
- `backend/PermissionSystem.Application/Users/CurrentUserAppService.cs`
  - 后续如引入用户权限缓存，需要在此接入缓存和清理策略。
- `backend/PermissionSystem.Application/UserSessions/UserSessionService.cs`
  - 后续如需要按角色批量失效会话，可新增批量方法或在矩阵服务中按用户循环调用现有方法。

### 9.3 字段授权后续新增

- `backend/PermissionSystem.Domain/Entities/FieldPermission.cs`
- `backend/PermissionSystem.Domain/Entities/RoleFieldPermission.cs`
- `backend/PermissionSystem.Infrastructure/Configurations/FieldPermissionConfiguration.cs`
- `backend/PermissionSystem.Infrastructure/Configurations/RoleFieldPermissionConfiguration.cs`
- 对应 EF Core migration
- 字段授权 Application Service 和 Controller

### 9.4 前端建议新增

- `frontend/permission-admin/src/views/system/role-permission-matrix/index.vue`
- `frontend/permission-admin/src/views/system/role-permission-matrix/components/RoleListPane.vue`
- `frontend/permission-admin/src/views/system/role-permission-matrix/components/PermissionMatrix.vue`
- `frontend/permission-admin/src/views/system/role-permission-matrix/components/PermissionModulePanel.vue`
- `frontend/permission-admin/src/views/system/role-permission-matrix/components/PermissionMenuRow.vue`
- `frontend/permission-admin/src/views/system/role-permission-matrix/components/RoleDataScopeDialog.vue`
- `frontend/permission-admin/src/views/system/role-permission-matrix/components/FieldPermissionDialog.vue`
- `frontend/permission-admin/src/api/rolePermissionMatrix.ts`
- `frontend/permission-admin/src/api/types/rolePermissionMatrix.ts`

### 9.5 前端建议修改

- `frontend/permission-admin/src/views/system/role/index.vue`
  - 增加“权限矩阵”入口。
  - 保留现有菜单/权限/数据范围弹窗作为过渡能力。
- `frontend/permission-admin/src/router/index.ts`
  - 新增隐藏路由 `/system/roles/:id/permission-matrix` 或 `/system/role-permission-matrix`。
- `frontend/permission-admin/src/stores/permission.ts`
  - 后续根据缓存/会话策略，确保授权变化后能重置动态路由和权限。

## 10. 分阶段实现计划

### 阶段一：矩阵只读能力

- 后端新增矩阵读取接口。
- 后端按菜单树和权限资源构建模块、菜单行和功能权限。
- 前端新增矩阵页面和角色列表。
- 支持模块展开/收起、半选状态展示。
- 不开放保存。

验收点：

- 选择角色后能看到菜单和功能权限矩阵。
- 已授权菜单和权限能正确回显。
- 未匹配权限能显示在“未归类权限”模块。

### 阶段二：矩阵保存能力

- 后端新增矩阵保存接口。
- 保存时一次性替换 `RoleMenu` 和 `RolePermission`。
- 保存时补齐父级菜单。
- 前端支持菜单行全选、模块全选、保存和刷新。
- 保存成功后记录操作日志。

验收点：

- 保存后刷新页面，勾选状态保持正确。
- 用户重新登录后菜单和按钮权限按新授权生效。
- 不破坏现有角色 CRUD、动态菜单、按钮权限。

### 阶段三：数据范围集成

- 矩阵行右侧接入角色级数据范围弹窗。
- 复用现有 `GET/POST /api/roles/{id}/data-scope`。
- 保存数据范围后刷新摘要。

验收点：

- 可从矩阵页面打开并保存角色数据范围。
- 自定义部门校验仍由后端完成。

### 阶段四：字段授权预留与后续落地

- 第一阶段仅展示禁用按钮或占位弹窗。
- 后续补字段授权实体、接口、弹窗和后端字段裁剪/脱敏策略。

验收点：

- 当前不会产生无效字段授权数据。
- 后续实体模型确定后可平滑接入。

### 阶段五：缓存、Token 和会话强化

- 保存矩阵后定位受影响用户。
- 吊销受影响用户会话或刷新权限版本。
- 清理相关缓存。
- 前端在 401 或权限刷新失败时清理本地状态并跳转登录。

验收点：

- 角色权限变更后，受影响用户不会长期持有旧权限。
- 当前操作者修改自身角色权限后能安全退出或刷新状态。
- 不影响 OpenIddict 标准 token 流程。

## 11. 当前落地状态

当前已落地矩阵式角色权限分配的查询、保存和前端弹窗能力。

后端接口：

- `GET /api/roles/{roleId}/permission-matrix`
  - 返回一级菜单模块、菜单行和功能权限项。
  - 需要 `system:role:permission-matrix` 或 `system:role:view`。
- `PUT /api/roles/{roleId}/permission-matrix`
  - 保存 `menuIds` 到 `RoleMenus`。
  - 保存 `permissionIds` 到 `RolePermissions`。
  - 自动补齐权限所属菜单和父级菜单。
  - 能根据 `Permission.Resource` 自动补齐同资源的 `:view` 权限。
  - 需要 `system:role:assign-permission`。
- `GET /api/roles/{roleId}/users`
  - 分页返回当前租户下用户，按角色关联关系返回 `checked`。
  - 同时返回完整 `selectedUserIds`，用于前端分页和搜索后提交完整用户集合。
  - 需要 `system:role:view` 或 `system:role:assign-user`。
- `PUT /api/roles/{roleId}/users`
  - 使用提交的 `userIds` 全量替换 `UserRoles`。
  - 校验角色存在、用户属于同租户且未禁用。
  - 非 SuperAdmin 不允许修改 `SuperAdmin` 角色的关联用户。
  - 需要 `system:role:assign-user`。

前端入口：

- 角色管理页操作列提供“分配权限”按钮。
- 按钮受 `v-permission="'system:role:assign-permission'"` 控制。
- 弹窗组件位于 `frontend/permission-admin/src/views/system/role/components/RolePermissionMatrixDialog.vue`。
- 角色管理页操作列新增“关联用户”按钮。
- 按钮受 `v-permission="'system:role:assign-user'"` 控制。
- 弹窗组件位于 `frontend/permission-admin/src/views/system/role/components/RoleUserDialog.vue`。
- 角色管理页不再显示独立“菜单”和“权限”按钮，避免与统一的“分配权限”入口重复。

权限保存后的缓存策略：

- 保存后清理 `ps:role-permission-matrix:{tenantId}:{roleId}`。
- 保存后清理该角色下用户的 `ps:user-menus:{tenantId}:{userId}`。
- 保存后清理该角色下用户的 `ps:user-permissions:{tenantId}:{userId}`。
- 当前项目普通用户的按钮权限仍主要来自 access token claims，因此被修改角色的用户重新登录后可以获得最新权限。
- 角色关联用户保存后，会清理新旧关联用户的 `ps:user-menus:{tenantId}:{userId}`、`ps:user-permissions:{tenantId}:{userId}` 和 `ps:user-roles:{tenantId}:{userId}`。
- 角色关联用户变更不会强制下线在线用户；完整按钮/API 权限以重新登录后的 token claims 为准。

数据范围当前状态：

- 当前数据范围仍是角色级，持久化到 `RoleDataScopes`。
- 矩阵行右侧提供“数据范围”入口，但任意一行设置后实际应用到整个角色。
- 尚未实现菜单行级 `RoleMenuDataScope`。

字段授权当前状态：

- 当前没有字段授权实体和字段权限接口。
- 前端保留“字段授权”入口并显示空状态。
- 后端保存请求保留 `fieldPermissions` 字段并校验菜单合法性，但不持久化字段授权。
