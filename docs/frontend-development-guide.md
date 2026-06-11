# 前端开发指南

## 开发环境

前端项目位于 `frontend/permission-admin`，使用 Vue 3、TypeScript、Vite、Pinia、Vue Router、Axios 和 Element Plus。

常用命令：

```powershell
cd frontend/permission-admin
npm install
npm run dev
npm run build
npm run preview
```

默认开发地址为 `http://localhost:5173`。

## 真实目录

```text
frontend/permission-admin/
  package.json
  vite.config.ts
  nginx.conf
  Dockerfile
  .env.example
  src/
    api/
    assets/
    components/
    directives/
    layouts/
    router/
    stores/
    styles/
    utils/
    views/
```

## 环境变量

示例文件：`frontend/permission-admin/.env.example`

```text
VITE_API_BASE_URL=https://localhost:5001
VITE_OAUTH_CLIENT_ID=permission-admin
VITE_OAUTH_CLIENT_SECRET=
```

本地开发通常需要创建 `.env.local` 或 `.env.development.local`，将 `VITE_API_BASE_URL` 指向后端。例如后端用 HTTP profile：

```text
VITE_API_BASE_URL=http://localhost:5264
VITE_OAUTH_CLIENT_ID=permission-admin
VITE_OAUTH_CLIENT_SECRET=your_oauth_client_secret
```

Docker 构建时 `docker-compose.yml` 将 `VITE_API_BASE_URL` 设为空字符串，前端使用相对路径，由 Nginx 代理到 API。

## API 调用

API 封装位于 `src/api/`，统一 HTTP 实例在 `src/utils/request.ts`。

当前 Axios 行为：

- `baseURL` 来自 `VITE_API_BASE_URL`，如果以 `/api` 结尾会自动去掉尾部 `/api`。
- 请求自动附加 `Authorization: Bearer <access_token>`。
- 非 GET/HEAD/OPTIONS 请求自动附加 `X-Idempotency-Key`。
- 401 时会尝试用 refresh token 获取新 access token。
- `x-session-revoked=true` 时会清理 token 并跳转登录页。
- 429 时显示请求过于频繁提示。

新增接口时优先在 `src/api/<module>.ts` 中封装函数，不要在页面里直接散落 Axios 调用。

## 路由、菜单与权限

路由入口位于 `src/router/index.ts`。登录后前端会加载当前用户、菜单和权限，动态生成可访问页面。

权限相关位置：

- API：`src/api/me.ts`、各模块 API 文件
- 状态：`src/stores/auth.ts`、`src/stores/permission.ts`
- 指令：`src/directives/permission.ts`
- 菜单页面：`src/views/system/menu/index.vue`
- 权限页面：`src/views/system/permission/index.vue`

按钮权限应使用 `v-permission`，权限码要与后端 `[Permission("...")]` 和种子数据中的权限码一致。

## 页面开发规范

后台管理页优先保持当前项目模式：

- 搜索表单
- 表格
- 分页
- 新增/编辑弹窗
- 行操作按钮
- Element Plus 组件
- Composition API + `<script setup lang="ts">`

页面文件位于 `src/views/<module>/<page>/index.vue`。如需新增业务模块，同时新增对应 API 文件、路由菜单种子数据和权限码。

## 登录与 token

登录页位于 `src/views/login/LoginView.vue`。token 工具位于 `src/utils/token.ts`。

当前支持：

- 用户名密码登录：调用 `/connect/token`
- refresh token 自动续期
- 本地退出：清理前端 token 和状态
- 服务端会话强制下线：后端返回 401 且带 `x-session-revoked=true`
- OIDC SSO 回调页：`src/views/sso/callback.vue`

## Docker 与 Nginx

前端 Dockerfile 构建静态文件，Nginx 配置在 `frontend/permission-admin/nginx.conf`。当前代理路径：

- `/api/`
- `/connect/`
- `/swagger/`
- `/hangfire/`
- `/hubs/`
- `/health`

注意：Swagger 是否可访问取决于后端环境。当前 API 只在 Development 开启 Swagger。

## 本地、Docker、生产差异

本地开发：

- Vite 服务运行在 `http://localhost:5173`。
- 需要配置 `VITE_API_BASE_URL` 指向本地 API。
- CORS 由后端 `Cors:AllowedOrigins` 放行 `http://localhost:5173`。

Docker：

- 前端运行在 Nginx 容器，默认宿主机 `http://localhost:8080`。
- `VITE_API_BASE_URL` 为空，走相对路径。
- Nginx 将 API 请求代理给 `permission-system-api:8080`。

生产：

- 建议使用构建产物或容器镜像部署。
- API 地址可以用相对路径配合反向代理，也可以在构建时设置 `VITE_API_BASE_URL`。
- OAuth 客户端配置必须与后端种子或生产配置一致。

## 常见问题

### 前端登录后 401

检查 `VITE_API_BASE_URL` 是否指向后端根地址，检查 `VITE_OAUTH_CLIENT_ID` 和 `VITE_OAUTH_CLIENT_SECRET` 是否与后端 `SeedData:OAuthClientSecret` 初始化出的客户端一致。

### 访问页面出现 403

检查当前用户是否拥有菜单权限和接口权限。调整角色权限后需要重新登录。

### Docker 前端请求 API 失败

检查 Nginx 代理路径、API 容器健康状态和 Docker 网络。宿主机默认 API 地址是 `http://localhost:5000`，容器内代理目标是 `http://permission-system-api:8080`。

### 构建失败

执行：

```powershell
cd frontend/permission-admin
npm install
npm run build
```

根据 TypeScript 或 Vue 类型错误定位。当前项目没有单独的前端 test/lint 脚本，`npm run build` 是主要前端验证命令。
