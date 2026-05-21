# 前端 UI 优化分析与实施计划

## 一、当前结构检查结论

### 1. layouts 目录结构

当前仅有一个布局文件：

- `frontend/permission-admin/src/layouts/AdminLayout.vue`

现状：

- `AdminLayout.vue` 同时承担侧边栏、顶部栏、用户下拉、通知入口、内容区渲染。
- 侧边栏宽度固定为 `232px`，折叠按钮已展示但没有绑定折叠状态。
- 主内容区直接渲染 `<router-view />`，未接入 tabs、breadcrumb、keep-alive。
- 菜单图标目前统一使用 `MenuIcon`，未使用后端菜单返回的 `icon` 字段。
- 当前激活菜单写死为 `default-active="/dashboard"`，切换页面后不能准确跟随当前路由。

### 2. router 配置

当前路由文件：

- `frontend/permission-admin/src/router/index.ts`

现状：

- 静态路由只有 `/login`、`/`、`/dashboard`。
- `/` 使用 `AdminLayout`，动态菜单路由由 `permission` store 后续注入到 `AdminRoot` 子路由。
- 路由守卫基于 token 判断登录态；未登录访问非公开页面时跳转 `/login?redirect=...`。
- 首次登录态访问业务页面时，会调用 `authStore.loadCurrentUser()` 加载用户、菜单、权限，再返回 `to.fullPath` 触发一次重新匹配。
- 当前静态路由 meta 只使用了 `public`，动态路由 meta 只写入了 `title`、`permissionCode`。

主要限制：

- 缺少 `hidden`、`icon`、`affix`、`keepAlive`、`activeMenu`、`breadcrumb` 等后台管理常用 meta。
- dashboard 路由缺少标题、图标、固定页签等 meta。
- 动态路由依赖字符串匹配组件，菜单组件路径没有形成稳定映射表。

### 3. stores 结构

当前 store：

- `auth.ts`：登录态、当前用户、是否超级管理员、登录/退出、权限判断。
- `permission.ts`：当前用户菜单、权限码、动态路由注入、动态路由重置。
- `notifications.ts`：通知未读数、最新通知、SignalR 连接。
- `index.ts`：创建并导出 Pinia 实例。

缺口：

- 没有布局状态 store，例如侧栏折叠、设备类型、主题、页面尺寸。
- 没有 tabs/tags-view store。
- 没有 keep-alive 缓存列表 store。
- 没有用户偏好 store，例如主题色、暗色模式、布局密度。

### 4. views 页面风格

当前页面集中在：

- `src/views/dashboard/IndexView.vue`
- `src/views/login/LoginView.vue`
- `src/views/system/**/index.vue`

现状：

- 系统管理页面大多采用 `section.page + el-form.toolbar + el-table + el-pagination + el-dialog` 的模式。
- 全局样式提供了 `.page`、`.toolbar`、`.pager`、`.full-width` 等基础类。
- 页面整体偏功能可用，适合继续演进为企业后台。
- 多数页面的查询区、表格、分页、弹窗都在单文件内完成，复用层较少。

主要问题：

- 页面缺少统一的页面头、操作区、表格容器、搜索区域、空状态和响应式规范。
- 查询表单在小屏和复杂筛选场景下容易拥挤。
- 表格区域没有统一高度策略，页面之间视觉密度不一致。
- 业务页没有统一 breadcrumb、tabs、刷新、返回顶部、页面缓存等后台常用体验。

### 5. 当前菜单渲染逻辑

位置：

- `src/layouts/AdminLayout.vue`
- `src/stores/permission.ts`

现状：

- `AdminLayout.vue` 从 `permissionStore.menus` 渲染菜单。
- 一级菜单有子级时渲染 `el-sub-menu`，子级渲染 `el-menu-item`。
- 没有递归菜单组件，目前只自然支持两级菜单展示。
- 菜单项 `index` 使用 `menu.path || menu.id`。
- 动态路由由 `permission.ts` 的 `buildRoutes()` 扁平化构建。
- `resolveMenuComponent()` 通过 `menu.component || menu.path` 的字符串关键字匹配实际 view 组件。

主要问题：

- 菜单渲染和布局耦合在 `AdminLayout.vue`，后续扩展折叠、递归、图标、隐藏菜单会比较吃力。
- 未使用 `visible`、`icon`、`redirect` 等菜单字段。
- 菜单 active 状态没有跟随 `route.path`。
- 动态路由 name 使用 `Menu_${menu.id}`，对 keep-alive 和 tabs 可用，但页面组件本身未设置稳定组件名。

