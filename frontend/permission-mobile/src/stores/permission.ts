import type { AxiosRequestConfig } from 'axios'
import { computed, ref, toValue, type MaybeRefOrGetter } from 'vue'
import { defineStore } from 'pinia'
import { getPermissionSnapshot } from '../api/permission'
import type { MenuTreeResponse } from '../api/me'

export const usePermissionStore = defineStore('permission', () => {
  const menus = ref<MenuTreeResponse[]>([])
  const permissionCodes = ref<string[]>([])
  const loading = ref(false)
  const routesLoaded = ref(false)
  const isSuperAdmin = ref(false)

  async function loadPermissions(config?: AxiosRequestConfig) {
    loading.value = true
    try {
      const snapshot = await getPermissionSnapshot(config)
      menus.value = snapshot.menus
      permissionCodes.value = snapshot.permissionCodes
      routesLoaded.value = true
      return snapshot
    } finally {
      loading.value = false
    }
  }

  function setSuperAdmin(value: boolean) {
    isSuperAdmin.value = value
  }

  function hasPermission(permissionCode?: string) {
    return !permissionCode || isSuperAdmin.value || permissionCodes.value.includes(permissionCode)
  }

  function canAny(codes: readonly string[]) {
    return isSuperAdmin.value || codes.some((code) => permissionCodes.value.includes(code))
  }

  function canAll(codes: readonly string[]) {
    return isSuperAdmin.value || codes.every((code) => permissionCodes.value.includes(code))
  }

  function reset() {
    menus.value = []
    permissionCodes.value = []
    loading.value = false
    routesLoaded.value = false
    isSuperAdmin.value = false
  }

  return {
    menus,
    permissionCodes,
    loading,
    routesLoaded,
    isSuperAdmin,
    loadPermissions,
    setSuperAdmin,
    hasPermission,
    canAny,
    canAll,
    reset,
  }
})

/** Small composable used by buttons and action lists in mobile views. */
export function usePermission(permissionCode?: MaybeRefOrGetter<string | undefined>) {
  const store = usePermissionStore()
  return computed(() => store.hasPermission(toValue(permissionCode)))
}
