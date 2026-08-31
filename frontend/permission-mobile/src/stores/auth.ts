import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  beginAuthorization,
  exchangeAuthorizationCode,
  revokeToken,
  type AuthorizationCallback,
  type OAuthClientConfig,
} from '../api/auth'
import {
  changeMyPassword,
  getCurrentUser,
  getMyProfile,
  logoutAllSessions,
  logoutSession,
  updateMyProfile,
  type ChangeMyPasswordRequest,
  type CurrentUserResponse,
  type MyProfileResponse,
  type UpdateMyProfileRequest,
} from '../api/me'
import {
  authorizationStateReloadRequestConfig,
  configureAuthorizationStateReloader,
  configureRequestHandlers,
} from '../utils/request'
import { clearPkceTransaction, validateAuthorizationIssuer } from '../utils/pkce'
import {
  clearTokens,
  getAccessToken,
  getRefreshToken,
  getTokenSnapshot as readTokenSnapshot,
  hydrateTokens,
  isAccessTokenExpired,
  setTokens,
  type TokenSnapshot,
} from '../utils/token'
import { useNotificationStore } from './notifications'
import { usePermissionStore } from './permission'
import { useTenantStore } from './tenant'

export const useAuthStore = defineStore('auth', () => {
  const currentUser = ref<CurrentUserResponse>()
  const currentProfile = ref<MyProfileResponse>()
  const isLoaded = ref(false)
  const loading = ref(false)
  const tokenRevision = ref(0)
  const tenantStore = useTenantStore()

  const isAuthenticated = computed(() => {
    tokenRevision.value
    return Boolean(getAccessToken())
  })
  const isSuperAdmin = computed(() => currentUser.value?.isSuperAdmin === true)
  const effectiveTenantId = computed(() =>
    tenantStore.targetTenantId || currentUser.value?.tenantId || '',
  )

  function touchTokenState() {
    tokenRevision.value += 1
  }

  function configureRequestLifecycle() {
    configureRequestHandlers({
      onUnauthorized: () => {
        clearSession()
      },
    })
    configureAuthorizationStateReloader(reloadAuthorizationState)
  }

  async function startLogin(overrides: Partial<OAuthClientConfig> = {}) {
    configureRequestLifecycle()
    return beginAuthorization(overrides)
  }

  // `login` intentionally starts the public-client OAuth flow. Credentials are
  // collected by the authorization server and never pass through this app.
  const login = startLogin

  async function handleAuthorizationCallback(callback: AuthorizationCallback | URLSearchParams | URL) {
    const values = callback instanceof URL || callback instanceof URLSearchParams
      ? callback instanceof URL ? callback.searchParams : callback
      : undefined
    const callbackObject = values ? undefined : callback as AuthorizationCallback
    const error = values?.get('error') || callbackObject?.error
    const errorDescription = values?.get('error_description') || callbackObject?.errorDescription || callbackObject?.error_description
    if (error) {
      clearPkceTransaction()
      throw new Error(errorDescription || `授权失败：${error}`)
    }

    try {
      validateAuthorizationIssuer(
        import.meta.env.VITE_OAUTH_ISSUER,
        values?.get('iss') || callbackObject?.iss,
      )
    } catch (reason) {
      clearPkceTransaction()
      throw reason
    }

    const code = values?.get('code') || callbackObject?.code
    const state = values?.get('state') || callbackObject?.state
    if (!code || !state) {
      throw new Error('授权回调缺少 code 或 state。')
    }

    loading.value = true
    try {
      const token = await exchangeAuthorizationCode(code, state)
      // exchangeAuthorizationCode stores the pair; setting it again is safe and
      // keeps this method usable if a custom exchange adapter returns a pair.
      await setTokens({
        accessToken: token.access_token,
        refreshToken: token.refresh_token,
        expiresIn: token.expires_in,
        tokenType: token.token_type,
      })
      touchTokenState()
      await loadCurrentUser()
      return token
    } finally {
      loading.value = false
    }
  }

  async function loadCurrentUser() {
    configureRequestLifecycle()
    const permissionStore = usePermissionStore()
    const user = await getCurrentUser()
    currentUser.value = user
    tenantStore.setUserScope(user.userId)
    tenantStore.syncCurrentTenant(user.tenantId)
    permissionStore.setSuperAdmin(user.isSuperAdmin)
    await permissionStore.loadPermissions()
    isLoaded.value = true
    return user
  }

  async function reloadAuthorizationState() {
    const permissionStore = usePermissionStore()
    const user = await getCurrentUser(authorizationStateReloadRequestConfig)
    currentUser.value = user
    tenantStore.setUserScope(user.userId)
    tenantStore.syncCurrentTenant(user.tenantId)
    permissionStore.setSuperAdmin(user.isSuperAdmin)
    await permissionStore.loadPermissions(authorizationStateReloadRequestConfig)
    isLoaded.value = true
  }

  async function initialize() {
    await hydrateTokens()
    touchTokenState()
    if (!getAccessToken() || isAccessTokenExpired()) {
      if (!getRefreshToken()) {
        clearSession()
        return undefined
      }
      const { refreshAccessTokenOnce } = await import('../utils/request')
      const accessToken = await refreshAccessTokenOnce()
      touchTokenState()
      if (!accessToken) return undefined
    }

    try {
      return await loadCurrentUser()
    } catch (error) {
      if (!getRefreshToken()) {
        clearSession()
      }
      throw error
    }
  }

  async function loadMyProfile() {
    currentProfile.value = await getMyProfile()
    return currentProfile.value
  }

  async function updateProfile(payload: UpdateMyProfileRequest) {
    currentProfile.value = await updateMyProfile(payload)
    return currentProfile.value
  }

  async function changePassword(payload: ChangeMyPasswordRequest) {
    return changeMyPassword(payload)
  }

  function hasPermission(permissionCode?: string) {
    return usePermissionStore().hasPermission(permissionCode)
  }

  function clearSession() {
    void clearTokens()
    tenantStore.reset()
    currentUser.value = undefined
    currentProfile.value = undefined
    isLoaded.value = false
    loading.value = false
    usePermissionStore().reset()
    useNotificationStore().stop()
    touchTokenState()
  }

  async function logout() {
    const refreshToken = getRefreshToken()
    try {
      await logoutSession(refreshToken)
    } catch {
      // Revocation below still runs when the API is temporarily unavailable.
    }
    try {
      await revokeToken(refreshToken)
    } finally {
      clearSession()
    }
  }

  async function logoutAll() {
    try {
      await logoutAllSessions()
    } finally {
      await revokeToken(getRefreshToken()).catch(() => undefined)
      clearSession()
    }
  }

  function getTokenSnapshot(): TokenSnapshot {
    return readTokenSnapshot()
  }

  return {
    currentUser,
    currentProfile,
    isLoaded,
    loading,
    isAuthenticated,
    isSuperAdmin,
    effectiveTenantId,
    login,
    startLogin,
    handleAuthorizationCallback,
    initialize,
    loadCurrentUser,
    reloadAuthorizationState,
    loadMyProfile,
    updateProfile,
    changePassword,
    hasPermission,
    getTokenSnapshot,
    clearSession,
    logout,
    logoutAll,
  }
})
