import axios, {
  type AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type AxiosResponse,
} from 'axios'
import { ApiError, type ApiErrorKind } from '../api/types'
import { createSingleFlight } from './singleFlight'
import { getTargetTenantId } from './tenant'
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from './token'

interface TokenResponse {
  access_token: string
  refresh_token?: string
  token_type?: string
  expires_in?: number
}

export interface MobileRequestConfig extends AxiosRequestConfig {
  /** Reuse this key when a caller explicitly retries a write operation. */
  idempotencyKey?: string
  /** Prevents the interceptor from adding auth or refreshing on public calls. */
  skipAuth?: boolean
  /** Prevents tenant context for endpoints that operate outside a tenant. */
  skipTenant?: boolean
  /** Prevents the 401 retry path for a request that must fail immediately. */
  skipAuthRefresh?: boolean
  _retry?: boolean
  _authorizationStateReload?: boolean
}

export interface AuthorizationStateReloadRequestConfig extends AxiosRequestConfig {
  _authorizationStateReload: true
}

export interface RequestLifecycleHandlers {
  onUnauthorized?: (error: ApiError) => void
  onForbidden?: (error: ApiError) => void
  onConflict?: (error: ApiError) => void
  onRateLimited?: (error: ApiError) => void
  onServerError?: (error: ApiError) => void
}

const apiHost = getApiHost()
let handlers: RequestLifecycleHandlers = {}
let authorizationStateReloader: (() => Promise<void>) | undefined

export const authorizationStateReloadRequestConfig: AuthorizationStateReloadRequestConfig = {
  _authorizationStateReload: true,
}

export const request = axios.create({
  baseURL: apiHost,
  timeout: 20000,
})

export function configureRequestHandlers(nextHandlers: RequestLifecycleHandlers) {
  handlers = { ...handlers, ...nextHandlers }
}

export function configureAuthorizationStateReloader(reloader?: () => Promise<void>) {
  authorizationStateReloader = reloader
}

export function getApiHost() {
  const configured = (import.meta.env.VITE_API_BASE_URL || '').trim().replace(/\/+$/, '')
  if (!configured) {
    return ''
  }

  return configured.endsWith('/api') ? configured.slice(0, -4) : configured
}

request.interceptors.request.use((config) => {
  const mobileConfig = config as MobileRequestConfig
  config.headers = config.headers ?? {}

  if (!mobileConfig.skipAuth && !isTokenRequest(config.url)) {
    const accessToken = getAccessToken()
    if (accessToken) {
      config.headers.Authorization = `Bearer ${accessToken}`
    }
  }

  if (!mobileConfig.skipTenant && shouldAttachTenantHeader(config.url)) {
    const tenantId = getTargetTenantId()
    if (tenantId) {
      config.headers['X-Tenant-Id'] = tenantId
    }
  }

  config.headers['X-Client-Version'] = import.meta.env.VITE_APP_VERSION || '0.1.0'
  config.headers['X-Client-Platform'] = 'mobile-web'

  if (shouldAttachIdempotencyKey(config.method) && !config.headers['X-Idempotency-Key']) {
    config.headers['X-Idempotency-Key'] = mobileConfig.idempotencyKey || createIdempotencyKey()
  }

  return config
})

request.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<unknown>) => {
    const originalRequest = error.config as MobileRequestConfig | undefined
    const status = error.response?.status

    if (
      status === 401 &&
      error.response?.headers?.['x-session-revoked'] === 'true'
    ) {
      await clearTokens()
      const apiError = toApiError(error)
      handlers.onUnauthorized?.(apiError)
      return Promise.reject(apiError)
    }

    if (
      status === 401 &&
      error.response?.headers?.['x-authorization-stale'] === 'true' &&
      originalRequest &&
      !originalRequest._retry &&
      !originalRequest._authorizationStateReload &&
      !originalRequest.skipAuthRefresh
    ) {
      originalRequest._retry = true
      try {
        const accessToken = await refreshAuthorizationSingleFlight()
        if (accessToken) {
          setAuthorizationHeader(originalRequest, accessToken)
          return request(originalRequest)
        }
      } catch {
        // The common 401 path below clears the local session and reports it.
      }
      await clearTokens()
      const apiError = toApiError(error)
      handlers.onUnauthorized?.(apiError)
      return Promise.reject(apiError)
    }

    if (
      status === 401 &&
      originalRequest &&
      !originalRequest._retry &&
      !originalRequest.skipAuth &&
      !originalRequest.skipAuthRefresh &&
      !isTokenRequest(originalRequest.url)
    ) {
      originalRequest._retry = true
      const accessToken = await refreshAccessTokenSingleFlight()
      if (accessToken) {
        setAuthorizationHeader(originalRequest, accessToken)
        return request(originalRequest)
      }
      await clearTokens()
      const apiError = toApiError(error)
      handlers.onUnauthorized?.(apiError)
      return Promise.reject(apiError)
    }

    const apiError = toApiError(error)
    dispatchLifecycleHandler(apiError)
    return Promise.reject(apiError)
  },
)

