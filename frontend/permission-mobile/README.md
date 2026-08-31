# PermissionSystem 移动工作台

这是 PermissionSystem 的 Vue 3 + Vite 移动 H5/PWA 客户端。它复用后端现有的 OpenIddict、租户、权限、工作流、通知和业务单据能力，不包含 client secret，也不建立新的认证或 RBAC 体系。

## 开发

需要 Node.js 20.19+（推荐 Node 22）和可访问的 PermissionSystem API。复制环境文件后启动：

```powershell
Copy-Item .env.example .env.local
npm install
npm run dev
```

默认 Vite 地址由工程配置决定；API 未配置时开发代理会将 `/api`、`/connect` 和 `/hubs` 转发到本地后端。建议通过 `VITE_API_BASE_URL` 指向实际 API，例如 `https://localhost:7281`。

常用命令：`npm run type-check`、`npm run lint`、`npm run test:unit`、`npm run build`。

## OAuth 配置

移动端使用 Authorization Code + PKCE（S256）。后端必须先注册 public client `permission-mobile`，并将每个环境的精确回调 URI 加入白名单；不能配置 client secret 或通配符回调。

关键变量：

| 变量 | 说明 |
| --- | --- |
| `VITE_API_BASE_URL` | API 根地址，不要包含 `/api/v1` |
| `VITE_OAUTH_ISSUER` | OpenIddict issuer，通常与 API 根地址相同 |
| `VITE_OAUTH_CLIENT_ID` | 固定为 `permission-mobile`（public client） |
| `VITE_OAUTH_REDIRECT_URI` | 精确的 `/authorize/callback` 地址 |
| `VITE_OAUTH_SCOPE` | 最小 scope，按后端资源配置填写（通常含 `openid profile offline_access permission-system-api`） |
| `VITE_DEFAULT_TENANT_CODE` | 可选的默认租户代码；登录时仍由用户确认，生产可留空 |

后端通过 `SeedData:MobileOAuthRedirectUris` 和 `SeedData:MobileOAuthPostLogoutRedirectUris` 配置精确白名单。授权请求显式携带租户代码，服务端只接受已启用租户，并把租户绑定到授权票据和最终 token。

生产构建时通过 Docker `--build-arg VITE_...` 注入这些公开配置。access token 保存在内存并仅在当前浏览器会话存储中恢复；refresh token 使用不可导出的 Web Crypto 密钥加密后保存，不支持 Web Crypto/IndexedDB 时退化为 sessionStorage。任何 token 都不能写入镜像、日志或静态资源。

## PWA 缓存边界

`public/sw.js` 只允许同源 GET 请求参与缓存：安装时缓存静态壳（`index.html`、manifest、图标），运行时仅缓存文件名包含内容哈希的 `/assets/*` 资源。导航采用 network-first，离线时回退到缓存的 `index.html`。

以下请求永不拦截、永不写入 Cache Storage：非 GET/HEAD 请求、`/api/*` 业务接口、`/connect/*` OAuth/令牌接口、`/hubs/*` SignalR，以及任何跨源请求。Service Worker 也不会缓存带 `Set-Cookie` 的响应，审批、提交、上传等写操作不会离线重放。

应用入口会在生产构建中注册 `/sw.js`（仅在 HTTPS 或 localhost 下生效）。发布新版本时由 Nginx 对 `sw.js`、`index.html` 和 manifest 设置 no-cache，浏览器可及时获取新 worker；旧缓存会在 activate 阶段自动删除。

## Docker 与 Nginx 部署

Dockerfile 使用 Node 22 构建阶段和 Nginx Alpine 运行阶段，最终镜像只包含 `dist` 与 Nginx。Nginx 提供 SPA fallback、哈希静态资源一年 immutable 缓存，并对 index/manifest/sw 设置 no-cache 和安全响应头。

```powershell
docker build `
  --build-arg VITE_API_BASE_URL=https://mobile.example.com `
  --build-arg VITE_OAUTH_ISSUER=https://mobile.example.com `
  --build-arg VITE_OAUTH_CLIENT_ID=permission-mobile `
  --build-arg VITE_OAUTH_REDIRECT_URI=https://mobile.example.com/authorize/callback `
  --build-arg VITE_OAUTH_SCOPE="openid profile offline_access permission-system-api" `
  --build-arg VITE_DEFAULT_TENANT_CODE=default `
  -t permission-mobile:latest .

docker run --rm -p 8080:80 `
  -e API_UPSTREAM=http://permission-system-api:8080 `
  permission-mobile:latest
```

`API_UPSTREAM` 是运行时环境变量形式的反向代理目标，使用完整的 scheme + host + port；例如 `-e API_UPSTREAM=http://api:8080`。Nginx 官方 entrypoint 配置为只替换 `${API_UPSTREAM}` 占位符，其他 `$host`、`$scheme` 等 Nginx 变量保持不变。API、OAuth 和 SignalR 走同源代理，因此生产环境只需在外层负载均衡器终止 HTTPS，并把移动域名的回调 URI 加入后端白名单。容器内置 `/healthz` 健康检查。

部署顺序应先更新后端 public client 和回调白名单，再发布前端静态资源；回滚时同时回滚前端构建与对应 OAuth 配置。
