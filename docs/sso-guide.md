# SSO 指南

## 当前实现范围

当前项目包含 SSO 管理和 OIDC 登录链路。后端已有 Provider、用户绑定、角色映射、部门映射和登录日志相关模块。

主要目录：

- `backend/PermissionSystem.Application/Sso`
- `backend/PermissionSystem.Infrastructure/Sso`
- `backend/PermissionSystem.Domain/Entities/Sso*.cs`
- `backend/PermissionSystem.Api/Controllers/Sso*.cs`
- `frontend/permission-admin/src/views/sso`
- `frontend/permission-admin/src/api/ssoAuth.ts`

枚举中存在 `Oidc`、`Saml`、`OAuth2` 类型，但当前可确认的完整登录链路是 OIDC：`/api/sso/oidc/...`。SAML 默认配置为关闭，不应在文档中当作已完整可用能力使用。

## 配置项

后端配置：

```json
{
  "Sso": {
    "Enabled": true,
    "EnableOidc": true,
    "EnableSaml": false,
    "DefaultCallbackPath": "/api/sso/oidc/callback",
    "RequireHttpsMetadata": false,
    "EncryptClientSecret": true,
    "AllowAutoCreateUser": true,
    "AllowLocalLoginFallback": true
  }
}
```

前端 OAuth 客户端配置：

```text
VITE_OAUTH_CLIENT_ID=permission-admin
VITE_OAUTH_CLIENT_SECRET=
```

`VITE_OAUTH_CLIENT_SECRET` 需要与后端种子数据中的 `SeedData:OAuthClientSecret` 对应。

## OIDC 登录流程

1. 前端获取可用 SSO Provider。
2. 用户点击 SSO 登录。
3. 前端调用 `GET /api/sso/oidc/{providerCode}/challenge`。
4. 后端返回外部身份提供商 redirectUrl。
5. 用户在外部身份提供商完成登录。
6. 外部身份提供商回调 `GET /api/sso/oidc/{providerCode}/callback`。
7. 后端解析 code 和 state，映射或创建本地用户，生成一次性 login_code。
8. 后端重定向到前端 `/sso/callback?login_code=...`。
9. 前端调用 `POST /api/sso/oidc/exchange`，grant_type 为 `sso_oidc`。
10. OpenIddict 签发本系统 access token 和 refresh token。

登录码有效期当前为 3 分钟，消费后会从缓存移除。

## Provider 管理

Provider 管理接口：

- `GET /api/sso/providers`
- `GET /api/sso/providers/enabled`
- `GET /api/sso/providers/{id}`
- `POST /api/sso/providers`
- `PUT /api/sso/providers/{id}`
- `DELETE /api/sso/providers/{id}`
- `POST /api/sso/providers/{id}/enable`
- `POST /api/sso/providers/{id}/disable`
- `POST /api/sso/providers/{id}/test`

常用字段：

- `ProviderCode`
- `ProviderName`
- `ProviderType`
- `Authority`
- `MetadataAddress`
- `ClientId`
- `ClientSecret`
- `Scopes`
- `CallbackPath`
- `UsePkce`
- `GetClaimsFromUserInfoEndpoint`
- Claim 映射字段
- `AutoCreateUser`
- `AutoBindUser`
- `DefaultRoleIds`
- `AllowLocalLoginFallback`

## 用户、角色、部门映射

用户绑定：

- `GET /api/sso/user-bindings`
- `GET /api/sso/user-bindings/{id}`
- `POST /api/sso/user-bindings/{id}/unbind`
- `DELETE /api/sso/user-bindings/{id}`

角色映射：

- `GET /api/sso/providers/{providerId}/role-mappings`
- `PUT /api/sso/providers/{providerId}/role-mappings`

部门映射：

- `GET /api/sso/providers/{providerId}/department-mappings`
- `PUT /api/sso/providers/{providerId}/department-mappings`

登录日志：

- `GET /api/sso/login-logs`
- `GET /api/sso/login-logs/{id}`

SSO 自动角色分配不允许分配 `SuperAdmin`。如果外部用户角色映射为空，会使用 Provider 的默认角色配置。

## 本地调试

本地 API 地址默认 `http://localhost:5264`，前端默认 `http://localhost:5173`。

本地 OIDC Provider 回调地址应配置为类似：

```text
http://localhost:5264/api/sso/oidc/{providerCode}/callback
```

如果外部 Provider 要回到前端，后端会重定向到 `/sso/callback` 或 returnUrl 指定路径。前端页面位于 `frontend/permission-admin/src/views/sso/callback.vue`。

## Docker 与生产差异

Docker：

- API 宿主机默认 `http://localhost:5000`。
- 前端默认 `http://localhost:8080`。
- 如果通过前端 Nginx 暴露回调，可结合反向代理地址配置 Provider。
- Redis 默认启用，适合保存一次性 SSO login_code。

生产：

- OIDC 回调地址必须使用生产 HTTPS 域名。
- `RequireHttpsMetadata` 建议为 true，除非身份提供商明确不支持。
- ClientSecret 必须加密存储并通过密钥管理注入。
- 多实例部署建议使用 Redis，避免 login_code 只存在某个实例内存。

## 常见问题

### challenge 成功但外部 Provider 回调失败

检查外部 Provider 上登记的 redirect_uri 是否与系统生成的 callback URL 完全一致，包括协议、域名、端口和路径。

### 回调后前端提示 SSO 登录失败

检查 `/api/sso/oidc/{providerCode}/callback` 日志、SSO 登录日志、state 是否过期、code 是否重复使用。

### exchange 返回 invalid_grant

login_code 为空、过期、已消费或缓存不可用。多实例环境下检查是否使用 Redis。

### SSO 用户未自动创建

检查 Provider 的 `AutoCreateUser` 是否开启、外部用户是否有可用唯一标识、租户是否启用、默认角色是否有效。

### 外部角色没有映射

检查 `RoleClaim`、外部 claims 内容、角色映射配置和本地角色状态。不要尝试通过 SSO 自动授予 `SuperAdmin`。
