import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { login as loginApi } from '../api/auth'
import type { CurrentUserResponse, MyProfileResponse } from '../api/me'
import { getCurrentUser, getMyProfile, logout as logoutApi } from '../api/me'
import { authorizationStateReloadRequestConfig } from '../utils/request'
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from '../utils/token'
import { useNotificationStore } from './notifications'
import { usePermissionStore } from './permission'
import { useTabsViewStore } from './tabsView'
import { useTenantStore } from './tenant'

export const useAuthStore = defineStore('auth', () => {
  const currentUser = ref<CurrentUserResponse>()
  const currentProfile = ref<MyProfileResponse>()
  const isLoaded = ref(false)
  const tenantStore = useTenantStore()

  const isAuthenticated = computed(() => Boolean(getAccessToken()))
  const isSuperAdmin = computed(() => currentUser.value?.isSuperAdmin === true)
  const effectiveTenantId = computed(() =>
    isSuperAdmin.value
      ? tenantStore.targetTenantId || currentUser.value?.tenantId || ''
      : currentUser.value?.tenantId || '',
  )

  async function login(username: string, password: string) {
    tenantStore.clearTarget()
    const token = await loginApi({ username, password })
    setTokens({
      accessToken: token.access_token,
      refreshToken: token.refresh_token,
    })

    await loadCurrentUser()
  }

  async function loadCurrentUser() {
    const permissionStore = usePermissionStore()

    currentUser.value = await getCurrentUser()
    synchronizeTenantSelection()
    await permissionStore.loadPermissions()
    isLoaded.value = true
  }

  async function reloadAuthorizationState() {
    const permissionStore = usePermissionStore()

    currentUser.value = await getCurrentUser(authorizationStateReloadRequestConfig)
    synchronizeTenantSelection()
    await permissionStore.loadPermissions(authorizationStateReloadRequestConfig)
    isLoaded.value = true
  }

  async function loadMyProfile() {
    currentProfile.value = await getMyProfile()
    return currentProfile.value
  }

  function hasPermission(permissionCode?: string) {
    if (!permissionCode) {
      return true
    }

    const permissionStore = usePermissionStore()
    return isSuperAdmin.value || permissionStore.hasPermission(permissionCode)
  }

  function clearSession() {
    const permissionStore = usePermissionStore()
    const notificationStore = useNotificationStore()
    const tabsViewStore = useTabsViewStore()

    clearTokens()
    tenantStore.clearTarget()
    currentUser.value = undefined
    currentProfile.value = undefined
    isLoaded.value = false
    notificationStore.stop()
    permissionStore.reset()
    tabsViewStore.reset()
  }

  function synchronizeTenantSelection() {
    if (!currentUser.value?.isSuperAdmin) {
      tenantStore.clearTarget()
      return
    }

    if (!tenantStore.targetTenantId && currentUser.value.tenantId) {
      tenantStore.selectTenant(currentUser.value.tenantId)
    }
  }

  async function logout() {
    const refreshToken = getRefreshToken()
    try {
      await logoutApi(refreshToken)
    } catch {
      // Local state must still be cleared even if the server-side logout call fails.
    } finally {
      clearSession()
    }
  }

  return {
    currentUser,
    currentProfile,
    isLoaded,
    isAuthenticated,
    isSuperAdmin,
    effectiveTenantId,
    login,
    loadCurrentUser,
    reloadAuthorizationState,
    loadMyProfile,
    hasPermission,
    clearSession,
    logout,
  }
})