### 6. 当前权限指令 v-permission

位置：

- `src/directives/permission.ts`

现状：

- 通过 `setupPermissionDirective(app)` 注册 `v-permission`。
- 指令在 `mounted` 阶段判断：
  - 超级管理员直接通过。
  - 否则检查 `permissionStore.hasPermission(binding.value)`。
  - 无权限时直接 `element.remove()`。

主要问题：

- 只在 `mounted` 执行一次，如果权限异步变化或用户信息刷新，DOM 不会自动恢复或再次判断。
- 只支持单个字符串权限，不支持数组和 `and/or` 模式。
- 直接移除元素，后续如果权限变化无法恢复。
- 页面级权限主要依赖后端菜单和动态路由，路由 meta 的 `permissionCode` 当前没有在前端守卫中二次校验。

### 7. 当前 token / request 封装

位置：

- `src/utils/token.ts`
- `src/utils/request.ts`
- `src/api/http.ts`
- `src/api/auth.ts`

现状：

- token 存储在 `localStorage`，key 为 `permission_system_access_token` 和 `permission_system_refresh_token`。
- Axios 实例统一在 `utils/request.ts` 创建。
- 请求拦截器自动添加 `Authorization: Bearer <token>`。
- 非 GET/HEAD/OPTIONS 请求自动添加 `X-Idempotency-Key`。
- 401 时支持 refresh token 单飞刷新，刷新成功后重放原请求。
- 401 且 `x-session-revoked=true` 时提示强制下线并跳转登录。
- 429 有统一提示。
- `api/http.ts` 只是把 `request` 重新导出为 `http`。

主要风险：

- `localStorage` 方案简单直接，但存在 XSS 场景下 token 暴露风险，需要依赖整体前端安全策略。
- `redirectToLogin()` 使用 `window.location.href`，会触发整页刷新，和 SPA 路由状态、tabs 状态不容易协同。
- 登录接口和刷新接口都依赖 `VITE_OAUTH_CLIENT_SECRET`，前端构建产物中不应放真正敏感的 confidential client secret；如果该值只是公开客户端占位，需要在部署说明中明确。

### 8. 当前 Element Plus 引入方式

位置：

- `src/main.ts`

现状：

- 全量引入 `ElementPlus`。
- 全量引入 `element-plus/dist/index.css`。
- 使用中文语言包 `zhCn`。
- 图标按需从 `@element-plus/icons-vue` 在页面中导入。

优化方向：

- 当前全量引入适合早期快速开发。
- 若后续关注首屏体积，可引入 `unplugin-vue-components`、`unplugin-auto-import` 做 Element Plus 组件与样式按需导入，但这会增加构建配置复杂度，不建议和本次 UI 结构优化第一步混在一起做。

### 9. tabs / tags-view / visited views 实现

检查结果：

- 未发现 `TabsView`、`tags-view`、`visitedViews`、`cachedViews` 等实现。
- 当前没有页签 store。
- 当前没有右键菜单。
- 当前没有固定首页页签。
- 当前没有关闭当前、关闭其他、关闭全部、刷新当前页等能力。

### 10. keep-alive 页面缓存实现

检查结果：

- `App.vue` 和 `AdminLayout.vue` 都是直接 `<router-view />`。
- 未发现 `<keep-alive>` / `<KeepAlive>`。
- 路由 meta 中没有 `keepAlive`、`noCache`、`cacheKey`。
- 页面组件没有统一 `defineOptions({ name: ... })`，后续按组件名缓存时需要补齐。

## 二、当前前端问题清单

1. 布局职责过重：`AdminLayout.vue` 同时处理品牌、菜单、头部、通知、用户操作和内容区。
2. 菜单不支持递归：目前主要支持两级菜单，后续三级及以上菜单展示受限。
3. 菜单 active 固定：`default-active="/dashboard"` 无法反映当前路由。
4. 菜单字段使用不足：`icon`、`visible`、`redirect` 等后端字段未完整消费。
5. 动态路由组件解析偏脆弱：通过字符串 `includes` 匹配组件，随着页面增多容易误匹配。
6. 路由 meta 不完整：无法支撑企业后台的标题、图标、隐藏菜单、固定页签、缓存、面包屑等能力。
7. 缺少 tabs/tags-view：多页面操作效率不足，用户无法快速切换最近访问页面。
8. 缺少页面缓存：列表页查询条件、滚动位置、弹窗上下文在切换页面时容易丢失。
9. 权限指令能力偏基础：不支持数组权限、权限模式，也不响应后续权限变化。
10. 页面容器规范不足：搜索区、表格区、分页区虽然有基础类，但还没有形成企业后台组件规范。
11. 主题体系未抽象：颜色、边框、布局尺寸散落在布局和全局 CSS 中。
12. 折叠按钮未生效：侧栏折叠 UI 已有入口，但没有状态和菜单折叠联动。
13. 页面标题体系不足：没有统一 breadcrumb、document title、页面头区域。
14. Element Plus 全量引入：功能可用，但长期可能影响包体积。
15. 登录跳转使用整页刷新：请求层 401 跳转不易与 Pinia、Router、Tabs 状态统一清理。

