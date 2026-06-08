# SSO 单点登录接入设计

## 当前实现与联调补充

### 当前已实现范围

- 已保留本地账号密码登录，`admin` 本地登录仍作为兜底入口。
- 已新增 SSO 基础实体、EF Core 映射和迁移：`SsoProvider`、`SsoUserBinding`、`SsoRoleMapping`、`SsoDepartmentMapping`、`SsoLoginLog`。
- 已实现 SSO Provider 管理接口、用户绑定查询/解绑接口、角色映射接口、部门映射接口和 SSO 登录日志接口。
- 已实现 OIDC Authorization Code 登录流程，默认启用 PKCE。
- 已实现一次性 `login_code` 换取本系统 OpenIddict token，不在 URL 暴露 `access_token` 或 `refresh_token`。
- 已实现前端登录页 SSO 按钮、SSO callback 页和后台管理页面。
- SAML2 和通用 OAuth2 当前仅作为 ProviderType、表结构和页面字段预留，尚未实现登录流程。

### 当前 OIDC 回调地址

当前后端 OIDC callback 路径为：

```text
{BackendBaseUrl}/api/sso/oidc/{providerCode}/callback
```

示例：

```text
http://localhost:5264/api/sso/oidc/KEYCLOAK/callback
https://api.example.com/api/sso/oidc/ENTRA/callback
```

外部 IdP 中配置的 redirect URI 必须与后端实际可访问地址完全一致，包括协议、域名、端口、路径和大小写。生产环境必须使用 HTTPS。

前端 callback 路径为：

```text
{FrontendBaseUrl}/sso/callback
```

后端 OIDC callback 成功后只把短期一次性 `login_code` 重定向到前端 callback，由前端再调用 `/api/sso/oidc/exchange` 换取本系统 token。

### Keycloak 配置参考

1. 在 Keycloak 中创建或选择 Realm。
2. 创建 OIDC Client，Client ID 填写本系统 `ClientId`。
3. Client 类型可使用 confidential。
4. Valid redirect URIs 填写 `{BackendBaseUrl}/api/sso/oidc/{providerCode}/callback`。
5. 启用 Authorization Code Flow，建议启用 PKCE，PKCE method 使用 `S256`。
6. 本系统 Provider 建议配置：
   - `Authority`: `https://idp.example.com/realms/{realm}`
   - `Scopes`: `openid profile email`
   - `UserIdClaim`: `sub`
   - `UserNameClaim`: `preferred_username`
   - `EmailClaim`: `email`
   - `RoleClaim`: `roles` 或 Keycloak 映射出的自定义 claim

### Microsoft Entra ID 配置参考

1. 在 Microsoft Entra admin center 注册应用。
2. 平台类型选择 Web。
3. Redirect URI 填写 `{BackendBaseUrl}/api/sso/oidc/{providerCode}/callback`。
4. 创建 Client secret，并只在本系统 SSO Provider 表单中录入一次。
5. API permissions 至少授予 `openid`、`profile`、`email`；如需读取用户组或目录信息，需要按组织策略额外授权。
6. 本系统 Provider 建议配置：
   - `Authority`: `https://login.microsoftonline.com/{tenantId}/v2.0`
   - `Scopes`: `openid profile email`
   - `UserIdClaim`: `sub`
   - `UserNameClaim`: `preferred_username`
   - `EmailClaim`: `email` 或 `preferred_username`
   - `RoleClaim`: 使用 App roles 时通常为 `roles`

### Authing 配置参考

1. 在 Authing 控制台创建 OIDC 应用。
2. 登录回调 URL 填写 `{BackendBaseUrl}/api/sso/oidc/{providerCode}/callback`。
3. 授权模式选择 Authorization Code。
4. 启用 PKCE，建议使用 `S256`。
5. 本系统 Provider 建议配置：
   - `Authority` 或 `MetadataAddress`: 使用 Authing 应用提供的 OIDC issuer/discovery 地址
   - `Scopes`: `openid profile email phone`
   - `UserIdClaim`: `sub`
   - `UserNameClaim`: `username`、`preferred_username` 或实际返回 claim
   - `EmailClaim`: `email`
   - `PhoneClaim`: `phone_number`

### 用户绑定策略

登录时按以下顺序解析本地用户：

1. `providerId + externalUserId` 查找 `SsoUserBinding`。
2. 未绑定且 Provider 启用 `AutoBindUser` 时，按 email 匹配本地用户。
3. email 未命中时按 phone 匹配。
4. phone 未命中时按 userName 匹配。
5. 仍未命中且 Provider 启用 `AutoCreateUser` 时自动创建本地用户。
6. 本地用户禁用、租户禁用、匹配到多个本地用户时拒绝登录并写入 SSO 登录日志。

### 角色和部门映射策略

- 外部角色来自 Provider 的 `RoleClaim`，支持逗号、分号、竖线和换行分隔。
- 命中 `SsoRoleMapping` 时给用户补充对应本地角色。
- 无有效角色映射时使用 Provider 的 `DefaultRoleIds`。
- 自动流程禁止赋予 `SuperAdmin`。后台角色映射页面也过滤 `SuperAdmin`，后端服务再次拦截。
- 外部部门来自 Provider 的 `DepartmentClaim`。
- 命中 `SsoDepartmentMapping` 时更新用户 `DepartmentId`。
- 无部门映射时不覆盖用户已有本地部门。

### 常见问题

- 登录页没有 SSO 按钮：确认 Provider 已启用、ProviderType 为 OIDC、当前数据库 SeedData 已执行、前端能访问 `/api/sso/providers/enabled`。
- IdP 提示 redirect URI 不匹配：确认 IdP 中配置的是后端 callback 地址，不是前端 `/sso/callback`。
- callback 后回到登录页：检查 SSO 登录日志中的失败原因、`state` 是否过期、IdP 是否返回了 `error`。
- nonce 校验失败：确认使用同一次 challenge 生成的授权请求，不要重复刷新或手工拼接 callback。
- login_code 交换失败：`login_code` 只能使用一次，且有效期约 3 分钟。
- SSO 成功但菜单为空：确认本地用户拥有角色，角色拥有菜单和权限；普通用户重新登录后才会拿到最新 token claims。
- 无法自动创建用户：确认 Provider `AutoCreateUser` 已开启，且外部 `UserIdClaim` 能正确解析。
- 角色映射没有生效：确认外部 token/userinfo 中 role claim 名称与 Provider 配置一致，且映射到的本地角色已启用。
- 不能配置 SuperAdmin 映射：这是安全限制，SuperAdmin 只能通过本地手工授权流程分配。
- ClientSecret 看不到明文：这是预期行为。管理接口只返回脱敏值，编辑时可重新输入新 secret。

