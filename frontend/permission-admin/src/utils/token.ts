const accessTokenKey = 'permission_system_access_token'
const refreshTokenKey = 'permission_system_refresh_token'

export interface TokenPair {
  accessToken: string
  refreshToken?: string
}

export function getAccessToken() {
  return localStorage.getItem(accessTokenKey)
}

export function getRefreshToken() {
  return localStorage.getItem(refreshTokenKey)
}

export function setTokens(tokens: TokenPair) {
  localStorage.setItem(accessTokenKey, tokens.accessToken)

  if (tokens.refreshToken) {
    localStorage.setItem(refreshTokenKey, tokens.refreshToken)
  }
}

export function clearTokens() {
  localStorage.removeItem(accessTokenKey)
  localStorage.removeItem(refreshTokenKey)
}
