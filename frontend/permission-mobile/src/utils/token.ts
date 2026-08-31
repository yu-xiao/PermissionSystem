import { getSecureStorageMode, readSecureValue, removeSecureValue, writeSecureValue, type SecureStorageMode } from './secureStorage'

const accessTokenKey = 'permission_mobile_access_token'
const refreshTokenKey = 'permission_mobile_refresh_token'
const expiresAtKey = 'permission_mobile_access_token_expires_at'

let memoryAccessToken: string | null = null
let memoryRefreshToken: string | null = null
let memoryExpiresAt: number | undefined
let hydrated = false

export interface TokenPair {
  accessToken: string
  refreshToken?: string | null
  expiresIn?: number
  tokenType?: string
}

export interface TokenSnapshot {
  accessToken: string | null
  refreshToken: string | null
  expiresAt?: number
  secureStorageMode: SecureStorageMode
}

function getSessionStorage() {
  if (typeof window === 'undefined') return undefined
  try { return window.sessionStorage } catch { return undefined }
}

function readSessionValue(key: string) {
  try { return getSessionStorage()?.getItem(key) ?? null } catch { return null }
}

function writeSessionValue(key: string, value: string) {
  try { getSessionStorage()?.setItem(key, value) } catch { /* Access token remains in memory. */ }
}

function removeSessionValue(key: string) {
  try { getSessionStorage()?.removeItem(key) } catch { /* Memory is cleared synchronously. */ }
}

export function getAccessToken() { return memoryAccessToken }
export function getRefreshToken() { return memoryRefreshToken }

export function getTokenSnapshot(): TokenSnapshot {
  return { accessToken: memoryAccessToken, refreshToken: memoryRefreshToken, expiresAt: memoryExpiresAt, secureStorageMode: getSecureStorageMode() }
}

export function isAccessTokenExpired(skewSeconds = 15) {
  if (!memoryAccessToken || !memoryExpiresAt) return !memoryAccessToken
  return memoryExpiresAt <= Date.now() + skewSeconds * 1000
}

export async function setTokens(tokens: TokenPair) {
  memoryAccessToken = tokens.accessToken
  if (tokens.refreshToken !== undefined) memoryRefreshToken = tokens.refreshToken || null
  memoryExpiresAt = tokens.expiresIn && tokens.expiresIn > 0 ? Date.now() + tokens.expiresIn * 1000 : undefined
  writeSessionValue(accessTokenKey, tokens.accessToken)
  if (memoryExpiresAt) writeSessionValue(expiresAtKey, String(memoryExpiresAt))
  else removeSessionValue(expiresAtKey)
  if (memoryRefreshToken) await writeSecureValue(refreshTokenKey, memoryRefreshToken)
  else await removeSecureValue(refreshTokenKey)
}

export async function setAccessToken(accessToken: string, expiresIn?: number) {
  await setTokens({ accessToken, expiresIn })
}

export async function setRefreshToken(refreshToken: string | null) {
  memoryRefreshToken = refreshToken
  if (refreshToken) await writeSecureValue(refreshTokenKey, refreshToken)
  else await removeSecureValue(refreshTokenKey)
}

export async function clearTokens() {
  memoryAccessToken = null
  memoryRefreshToken = null
  memoryExpiresAt = undefined
  removeSessionValue(accessTokenKey)
  removeSessionValue(expiresAtKey)
  await removeSecureValue(refreshTokenKey)
}

export async function hydrateTokens() {
  if (!hydrated) {
    memoryAccessToken = readSessionValue(accessTokenKey)
    const storedExpiresAt = readSessionValue(expiresAtKey)
    memoryExpiresAt = storedExpiresAt ? Number(storedExpiresAt) || undefined : undefined
    memoryRefreshToken = await readSecureValue(refreshTokenKey)
    hydrated = true
  }
  return getTokenSnapshot()
}

export const tokenStorageKeys = { accessToken: accessTokenKey, refreshToken: refreshTokenKey, expiresAt: expiresAtKey } as const