本文基于当前 `PermissionSystem` 项目、AGENTS.md 约束，以及现有 OpenIddict / OAuth2 登录体系，规划“本系统作为业务系统接入外部统一身份源”的 SSO 能力。本文只做设计，不包含代码实现。

## 1. 设计目标

### 1.1 目标

让用户可以通过外部统一身份源登录本系统，并在外部身份校验通过后，绑定或创建本地用户，再由本系统继续签发自己的 `AccessToken` 和 `RefreshToken`。

核心目标：

- 保留现有本地账号密码登录，不改变 `/connect/token` 密码模式、刷新令牌、客户端凭证等已有能力。
- 优先实现 OIDC SSO，使用标准 OpenID Connect Authorization Code + PKCE 流程。
- SAML2 仅做模型和扩展点预留，后续按独立阶段落地。
- 外部身份只作为登录入口和身份来源，本系统仍负责本地用户、角色、菜单、权限、数据范围和审计。
- 不实现自定义 JWT 服务，不绕过 OpenIddict 令牌签发体系。

### 1.2 当前系统约束

当前认证链路主要特征：

- OpenIddict Server 已提供 `/connect/token`、`/connect/revoke`、`/connect/logout`。
- 当前支持 Password、Refresh Token、Client Credentials、Authorization Code + PKCE。
- 登录后 access token claims 包含：
  - `sub`
  - `user_id`
  - `user_name`
  - `tenant_id`
  - `department_id`
  - `session_id`
  - `access_token_id`
  - `refresh_token_id`
  - role claims
  - `permission_code`
- 当前已有 `LoginLog`、`UserSession`、`Tenant`、`Role`、`Department`、`Permission`、`IConfigValueProtector` 等能力可复用。

SSO 登录完成后，应复用上述 token、claims、会话和审计语义，避免产生平行认证体系。

## 2. 支持的登录模式

### 2.1 本地账号密码登录

继续保留现有模式：

- 前端使用用户名和密码登录。
- 后端使用 OpenIddict Password Flow 处理。
- 登录日志 `LoginType` 继续记录为 `password`。
- 默认 `admin` 本地账号必须保留为兜底入口。

约束：

- SSO 配置错误、外部 IdP 不可用、回调异常时，不影响本地账号密码登录。
- 不允许因开启 SSO 而禁用默认管理员的本地登录能力。

### 2.2 OIDC 单点登录

优先实现 OIDC：

- 使用外部 IdP 的 Authorization Code Flow。
- 启用 PKCE。
- 校验 `state`、`nonce`、issuer、audience、signature、过期时间。
- 从外部 IdP 获取 `id_token`，必要时调用 `userinfo_endpoint` 补充用户信息。
- 外部身份校验成功后，绑定或创建本地用户。
- 最终由本系统签发自己的 access token 和 refresh token。

推荐支持的 OIDC 配置：

- `Authority`
- `MetadataAddress`
- `ClientId`
- `ClientSecret`
- `ResponseType = code`
- `Scope = openid profile email phone`
- `CallbackPath`
- `SignedOutCallbackPath`
- `RequireHttpsMetadata`
- `UsePkce`
- `ClaimMappings`

### 2.3 SAML2 单点登录预留

SAML2 后续预留，不在第一阶段实现完整登录。

预留方向：

- `SsoProvider.Protocol` 支持 `Saml2`。
- Provider 中预留 SAML 元数据字段。
- 用户绑定表使用通用外部身份字段，不绑定 OIDC 特有命名。
- 登录日志记录 `Protocol`，为后续 SAML2 复用。

SAML2 预留字段包括：

- `MetadataUrl`
- `EntityId`
- `SsoUrl`
- `Certificate`
- `NameIdFormat`
- `AttributeMappingsJson`

## 3. SSO 总体流程

### 3.1 标准 OIDC 登录流程

```text
----------+       +-------------+        +-------------+        +----------------+
| Frontend |       | Backend API |        | External IdP |        | Permission DB  |
+----------+       +-------------+        +-------------+        +----------------+
     |                    |                       |                       |
     | 点击 SSO 登录       |                       |                       |
     | GET /api/sso/login |                       |                       |
     |------------------->|                       |                       |
     |                    | 解析 tenant/provider  |                       |
     |                    | 生成 state/nonce/PKCE |                       |
     |                    |---------------------->|                       |
     |                    |      OIDC Challenge   |                       |
     |<-------------------|                       |                       |
     | 跳转 IdP 登录       |                       |                       |
     |------------------------------------------->|                       |
     |                    |                       | 用户完成认证           |
     |                    |<----------------------|                       |
     |                    | OIDC callback         |                       |
     |                    | 校验 code/state/nonce |                       |
     |                    | 换取 token/userinfo   |                       |
     |                    | 校验外部身份           |                       |
     |                    | 查询/创建绑定          |---------------------->|
     |                    | 查询/创建本地用户       |---------------------->|
     |                    | 同步角色/部门映射       |---------------------->|
     |                    | 创建本地 UserSession   |---------------------->|
     |                    | 写入 SSO 登录日志       |---------------------->|
     |                    | 生成本系统 token        |                       |
     |<-------------------| 回跳前端 callback 页面 |                       |
     | 保存 token          |                       |                       |
     | 进入系统            |                       |                       |
```

### 3.2 关键步骤说明

1. 前端点击 SSO 登录。
   - 登录页展示一个或多个 SSO Provider 按钮。
   - 如果只有一个启用的 Provider，可以直接进入 SSO。
   - 如果存在多租户或多个 Provider，需要传递 `tenantId`、`tenantCode` 或 `providerCode`。

2. 后端发起 OIDC challenge。
   - 后端根据租户和 Provider 读取配置。
   - 校验 Provider 是否启用、租户是否启用。
   - 生成并保存 `state`、`nonce`、`code_verifier`。
   - 跳转外部 IdP 授权地址。

3. 外部 IdP 登录。
   - 用户在外部统一身份源完成登录和必要 MFA。
   - 外部 IdP 回调本系统。

4. 回调本系统。
   - 后端处理 `code`、`state`、`error` 等参数。
   - 校验 `state` 防 CSRF。
   - 使用 `code_verifier` 换取 token。
   - 校验 `id_token` 的签名、issuer、audience、nonce、过期时间。

