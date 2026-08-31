# PermissionSystem 移动端功能与技术架构方案

| 项目 | 内容 |
| --- | --- |
| 文档状态 | 已确认方案，供开发、DBA、测试和评审执行 |
| 适用范围 | 移动 H5 / PWA 首期，后续可封装 Android / iOS |
| 后端版本 | 复用当前 .NET 10、ASP.NET Core Web API、OpenIddict、EF Core 架构 |
| 前端版本 | 新增 Vue 3 + TypeScript + Vite 客户端，复用 Pinia、Axios、Vue Router 的工程模式 |
| API 版本 | 业务接口统一使用 `/api/v1/...`，协议端点仍使用 `/connect/...`、`/hubs/...` |
| 数据库结论 | MVP 不新增业务表、不新增 EF Core Migration |
| 维护人 | PermissionSystem 项目组 |

## [Architect] 1. 目标与设计原则

### 1.1 建设目标

为企业用户提供适合手机屏幕和碎片化办公场景的权限工作台，优先解决以下高频任务：

- 随时登录、切换租户、查看个人待办和通知。
- 在手机上查看业务单据、审批详情和流程轨迹。
- 完成同意、驳回、转交、加签、提交、撤回、取消等受授权操作。
- 通过拍照或文件选择上传业务附件。
- 在弱网和移动网络切换时保持可恢复、可重试和可解释的状态。

移动端是现有平台的一个新客户端，不是新的业务系统。用户、租户、角色、权限、审批规则、数据权限、审计和文件安全继续由现有后端负责。

### 1.2 非目标

- 不复制后台管理的全量 CRUD，不在移动端重新实现角色、菜单、租户、SSO 配置等治理页面。
- 不新建 JWT、用户中心或平行 RBAC 模型。
- 不把审批规则、状态机规则或租户过滤逻辑下沉到前端。
- 不因移动端需求拆分微服务；继续使用当前模块化单体。
- MVP 不承诺完整离线审批。离线仅用于草稿和查询缓存，所有写操作必须在线并由服务端最终确认。

### 1.3 已确认决策

1. 使用现有 `docs/` 目录维护方案和后续开发文档。
2. 首期交付移动 H5，并提供 PWA 安装、更新和离线壳能力。
3. 移动认证采用 Authorization Code + PKCE，不在客户端保存或分发 client secret。
4. MVP 纳入业务单据和附件能力。

## [Architect] 2. 现有项目能力与复用边界

当前仓库采用前后端分离、模块化单体和分层架构：

```text
移动 H5 / PWA
    |
    | HTTPS + Authorization Code/PKCE + Bearer
    v
PermissionSystem.Api
    | 认证、会话、租户、权限、限流、幂等、审计、TraceId
    v
Application（Platform / Identity / Workflow / Integration / Operations / Demo）
    |
Domain（实体、枚举、领域规则）
    |
Infrastructure（EF Core、SQL Server、Redis、文件、消息、Hangfire）
```

移动端需要遵守以下边界：

| 层级 | 移动端职责 | 禁止事项 |
| --- | --- | --- |
| App Shell | 启动、路由、生命周期、错误兜底、更新提示 | 不承载业务规则 |
| API / DTO | 请求封装、响应解包、取消、重试、幂等头 | 不直接在页面散落 Axios |
| Pinia Store | 会话、租户、权限、通知摘要、临时草稿 | 不缓存跨租户敏感数据 |
| Module Feature | 页面、交互、表单校验、状态展示 | 不决定服务端审批结果 |
| Backend Application | 用例编排、数据权限、状态变更、通知和审计 | 不依赖移动端实现 |
| Backend Domain | 领域规则和状态机 | 不依赖 HTTP、Vue 或移动 SDK |

## [Architect] 3. 客户端工程方案

### 3.1 工程位置与命名

新增工程：`frontend/permission-mobile`。

推荐目录：

