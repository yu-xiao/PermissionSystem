import { defineStore } from 'pinia'
import { ref } from 'vue'
import type { RouteRecordRaw } from 'vue-router'
import type { MenuTreeResponse } from '../api/me'
import { getCurrentUserMenus, getCurrentUserPermissionCodes } from '../api/me'
import { router } from '../router'

const dynamicRouteNames = new Set<string>()

export const usePermissionStore = defineStore('permission', () => {
  const menus = ref<MenuTreeResponse[]>([])
  const permissionCodes = ref<string[]>([])
  const routesLoaded = ref(false)

  async function loadPermissions() {
    const [menuData, permissionData] = await Promise.all([
      getCurrentUserMenus(),
      getCurrentUserPermissionCodes(),
    ])

    menus.value = menuData
    permissionCodes.value = permissionData
    setupDynamicRoutes(menuData)
    routesLoaded.value = true
  }

  function hasPermission(permissionCode?: string) {
    if (!permissionCode) {
      return true
    }

    return permissionCodes.value.includes(permissionCode)
  }

  function reset() {
    menus.value = []
    permissionCodes.value = []
    routesLoaded.value = false

    for (const name of dynamicRouteNames) {
      if (router.hasRoute(name)) {
        router.removeRoute(name)
      }
    }

    dynamicRouteNames.clear()
  }

  function setupDynamicRoutes(menuTree: MenuTreeResponse[]) {
    const routes = buildRoutes(menuTree)

    for (const route of routes) {
      if (route.name && !router.hasRoute(route.name)) {
        router.addRoute('AdminRoot', route)
        dynamicRouteNames.add(String(route.name))
      }
    }
  }

  return {
    menus,
    permissionCodes,
    routesLoaded,
    loadPermissions,
    hasPermission,
    reset,
  }
})

function buildRoutes(menuTree: MenuTreeResponse[]): RouteRecordRaw[] {
  return menuTree.flatMap((menu) => {
    const current = menu.path
      ? [
          {
            path: normalizePath(menu.path),
            name: `Menu_${menu.id}`,
            meta: {
              title: menu.name,
              icon: menu.icon,
              hidden: menu.visible === false,
              permissionCode: menu.permissionCode,
              order: menu.sort,
              noCache: false,
              cacheName: resolveMenuCacheName(menu),
            },
            component: resolveMenuComponent(menu),
          } satisfies RouteRecordRaw,
        ]
      : []

    return [...current, ...buildRoutes(menu.children ?? [])]
  })
}

function normalizePath(path: string) {
  return path.replace(/^\/+/, '')
}

function resolveMenuComponent(menu: MenuTreeResponse) {
  const key = (menu.component || menu.path || '').toLowerCase()

  if (key.includes('online-user') || key.includes('online')) {
    return () => import('../views/system/online-user/index.vue')
  }

  if (key.includes('user')) {
    return () => import('../views/system/user/index.vue')
  }

  if (key.includes('tenant')) {
    return () => import('../views/system/tenant/index.vue')
  }

  if (key.includes('department')) {
    return () => import('../views/system/department/index.vue')
  }

  if (key.includes('dict')) {
    return () => import('../views/system/dict/index.vue')
  }

  if (key.includes('config')) {
    return () => import('../views/system/config/index.vue')
  }

  if (key.includes('file')) {
    return () => import('../views/system/file/index.vue')
  }

  if (key.includes('role')) {
    return () => import('../views/system/role/index.vue')
  }

  if (key.includes('menu')) {
    return () => import('../views/system/menu/index.vue')
  }

  if (key.includes('permission')) {
    return () => import('../views/system/permission/index.vue')
  }

  if (key.includes('operation-log')) {
    return () => import('../views/system/operation-log/index.vue')
  }

  if (key.includes('login-log')) {
    return () => import('../views/system/login-log/index.vue')
  }

  if (key.includes('outbox-message') || key.includes('outbox')) {
    return () => import('../views/system/outbox-message/index.vue')
  }

  if (key.includes('inbox-message') || key.includes('inbox')) {
    return () => import('../views/system/inbox-message/index.vue')
  }

  if (key.includes('health')) {
    return () => import('../views/system/health/index.vue')
  }

  if (key.includes('job')) {
    return () => import('../views/system/job/index.vue')
  }

  if (key.includes('notification-admin')) {
    return () => import('../views/system/notification-admin/index.vue')
  }

  if (key.includes('notification')) {
    return () => import('../views/system/notification/index.vue')
  }

  if (key.includes('scheduled-task') || key.includes('scheduled')) {
    return () => import('../views/system/scheduled-task/index.vue')
  }

  return () => import('../views/RoutePlaceholder.vue')
}

function resolveMenuCacheName(menu: MenuTreeResponse) {
  const key = (menu.component || menu.path || '').toLowerCase()

  if (key.includes('online-user') || key.includes('online')) {
    return 'SystemOnlineUser'
  }

  if (key.includes('user')) {
    return 'SystemUser'
  }

  if (key.includes('tenant')) {
    return 'SystemTenant'
  }

  if (key.includes('department')) {
    return 'SystemDepartment'
  }

  if (key.includes('dict')) {
    return 'SystemDict'
  }

  if (key.includes('config')) {
    return 'SystemConfig'
  }

  if (key.includes('file')) {
    return 'SystemFile'
  }

  if (key.includes('role')) {
    return 'SystemRole'
  }

  if (key.includes('menu')) {
    return 'SystemMenu'
  }

  if (key.includes('permission')) {
    return 'SystemPermission'
  }

  if (key.includes('operation-log')) {
    return 'SystemOperationLog'
  }

  if (key.includes('login-log')) {
    return 'SystemLoginLog'
  }

  if (key.includes('outbox-message') || key.includes('outbox')) {
    return 'SystemOutboxMessage'
  }

  if (key.includes('inbox-message') || key.includes('inbox')) {
    return 'SystemInboxMessage'
  }

  if (key.includes('health')) {
    return 'SystemHealth'
  }

  if (key.includes('job')) {
    return 'SystemJob'
  }

  if (key.includes('notification-admin')) {
    return 'SystemNotificationAdmin'
  }

  if (key.includes('notification')) {
    return 'SystemNotification'
  }

  if (key.includes('scheduled-task') || key.includes('scheduled')) {
    return 'SystemScheduledTask'
  }

  return 'RoutePlaceholder'
}