5. 校验外部身份。
   - 解析外部用户唯一标识。
   - 读取 email、phone、userName、displayName、groups、roles、department 等 claims。
   - 根据 Provider 的 claim 映射规则转换为本系统统一外部身份模型。

6. 绑定或创建本地用户。
   - 优先使用 `SsoUserBinding` 查找绑定。
   - 未绑定时按 email、phone、userName 顺序匹配本地用户。
   - 如允许自动创建，则创建本地用户。
   - 禁用用户、软删除用户、禁用租户均不能登录。

7. 生成本系统 AccessToken / RefreshToken。
   - 由本系统创建 `UserSession`。
   - 读取本地用户角色、权限、部门。
   - 写入与本地登录一致的 claims。
   - 使用 OpenIddict 签发本系统 token。

8. 进入本系统。
   - 后端回跳前端 SSO callback 页面。
   - 前端保存 token，拉取用户信息、菜单和权限。
   - 跳转首页或原始目标地址。

## 4. 数据库设计

所有新增实体默认继承 `BaseEntity`，保留 `TenantId`、审计字段和软删除能力。

### 4.1 SsoProvider

SSO 身份源配置表。每个租户可配置一个或多个 Provider。

建议字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Id | Guid | 主键 |
| TenantId | Guid | 租户 ID |
| ProviderCode | string | Provider 编码，租户内唯一 |
| ProviderName | string | Provider 名称 |
| Protocol | string | `Oidc`、`Saml2` |
| IsEnabled | bool | 是否启用 |
| IsDefault | bool | 是否租户默认 Provider |
| Authority | string? | OIDC authority |
| MetadataAddress | string? | OIDC discovery 地址 |
| ClientId | string? | OIDC client id |
| ClientSecretEncrypted | string? | 加密后的 client secret |
| CallbackPath | string? | 本系统回调路径 |
| SignedOutCallbackPath | string? | 登出回调路径 |
| Scopes | string | scope 列表，建议空格分隔 |
| RequireHttpsMetadata | bool | 是否要求 HTTPS metadata |
| UsePkce | bool | 是否启用 PKCE，OIDC 默认 true |
| ClaimMappingsJson | string? | claim 映射配置 |
| DefaultRoleId | Guid? | 无角色映射时默认角色 |
| AutoCreateUser | bool | 是否允许自动创建用户 |
| AutoBindUser | bool | 是否允许按 email/phone/userName 自动绑定 |
| SyncRolesOnLogin | bool | 登录时是否同步角色 |
| SyncDepartmentsOnLogin | bool | 登录时是否同步部门 |
| SamlMetadataUrl | string? | SAML2 预留 |
| SamlEntityId | string? | SAML2 预留 |
| SamlSsoUrl | string? | SAML2 预留 |
| SamlCertificateEncrypted | string? | SAML2 预留 |
| Description | string? | 备注 |

索引建议：

- 唯一索引：`TenantId + ProviderCode`
- 普通索引：`TenantId + Protocol + IsEnabled`
- 普通索引：`TenantId + IsDefault`

### 4.2 SsoUserBinding

外部身份与本地用户绑定表。

建议字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Id | Guid | 主键 |
| TenantId | Guid | 租户 ID |
| ProviderId | Guid | SSO Provider ID |
| UserId | Guid | 本地用户 ID |
| ExternalUserId | string | 外部用户唯一 ID，OIDC 通常来自 `sub` |
| ExternalUserName | string? | 外部用户名 |
| ExternalEmail | string? | 外部邮箱 |
| ExternalPhone | string? | 外部手机号 |
| ExternalDisplayName | string? | 外部显示名 |
| LastLoginAt | DateTimeOffset? | 最近 SSO 登录时间 |
| LastSyncAt | DateTimeOffset? | 最近同步时间 |
| IsEnabled | bool | 是否启用绑定 |
| BindingSource | string | `Manual`、`ExternalUserId`、`Email`、`Phone`、`UserName`、`AutoCreate` |
| RawClaimsJson | string? | 最近一次外部 claims 摘要，需脱敏 |

索引建议：

- 唯一索引：`TenantId + ProviderId + ExternalUserId`
- 唯一索引：`TenantId + ProviderId + UserId`
- 普通索引：`TenantId + UserId`
- 普通索引：`TenantId + ExternalEmail`
- 普通索引：`TenantId + ExternalPhone`

### 4.3 SsoRoleMapping

外部 group / role 到本地角色的映射表。

建议字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Id | Guid | 主键 |
| TenantId | Guid | 租户 ID |
| ProviderId | Guid | SSO Provider ID |
| ExternalRoleType | string | `group`、`role`、`claim` |
| ExternalRoleValue | string | 外部 group/role 值 |
| RoleId | Guid | 本地角色 ID |
| IsEnabled | bool | 是否启用 |
| Priority | int | 优先级 |
| Description | string? | 备注 |

索引建议：

- 唯一索引：`TenantId + ProviderId + ExternalRoleType + ExternalRoleValue + RoleId`
- 普通索引：`TenantId + ProviderId + IsEnabled`

### 4.4 SsoDepartmentMapping

外部部门到本地部门的映射表。

建议字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Id | Guid | 主键 |
| TenantId | Guid | 租户 ID |
| ProviderId | Guid | SSO Provider ID |
| ExternalDepartmentId | string? | 外部部门 ID |
| ExternalDepartmentCode | string? | 外部部门编码 |
| ExternalDepartmentName | string? | 外部部门名称 |
| DepartmentId | Guid | 本地部门 ID |
| IsEnabled | bool | 是否启用 |
| Priority | int | 优先级 |
| Description | string? | 备注 |

索引建议：

- 普通索引：`TenantId + ProviderId + ExternalDepartmentId`
- 普通索引：`TenantId + ProviderId + ExternalDepartmentCode`
- 普通索引：`TenantId + DepartmentId`

### 4.5 SsoLoginLog

SSO 登录审计日志。

建议字段：

| 字段 | 类型 | 说明 |
| --- | --- | --- |
| Id | Guid | 主键 |
| TenantId | Guid | 租户 ID |
| ProviderId | Guid? | Provider ID |
| ProviderCode | string? | Provider 编码 |
| Protocol | string | `Oidc`、`Saml2` |
| UserId | Guid? | 本地用户 ID |
| UserName | string? | 本地用户名 |
| ExternalUserId | string? | 外部用户 ID |
| ExternalUserName | string? | 外部用户名 |
| LoginResult | string | `Succeeded`、`Failed` |
| FailureReason | string? | 失败原因 |
| BindingSource | string? | 绑定来源 |
| IpAddress | string | IP |
| UserAgent | string | User-Agent |
| TraceId | string | TraceId |
| StateId | string? | state 关联 ID，不保存完整敏感值 |
| CreatedAt | DateTimeOffset | 创建时间 |