export async function refreshAccessTokenOnce() {
  return refreshAccessTokenSingleFlight()
}

async function refreshAccessToken() {
  const refreshToken = getRefreshToken()
  if (!refreshToken) {
    return null
  }

  try {
    const form = new URLSearchParams()
    form.set('grant_type', 'refresh_token')
    form.set('refresh_token', refreshToken)
    form.set('client_id', getOAuthClientId())
    form.set('scope', getOAuthScope())

    const response = await axios.post<TokenResponse>(`${apiHost}/connect/token`, form, {
      timeout: 20000,
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    })
    await setTokens({
      accessToken: response.data.access_token,
      refreshToken: response.data.refresh_token ?? refreshToken,
      expiresIn: response.data.expires_in,
      tokenType: response.data.token_type,
    })
    return response.data.access_token
  } catch {
    await clearTokens()
    return null
  }
}

async function refreshAuthorization() {
  const accessToken = await refreshAccessTokenSingleFlight()
  if (!accessToken) {
    return null
  }
  await authorizationStateReloader?.()
  return accessToken
}

const refreshAccessTokenSingleFlight = createSingleFlight(refreshAccessToken)
const refreshAuthorizationSingleFlight = createSingleFlight(refreshAuthorization)

function setAuthorizationHeader(config: AxiosRequestConfig, accessToken: string) {
  config.headers = config.headers ?? {}
  config.headers.Authorization = `Bearer ${accessToken}`
}

function getOAuthClientId() {
  return import.meta.env.VITE_OAUTH_CLIENT_ID || 'permission-mobile'
}

function getOAuthScope() {
  return import.meta.env.VITE_OAUTH_SCOPE || 'openid profile offline_access permission-system-api'
}

function isTokenRequest(url?: string) {
  if (!url) {
    return false
  }
  return url.split('?')[0].replace(/\/+$/, '') === '/connect/token'
}

function shouldAttachTenantHeader(url?: string) {
  if (!url) {
    return true
  }

  const path = url.split('?')[0].replace(/\/+$/, '')
  if (path.startsWith('/connect/')) {
    return false
  }

  return !(
    path === '/api/v1/me' ||
    path.startsWith('/api/v1/me/profile') ||
    path.startsWith('/api/v1/me/password') ||
    path.startsWith('/api/v1/me/logout') ||
    path.startsWith('/api/v1/tenants')
  )
}

function shouldAttachIdempotencyKey(method?: string) {
  const normalizedMethod = (method || 'get').toLowerCase()
  return !['get', 'head', 'options'].includes(normalizedMethod)
}

function createIdempotencyKey() {
  if (globalThis.crypto?.randomUUID) {
    return globalThis.crypto.randomUUID()
  }
  return `${Date.now()}-${Math.random().toString(16).slice(2)}`
}

function dispatchLifecycleHandler(error: ApiError) {
  switch (error.kind) {
    case 'forbidden':
      handlers.onForbidden?.(error)
      break
    case 'conflict':
      handlers.onConflict?.(error)
      break
    case 'rate-limited':
      handlers.onRateLimited?.(error)
      break
    case 'server':
      handlers.onServerError?.(error)
      break
    default:
      break
  }
}

function toApiError(error: AxiosError<unknown>) {
  if (error instanceof ApiError) {
    return error
  }

  const responseData = error.response?.data as
    | { code?: number; message?: string; error?: string; error_description?: string; traceId?: string }
    | undefined
  const status = error.response?.status
  const kind = getErrorKind(status)
  return new ApiError(
    responseData?.message || responseData?.error_description || error.message || '请求失败',
    {
      status,
      code: responseData?.code,
      traceId: responseData?.traceId || getHeader(error.response, 'x-trace-id'),
      kind,
      retryable: kind === 'rate-limited' || kind === 'server' || !status,
      cause: error,
    },
  )
}

function getHeader(response: AxiosResponse<unknown> | undefined, name: string) {
  const value = response?.headers?.[name]
  return typeof value === 'string' ? value : undefined
}

function getErrorKind(status?: number): ApiErrorKind {
  if (status === 401) return 'unauthorized'
  if (status === 403) return 'forbidden'
  if (status === 409) return 'conflict'
  if (status === 422) return 'validation'
  if (status === 429) return 'rate-limited'
  if (status !== undefined && status >= 500) return 'server'
  return 'network'
}

// Keep the instance type visible to consumers that need to install a test
// adapter or compose a request with a cancellation signal.
export type MobileHttpClient = AxiosInstance
export const http = request