## 三、推荐的企业级后台布局设计

推荐演进为分层布局：

```text
src/layouts/
├── AdminLayout.vue
├── components/
│   ├── AppSidebar.vue
│   ├── AppMenu.vue
│   ├── AppMenuItem.vue
│   ├── AppHeader.vue
│   ├── Breadcrumb.vue
│   ├── TabsView.vue
│   ├── TabsContextMenu.vue
│   ├── UserDropdown.vue
│   └── ThemeSwitch.vue
```

布局结构建议：

```text
AdminLayout
├── AppSidebar
│   └── AppMenu
│       └── AppMenuItem 递归渲染菜单
├── 主区域
│   ├── AppHeader
│   │   ├── 折叠按钮
│   │   ├── Breadcrumb
│   │   ├── NotificationBell
│   │   └── UserDropdown
│   ├── TabsView
│   └── AppMain
│       └── RouterView + KeepAlive
```

设计原则：

- 不改变现有登录、权限、动态菜单主流程。
- 先把布局能力拆出来，再接入 tabs 和 keep-alive。
- 动态菜单仍以 `/api/me/menus` 为数据源。
- 权限仍以 `permissionStore.permissionCodes` 和后端菜单为主，不绕过现有授权链路。

## 四、需要新增的组件列表

### 布局组件

- `AppSidebar.vue`：侧栏容器，负责折叠宽度、品牌区、滚动区域。
- `AppHeader.vue`：顶部栏，承载折叠按钮、面包屑、通知、用户菜单、主题入口。
- `AppMain.vue`：主内容容器，负责 router-view、keep-alive、刷新 key。
- `Breadcrumb.vue`：基于 `route.matched` 和 meta 生成面包屑。

### 菜单组件

- `AppMenu.vue`：接收菜单树、当前 active path、折叠状态。
- `AppMenuItem.vue`：递归渲染 `el-menu-item` / `el-sub-menu`。
- `MenuIcon.vue`：根据后端 `icon` 字段映射 Element Plus 图标，找不到时降级为默认图标。

### Tabs 组件

- `TabsView.vue`：页签容器，渲染 visited views。
- `TabsContextMenu.vue`：页签右键菜单。

### 通用页面组件

- `PageContainer.vue`：统一页面内边距、背景、表格高度策略。
- `SearchPanel.vue`：统一搜索区折叠、重置、查询按钮布局。
- `TableToolbar.vue`：统一新增、导入、导出、刷新、密度等操作。

这些通用页面组件建议在布局稳定后逐步引入，不建议一次性重写所有业务页面。

## 五、需要新增的 store

### 1. `app` store

建议文件：

- `src/stores/app.ts`

职责：

- `sidebarCollapsed`
- `device`
- `layoutMode`
- `contentWidth`
- `toggleSidebar()`
- `setDevice()`

### 2. `tagsView` store

建议文件：

- `src/stores/tags-view.ts`

职责：

- `visitedViews`
- `cachedViews`
- `addView(route)`
- `addVisitedView(route)`
- `addCachedView(route)`
- `delView(route)`
- `delCachedView(route)`
- `delOthersViews(route)`
- `delLeftViews(route)`
- `delRightViews(route)`
- `delAllViews()`
- `updateVisitedView(route)`

### 3. `settings` store

建议文件：

- `src/stores/settings.ts`

职责：

- `themeColor`
- `size`
- `showTabs`
- `showBreadcrumb`
- `fixedHeader`
- `enablePageCache`
- 持久化用户偏好。

## 六、需要调整的路由 meta 字段

建议扩展 `RouteMeta`：

```ts
interface RouteMeta {
  public?: boolean
  title?: string
  icon?: string
  permissionCode?: string
  hidden?: boolean
  affix?: boolean
  closable?: boolean
  keepAlive?: boolean
  cacheKey?: string
  activeMenu?: string
  breadcrumb?: boolean
  noRedirect?: boolean
  order?: number
}
```