说明：

- 可继续复用现有 `LoginLog`，将 `LoginType` 记录为 `sso-oidc`。
- `SsoLoginLog` 更适合记录外部身份、Provider 和绑定细节。
- 第一阶段可同时写入 `LoginLog` 和 `SsoLoginLog`，便于统一登录审计和 SSO 专项审计。

## 5. 后端接口设计

所有管理接口返回 `ApiResult` 或 `PagedResult`，不直接暴露 Entity。所有接口使用 `async/await` 和 `CancellationToken`。

### 5.1 SSO Provider 管理

建议 Controller：

- `SsoProviderController`
- 路由前缀：`/api/sso/providers`

接口：

| 方法 | 路径 | 说明 | 权限码 |
| --- | --- | --- | --- |
| GET | `/api/sso/providers` | 分页查询 Provider | `system:sso-provider:view` |
| GET | `/api/sso/providers/{id}` | 查询 Provider 详情 | `system:sso-provider:view` |
| POST | `/api/sso/providers` | 创建 Provider | `system:sso-provider:create` |
| PUT | `/api/sso/providers/{id}` | 更新 Provider | `system:sso-provider:update` |
| DELETE | `/api/sso/providers/{id}` | 删除 Provider | `system:sso-provider:delete` |
| POST | `/api/sso/providers/{id}/enable` | 启用 Provider | `system:sso-provider:enable` |
| POST | `/api/sso/providers/{id}/disable` | 禁用 Provider | `system:sso-provider:disable` |
| POST | `/api/sso/providers/{id}/test` | 测试 OIDC metadata 和基础配置 | `system:sso-provider:test` |
| POST | `/api/sso/providers/{id}/rotate-secret` | 轮换 ClientSecret | `system:sso-provider:update` |

Provider 详情返回时：

- 不返回明文 `ClientSecret`。
- 返回 `HasClientSecret` 表示是否已配置。
- SAML 证书等敏感内容同样脱敏。

### 5.2 SSO 登录入口

建议 Controller：

- `SsoAuthController`
- 路由前缀：`/api/sso`

接口：

```http
GET /api/sso/login?tenantId={tenantId}&providerCode={providerCode}&returnUrl={returnUrl}
```

说明：

- `tenantId`、`tenantCode`、`providerCode` 至少应能解析到唯一启用的 Provider。
- `returnUrl` 只能允许本系统前端白名单路径，不能允许任意外部 URL。
- 该接口返回 Challenge 或 Redirect，不返回普通 JSON。

可选接口：

```http
GET /api/sso/providers/public?tenantCode={tenantCode}
```

说明：

- 登录页获取当前租户可用的公开 Provider 列表。
- 只返回 `ProviderCode`、`ProviderName`、`Protocol`、`LoginButtonText`、`IsDefault` 等非敏感信息。

### 5.3 SSO 回调

接口：

```http
GET /api/sso/callback/{providerCode}
```

或：

```http
GET /api/sso/oidc/callback
```

建议优先使用带 Provider 标识的回调路径，便于多 Provider 场景。

处理逻辑：

- 处理 IdP 返回的 `error`，写入失败日志后回跳前端错误页。
- 校验 `state`，恢复 tenant、provider、returnUrl、correlation id。
- 校验 `nonce`。
- 使用授权码换取 token。
- 校验外部身份。
- 执行用户绑定、角色映射、部门映射。
- 生成本系统 token。
- 回跳前端 `/sso/callback`，通过安全方式传递 token。

token 回传建议：

- 优先使用后端短期一次性 `loginTicket`：
  - 后端生成一次性票据并缓存 1 到 3 分钟。
  - 前端回调页使用票据调用 `/api/sso/exchange-ticket`。
  - 后端返回本系统 token。
- 不建议直接把 access token 和 refresh token 放在 URL query 中。

票据交换接口：

```http
POST /api/sso/exchange-ticket
```

请求：

```json
{
  "ticket": "one-time-login-ticket"
}
```

返回：

```json
{
  "accessToken": "string",
  "refreshToken": "string",
  "expiresIn": 3600,
  "tokenType": "Bearer"
}
```

### 5.4 用户绑定管理

接口：

| 方法 | 路径 | 说明 | 权限码 |
| --- | --- | --- | --- |
| GET | `/api/sso/user-bindings` | 分页查询绑定 | `system:sso-binding:view` |
| GET | `/api/sso/user-bindings/{id}` | 查询绑定详情 | `system:sso-binding:view` |
| POST | `/api/sso/user-bindings` | 手工创建绑定 | `system:sso-binding:create` |
| PUT | `/api/sso/user-bindings/{id}` | 更新绑定 | `system:sso-binding:update` |
| DELETE | `/api/sso/user-bindings/{id}` | 删除绑定 | `system:sso-binding:delete` |
| POST | `/api/sso/user-bindings/{id}/enable` | 启用绑定 | `system:sso-binding:update` |
| POST | `/api/sso/user-bindings/{id}/disable` | 禁用绑定 | `system:sso-binding:update` |

手工绑定规则：

- 校验 Provider、用户和绑定均属于当前租户。
- 同一个 Provider 下，一个 `ExternalUserId` 只能绑定一个本地用户。
- 同一个 Provider 下，一个本地用户只建议绑定一个外部身份。确需多身份绑定时，应明确业务规则。

### 5.5 角色映射管理

接口：

| 方法 | 路径 | 说明 | 权限码 |
| --- | --- | --- | --- |
| GET | `/api/sso/role-mappings` | 分页查询角色映射 | `system:sso-role-mapping:view` |
| POST | `/api/sso/role-mappings` | 创建角色映射 | `system:sso-role-mapping:create` |
| PUT | `/api/sso/role-mappings/{id}` | 更新角色映射 | `system:sso-role-mapping:update` |
| DELETE | `/api/sso/role-mappings/{id}` | 删除角色映射 | `system:sso-role-mapping:delete` |
| POST | `/api/sso/role-mappings/{id}/enable` | 启用映射 | `system:sso-role-mapping:update` |
| POST | `/api/sso/role-mappings/{id}/disable` | 禁用映射 | `system:sso-role-mapping:update` |