```text
frontend/permission-mobile/
  public/
    manifest.webmanifest
    icons/
  src/
    api/                  # 按模块封装 /api/v1 和 /connect 请求
    components/           # 移动端通用列表、状态、上传、审批动作组件
    composables/          # usePermission、useTenant、useNetwork、useInfiniteList
    layouts/              # 登录布局、主工作台布局、详情布局
    router/               # 静态路由、鉴权守卫、深链处理
    stores/               # auth、tenant、permission、notification、draft
    views/                # login、home、workflow、orders、notifications、profile
    utils/                # request、token、secure-storage、idempotency、telemetry
    styles/               # 主题、暗色模式、可访问性和安全区域适配
  vite.config.ts
  package.json
  Dockerfile
  nginx.conf
  .env.example
```

### 3.2 技术选型

- Vue 3 `<script setup>` + TypeScript，保持现有前端编码方式。
- Vite 作为构建和本地开发工具，移动端单独构建产物，不与管理后台共享页面包。
- Pinia 管理会话、租户、权限和跨页面状态。
- Axios 复用现有拦截器行为：Bearer、401 刷新、强制下线、429、TraceId 展示和幂等键。
- Vue Router 负责登录守卫、权限守卫、深链和返回栈。
- 移动 UI 采用与 Vue 3 兼容的轻量移动组件库；实现前优先评审 Vant 4。若不引入组件库，使用项目内基础组件，避免重复引入多套视觉体系。
- PWA 使用标准 `manifest.webmanifest`、Service Worker 和缓存策略；是否引入 `vite-plugin-pwa` 需在依赖评审中确定。
- 不在移动端使用 Element Plus，避免桌面组件的触控、体积和交互问题。

### 3.3 响应式与交互约束

- 设计基准宽度 360px，兼容 320px 至 768px；内容区域使用安全区 inset。
- 主导航保持 4 个一级入口：工作台、待办、通知、我的；业务入口通过工作台快捷方式和权限菜单呈现。
- 列表采用分页或游标加载、下拉刷新、触底加载和骨架屏；禁止无限制一次性加载。
- 详情页固定展示状态、关键字段、流程轨迹和可用动作；动作按钮必须由权限和状态共同决定。
- 写操作提供提交中、成功、失败、可重试、幂等冲突和并发冲突状态。
- 文件选择支持相机、相册和文件管理器，但最终类型、大小、病毒扫描和 ACL 以服务端为准。

## [Architect] 4. 功能范围与优先级

### 4.1 MVP 功能

| 模块 | 功能 | 主要说明 |
| --- | --- | --- |
| 登录与会话 | 授权码登录、PKCE、刷新、撤销、退出 | 支持浏览器回调、会话失效和重新登录 |
| 租户 | 当前租户展示、可用租户切换 | 切换后清理旧租户缓存并重新加载权限 |
| 工作台 | 待办数、未读数、快捷入口、最近访问 | 只显示用户有权限且后端返回的模块 |
| 我的待办 | 分页列表、筛选、搜索、下拉刷新 | 复用 Workflow Task 查询和数据权限 |
| 审批详情 | 业务摘要、表单只读视图、流程轨迹、附件 | 详情内容按业务类型注册渲染器 |
| 审批动作 | 同意、驳回、转交、加签 | 复用服务端幂等、并发和权限校验 |
| 已办记录 | 已处理任务查询和详情 | 与待办共用列表组件和查询模型 |
| 通知 | 未读数、通知列表、标记已读、全部已读、删除 | MVP 默认轮询，SignalR 作为增强能力 |
| 业务单据 | 查询、详情、新建或编辑草稿、提交、撤回、取消 | 首期以现有 Demo Business/Approval Order 为样板 |
| 附件 | 查看、上传、失败重试、删除入口（按接口能力） | 使用现有 File 和业务附件授权 |
| 个人中心 | 资料、修改密码、当前会话、退出所有设备 | 复用 `/api/me` 用例和会话安全机制 |

