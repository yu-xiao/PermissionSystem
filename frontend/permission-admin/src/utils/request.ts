import axios, {
  type AxiosError,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios'
import { ElMessage } from 'element-plus'
import { doneProgress, startProgress } from './progress'
import { getTargetTenantId } from './tenant'
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from './token'
import { createSingleFlight } from './singleFlight'

interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
  _authorizationStateReload?: boolean
}

export interface AuthorizationStateReloadRequestConfig extends AxiosRequestConfig {
  _authorizationStateReload: true
}

interface TokenResponse {
  access_token: string
  refresh_token?: string
  token_type: string
  expires_in?: number
}

let authorizationStateReloader: (() => Promise<void>) | undefined
const apiHost = getApiHost()

export const authorizationStateReloadRequestConfig: AuthorizationStateReloadRequestConfig = {
  _authorizationStateReload: true,
}

export function configureAuthorizationStateReloader(reloader: () => Promise<void>) {
  authorizationStateReloader = reloader
}

export const request = axios.create({
  baseURL: apiHost,
  timeout: 15000,
})

request.interceptors.request.use((config) => {
  if (!shouldSkipProgress(config)) {
    startProgress()
  }

  const accessToken = getAccessToken()

  // Token requests must not inherit a previous session's identity.
  if (accessToken && !isTokenRequest(config.url)) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }

  const targetTenantId = getTargetTenantId()
  if (targetTenantId && shouldAttachTenantHeader(config.url)) {
    config.headers['X-Tenant-Id'] = targetTenantId
  }

  if (shouldAttachIdempotencyKey(config.method) && !config.headers['X-Idempotency-Key']) {
    config.headers['X-Idempotency-Key'] = createIdempotencyKey()
  }

  return config
})

request.interceptors.response.use(
  (response) => {
    if (!shouldSkipProgress(response.config)) {
      doneProgress()
    }

    return response
  },
  async (error: AxiosError) => {
    if (error.config && !shouldSkipProgress(error.config)) {
      doneProgress()
    }

    const response = error.response
    const originalRequest = error.config as RetryableRequestConfig | undefined

    if (response?.status === 401 && response.headers?.['x-session-revoked'] === 'true') {
      ElMessage.error('当前登录会话已被强制下线，请重新登录')
      redirectToLogin()
      return Promise.reject(error)
    }

    if (response?.status === 401 && response.headers?.['x-authorization-stale'] === 'true') {
      if (!originalRequest || originalRequest._retry || originalRequest._authorizationStateReload) {
        return Promise.reject(error)
      }

      originalRequest._retry = true

      let accessToken: string | null
      try {
        accessToken = await refreshAuthorizationOnce()
      } catch {
        return Promise.reject(error)
      }

      if (accessToken) {
        originalRequest.headers.Authorization = `Bearer ${accessToken}`
        return request(originalRequest)
      }

      redirectToLogin()
      return Promise.reject(error)
    }

    if (response?.status === 401 && originalRequest && !originalRequest._retry) {
      originalRequest._retry = true

      const accessToken = await refreshAccessTokenOnce()
      if (accessToken) {
        originalRequest.headers.Authorization = `Bearer ${accessToken}`
        return request(originalRequest)
      }

      redirectToLogin()
      return Promise.reject(error)
    }

    if (response?.status === 401) {
      redirectToLogin()
      return Promise.reject(error)
    }

    if (response?.status === 429) {
      ElMessage.error('请求过于频繁，请稍后再试')
      return Promise.reject(error)
    }

    const message = getErrorMessage(error)
    ElMessage.error(message)
    return Promise.reject(error)
  },
)

async function refreshAccessTokenOnce() {
  return refreshAccessTokenSingleFlight()
}

async function refreshAuthorizationOnce() {
  return refreshAuthorizationSingleFlight()
}

async function refreshAuthorization() {
  const accessToken = await refreshAccessTokenOnce()
  if (!accessToken) {
    return null
  }

  await authorizationStateReloader?.()
  return accessToken
}

async function refreshAccessToken() {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    clearTokens()
    return null
  }

  try {
    const form = new URLSearchParams()
    form.set('grant_type', 'refresh_token')
    form.set('refresh_token', refreshToken)
    form.set('client_id', import.meta.env.VITE_OAUTH_CLIENT_ID)
    form.set('client_secret', import.meta.env.VITE_OAUTH_CLIENT_SECRET)

    const { data } = await axios.post<TokenResponse>(`${apiHost}/connect/token`, form, {
      headers: {
        'Content-Type': 'application/x-www-form-urlencoded',
      },
    })

    setTokens({
      accessToken: data.access_token,
      refreshToken: data.refresh_token,
    })

    return data.access_token
  } catch (error) {
    if (axios.isAxiosError(error) && error.response?.status === 429) {
      ElMessage.error('请求过于频繁，请稍后再试')
    }

    clearTokens()
    return null
  }
}

const refreshAccessTokenSingleFlight = createSingleFlight(refreshAccessToken)
const refreshAuthorizationSingleFlight = createSingleFlight(refreshAuthorization)

function redirectToLogin() {
  clearTokens()

  if (window.location.pathname !== '/login') {
    window.location.href = `/login?redirect=${encodeURIComponent(window.location.pathname)}`
  }
}

function getErrorMessage(error: AxiosError) {
  const responseData = error.response?.data as
    { error_description?: string; message?: string } | undefined
  return responseData?.message ?? responseData?.error_description ?? error.message ?? '请求失败'
}

function getApiHost() {
  const value = (import.meta.env.VITE_API_BASE_URL || '').trim().replace(/\/+$/, '')

  if (value.endsWith('/api')) {
    return value.slice(0, -4)
  }

  return value
}

function isTokenRequest(url?: string) {
  if (!url) {
    return false
  }

  const path = url.split('?')[0].replace(/\/+$/, '')
  return path === '/connect/token'
}

function shouldAttachTenantHeader(url?: string) {
  if (!url) {
    return true
  }

  const path = url.split('?')[0].replace(/\/+$/, '')
  return (
    !isTokenRequest(url) &&
    path !== '/connect/logout' &&
    path !== '/api/me' &&
    !path.startsWith('/api/me/profile') &&
    !path.startsWith('/api/me/password') &&
    !path.startsWith('/api/me/logout')
  )
}

function shouldAttachIdempotencyKey(method?: string) {
  const normalizedMethod = (method ?? 'get').toLowerCase()
  return !['get', 'head', 'options'].includes(normalizedMethod)
}

function shouldSkipProgress(config: InternalAxiosRequestConfig) {
  return config.headers?.['X-Skip-Progress'] === 'true'
}

function createIdempotencyKey() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID()
  }

  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}