### 5.6 部门映射管理

接口：

| 方法 | 路径 | 说明 | 权限码 |
| --- | --- | --- | --- |
| GET | `/api/sso/department-mappings` | 分页查询部门映射 | `system:sso-department-mapping:view` |
| POST | `/api/sso/department-mappings` | 创建部门映射 | `system:sso-department-mapping:create` |
| PUT | `/api/sso/department-mappings/{id}` | 更新部门映射 | `system:sso-department-mapping:update` |
| DELETE | `/api/sso/department-mappings/{id}` | 删除部门映射 | `system:sso-department-mapping:delete` |
| POST | `/api/sso/department-mappings/{id}/enable` | 启用映射 | `system:sso-department-mapping:update` |
| POST | `/api/sso/department-mappings/{id}/disable` | 禁用映射 | `system:sso-department-mapping:update` |

### 5.7 SSO 登录日志

接口：

| 方法 | 路径 | 说明 | 权限码 |
| --- | --- | --- | --- |
| GET | `/api/sso/login-logs` | 分页查询 SSO 登录日志 | `system:sso-login-log:view` |
| GET | `/api/sso/login-logs/{id}` | 查询日志详情 | `system:sso-login-log:view` |
| GET | `/api/sso/login-logs/export` | 导出日志 | `system:sso-login-log:export` |

查询条件：

- Provider
- Protocol
- LoginResult
- UserName
- ExternalUserId
- IP
- 时间范围
- TraceId

## 6. 用户绑定策略

### 6.1 外部身份标准模型

OIDC claims 经映射后，转换为统一模型：

```csharp
public sealed class ExternalSsoUser
{
    public string ExternalUserId { get; init; } = string.Empty;
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? DisplayName { get; init; }
    public IReadOnlyCollection<string> Groups { get; init; } = [];
    public IReadOnlyCollection<string> Roles { get; init; } = [];
    public IReadOnlyCollection<string> Departments { get; init; } = [];
}
```

默认 OIDC claim 来源：

- `ExternalUserId`：优先 `sub`
- `UserName`：优先 `preferred_username`，其次 `name`
- `Email`：`email`
- `Phone`：`phone_number`
- `DisplayName`：优先 `name`
- `Groups`：`groups`
- `Roles`：`roles` 或自定义 claim
- `Departments`：`department`、`department_id` 或自定义 claim

### 6.2 绑定优先级

绑定顺序：

1. 使用 `ProviderId + ExternalUserId` 查找 `SsoUserBinding`。
2. 如果已有绑定，校验绑定启用、本地用户存在、本地用户启用、租户启用。
3. 如果无绑定且允许自动绑定，按以下顺序匹配本地用户：
   - email 匹配。
   - phone 匹配。
   - userName 匹配。
4. 如果匹配到唯一用户，则创建 `SsoUserBinding`。
5. 如果匹配不到且允许自动创建用户，则创建本地用户和绑定。
6. 如果匹配不到且不允许自动创建，则登录失败。

### 6.3 匹配规则

externalUserId 优先：

- 只在同一个 Provider 下匹配。
- `ExternalUserId` 不允许为空。
- OIDC 下默认使用 `sub`，不要使用 email 作为外部唯一 ID。

email 匹配：

- 仅在 Provider 配置允许自动绑定时启用。
- email 需标准化为小写并去除首尾空白。
- 如果命中多个用户，登录失败并要求管理员手工绑定。

phone 匹配：

- 仅在 Provider 配置允许自动绑定时启用。
- 建议标准化手机号格式。
- 如果命中多个用户，登录失败并要求管理员手工绑定。

userName 匹配：

- 使用本地 `NormalizedUserName`。
- 仅在 email 和 phone 均未命中时使用。
- 如果外部用户名为空，不执行该策略。

自动创建用户：

- 仅在 Provider `AutoCreateUser = true` 时启用。
- 创建用户时 `TenantId` 来自当前 SSO 登录解析结果。
- 用户名优先使用外部 `UserName`，否则根据 email 前缀生成。
- 如果用户名冲突，需要追加短随机后缀或要求手工处理。
- 自动创建用户默认 `IsEnabled = true`，但必须分配非 SuperAdmin 的默认角色或映射角色。
- 密码可生成不可登录随机密码哈希，或标记为外部账号。第一阶段建议仍设置随机密码哈希，不暴露给用户。

禁用用户不能登录：

- 本地用户 `IsEnabled = false` 时拒绝登录。
- 用户软删除时拒绝登录。
- 绑定 `IsEnabled = false` 时拒绝登录。
- 租户 `IsEnabled = false` 时拒绝登录。

## 7. 角色映射策略

### 7.1 外部角色来源

支持从以下外部信息映射本地角色：

- OIDC `groups`
- OIDC `roles`
- 自定义 claim，例如 `department_role`、`app_roles`

Provider 的 `ClaimMappingsJson` 应支持配置角色 claim 名称。

### 7.2 映射规则

规则：

- 外部 group / role 值通过 `SsoRoleMapping` 映射到本地 `Role`。
- 只使用当前租户、当前 Provider、已启用的映射。
- 映射到的本地角色必须存在且启用。
- 如果多个外部值映射到同一个角色，去重后分配。
- 如果无映射，使用 Provider 配置的默认角色。
- 如果无映射且未配置默认角色，可以允许登录但只具备基础菜单，或直接失败。第一阶段建议要求配置默认角色。

### 7.3 SuperAdmin 保护

安全规则：

- 禁止通过 SSO 自动分配 `SuperAdmin`。
- `SsoRoleMapping.RoleId` 不允许选择 `SuperAdmin` 角色。
- `SsoProvider.DefaultRoleId` 不允许选择 `SuperAdmin` 角色。
- 自动创建用户时不允许赋予 `SuperAdmin`。
- `SuperAdmin` 只能通过本系统角色管理手工分配，并继续走现有敏感操作校验。

### 7.4 登录时同步策略

建议 Provider 提供配置：

- `SyncRolesOnLogin = false`：只在首次绑定或创建用户时分配角色。
- `SyncRolesOnLogin = true`：每次 SSO 登录时根据外部角色重新计算本地角色。

第一阶段建议：

- 默认关闭全量覆盖。
- 支持“追加映射角色，不移除管理员手工分配角色”。
- 后续如需强同步，应区分角色来源，避免 SSO 覆盖本地管理员手工授权。

## 8. 部门映射策略

### 8.1 映射来源

