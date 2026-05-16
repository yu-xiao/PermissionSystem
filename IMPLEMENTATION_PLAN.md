# PermissionSystem Implementation Plan

## 1. 当前项目理解

PermissionSystem 是一个 Vue 3 + ASP.NET Core Web API (.NET 10) 的前后端分离企业级权限管理系统，目标是以 Modular Monolith 和 DDD 风格分层为基础，逐步建设可维护、可扩展、可长期演进的权限平台。

当前仓库目录状态：

```text
PermissionSystem/
├── AGENTS.md
├── backend/
├── docs/
├── frontend/
└── scripts/
```

当前 `backend/`、`frontend/`、`docs/`、`scripts/` 目录为空，后续实现应先搭建可运行骨架，再逐步补齐基础设施、认证授权、RBAC 和前端管理界面。

## 2. 后端项目结构

建议后端使用 .NET 10 Solution，保持分层边界清晰：

```text
backend/
├── PermissionSystem.sln
├── Directory.Build.props
├── PermissionSystem.Api/
│   ├── Controllers/
│   ├── Extensions/
│   ├── Middlewares/
│   ├── Authorization/
│   ├── Options/
│   ├── Program.cs
│   └── appsettings*.json
├── PermissionSystem.Application/
│   ├── Abstractions/
│   ├── Contracts/
│   ├── DTOs/
│   ├── Requests/
│   ├── Responses/
│   ├── Services/
│   └── UseCases/
├── PermissionSystem.Domain/
│   ├── Common/
│   ├── Entities/
│   ├── ValueObjects/
│   ├── DomainServices/
│   ├── Events/
│   └── Repositories/
├── PermissionSystem.Infrastructure/
│   ├── Data/
│   ├── Configurations/
│   ├── Repositories/
│   ├── UnitOfWork/
│   ├── OpenIddict/
│   ├── Redis/
│   └── DependencyInjection.cs
├── PermissionSystem.Shared/
│   ├── Results/
│   ├── Exceptions/
│   ├── Constants/
│   ├── Pagination/
│   └── Helpers/
└── PermissionSystem.Worker/
    └── Program.cs
```

依赖方向：

```text
Api -> Application -> Domain
Api -> Infrastructure
Infrastructure -> Application
Infrastructure -> Domain
Application -> Shared
Api -> Shared
Domain 尽量保持纯净，不依赖 EF Core、HTTP、OpenIddict。
```

核心约束：

- Controller 只做参数接收、基础验证和结果返回。
- Application 层承载用例编排和业务流程。
- Domain 层承载实体、值对象、领域规则和领域事件。
- Infrastructure 层承载 EF Core、OpenIddict、Redis、Repository、UnitOfWork。
- 所有 API 返回 `ApiResult<T>` 或 `PagedResult<T>`。
- 所有实体继承 `BaseEntity`，默认包含多租户、审计和软删除字段。

## 3. 前端项目结构

建议前端使用 Vite + Vue 3 + TypeScript + Pinia + Vue Router + Axios + Element Plus：

```text
frontend/
├── package.json
├── vite.config.ts
├── tsconfig.json
├── index.html
├── .env.example
└── src/
    ├── api/
    │   ├── http.ts
    │   ├── auth.ts
    │   ├── users.ts
    │   ├── roles.ts
    │   ├── menus.ts
    │   └── permissions.ts
    ├── assets/
    ├── components/
    ├── directives/
    │   └── permission.ts
    ├── layouts/
    │   ├── BasicLayout.vue
    │   └── components/
    ├── router/
    │   ├── index.ts
    │   ├── guards.ts
    │   └── dynamic-routes.ts
    ├── stores/
    │   ├── auth.ts
    │   ├── user.ts
    │   ├── permission.ts
    │   └── menu.ts
    ├── types/
    ├── utils/
    │   ├── token.ts
    │   └── request.ts
    ├── views/
    │   ├── login/
    │   ├── dashboard/
    │   └── system/
    │       ├── users/
    │       ├── roles/
    │       ├── menus/
    │       └── permissions/
    ├── App.vue
    └── main.ts
```

前端核心约束：

- 全部使用 Composition API、`script setup`、TypeScript。
- Axios 二次封装，自动携带 access token。
- 支持 refresh token 自动续期。
- 401 自动清理登录状态并跳转登录页。
- 动态菜单来自后端。
- 动态路由由菜单和权限生成。
- 按钮权限通过 `v-permission` 指令控制。
- 默认管理页包含查询栏、表格、分页、弹窗表单。

## 4. 数据库设计

### 4.1 通用字段