### 4.2 P1 功能

- SignalR 实时通知和任务数更新，断线自动退回轮询。
- PWA 安装引导、版本更新提示、基础离线壳和已查看详情缓存。
- 相机扫码、拍照压缩、图片预览和上传队列。
- 移动端 SSO/OIDC 回调和企业微信/钉钉容器适配。
- 生物识别解锁本地加密存储、关键操作二次认证（Step-up）。
- 草稿本地保存、网络恢复后由用户主动重试；不自动执行审批动作。

### 4.3 P2 功能

- 报表移动只读视图和导出任务查看。
- AI 对话、业务单据草稿和执行确认移动入口，继续受 AI 权限和租户白名单控制。
- 面向 ERP/WMS 的独立业务模块移动页面。
- 推送通道适配 APNs、FCM、企业微信或钉钉；具体供应商另行评审。

## [Architect] 5. 信息架构与页面清单

```text
/login                       登录与 OAuth 回调
/authorize/callback          PKCE 授权码回调
/home                        工作台
/tasks/todo                  我的待办
/tasks/done                  已办记录
/tasks/:id                   审批任务详情
/notifications               通知中心
/orders                      业务单据列表
/orders/new                  新建单据草稿
/orders/:id                  单据详情
/orders/:id/edit             编辑草稿
/profile                     我的资料与安全
/sessions                    当前会话与设备
```

路由准入顺序：

1. 检查 access token 是否存在且未过期。
2. 无 token 时跳转 `/login`，保留原始深链。
3. 登录后加载 `/api/me`、`/api/me/menus`、`/api/me/permissions`。
4. 根据菜单和权限码过滤路由；无权限返回业务 403 页面。
5. 每次租户切换或授权状态失效后重新加载第 3 步数据。

## [Architect] 6. API 复用与接口契约

移动端新代码统一调用稳定版本路径。下表中的 `/api/...` 是控制器当前模板，实际客户端应使用版本化后的 `/api/v1/...` 路径；`/connect/...` 和 `/hubs/...` 不加业务版本前缀。

| 能力 | 客户端接口 | 权限 / 约束 | 移动端处理 |
| --- | --- | --- | --- |
| 授权登录 | `GET /connect/authorize`、`POST /connect/token` | OpenIddict、PKCE、回调 URI 白名单 | 使用系统浏览器或同源授权页，禁止内置 secret |
| 刷新与退出 | `POST /connect/token`、`POST /connect/revoke` | refresh token 绑定用户会话和 IP 策略 | 单飞刷新，失败清理本地凭据 |
| 当前用户 | `GET /api/v1/me` | Bearer、会话状态、租户上下文 | 启动时加载并缓存短时用户信息 |
| 菜单权限 | `GET /api/v1/me/menus`、`GET /api/v1/me/permissions` | 服务端权限策略 | 只用于导航和展示；服务端仍最终校验 |
| 个人资料 | `GET/PUT /api/v1/me/profile` | 登录用户自身 | 成功后刷新用户摘要 |
| 修改密码 | `PUT /api/v1/me/password` | 旧密码、密码策略 | 成功后按后端提示重新登录 |
| 会话管理 | `POST /api/v1/me/logout`、`POST /api/v1/me/logout-all` | 会话撤销和审计 | 退出当前设备或全部设备 |
| 通知 | `GET /api/v1/notifications/my`、`GET .../unread-count` | `system:notification:view` | 游标/分页、未读数合并去重 |
| 通知写操作 | `POST .../read`、`POST .../read-all`、`DELETE .../{id}` | 同上、幂等 | 写请求带 `X-Idempotency-Key` |
| 待办/已办 | `GET /api/v1/workflow/tasks/todo|done` | `workflow:task:todo` | 列表分页，详情按业务绑定加载 |
| 审批动作 | `POST /api/v1/workflow/tasks/{id}/approve|reject|transfer|add-sign` | 对应 workflow action 权限 | 必须提交意见、版本或并发标识（以 DTO 为准） |
| 业务单据 | `/api/v1/demo-business-orders`、`/api/v1/demo-approval-orders` | 业务权限码和数据权限 | 先支持现有样例，再抽象业务渲染器 |
| 附件 | 业务附件查询/上传端点、`/api/v1/files` 能力 | ACL、租户、文件安全策略 | 分片/断点续传不在 MVP 强制要求 |
| 实时通知 | `/hubs/notifications` | Bearer、SignalR 配置 | P1 开启，失败回退轮询 |