部门来源：

- `department`
- `department_id`
- `department_code`
- 自定义 claim

### 8.2 映射规则

规则：

- 使用 `SsoDepartmentMapping` 将外部部门映射到本地 `Department`。
- 优先按 `ExternalDepartmentId` 匹配。
- 其次按 `ExternalDepartmentCode` 匹配。
- 最后可按 `ExternalDepartmentName` 匹配，但不建议作为唯一规则。
- 如果匹配成功且 Provider 开启部门同步，更新用户 `DepartmentId`。
- 如果无映射，不自动创建部门，避免外部组织结构污染本地数据。

### 8.3 同步策略

第一阶段建议：

- 支持登录时同步用户部门。
- 不自动创建本地部门。
- 不删除或禁用本地部门。
- 用户已有本地部门且外部无映射时，不覆盖为 null。

## 9. 租户策略

### 9.1 每租户独立 Provider

设计规则：

- `SsoProvider` 继承 `BaseEntity`，配置归属具体 `TenantId`。
- 每个租户可配置多个 Provider。
- 每个租户最多一个默认 Provider，建议通过应用服务校验。
- Provider 配置、用户绑定、角色映射、部门映射、登录日志均按租户隔离。

### 9.2 登录时解析租户

支持方式：

- URL 参数：`tenantId`
- URL 参数：`tenantCode`
- URL 参数：`providerCode`
- 子域名或请求头解析，后续可与现有 `TenantResolver` 协调

推荐规则：

- 如果传入 `tenantId + providerCode`，按二者定位 Provider。
- 如果传入 `tenantCode + providerCode`，先解析租户，再定位 Provider。
- 如果只传入 `providerCode`，仅当全局唯一时允许，否则要求补充租户。
- 如果只传入租户，使用该租户默认启用 Provider。

### 9.3 禁用租户不能登录

校验点：

- SSO 登录入口发起 challenge 前校验租户启用状态。
- SSO 回调完成外部身份校验后再次校验租户启用状态。
- 票据交换时再次校验本地用户和租户状态。

失败时：

- 写入 SSO 登录失败日志。
- 回跳前端 SSO 错误页。
- 不影响本地管理员使用本地账号登录其他启用租户。

## 10. 安全策略

### 10.1 state 防 CSRF

要求：

- 每次发起 SSO 登录生成不可预测的 `state`。
- `state` 与 tenant、provider、returnUrl、nonce、PKCE verifier、过期时间绑定。
- `state` 保存到安全 cookie、分布式缓存或服务端临时表。
- 回调时必须校验 `state` 存在、未过期、未使用。
- 成功或失败后立即作废 `state`。

### 10.2 nonce 校验

要求：

- 每次 OIDC 登录生成 `nonce`。
- `nonce` 必须写入授权请求。
- 回调时校验 `id_token` 中的 `nonce` 与服务端记录一致。
- 校验失败直接拒绝登录。

### 10.3 PKCE

要求：

- OIDC Provider 默认启用 PKCE。
- 发起登录时生成 `code_verifier` 和 `code_challenge`。
- 回调换 token 时提交 `code_verifier`。
- Provider 不支持 PKCE 时需要显式配置并记录风险，不作为默认行为。

### 10.4 ClientSecret 加密存储

要求：

- `ClientSecret` 使用现有 `IConfigValueProtector` 或等价加密服务加密存储。
- 管理接口不返回明文 secret。
- 创建或轮换 secret 时仅在当前响应显示一次。
- 日志、异常、审计中不打印 secret、token、authorization code、id token、refresh token。

### 10.5 登录审计

必须记录：

- Provider
- Protocol
- 外部用户 ID
- 本地用户 ID
- 登录结果
- 失败原因
- 绑定来源
- IP
- User-Agent
- TraceId
- 时间

审计写入失败时：

- 不应导致成功登录失败。
- 应写 Serilog warning，避免中断主流程。

### 10.6 本地 admin 兜底

要求：

- 默认 `admin` 本地账号保留本地密码登录入口。
- SSO 开关、Provider 配置错误、IdP 不可用不能锁死系统。
- SSO 管理页面的高风险操作建议接入敏感操作校验。
- 修改 `SuperAdmin` 相关授权仍走现有保护策略。

### 10.7 SSO 配置错误保护

要求：

- Provider 保存前做基础校验。
- 提供 Provider 测试接口，检查 metadata、issuer、回调地址、client id 等配置。
- 禁用或删除 Provider 前提示影响范围。
- 配置错误只影响该 Provider 登录，不影响本地登录和其他 Provider。

### 10.8 returnUrl 安全

要求：

- `returnUrl` 只允许本系统前端相对路径或白名单 origin。
- 禁止开放重定向到任意外部地址。
- 失败回跳也应使用白名单路径。

## 11. 前端页面设计

前端继续使用 Vue 3、TypeScript、Pinia、Vue Router、Axios wrapper 和 Element Plus。

### 11.1 登录页 SSO 按钮

位置：

- `frontend/permission-admin/src/views/login/LoginView.vue`

设计：

- 保留现有账号密码登录表单。
- 在密码登录下方或旁边展示 SSO 登录按钮。
- 根据 `/api/sso/providers/public` 返回的 Provider 列表展示按钮。
- 多 Provider 时显示 Provider 名称。
- 单 Provider 时可显示“单点登录”。
- 点击后跳转 `/api/sso/login?tenantCode=...&providerCode=...&returnUrl=...`。

交互：

- SSO 不可用时不隐藏本地登录。
- 点击 SSO 后显示跳转 loading。
- 登录失败回到登录页并显示后端返回的安全错误消息。

### 11.2 SSO 回调页

建议页面：

- `frontend/permission-admin/src/views/login/SsoCallbackView.vue`

路由：

- `/sso/callback`

职责：

- 读取后端回跳携带的 `ticket`、`error`、`returnUrl`。
- 如果有 `error`，显示失败提示并提供返回登录页按钮。
- 如果有 `ticket`，调用 `/api/sso/exchange-ticket` 换取本系统 token。
- 保存 token 后调用当前用户信息、菜单、权限接口。
- 跳转 `returnUrl` 或首页。

### 11.3 SSO Provider 管理

建议页面：

- `frontend/permission-admin/src/views/system/sso-provider/index.vue`

页面结构：

- 搜索表单：
  - Provider 名称
  - Provider 编码
  - 协议
  - 启用状态
- 表格：
  - Provider 名称
  - Provider 编码
  - 协议
  - 默认 Provider
  - 启用状态
  - 更新时间