所有业务实体继承 `BaseEntity`：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Id | uniqueidentifier | 主键 |
| TenantId | uniqueidentifier | 租户 ID |
| CreatedAt | datetimeoffset | 创建时间 |
| CreatedBy | uniqueidentifier/null | 创建人 |
| UpdatedAt | datetimeoffset/null | 更新时间 |
| UpdatedBy | uniqueidentifier/null | 更新人 |
| IsDeleted | bit | 软删除 |

### 4.2 核心表

租户与用户：

- `Tenants`
- `Users`
- `UserProfiles`，可选

角色与用户关系：

- `Roles`
- `UserRoles`

菜单与权限：

- `Menus`
- `Permissions`
- `RolePermissions`
- `RoleMenus`

审计与日志：

- `AuditLogs`
- `LoginLogs`

OpenIddict 官方表：

- `OpenIddictApplications`
- `OpenIddictAuthorizations`
- `OpenIddictScopes`
- `OpenIddictTokens`

### 4.3 关键字段建议

`Users`：

- `UserName`
- `NormalizedUserName`
- `Email`
- `PhoneNumber`
- `PasswordHash`
- `DisplayName`
- `AvatarUrl`
- `Status`
- `LastLoginAt`

`Roles`：

- `Code`
- `Name`
- `Description`
- `Status`
- `Sort`

`Menus`：

- `ParentId`
- `Name`
- `Path`
- `Component`
- `Redirect`
- `Icon`
- `Sort`
- `Visible`
- `KeepAlive`
- `PermissionCode`
- `MenuType`，目录、菜单、按钮

`Permissions`：

- `Code`
- `Name`
- `Group`
- `Description`
- `Resource`
- `Action`

### 4.4 EF Core 设计

- 使用 Code First。
- 每个实体单独使用 `IEntityTypeConfiguration<T>`。
- 通过全局查询过滤器处理 `TenantId` 和 `IsDeleted`。
- 通过 SaveChanges 拦截或重写统一写入审计字段。
- Repository 只负责数据访问，不承载业务判断。
- UnitOfWork 负责事务边界。

## 5. OAuth2 / OpenIddict 设计

### 5.1 授权模式

优先使用 OpenIddict 官方能力，不手写 JWT Token 服务。

支持授权模式：

- Password Flow：用于第一阶段管理后台登录，后续可逐步收敛。
- Refresh Token：用于前端静默续期。
- Client Credentials：用于服务间调用预留。
- Authorization Code + PKCE：用于后续标准化 Web 登录和第三方客户端。

### 5.2 Token 与 Claim

Access Token 推荐包含：

- `sub`：用户 ID
- `tenant_id`：租户 ID
- `name`：用户名或显示名
- `role`：角色编码
- `permission`：权限编码集合
- `scope`：授权范围

Refresh Token：

- 由 OpenIddict 管理生命周期。
- 前端只保存必要 token。
- 退出登录时撤销 refresh token。

### 5.3 服务端组件

后端认证授权组件：

- OpenIddict Server 配置
- OpenIddict Validation 配置
- ASP.NET Core Authentication
- ASP.NET Core Authorization
- 登录端点或 Token 端点集成
- Scope 初始化
- Client 初始化

安全约束：

- 生产环境必须使用正式证书。
- Token 生命周期通过配置管理。
- 密码存储只保存 Hash。
- 登录失败次数、锁定策略和审计日志分阶段加入。

## 6. RBAC 权限设计

### 6.1 权限模型

基础关系：

```text
User *..* Role
Role *..* Permission
Role *..* Menu
Menu 可绑定 PermissionCode
```

权限范围：

- 页面访问权限：由菜单和路由控制。
- 按钮操作权限：由 `PermissionCode` 和前端 `v-permission` 控制。
- API 访问权限：由 `PermissionAttribute` 和 `PermissionAuthorizationHandler` 控制。
- 数据权限：预留 `DataScope`，后续支持本人、本部门、本部门及下级、自定义范围。

### 6.2 后端权限校验

建议后端提供：

- `PermissionAttribute`
- `PermissionRequirement`
- `PermissionAuthorizationHandler`
- `ICurrentUser`
- `ICurrentTenant`

API 示例策略：

```text
[Permission("system:user:list")]
[Permission("system:role:create")]
[Permission("system:menu:update")]
```

校验流程：

1. 用户登录成功。
2. Application 层查询用户角色与权限。
3. OpenIddict 发放包含权限 Claim 的 token。
4. 请求进入 API。
5. Authorization Handler 从 Claims 读取权限编码。
6. 与接口要求的 PermissionCode 比对。

### 6.3 前端权限控制

前端登录后：

1. 保存 token。
2. 拉取当前用户信息。
3. 拉取菜单树和权限编码。
4. 生成动态路由。
5. 渲染侧边栏菜单。
6. 页面按钮通过 `v-permission` 判断显示。

## 7. 分阶段实现顺序

### Phase 0：仓库基础与约定

