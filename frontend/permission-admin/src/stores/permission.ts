import { defineStore } from 'pinia'
import type { AxiosRequestConfig } from 'axios'
import { ref } from 'vue'
import type { MenuTreeResponse } from '../api/me'
import { getCurrentUserMenus, getCurrentUserPermissionCodes } from '../api/me'
import { router } from '../router'
import { buildMenuRoutes } from '../router/menuRoutes'

const dynamicRouteNames = new Set<string>()

export const usePermissionStore = defineStore('permission', () => {
  const menus = ref<MenuTreeResponse[]>([])
  const permissionCodes = ref<string[]>([])
  const routesLoaded = ref(false)

  async function loadPermissions(config?: AxiosRequestConfig) {
    const [menuData, permissionData] = await Promise.all([
      getCurrentUserMenus(config),
      getCurrentUserPermissionCodes(config),
    ])

    removeDynamicRoutes()
    menus.value = menuData
    permissionCodes.value = permissionData
    setupDynamicRoutes(menuData)
    routesLoaded.value = true
  }

  function hasPermission(permissionCode?: string) {
    return !permissionCode || permissionCodes.value.includes(permissionCode)
  }

  function reset() {
    menus.value = []
    permissionCodes.value = []
    routesLoaded.value = false
    removeDynamicRoutes()
  }

  function removeDynamicRoutes() {
    for (const name of dynamicRouteNames) {
      if (router.hasRoute(name)) {
        router.removeRoute(name)
      }
    }
    dynamicRouteNames.clear()
  }

  function setupDynamicRoutes(menuTree: MenuTreeResponse[]) {
    for (const route of buildMenuRoutes(menuTree)) {
      if (route.name && !router.hasRoute(route.name)) {
        router.addRoute('AdminRoot', route)
        dynamicRouteNames.add(String(route.name))
      }
    }
  }

  return { menus, permissionCodes, routesLoaded, loadPermissions, hasPermission, reset }
})