- 操作：
  - 新增
  - 编辑
  - 启用
  - 禁用
  - 测试配置
  - 轮换 secret
  - 删除
- Modal 表单：
  - 基础信息
  - OIDC 配置
  - Claim 映射
  - 自动绑定和自动创建策略
  - 默认角色
  - SAML2 预留配置，可第一阶段禁用显示或折叠展示

### 11.4 用户绑定管理

建议页面：

- `frontend/permission-admin/src/views/system/sso-binding/index.vue`

页面结构：

- 搜索：
  - Provider
  - 本地用户名
  - 外部用户 ID
  - 外部邮箱
  - 启用状态
- 表格：
  - Provider
  - 本地用户
  - 外部用户 ID
  - 外部用户名
  - 外部邮箱
  - 绑定来源
  - 最近登录时间
  - 启用状态
- 操作：
  - 新增绑定
  - 编辑绑定
  - 启用
  - 禁用
  - 删除

### 11.5 角色映射管理

建议页面：

- `frontend/permission-admin/src/views/system/sso-role-mapping/index.vue`

页面结构：

- 搜索：
  - Provider
  - 外部类型
  - 外部值
  - 本地角色
  - 启用状态
- 表格：
  - Provider
  - 外部类型
  - 外部值
  - 本地角色
  - 优先级
  - 启用状态
- 表单：
  - Provider
  - 外部类型
  - 外部值
  - 本地角色
  - 优先级
  - 备注

限制：

- 本地角色选择器中不允许选择 `SuperAdmin`。

### 11.6 部门映射管理

建议页面：

- `frontend/permission-admin/src/views/system/sso-department-mapping/index.vue`

页面结构：

- 搜索：
  - Provider
  - 外部部门 ID
  - 外部部门编码
  - 外部部门名称
  - 本地部门
  - 启用状态
- 表格：
  - Provider
  - 外部部门 ID
  - 外部部门编码
  - 外部部门名称
  - 本地部门
  - 优先级
  - 启用状态

### 11.7 SSO 登录日志

建议页面：

- `frontend/permission-admin/src/views/system/sso-login-log/index.vue`

页面结构：

- 搜索：
  - Provider
  - 协议
  - 登录结果
  - 本地用户名
  - 外部用户 ID
  - IP
  - 时间范围
- 表格：
  - 时间
  - Provider
  - 协议
  - 本地用户
  - 外部用户 ID
  - 登录结果
  - IP
  - TraceId
- 操作：
  - 查看详情
  - 导出

## 12. 权限码设计

建议新增权限码：

### 12.1 SSO Provider

- `system:sso-provider:view`
- `system:sso-provider:create`
- `system:sso-provider:update`
- `system:sso-provider:delete`
- `system:sso-provider:enable`
- `system:sso-provider:disable`
- `system:sso-provider:test`

### 12.2 用户绑定

- `system:sso-binding:view`
- `system:sso-binding:create`
- `system:sso-binding:update`
- `system:sso-binding:delete`

### 12.3 角色映射

- `system:sso-role-mapping:view`
- `system:sso-role-mapping:create`
- `system:sso-role-mapping:update`
- `system:sso-role-mapping:delete`

### 12.4 部门映射

- `system:sso-department-mapping:view`
- `system:sso-department-mapping:create`
- `system:sso-department-mapping:update`
- `system:sso-department-mapping:delete`

### 12.5 SSO 登录日志

- `system:sso-login-log:view`
- `system:sso-login-log:export`

### 12.6 菜单建议

建议放在“系统管理”或“安全中心”下：

- SSO 配置
  - Provider 管理：`system:sso-provider:view`
  - 用户绑定：`system:sso-binding:view`
  - 角色映射：`system:sso-role-mapping:view`
  - 部门映射：`system:sso-department-mapping:view`
  - SSO 登录日志：`system:sso-login-log:view`

## 13. 后端分层建议

### 13.1 Domain 层

新增实体：

- `SsoProvider`
- `SsoUserBinding`
- `SsoRoleMapping`
- `SsoDepartmentMapping`
- `SsoLoginLog`

可选枚举：

- `SsoProtocol`
- `SsoLoginResult`
- `SsoBindingSource`
- `SsoExternalRoleType`

Domain 层只保留业务规则和状态，不依赖 EF Core、HTTP、OIDC middleware。

### 13.2 Application 层

建议服务：

- `SsoProviderService`
- `SsoLoginService`
- `SsoUserBindingService`
- `SsoRoleMappingService`
- `SsoDepartmentMappingService`
- `SsoLoginLogService`

建议模型：

- `SsoProviderModels.cs`
- `SsoLoginModels.cs`
- `SsoUserBindingModels.cs`
- `SsoRoleMappingModels.cs`
- `SsoDepartmentMappingModels.cs`
- `SsoLoginLogModels.cs`

职责：

- Provider CRUD。
- 外部身份统一模型转换。
- 用户绑定和自动创建。
- 角色和部门映射。
- SSO 登录日志写入。
- 生成本地用户会话前的业务编排。

### 13.3 Infrastructure 层

职责：

- EF Core configuration。
- ClientSecret 加密存储。
- OIDC metadata 读取和配置测试。
- SAML2 后续集成实现。
- 缓存 `state`、`nonce`、PKCE verifier、一次性登录票据。

建议复用：

- `IConfigValueProtector`
- `ICacheService`
- Repository
- UnitOfWork

### 13.4 Api 层

职责：

- SSO 管理 Controller。
- OIDC challenge 和 callback endpoint。
- Authentication scheme 注册。
- Swagger 暴露管理接口。
- 权限校验和审计中间件复用。

注意：

- Api 层不直接写 DbContext。
- OIDC 回调中不要塞入复杂业务逻辑，应调用 Application 服务编排。

## 14. OpenIddict 集成策略

### 14.1 本系统令牌签发

外部 SSO 登录成功后，最终仍由本系统签发 token。建议抽取内部方法或应用服务，复用当前 `ConnectController` Password Flow 中的以下逻辑：

- 创建 `UserSession`。
- 添加 claims。
- 设置 scopes 和 resource。
- 调用 OpenIddict Server scheme `SignIn`。

目标是避免 SSO 和密码登录产生两套 claims 结构。

### 14.2 外部 OIDC 登录与本地 OpenIddict 的边界

边界：