字段说明：

- `title`：菜单、页签、面包屑、document title 统一使用。
- `icon`：菜单图标。
- `hidden`：不在侧栏展示，但仍允许路由访问。
- `affix`：固定页签，例如 dashboard。
- `closable`：是否允许关闭，默认 true，固定页签 false。
- `keepAlive`：是否缓存页面组件。
- `cacheKey`：缓存名，默认可使用 route name。
- `activeMenu`：详情页等隐藏页面激活对应父菜单。
- `breadcrumb`：是否显示在面包屑，默认 true。
- `permissionCode`：页面级权限标识，可在守卫中二次校验。
- `order`：菜单或页签排序辅助字段。

动态路由构建时建议从后端菜单映射：

- `menu.name -> meta.title`
- `menu.icon -> meta.icon`
- `menu.visible === false -> meta.hidden`
- `menu.permissionCode -> meta.permissionCode`
- `menu.sort -> meta.order`
- `menu.redirect -> route.redirect`
- `menu.component -> component 映射表`

## 七、TabsView 页签设计

### 数据结构

```ts
interface TagView {
  name?: string
  path: string
  fullPath: string
  title: string
  query?: Record<string, unknown>
  params?: Record<string, unknown>
  meta: RouteMeta
}
```

### 添加规则

- 在 `router.afterEach` 中添加当前路由到 `visitedViews`。
- `meta.hidden === true` 的路由默认不添加页签，除非设置 `meta.activeMenu` 或明确允许。
- `/dashboard` 设置为 `affix: true`，固定在首位。
- 相同 `fullPath` 不重复添加；同 path 不同 query 可根据业务决定是否视为不同页签。

### 展示规则

- 页签标题优先取 `route.meta.title`。
- 当前页签高亮匹配 `route.fullPath`，必要时用 `activeMenu` 辅助。
- 固定页签不显示关闭按钮。
- 页签过多时横向滚动。

### 操作能力

- 关闭当前。
- 关闭左侧。
- 关闭右侧。
- 关闭其他。
- 关闭全部。
- 刷新当前。
- 重新加载当前页面时不清空其他页签。

## 八、右键菜单设计

右键菜单触发位置：

- `TabsView` 中每一个页签。

菜单项建议：

- `刷新当前页`
- `关闭当前页`
- `关闭左侧页签`
- `关闭右侧页签`
- `关闭其他页签`
- `关闭全部页签`

禁用规则：

- 固定页签禁用 `关闭当前页`。
- 当前页签左侧没有可关闭页签时禁用 `关闭左侧页签`。
- 当前页签右侧没有可关闭页签时禁用 `关闭右侧页签`。
- 只有一个非固定页签时禁用 `关闭其他页签`。
- 所有页签均固定时禁用 `关闭全部页签`。

交互细节：

- 右键菜单使用绝对定位，记录鼠标坐标。
- 点击菜单项后关闭菜单。
- 点击页面其他区域或切换路由时关闭菜单。
- 菜单层级应高于 header 和 tabs。

## 九、页面缓存设计

### 推荐实现

在 `AppMain.vue` 中使用：

```vue
<router-view v-slot="{ Component, route }">
  <keep-alive :include="cachedViews">
    <component :is="Component" :key="route.fullPath" />
  </keep-alive>
</router-view>
```

需要注意：

- `cachedViews` 应保存组件 name，而不是 path。
- 使用 `<script setup>` 的页面组件需要补齐 `defineOptions({ name: 'SystemUser' })` 这类稳定名称。
- 动态路由 name、组件 name、缓存 name 要建立一致规则。
- 不适合缓存的页面设置 `meta.keepAlive = false`。
- 刷新当前页时，可以临时从 `cachedViews` 移除当前组件，再恢复并重新进入。

### 缓存策略建议

默认缓存：

- 列表页。
- 查询条件复杂的管理页。
- 用户、角色、菜单、权限、租户、字典、配置、任务等系统页。

默认不缓存：

- 登录页。
- 纯详情弹窗页。
- 实时性强的健康检查页。
- 通知列表可以按业务体验决定。

### 风险点

- 如果直接用 `route.fullPath` 作为组件 key，每个 query 都可能生成新的组件实例。
- 如果只用 `route.path`，同一路由不同参数页面可能互相覆盖状态。
- 对列表页推荐先用 `route.name` 缓存，再由页面自己控制查询参数恢复策略。

