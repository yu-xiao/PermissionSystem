import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { login as loginApi } from '../api/auth'
import type { CurrentUserResponse, MyProfileResponse } from '../api/me'
import { getCurrentUser, getMyProfile, logout as logoutApi } from '../api/me'
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from '../utils/token'
import { useNotificationStore } from './notifications'
import { usePermissionStore } from './permission'
import { useTabsViewStore } from './tabsView'

export const useAuthStore = defineStore('auth', () => {
  const currentUser = ref<CurrentUserResponse>()
  const currentProfile = ref<MyProfileResponse>()
  const isLoaded = ref(false)

  const isAuthenticated = computed(() => Boolean(getAccessToken()))
  const isSuperAdmin = computed(() => currentUser.value?.isSuperAdmin === true)

  async function login(username: string, password: string) {
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
    await permissionStore.loadPermissions()
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
    currentUser.value = undefined
    currentProfile.value = undefined
    isLoaded.value = false
    notificationStore.stop()
    permissionStore.reset()
    tabsViewStore.reset()
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
    login,
    loadCurrentUser,
    loadMyProfile,
    hasPermission,
    clearSession,
    logout,
  }
})
