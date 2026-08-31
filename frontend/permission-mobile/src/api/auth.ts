import axios from 'axios'
import {
  buildAuthorizationUrl,
  clearPkceTransaction,
  consumePkceTransaction,
  createAuthorizationTransaction,
  type AuthorizationUrlOptions,
} from '../utils/pkce'
import { getApiHost } from '../utils/request'
import { clearTokens, getRefreshToken, setTokens } from '../utils/token'

export interface TokenResponse {
  access_token: string
  refresh_token?: string
  expires_in?: number
  token_type: string
  scope?: string
}

export interface AuthorizationCodeResult extends TokenResponse {
  returnPath: string
}

export interface AuthorizationCallback {
  code?: string | null
  state?: string | null
  error?: string | null
  errorDescription?: string | null
  error_description?: string | null
  iss?: string | null
}

export interface OAuthClientConfig extends AuthorizationUrlOptions {
  issuer: string
  clientId: string
  redirectUri: string
  scope: string
  returnPath?: string
}

function getClientConfig(overrides: Partial<OAuthClientConfig> = {}): OAuthClientConfig {
  const issuer = (overrides.issuer || import.meta.env.VITE_OAUTH_ISSUER || getApiHost() || window.location.origin)
    .replace(/\/+$/, '')
  return {
    issuer,
    clientId: overrides.clientId || import.meta.env.VITE_OAUTH_CLIENT_ID || 'permission-mobile',
    redirectUri: overrides.redirectUri || import.meta.env.VITE_OAUTH_REDIRECT_URI || `${window.location.origin}/authorize/callback`,
    scope: overrides.scope || import.meta.env.VITE_OAUTH_SCOPE || 'openid profile offline_access permission-system-api',
    tenant: overrides.tenant,
  }
}

export function getOAuthClientConfig(overrides: Partial<OAuthClientConfig> = {}) {
  return getClientConfig(overrides)
}

/** Creates and stores a short-lived PKCE transaction, then returns the IdP URL. */
export async function beginAuthorization(overrides: Partial<OAuthClientConfig> = {}) {
  const config = getClientConfig(overrides)
  const transaction = await createAuthorizationTransaction({ ...config, returnPath: overrides.returnPath })
  return buildAuthorizationUrl(config, transaction)
}

export const startAuthorization = beginAuthorization
export const getAuthorizationUrl = beginAuthorization

export async function exchangeAuthorizationCode(
  code: string,
  state: string,
  overrides: Partial<OAuthClientConfig> = {},
) {
  if (!code || !state) {
    clearPkceTransaction()
    throw new Error('授权回调缺少 code 或 state。')
  }

  const transaction = consumePkceTransaction(state)
  const config = getClientConfig({ ...overrides, redirectUri: overrides.redirectUri || transaction.redirectUri })
  const form = new URLSearchParams()
  form.set('grant_type', 'authorization_code')
  form.set('client_id', config.clientId)
  form.set('redirect_uri', config.redirectUri)
  form.set('code', code)
  form.set('code_verifier', transaction.codeVerifier)

  const { data } = await axios.post<TokenResponse>(`${config.issuer}/connect/token`, form, {
    timeout: 20000,
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  })
  await setTokens({
    accessToken: data.access_token,
    refreshToken: data.refresh_token,
    expiresIn: data.expires_in,
    tokenType: data.token_type,
  })
  return { ...data, returnPath: transaction.returnPath || '/home' } satisfies AuthorizationCodeResult
}

export async function refreshToken(refreshTokenValue = getRefreshToken()) {
  if (!refreshTokenValue) {
    throw new Error('缺少 refresh token。')
  }

  const config = getClientConfig()
  const form = new URLSearchParams()
  form.set('grant_type', 'refresh_token')
  form.set('refresh_token', refreshTokenValue)
  form.set('client_id', config.clientId)
  form.set('scope', config.scope)

  const { data } = await axios.post<TokenResponse>(`${config.issuer}/connect/token`, form, {
    timeout: 20000,
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
  })
  await setTokens({
    accessToken: data.access_token,
    refreshToken: data.refresh_token ?? refreshTokenValue,
    expiresIn: data.expires_in,
    tokenType: data.token_type,
  })
  return data
}

export async function revokeToken(refreshTokenValue = getRefreshToken()) {
  if (!refreshTokenValue) {
    await clearTokens()
    return
  }

  const config = getClientConfig()
  const form = new URLSearchParams()
  form.set('token', refreshTokenValue)
  form.set('token_type_hint', 'refresh_token')
  form.set('client_id', config.clientId)

  try {
    await axios.post(`${config.issuer}/connect/revoke`, form, {
      timeout: 10000,
      withCredentials: true,
      headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    })
  } finally {
    await clearTokens()
  }
}

export const logoutOAuth = revokeToken