### 6.1 统一请求约定

- 所有业务请求携带 `Authorization: Bearer <access_token>`。
- 需要租户上下文的请求携带 `X-Tenant-Id`；token、退出和个人资料请求遵循现有例外规则。
- 所有非 GET/HEAD/OPTIONS 请求默认生成 `X-Idempotency-Key`，重试必须复用同一个 key。
- 请求携带客户端生成的 `X-Client-Version`、`X-Client-Platform` 和可选 `X-Trace-Parent`，具体是否接收由 API 网关评审确定。
- 统一处理 `401`、`403`、`409`、`422`、`429`、`5xx`：区分重新认证、无权限、并发冲突、表单校验、限流和暂时性故障。
- DTO、错误码、分页模型复用 `PermissionSystem.Shared` 的 `ApiResult`、`PagedResult` 和 `ErrorCode` 语义，不在客户端猜测响应结构。

## [Architect] 7. OAuth Authorization Code + PKCE 方案

### 7.1 客户端注册

新增独立的公共客户端 `permission-mobile`，不设置 client secret：

- `ClientType = public`，启用 Authorization、Token、Revocation、EndSession 端点。
- 只允许 Authorization Code、Refresh Token grant 和 `code` response type。
- 要求 Proof Key for Code Exchange，客户端每次登录生成高熵 `code_verifier` 和 `code_challenge`。
- Web H5/PWA 使用正式 HTTPS 回调，例如 `https://mobile.example.com/authorize/callback`。
- 本地开发回调使用明确登记的开发地址，不允许通配符。
- scope 最小化为 `openid profile offline_access api`（实际资源名以 `AiCenterConstants.ApiResource` 和 OpenIddict 配置为准）。
- 不把 `permission-admin` 的 confidential client 配置复制到移动端，不在 Vite 环境变量或 APK 中放置 secret。

当前后端已配置 `/connect/authorize`、`/connect/token`、PKCE 要求和 Authorization Code flow；但现有 `ConnectController` 的 token 处理重点覆盖 password、refresh、client credentials。实施阶段必须验证授权端点的登录、同意页、用户会话创建、回调和错误处理是否完整，必要时在 Api/Application 按现有 SSO 模式补齐，不得由移动端绕过授权流程。

### 7.2 H5/PWA 登录时序

```text
移动端生成 state + code_verifier
        |
        v
浏览器访问 /connect/authorize?client_id=permission-mobile
        |
用户认证、租户选择、同意授权
        |
回调 /authorize/callback?code=...&state=...
        |
校验 state，使用 code_verifier POST /connect/token
        |
获得 access_token + refresh_token，加载 /api/v1/me*
```

安全要求：

- `state`、`code_verifier` 只保存在短时 session storage；回调成功或失败后立即清除。
- 回调必须校验 issuer、state、错误码和当前浏览器会话，防止登录 CSRF 和授权码注入。
- access token 仅保存在内存或受控短时存储；refresh token 使用 Web Crypto 加密后存储，并在支持的平台使用系统安全存储。
- 退出时先调用 revoke，再清理本地 token；网络不可用时标记待撤销状态并禁止继续使用旧 token。
- 401 且响应含 `x-session-revoked=true` 时立即清理 token 并回登录页，沿用现有前端行为。

## [Architect] 8. 权限、租户与业务状态