- 外部 OIDC 是身份提供者。
- 本系统 OpenIddict 是本系统 API 的令牌签发者。
- 外部 IdP 的 token 不直接用于访问本系统 API。
- 本系统 API 只接受本系统 OpenIddict 验证通过的 access token。

### 14.3 多 Provider 注册问题

由于 Provider 是租户级动态配置，第一阶段有两种实现路线：

方案 A：动态 Authentication Scheme。

- 根据 Provider 注册不同 OIDC scheme。
- 登录时 challenge 对应 scheme。
- 优点是符合 ASP.NET Core OIDC middleware 常规模式。
- 风险是动态租户配置热更新复杂。

方案 B：手工构建 OIDC 授权地址和回调换 token。

- Application/Infrastructure 层读取 Provider 配置。
- 自行生成授权地址、state、nonce、PKCE。
- 回调时调用 discovery/token/userinfo 并校验 id token。
- 最终仍使用 OpenIddict 签发本系统 token。
- 优点是多租户动态配置更可控。
- 风险是 OIDC 校验实现复杂，必须严格使用成熟库校验 token 和 metadata。

建议：

- 第一阶段优先评估方案 A。如果动态 Provider 数量少且配置变更不频繁，可先采用 scheme 注册。
- 如果每租户多 Provider 且要求在线配置即时生效，采用方案 B，但必须使用 Microsoft.IdentityModel.Protocols.OpenIdConnect、Microsoft.IdentityModel.Tokens 等标准库做 token 验证，不手写签名校验。

## 15. 分阶段实现计划

### 阶段一：OIDC 基础登录闭环

目标：

- 支持配置一个租户的 OIDC Provider。
- 支持 SSO 登录入口、回调、绑定或自动创建用户。
- 成功后签发本系统 token 并进入系统。

范围：

- 新增 SSO 核心实体和 migration。
- 新增 Provider 管理基础接口。
- 新增 SSO 登录入口和回调。
- 新增 SSO callback 前端页面。
- 登录页增加 SSO 按钮。
- 写入 `LoginLog` 和 `SsoLoginLog`。

验收：

- 本地账号密码登录不受影响。
- OIDC 登录成功后能进入系统。
- access token claims 与本地登录一致。
- 禁用用户、禁用租户不能登录。
- SSO 失败不影响默认 admin 本地登录。

### 阶段二：用户绑定管理和安全加固

目标：

- 完整支持用户绑定管理。
- 加强 state、nonce、PKCE、returnUrl 和 ticket 安全。

范围：

- 用户绑定管理页面和接口。
- 一次性 `loginTicket` 交换机制。
- Provider 配置测试接口。
- ClientSecret 加密和轮换。
- 登录失败日志细化。

验收：

- 管理员可手工绑定和解绑用户。
- token 不通过 URL query 暴露。
- Provider 配置错误可被测试接口发现。
- 日志不包含 secret、id token、access token、refresh token。

### 阶段三：角色映射和部门映射

目标：

- 外部 group / role 映射到本地角色。
- 外部部门映射到本地部门。

范围：

- 角色映射管理。
- 部门映射管理。
- 登录时角色映射。
- 登录时部门同步。
- SuperAdmin 自动分配保护。

验收：

- 外部 group/role 可映射本地角色。
- 无映射时使用默认角色。
- 任何 SSO 自动流程都不能分配 `SuperAdmin`。
- 外部部门可映射到本地部门。

### 阶段四：多租户和多 Provider 完善

目标：

- 每个租户独立配置 Provider。
- 支持登录页按租户展示可用 Provider。

范围：

- `tenantCode`、`providerCode` 解析策略完善。
- 多 Provider 默认值和启停规则。
- Provider 配置缓存。
- 配置变更缓存失效。

验收：

- 不同租户 Provider 相互隔离。
- 禁用租户不能 SSO 登录。
- 禁用 Provider 不能发起 SSO。
- 多 Provider 场景可正确选择登录入口。

### 阶段五：SAML2 预留落地

目标：

- 在不影响 OIDC 的前提下，接入 SAML2。

范围：

- 引入 SAML2 标准库。
- 实现 SAML2 metadata、redirect/post binding、assertion 校验。
- 复用 `SsoProvider`、`SsoUserBinding`、映射和登录日志。

验收：

- SAML2 登录能复用本地用户绑定和本系统 token 签发。
- OIDC 登录不受影响。
- SAML2 配置错误不影响本地登录和 OIDC 登录。

## 16. 风险和规避

### 16.1 认证体系分裂

风险：

- SSO 登录绕过 OpenIddict，导致 token claims、会话和权限不一致。

规避：

- 外部身份只用于认证。
- 本系统 API token 仍由 OpenIddict 统一签发。
- SSO 和本地登录复用同一套 claims 构建、UserSession 和权限读取逻辑。

### 16.2 自动绑定误匹配

风险：

- email、phone、userName 匹配错误导致用户冒用。

规避：

- `externalUserId` 绑定优先。
- email/phone/userName 自动绑定必须由 Provider 显式开启。
- 多用户命中时拒绝登录并要求手工绑定。
- 自动绑定结果写入审计。

### 16.3 权限过度分配

风险：

- 外部 group/role 映射错误导致权限过大。

规避：

- 默认角色应为低权限角色。
- 禁止自动分配 `SuperAdmin`。
- 角色映射变更记录操作日志。
- 高风险映射变更建议接入敏感操作校验。

### 16.4 SSO 配置锁死系统

风险：

- Provider 配置错误导致管理员无法登录。

规避：

- 保留默认 admin 本地登录。
- 登录页始终保留本地登录入口。
- Provider 测试通过后再启用。
- SSO 错误只影响当前 Provider。

### 16.5 动态 OIDC Provider 实现复杂

风险：

- 多租户动态配置和 ASP.NET Core OIDC scheme 生命周期冲突。

规避：

- 第一阶段先支持少量 Provider。
- 优先使用标准中间件或标准 OIDC 校验库。
- 不手写 token 签名校验。
- 对 Provider 配置读取加入缓存和失效机制。

## 17. 当前阶段结论

建议先实现 OIDC SSO 的最小闭环：Provider 配置、登录入口、回调、外部身份校验、本地用户绑定或创建、本系统 token 签发、登录日志和前端回调页。角色映射、部门映射、多 Provider 和 SAML2 按阶段逐步增强。

整个设计不改变现有本地账号密码登录，不替换 OpenIddict，不引入 WMS / ERP 业务代码。SSO 只是新增登录入口，本系统仍以本地用户、角色、菜单、权限和租户隔离作为最终授权依据。
