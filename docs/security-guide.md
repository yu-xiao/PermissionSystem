# 安全指南

## 安全边界

PermissionSystem 的安全能力主要覆盖：

- OpenIddict 认证和 token 生命周期
- RBAC 权限码授权
- 租户隔离
- 用户会话与强制下线
- API Key 开放集成
- 请求限流
- 幂等与重复提交保护
- IP 黑白名单
- 敏感操作校验
- 文件上传限制
- 敏感配置加密
- 操作日志和登录日志

不要新增绕过认证、授权、审计或安全校验的代码。

## 认证

后端使用 OpenIddict，不使用自定义 JWT 签发服务。当前在 `Program.cs` 中启用：

- Password Flow
- Refresh Token Flow
- Client Credentials Flow
- Authorization Code Flow + PKCE
- 自定义 SSO OIDC 登录码 grant：`sso_oidc`

主要接口：

- `POST /connect/token`
- `POST /connect/revoke`
- `GET|POST /connect/logout`
- `GET /api/sso/oidc/{providerCode}/challenge`
- `GET /api/sso/oidc/{providerCode}/callback`
- `POST /api/sso/oidc/exchange`

本地和 Docker 环境关闭了 OpenIddict 的 HTTPS 传输强制要求，Production 不应依赖该开发行为。

## 授权

接口使用 `PermissionAttribute`：

```csharp
[Permission("system:user:view")]
```

相关代码位置：

- `backend/PermissionSystem.Api/Authorization/PermissionAttribute.cs`
- `backend/PermissionSystem.Api/Authorization/PermissionAuthorizationHandler.cs`
- `backend/PermissionSystem.Api/Authorization/PermissionAuthorizationPolicyProvider.cs`

权限码存储在用户 token claims 中。角色权限更新后，已登录用户通常需要重新登录或刷新会话才能拿到最新权限。

前端按钮权限使用：

- `frontend/permission-admin/src/directives/permission.ts`
- `frontend/permission-admin/src/stores/permission.ts`

## 租户隔离

租户上下文由 API 中间件解析，来源包括 `X-Tenant-Id`、claims 和默认租户配置。实体继承 `BaseEntity` 后包含 `TenantId` 和软删除字段。

开发注意：

- 普通业务查询应尊重租户过滤。
- 只有种子数据、系统管理或明确跨租户场景才考虑忽略查询过滤。
- 新增实体必须继承 `BaseEntity`，并在创建时设置正确 `TenantId`。

## 密钥和配置

不得提交真实敏感信息。以下配置必须使用本地 local 文件、环境变量或密钥管理：

- `ConnectionStrings:DefaultConnection`
- `ConnectionStrings:Redis`
- `SeedData:AdminPassword`
- `SeedData:OAuthClientSecret`
- `Security:SystemConfigEncryptionKey`
- `RabbitMQ:Password`
- `VITE_OAUTH_CLIENT_SECRET`
- SSO Provider 的 `ClientSecret`
- API Client Secret

本地建议放在 `backend/PermissionSystem.Api/appsettings.Development.local.json`。Docker 使用根目录 `.env`。

## 请求限流

限流配置位于 `RateLimit`：

- `GlobalPermitLimit`
- `GlobalWindowSeconds`
- `LoginPermitLimit`
- `LoginWindowSeconds`
- `RefreshTokenPermitLimit`
- `RefreshTokenWindowSeconds`
- `QueueLimit`

健康检查和 Swagger 路径被排除在全局限流之外。登录和刷新 token 有单独策略，触发后返回 429。

## 幂等与重复提交

前端对非 GET 请求自动加 `X-Idempotency-Key`。后端全局注册：

- `IdempotencyFilter`
- `PreventDuplicateSubmitFilter`

Memory 模式只适合单实例开发。多实例、Docker 或生产建议使用 Redis，以避免重复提交保护在实例之间不共享。

## 文件上传安全

配置位于 `FileStorage`。默认：

- 最大文件大小：20 MB
- 本地目录：`uploads/default`
- 阻止 `.exe`、`.dll`、`.bat`、`.cmd`、`.ps1`、`.sh`、`.js`、`.vbs`、`.jar` 等风险扩展名

生产部署时需要：

- 将上传目录挂载到持久化磁盘
- 限制执行权限
- 定期备份
- 避免将上传目录映射为可执行脚本目录

## SSO 安全

当前可用登录链路是 OIDC。Provider 管理包含 ClientId、ClientSecret、Scopes、CallbackPath、Claim 映射、自动创建用户、自动绑定用户、默认角色等配置。

注意：

- 不允许 SSO 自动分配 `SuperAdmin` 角色，代码中有保护。
- `ClientSecret` 应加密存储，相关开关为 `Sso:EncryptClientSecret`。
- 生产环境 OIDC Provider 应使用 HTTPS metadata。
- 登录码有效期当前为 3 分钟，并存入缓存后一次性消费。

## 本地、Docker、生产差异

本地：

- 可用 HTTP 调试。
- Swagger 开启。
- Memory 缓存默认启用。

Docker：

- API 默认使用 Redis。
- `.env` 管理密钥。
- Compose 仅适合开发/测试或作为部署参考。

生产：

- 必须启用 HTTPS。
- 不提交、不打印、不暴露密钥。
- 建议使用 Redis 支撑分布式安全能力。
- 关闭或限制 Swagger。
- 数据库、Redis、RabbitMQ 使用独立账号和最小权限。

## 常见问题

### 登录失败但账号密码正确

检查账号是否禁用、租户是否禁用、登录失败锁定策略、OAuth 客户端密钥是否匹配、`/connect/token` 是否被限流。

### 调整角色后权限不生效

用户 token 中已有旧权限声明。让用户重新登录，或执行强制下线后重新登录。

### SSO 登录后没有角色

检查角色映射、默认角色、角色是否启用，以及是否试图自动分配 `SuperAdmin`。

### 上传文件被拒绝

检查扩展名、文件大小、上传目录写权限和是否触发阻止扩展名规则。

### Docker 下请求频繁出现 429

检查是否在短时间内反复登录或刷新 token。可临时调大 `RateLimit` 配置，但生产不建议直接关闭限流。