### 8.1 权限模型

- 登录后从 `/api/v1/me/permissions` 获取权限码，从 `/api/v1/me/menus` 获取可见导航。
- 客户端通过 `usePermission(code)` 和 `canAny(codes)` 控制按钮、快捷入口和动作区域。
- 权限码必须复用种子数据和后端 `[Permission("...")]` 的值，例如 `workflow:task:approve`、`demo-business-order:submit`。
- 客户端隐藏按钮不等于授权；所有写接口仍由 API 授权处理。
- 403 页面需展示 trace id 和重新加载授权入口，不能通过重试绕过权限。

### 8.2 租户隔离

- token 中的租户声明、`X-Tenant-Id` 和后端 `ITenantContext` 共同决定请求上下文，客户端不能自行扩大租户范围。
- 切换租户前确认未提交草稿，切换成功后清理旧租户的列表、详情、未读数和本地草稿缓存。
- 所有本地缓存 key 必须包含 `tenantId + userId + resource`，防止同设备多租户串数据。
- 租户被禁用、会话失效或授权状态过期时，停止后台轮询并提示用户。

### 8.3 审批和单据状态

客户端只展示服务端返回的状态和动作集合：

```text
草稿 -> 已提交 -> 审批中 -> 已通过
                   |          |
                   v          v
                 已驳回     已撤回/已取消
```

状态转换、条件分支、会签/或签、超时、转交、加签、通知和审计均由 Workflow Application/Domain 决定。客户端提交动作后必须重新拉取详情和待办列表，不能本地乐观改写最终状态。

## [DBA] 9. 数据库与基础设施评审

### 9.1 MVP 结论：无数据库变更

MVP 直接复用现有表和服务：

- OpenIddict 应用、授权、令牌表。
- 用户会话、登录日志和操作日志。
- 租户、用户、角色、菜单、权限和数据权限表。
- 工作流定义、实例、任务、抄送和业务绑定表。
- 通知、文件资源、业务附件和 Outbox/Inbox 表。

不新增实体、不新增迁移、不修改既有索引。移动端身份由 `permission-mobile` OpenIddict application 配置表达，不创建第二套用户表。

### 9.2 P1/P2 可能的数据变更

以下能力需要独立 DBA 评审后才能实现：

| 能力 | 可能实体 | 必审字段和约束 |
| --- | --- | --- |
| 推送订阅 | `PushSubscription` | `TenantId`、`UserId`、端点唯一性、撤销时间、加密密钥 |
| 设备绑定 | `DeviceSession` | 设备指纹哈希、会话关联、最后活动、撤销和并发控制 |
| 离线同步 | `MobileSyncCursor` | 用户/租户/资源维度、游标单调性、过期和重放 |
| 上传队列 | 优先使用客户端本地存储 | 不把未完成文件内容直接写入数据库 |

所有新增领域实体必须继承 `BaseEntity`，保留租户、审计和软删除字段；索引必须覆盖租户和查询主键，不能引入跨租户唯一约束错误。RabbitMQ、Redis、Hangfire 和文件存储继续复用现有基础设施，不为移动端单独部署一套消息或缓存系统。

## [Developer] 10. 预期代码变更清单

本文件是设计方案，以下为后续实施时的最小变更边界：

### 10.1 后端

- 在 SeedData/配置中新增 `permission-mobile` public client、正式/开发回调 URI 和最小 scope。
- 按现有 OpenIddict/Sso 模式完成或校验 `/connect/authorize` 的登录、租户上下文、同意和错误回调。
- 保持 `Application` 用例和 `Domain` 规则不变；只有当移动端需要缺失的聚合查询时，新增公开 DTO/Contracts。
- 如现有业务接口缺少移动详情所需字段，增加向后兼容的可选响应字段或专用查询用例，不复制实体到 Api。
- 通过现有权限常量、审计、幂等、限流和并发控制覆盖新增端点。
- 更新 OpenAPI v1 并执行 `scripts/check-openapi-breaking.ps1`。

