# 前端 UI 使用指南

## 1. 布局说明

前端后台布局由 `src/layouts/AdminLayout.vue` 统一承载，结构如下：

- `Sidebar`：左侧菜单，来源于当前用户动态菜单。
- `AppHeader`：顶部栏，包含折叠菜单、面包屑、全屏、主题切换、通知和用户下拉。
- `TabsView`：顶部页签区，位于 Header 下方、内容区上方。
- `AppMain`：主内容区，负责 `router-view` 和 `keep-alive`。

布局状态：

- 侧边栏折叠状态保存在 `localStorage`。
- 主题状态保存在 `localStorage`。
- 页签状态保存在 `sessionStorage`。
- 退出登录会清理用户状态、动态菜单、通知连接和页签状态。

## 2. TabsView 使用说明

TabsView 位于：

- `src/layouts/components/TabsView.vue`
- `src/stores/tabsView.ts`

行为规则：

- 路由切换后会自动加入页签。
- 首页固定显示，不能关闭。
- 当前页签高亮展示。
- 点击页签会跳转到对应路由。
- 页签过多时横向滚动，不撑破页面。
- 页签状态在当前浏览器会话内保留。

支持操作：

- 刷新当前页。
- 关闭当前页。
- 关闭其他页。
- 关闭左侧页。
- 关闭右侧页。
- 关闭全部页。

关闭规则：

- 固定页签不会被关闭。
- 关闭全部后保留首页。
- 关闭当前页后优先跳转右侧页签，否则跳转左侧页签，最后回到首页。

## 3. 路由 Meta 说明

推荐新增页面时补齐以下 meta：

```ts
meta: {
  title: '页面标题',
  icon: 'Menu',
  hidden: false,
  affix: false,
  noCache: false,
  cacheName: 'StableComponentName',
  alwaysShowTab: false,
  activeMenu: '/system/example',
  permissionCode: 'system:example:view',
}
```

字段说明：

- `title`：菜单、面包屑、页签标题。
- `icon`：菜单图标名称，优先使用 Element Plus 图标。
- `hidden`：是否隐藏在菜单和页签中。
- `affix`：是否固定页签，首页使用 `true`。
- `noCache`：是否禁用页面缓存。
- `cacheName`：缓存组件名，需要与页面 `defineOptions({ name })` 一致。
- `alwaysShowTab`：隐藏菜单路由仍需要进入页签时设为 `true`，如个人中心。
- `activeMenu`：详情页等隐藏路由需要高亮的菜单路径。
- `permissionCode`：页面级权限兜底校验。

## 4. 页面缓存说明

页面缓存由 `AppMain.vue` 和 `tabsView` store 控制：

- `meta.noCache = true` 的页面不缓存。
- 普通页面默认可缓存。
- `cachedViews` 保存组件 `name`。
- 页面组件需要设置稳定名称：

```ts
defineOptions({
  name: 'SystemUser',
})
```

刷新当前页时：

- 不刷新整个应用。
- 不清空所有页签。
- 只重新渲染当前内容区。
- 对缓存页会临时移除当前缓存名，再恢复缓存。

## 5. 右键菜单说明

页签支持鼠标右键菜单：

- 刷新当前页。
- 关闭当前页。
- 关闭其他页。
- 关闭左侧页。
- 关闭右侧页。
- 关闭全部页。

交互细节：

- 菜单跟随鼠标位置显示。
- 菜单会尽量避免超出窗口边界。
- 点击其他区域、路由变化、窗口滚动或窗口尺寸变化会自动关闭。
- 固定页签相关关闭操作会自动禁用或保留固定页签。

## 6. 主题切换说明

主题由 `src/stores/app.ts` 管理。

当前支持：

- `light`
- `dark`

实现规则：

- 主题状态保存到 `localStorage`。
- 应用启动时立即应用主题。
- 切换主题不刷新页面。
- Element Plus 引入了 dark CSS 变量。
- 自定义样式通过 `src/styles/variables.scss` 中的 CSS 变量适配主题。

新增样式时优先使用：

- `--app-bg`
- `--app-surface`
- `--app-surface-soft`
- `--app-border-color`
- `--app-border-soft`
- `--app-text`
- `--app-text-secondary`
- `--app-primary`

## 7. 新增页面开发规范

新增后台管理页面建议遵循：

1. 页面文件使用 `script setup` 和 TypeScript。
2. 设置稳定组件名，用于 keep-alive。
3. 使用 `PageContainer` 作为页面外层。
4. 使用 `TableToolbar` 放置刷新、密度、列设置和全屏操作。
5. 查询区使用 `el-form.toolbar`。
6. 表格使用 `el-table`，需要横向空间的列设置 `min-width`。
7. 分页使用 `.pager` 类。
8. 操作按钮继续使用 `v-permission`。
9. 接口调用继续复用 `src/api` 和 `src/utils/request.ts`。
10. 不在页面中直接绕过 token、权限或路由守卫。

推荐页面骨架：

