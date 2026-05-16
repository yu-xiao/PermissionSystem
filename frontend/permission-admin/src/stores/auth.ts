import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { login as loginApi } from '../api/auth'
import type { CurrentUserResponse } from '../api/me'
import { getCurrentUser } from '../api/me'
import { clearTokens, getAccessToken, setTokens } from '../utils/token'
import { usePermissionStore } from './permission'

export const useAuthStore = defineStore('auth', () => {
  const currentUser = ref<CurrentUserResponse>()
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

  function hasPermission(permissionCode?: string) {
    if (!permissionCode) {
      return true
    }

    const permissionStore = usePermissionStore()
    return isSuperAdmin.value || permissionStore.hasPermission(permissionCode)
  }

  function logout() {
    const permissionStore = usePermissionStore()

    clearTokens()
    currentUser.value = undefined
    isLoaded.value = false
    permissionStore.reset()
  }

  return {
    currentUser,
    isLoaded,
    isAuthenticated,
    isSuperAdmin,
    login,
    loadCurrentUser,
    hasPermission,
    logout,
  }
})