### 10.2 前端

- 新建 `frontend/permission-mobile`，实现 request、token、OAuth PKCE、路由守卫、Pinia stores 和页面模块。
- 从 `permission-admin` 提取可复用的协议逻辑时采用复制后收敛或共享纯工具包的方式，避免移动端依赖 Element Plus 布局。
- 增加 PWA manifest、Service Worker、版本号和缓存失效策略。
- 增加移动端 API DTO 类型，不在页面直接使用后端实体形状。
- 为列表、审批动作、上传、弱网重试和深链补充单元与 E2E 测试。

### 10.3 部署

- 新增移动端 Dockerfile 和 Nginx 配置，静态资源单独托管。
- Nginx 仅代理 `/api/`、`/connect/`、`/hubs/` 和健康检查，沿用 API 的 TLS、CORS、CSP 和安全响应头策略。
- 生产通过环境变量提供 API issuer、client id、回调基址和版本信息；不提供 client secret。
- 发布时先部署后端 OAuth client 和回调白名单，再发布前端，避免登录回调短暂失效。

## [Reviewer] 11. 安全、可靠性与可观测性检查项

### 11.1 安全检查

- PKCE `S256`、state、issuer、redirect URI 严格校验。
- 不记录 access token、refresh token、授权码、密码或上传文件内容。
- Token、租户和用户缓存按用户/租户隔离，退出和强制下线立即失效。
- 文件上传校验扩展名、MIME、大小、病毒扫描、ACL 和租户归属；客户端预览使用受控 URL。
- 关键审批动作按现有 Step-up Authentication 策略触发二次认证。
- PWA 设置 CSP、Permissions-Policy、X-Frame-Options/同等策略和安全 cookie 属性。
- 限制深链可访问路径，防止未授权页面在 token 恢复前闪现敏感数据。

### 11.2 可靠性检查

- 所有写操作显示明确的提交中状态并禁用重复点击。
- 网络超时只对幂等请求自动重试；审批、提交、撤回等动作必须复用幂等 key 后由用户确认重试。
- 收到 `409` 时重新加载详情，向用户解释数据已被其他人处理。
- 轮询使用退避和页面可见性控制，后台不可见时降低频率。
- Service Worker 只缓存静态资源和明确允许的只读数据，不缓存 token、审批写请求和跨租户响应。

### 11.3 可观测性

- 使用服务端返回的 trace id 串联登录、列表、审批、上传和通知请求。
- 客户端采集版本、平台、网络类型、页面耗时、错误码和重试次数，不采集业务敏感字段。
- API 继续使用 Serilog、健康检查、OpenTelemetry/现有指标和操作日志。
- 监控 OAuth 回调失败率、token 刷新失败率、审批动作 409/403 比例、附件上传失败率和 PWA 版本分布。

## [Developer] 12. 测试与验收方案

### 12.1 自动化测试

| 层级 | 覆盖内容 |
| --- | --- |
| 前端单元 | PKCE state 校验、token 刷新单飞、租户缓存 key、权限判断、分页合并、上传重试 |
| API 集成 | public client + PKCE、回调错误、refresh/revoke、租户切换、401/403/409/429 |
| 后端单元 | 现有授权处理器、Workflow 动作、文件 ACL、通知读取和幂等行为不回归 |
| E2E | 登录深链、首页加载、待办审批、业务单据提交/撤回、通知已读、附件上传 |
| 安全 | token 泄露检查、跨租户访问、越权动作、重放、错误回调、CSP 和安全头 |
| 兼容性 | 320/360/390/414/768 宽度、iOS Safari、Android Chrome、桌面浏览器 PWA |
| 弱网 | 延迟、断网、网络切换、重复提交、刷新 token 失败和 Service Worker 更新 |

### 12.2 验收标准