```vue
<script setup lang="ts">
defineOptions({
  name: 'SystemExample',
})

import PageContainer from '../../components/PageContainer/index.vue'
import TableToolbar from '../../components/TableToolbar/index.vue'

function loadData() {
  // 查询数据
}
</script>

<template>
  <PageContainer title="示例管理" description="维护示例数据。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <!-- 查询条件 -->
    </el-form>

    <el-table border>
      <!-- 表格列 -->
    </el-table>
  </PageContainer>
</template>
```

## 8. 当前已统一的页面

以下页面已接入统一页面容器和工具栏：

- 用户管理
- 角色管理
- 菜单管理
- 权限管理
- 部门管理
- 租户管理
- 字典管理
- 参数配置
- 操作日志
- 登录日志
- 文件管理
- 健康检查
- 任务管理
- 通知中心
- 在线用户
- 定时任务

## 9. 验收建议

每次调整 UI 框架后至少验证：

- `npm run build`
- 登录页是否可访问。
- 未登录访问后台是否跳转登录页。
- 登录后动态菜单是否正常加载。
- 点击菜单是否新增页签。
- 页签右键菜单是否可用。
- 退出登录是否清理页签。
- light/dark 主题切换是否正常。

## 10. Account Center

The top-right user dropdown is the entry point for current-user account features.

- Personal Center opens `src/views/account/profile/index.vue` through `/account/profile`.
- The route uses `meta.hidden = true` so it is not displayed in the sidebar menu.
- The route uses `meta.alwaysShowTab = true` so it can still be opened in TabsView.
- The page uses `PageContainer` and current-user APIs from `src/api/me.ts`.

Personal Center supports:

- viewing the current user's tenant, department, roles, permission count, last login time, and creation time;
- editing nickname/display name, real name compatibility field, avatar URL, email, and phone number;
- calling `PUT /api/me/profile` and refreshing the auth store profile after a successful save;
- calling `POST /api/me/logout-all` to revoke all sessions and redirect to login.

Change Password is implemented as `src/components/ChangePasswordDialog/index.vue`.

- It is opened from `src/layouts/components/UserDropdown.vue`.
- It validates old password, new password, and confirm password before submitting.
- New passwords must be at least 8 characters and contain both letters and numbers.
- After `PUT /api/me/password` succeeds, the frontend clears local auth state and redirects to `/login`.

Logout behavior:

- The dropdown confirms before logout.
- It calls `POST /api/me/logout` with the stored refresh token.
- Local cleanup runs even when the backend request fails.
- Cleanup removes access token, refresh token, current user/profile state, dynamic menus, permissions, notification connection state, dynamic routes, and TabsView state.

## 11. Role Permission Matrix

The role management page includes a matrix-style permission assignment dialog.

- Entry: `src/views/system/role/index.vue`.
- Dialog: `src/views/system/role/components/RolePermissionMatrixDialog.vue`.
- Module panel: `src/views/system/role/components/PermissionModulePanel.vue`.
- Menu row: `src/views/system/role/components/PermissionMenuRow.vue`.
- Data scope dialog: `src/views/system/role/components/DataScopeDialog.vue`.
- Field permission dialog: `src/views/system/role/components/FieldPermissionDialog.vue`.
- Role users dialog: `src/views/system/role/components/RoleUserDialog.vue`.

UI behavior:

- The operation column shows `分配权限` when the user has `system:role:assign-permission`.
- The operation column shows `关联用户` when the user has `system:role:assign-user`.
- The role operation column keeps `编辑`, `分配权限`, `关联用户`, `数据范围`, and `删除`.
- The previous separate `菜单` and `权限` buttons are not shown on the role page; menu and button/API authorization is handled by `分配权限`.
- The dialog uses a wide layout with a fixed menu-name column, adaptive permission checkbox grid, and fixed right action column.
- Modules can be expanded or collapsed.
- Module checkbox selects or clears all menu rows and permission items under the module.
- Menu row checkbox selects or clears the menu and all permission items in that row.
- Selecting an action permission automatically selects the menu.
- Selecting create, update, delete, import, export, and similar action permissions also selects the row's view permission when it exists.
- Clearing all permission items in a row clears the menu row.

Data scope behavior:

- The current backend model supports role-level data scope only.
- The matrix shows a data-scope link on each row for convenience.
- Setting data scope from any row applies the selected scope to the whole role when saving.
- Custom department scope uses the existing department tree API.

Field permission behavior:

- Field authorization is currently reserved.
- The dialog opens from each row and shows an empty state when no field configuration source exists.
- It does not block menu and button/API permission saving.

Role user association behavior:

- The `关联用户` dialog supports keyword search, pagination, checked-state restore, cross-page selection retention, and full `userIds` save.
- Disabled users are not intended to be newly associated with roles; backend validation rejects disabled or cross-tenant users.
- After role-user relations are saved, affected users' menu, permission, and user-role caches are cleared. Normal users should log in again to refresh permission claims embedded in tokens.