目标：

- 建立 README、环境配置示例、Docker Compose 规划。
- 明确后端、前端、文档、脚本目录职责。
- 固化开发、构建、运行命令。

交付物：

- `README.md`
- `.env.example`
- `docker-compose.yml`
- `docs/architecture.md`

验证命令：

```powershell
Get-ChildItem -Force
Get-ChildItem -Recurse backend
Get-ChildItem -Recurse frontend
```

### Phase 1：后端 Solution 骨架

目标：

- 创建 .NET 10 Solution。
- 创建 Api、Application、Domain、Infrastructure、Shared、Worker 项目。
- 配置项目引用关系。
- 接入 Swagger、Health Check、Serilog、全局异常处理中间件。

验证命令：

```powershell
dotnet --version
dotnet restore .\backend\PermissionSystem.sln
dotnet build .\backend\PermissionSystem.sln
dotnet run --project .\backend\PermissionSystem.Api\PermissionSystem.Api.csproj
```

### Phase 2：基础领域模型与 EF Core

目标：

- 建立 `BaseEntity`。
- 建立 User、Role、Menu、Permission、Tenant 等核心实体。
- 建立 DbContext、EntityTypeConfiguration。
- 建立 Repository、UnitOfWork。
- 接入 SQL Server。
- 生成首个 Migration。

验证命令：

```powershell
dotnet build .\backend\PermissionSystem.sln
dotnet ef migrations add InitialCreate --project .\backend\PermissionSystem.Infrastructure --startup-project .\backend\PermissionSystem.Api
dotnet ef database update --project .\backend\PermissionSystem.Infrastructure --startup-project .\backend\PermissionSystem.Api
```

### Phase 3：OpenIddict 认证

目标：

- 接入 OpenIddict Server 和 Validation。
- 初始化 scopes、clients。
- 实现登录、刷新、退出相关流程。
- 使用官方 OpenIddict Token 机制，不手写 JWT 服务。
- 建立当前用户和当前租户上下文。

验证命令：

```powershell
dotnet build .\backend\PermissionSystem.sln
dotnet run --project .\backend\PermissionSystem.Api\PermissionSystem.Api.csproj
Invoke-RestMethod -Method Post -Uri http://localhost:5000/connect/token
```

### Phase 4：RBAC 授权

目标：

- 实现角色、菜单、权限基础 Application Service。
- 实现 `PermissionAttribute`。
- 实现 `PermissionAuthorizationHandler`。
- 登录后写入角色和权限 Claims。
- 提供当前用户信息、菜单树、权限编码接口。

验证命令：

```powershell
dotnet build .\backend\PermissionSystem.sln
dotnet test .\backend\PermissionSystem.sln
Invoke-RestMethod -Headers @{ Authorization = "Bearer <access_token>" } -Uri http://localhost:5000/api/me
```

### Phase 5：前端基础框架

目标：

- 创建 Vue 3 + TypeScript + Vite 项目。
- 接入 Element Plus、Pinia、Vue Router、Axios。
- 实现基础 Layout、登录页、路由守卫。
- 实现 Axios token 注入、refresh token、401 跳转。

验证命令：

```powershell
cd .\frontend
npm install
npm run type-check
npm run build
npm run dev
```

### Phase 6：前端动态菜单与权限

目标：

- 实现用户状态 Store。
- 实现权限 Store。
- 实现动态菜单渲染。
- 实现动态路由生成。
- 实现 `v-permission` 指令。

验证命令：

```powershell
cd .\frontend
npm run type-check
npm run build
npm run dev
```

### Phase 7：系统管理页面

目标：

- 实现用户管理。
- 实现角色管理。
- 实现菜单管理。
- 实现权限管理。
- 页面默认包含查询栏、表格、分页、弹窗表单。

验证命令：

```powershell
dotnet test .\backend\PermissionSystem.sln
cd .\frontend
npm run type-check
npm run build
```

### Phase 8：部署与运维基础

目标：

- 完成 Docker Compose。
- 增加 SQL Server、Redis、Backend API、Frontend Nginx。
- 增加健康检查。
- 完善 README 启动说明。
- 增加 Serilog 请求日志、错误日志、审计日志说明。

验证命令：

```powershell
docker compose config
docker compose up -d
docker compose ps
Invoke-RestMethod -Uri http://localhost:5000/health
```

## 8. 推荐实施原则

- 每个阶段都必须保持可运行。
- 优先完成骨架和主流程，再补充细节能力。
- Controller 不写业务逻辑。
- Repository 不写业务逻辑。
- 不直接暴露 Entity。
- 不手写 JWT Token 服务。
- 不提前生成 ERP、WMS、财务、库存等业务模块。
- 每阶段完成后先构建和验证，再进入下一阶段。

