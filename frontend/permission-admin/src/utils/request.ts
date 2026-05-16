import axios, { type AxiosError, type InternalAxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import { clearTokens, getAccessToken, getRefreshToken, setTokens } from './token'

interface RetryableRequestConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

interface TokenResponse {
  access_token: string
  refresh_token?: string
  token_type: string
  expires_in?: number
}

let refreshingTokenPromise: Promise<string | null> | null = null
const apiHost = getApiHost()

export const request = axios.create({
  baseURL: apiHost,
  timeout: 15000,
})

request.interceptors.request.use((config) => {
  const accessToken = getAccessToken()

  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`
  }

  return config
})

request.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const response = error.response
    const originalRequest = error.config as RetryableRequestConfig | undefined

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

    const message = getErrorMessage(error)
    ElMessage.error(message)
    return Promise.reject(error)
  },
)

async function refreshAccessTokenOnce() {
  if (!refreshingTokenPromise) {
    refreshingTokenPromise = refreshAccessToken().finally(() => {
      refreshingTokenPromise = null
    })
  }

  return refreshingTokenPromise
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

    const { data } = await axios.post<TokenResponse>(
      `${apiHost}/connect/token`,
      form,
      {
        headers: {
          'Content-Type': 'application/x-www-form-urlencoded',
        },
      },
    )

    setTokens({
      accessToken: data.access_token,
      refreshToken: data.refresh_token,
    })

    return data.access_token
  } catch {
    clearTokens()
    return null
  }
}

function redirectToLogin() {
  clearTokens()

  if (window.location.pathname !== '/login') {
    window.location.href = `/login?redirect=${encodeURIComponent(window.location.pathname)}`
  }
}

function getErrorMessage(error: AxiosError) {
  const responseData = error.response?.data as { error_description?: string; message?: string } | undefined
  return responseData?.message ?? responseData?.error_description ?? error.message ?? '请求失败'
}

function getApiHost() {
  const value = (import.meta.env.VITE_API_BASE_URL || '').trim().replace(/\/+$/, '')

  if (value.endsWith('/api')) {
    return value.slice(0, -4)
  }

  return value
}