- 未登录访问任何业务深链都会回到登录，成功后可回原页面。
- 授权码只能使用一次，错误 state、错误 redirect URI 和过期 code 均被拒绝。
- 任何移动端写操作都能在服务端审计中看到用户、租户、trace id 和结果。
- 无权限用户看不到对应入口，直接调用接口返回 403。
- 待办动作在重复点击、刷新、网络重试和并发处理下不会产生重复业务结果。
- 业务单据和附件严格遵循现有数据权限、租户隔离、文件 ACL 和软删除规则。
- PWA 离线时只展示允许的缓存内容，恢复联网后不会自动执行审批或提交。
- API v1 OpenAPI 兼容性检查通过，现有管理后台接口行为不回归。

## [Architect] 13. 里程碑与交付顺序

| 阶段 | 产出 | 完成条件 |
| --- | --- | --- |
| M0 基线 | API 契约、OAuth client、回调 URI、设计稿和测试数据 | PKCE 登录链路在测试环境闭环 |
| M1 骨架 | 移动工程、主题、路由、request、Pinia、PWA 壳 | 能安装、启动、登录并加载当前用户 |
| M2 工作台 | 首页、租户、权限菜单、待办/已办、通知 | 列表分页、未读数和错误态完整 |
| M3 审批 | 任务详情、流程轨迹、同意/驳回/转交/加签 | 幂等、并发、审计和权限测试通过 |
| M4 单据 | 单据列表、详情、草稿、提交/撤回/取消、附件 | 业务权限、文件 ACL、失败重试通过 |
| M5 稳定性 | 弱网、PWA 更新、监控、安全头和兼容性 | E2E、性能、安全验收通过 |
| M6 发布 | Docker/Nginx、生产 OAuth 配置、运维手册 | 灰度发布和回滚演练完成 |

建议先以现有 Demo Business Order / Demo Approval Order 验证端到端链路，再按相同 Contracts 接入 ERP/WMS 业务模块，避免在移动端为每个业务复制一套审批实现。

## [Reviewer] 14. 风险与待决策项

### 14.1 当前风险

1. 现有 OpenIddict 已启用授权码和 PKCE，但授权端点的移动登录/同意交互需要实际联调确认；若缺失，必须补齐后才能发布移动端。
2. 当前管理后台种子客户端 `permission-admin` 是 confidential client，不能直接用于移动端；`permission-mobile` 的回调白名单和生命周期必须单独管理。
3. 现有部分控制器源码使用 `api/...` 模板，客户端必须依赖最终生成的 `/api/v1/...` 稳定路径，并在 OpenAPI 中验证真实路由。
4. PWA 的推送、后台同步和系统安全存储受浏览器能力限制，不能把它们当作 MVP 的强一致能力。
5. 业务附件的大小、类型、扫描和预览策略由后端配置决定，移动端只能提供友好入口，不能放宽服务端限制。

### 14.2 开发前需要锁定的配置

- 生产移动端域名、HTTPS 证书和授权回调 URI。
- `permission-mobile` 的 client id、允许 scope、授权同意策略和 refresh token 生命周期。
- 是否允许企业微信/钉钉内嵌浏览器完成 OAuth，以及对应回调限制。
- PWA Service Worker 的更新窗口、缓存上限和强制更新策略。
- P1 推送供应商和隐私合规要求。

## [Reviewer] 15. 结论

本方案在现有 PermissionSystem 分层和模块边界内增加一个移动客户端，不改变现有领域模型和权限体系。首期以 H5/PWA 聚焦“登录、工作台、审批、通知、业务单据、附件和个人安全”，后续能力通过公开 API Contracts、领域事件和现有基础设施演进。

MVP 的主要前置条件是完成 `permission-mobile` public client 的 Authorization Code + PKCE 闭环验证，以及确认所有移动端依赖的业务接口均已通过 `/api/v1` OpenAPI 校验。除此之外，MVP 无数据库迁移要求，不影响现有管理后台和 Worker 部署拓扑。