## 十、主题和样式设计

### CSS 变量

建议在全局样式中抽象：

```css
:root {
  --app-sidebar-width: 232px;
  --app-sidebar-collapsed-width: 64px;
  --app-header-height: 56px;
  --app-tabs-height: 40px;
  --app-border-color: #d9e2ef;
  --app-bg: #f5f7fb;
  --app-surface: #ffffff;
  --app-text: #1f2937;
  --app-text-secondary: #64748b;
}
```

### 视觉原则

- 后台系统以高信息密度、清晰层级、稳定操作为主。
- 保持白色内容面、浅灰背景、清晰边框。
- 避免过度装饰和大面积营销式视觉。
- 搜索区、表格区、分页区统一间距。
- 操作按钮统一主次层级：查询/保存为 primary，重置/取消为默认，删除为 danger。

### Element Plus 主题

短期：

- 继续使用全量 Element Plus，降低改动风险。
- 通过 CSS 变量和局部类统一布局。

中期：

- 引入主题变量文件，例如 `src/styles/variables.css`。
- 按模块拆分 `base.css`、`layout.css`、`element-overrides.css`。

长期：

- 如关注构建体积，再评估 Element Plus 自动导入和样式按需。

## 十一、分步骤实现计划

### 第 1 步：补齐路由 meta 设计

- 为 dashboard 增加 `title`、`icon`、`affix`、`keepAlive`。
- 动态路由构建时映射 `icon`、`visible`、`redirect`、`sort`。
- 增加统一 RouteMeta 类型声明。
- 不改变现有登录和动态菜单加载流程。

### 第 2 步：拆分布局组件

- 从 `AdminLayout.vue` 拆出 `AppSidebar`、`AppHeader`、`AppMain`。
- 保持 UI 外观基本不变。
- 接入 `app` store 实现侧栏折叠。
- 修正菜单 active 为当前路由或 `route.meta.activeMenu`。

### 第 3 步：实现递归菜单

- 新增 `AppMenu` 和 `AppMenuItem`。
- 支持多级菜单。
- 支持隐藏菜单。
- 支持图标映射和默认图标降级。
- 保持后端菜单权限过滤结果为唯一菜单来源。

### 第 4 步：新增 tagsView store

- 新增 `visitedViews` 和 `cachedViews`。
- 在路由切换后添加访问页签。
- 固定 dashboard。
- 退出登录时清空页签和缓存。

### 第 5 步：实现 TabsView

- 展示页签列表。
- 支持关闭当前、关闭其他、关闭全部。
- 路由切换和页签点击互相同步。
- 不破坏已有动态路由访问。

### 第 6 步：实现右键菜单

- 在 TabsView 页签上接入右键菜单。
- 实现刷新、关闭左侧、关闭右侧等操作。
- 完成固定页签和边界状态禁用逻辑。

### 第 7 步：接入 keep-alive

- 在 `AppMain` 中接入 `<keep-alive>`。
- 给需要缓存的页面补稳定组件名。
- 路由 meta 增加 `keepAlive`。
- 实现刷新当前页时清理当前缓存。

### 第 8 步：统一页面容器与样式

- 新增 `PageContainer`、`SearchPanel`、`TableToolbar`。
- 先选择 1 到 2 个页面试点，例如用户管理、角色管理。
- 验证模式稳定后再逐步迁移其他页面。
- 不删除现有页面，不一次性重写业务模块。

### 第 9 步：增强权限指令

- 支持字符串和字符串数组。
- 支持 `and/or` 模式。
- 避免直接不可逆移除 DOM，可考虑响应式控制显示。
- 路由守卫中对 `meta.permissionCode` 做前端二次兜底校验。

### 第 10 步：验证与回归

- 验证登录、退出、刷新 token、强制下线。
- 验证动态菜单、动态路由、权限按钮。
- 验证 dashboard 固定页签。
- 验证关闭页签后的跳转目标。
- 验证 keep-alive 对列表查询条件和页面状态的保持。
- 验证无权限菜单和无权限按钮不展示。

## 十二、推荐实施顺序

优先级建议：

1. 路由 meta 规范。
2. 布局组件拆分。
3. 递归菜单和 active 菜单。
4. tabsView store。
5. TabsView UI。
6. 右键菜单。
7. keep-alive。
8. 页面容器组件。
9. 权限指令增强。
10. Element Plus 按需引入评估。

这样可以先补足企业后台框架能力，再逐步统一业务页面体验，风险较低，也不会破坏当前登录、权限和动态菜单主链路。
