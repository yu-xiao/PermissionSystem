const transactionStorageKey = 'permission_mobile_pkce_transaction'
const transactionMaxAgeMs = 10 * 60 * 1000

export interface PkcePair {
  codeVerifier: string
  codeChallenge: string
}

export interface PkceTransaction extends PkcePair {
  state: string
  redirectUri: string
  createdAt: number
  nonce?: string
  returnPath?: string
}

export class PkceError extends Error {
  constructor(message: string) {
    super(message)
    this.name = 'PkceError'
  }
}

function toBase64Url(bytes: Uint8Array) {
  let binary = ''
  for (const byte of bytes) {
    binary += String.fromCharCode(byte)
  }

  // Browser H5/PWA always exposes btoa; the small fallback keeps the helper
  // usable in test runners that provide neither DOM nor Node globals.
  const base64 = typeof btoa === 'function' ? btoa(binary) : encodeBase64Fallback(bytes)

  return base64.replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/g, '')
}

function encodeBase64Fallback(bytes: Uint8Array) {
  const alphabet = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789+/'
  let output = ''
  for (let index = 0; index < bytes.length; index += 3) {
    const first = bytes[index]
    const second = bytes[index + 1]
    const third = bytes[index + 2]
    output += alphabet[first >> 2]
    output += alphabet[((first & 3) << 4) | ((second ?? 0) >> 4)]
    output += second === undefined ? '=' : alphabet[((second & 15) << 2) | ((third ?? 0) >> 6)]
    output += third === undefined ? '=' : alphabet[third & 63]
  }
  return output
}

function randomBytes(length: number) {
  const bytes = new Uint8Array(length)
  if (globalThis.crypto?.getRandomValues) {
    globalThis.crypto.getRandomValues(bytes)
    return bytes
  }

  for (let index = 0; index < bytes.length; index += 1) {
    bytes[index] = Math.floor(Math.random() * 256)
  }
  return bytes
}

export function createRandomString(length = 32) {
  return toBase64Url(randomBytes(length)).slice(0, Math.max(43, length))
}

export function createState() {
  return toBase64Url(randomBytes(32))
}

export async function createCodeChallenge(codeVerifier: string) {
  if (!globalThis.crypto?.subtle) {
    throw new PkceError('当前环境不支持 PKCE 所需的 Web Crypto。')
  }

  const digest = await globalThis.crypto.subtle.digest(
    'SHA-256',
    new TextEncoder().encode(codeVerifier),
  )
  return toBase64Url(new Uint8Array(digest))
}

export async function createPkcePair(): Promise<PkcePair> {
  const codeVerifier = createRandomString(64)
  return {
    codeVerifier,
    codeChallenge: await createCodeChallenge(codeVerifier),
  }
}

function sessionStorageSafe() {
  return typeof window === 'undefined' ? undefined : window.sessionStorage
}

export function savePkceTransaction(transaction: PkceTransaction) {
  try {
    sessionStorageSafe()?.setItem(transactionStorageKey, JSON.stringify(transaction))
  } catch {
    throw new PkceError('无法保存登录状态，请检查浏览器会话存储设置。')
  }
}

export function validateReturnPath(value: string | null | undefined) {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.includes('://')) return '/home'
  try {
    const url = new URL(value, window.location.origin)
    if (url.origin !== window.location.origin) return '/home'
    return url.pathname + url.search + url.hash
  } catch {
    return '/home'
  }
}

export function readPkceTransaction() {
  try {
    const raw = sessionStorageSafe()?.getItem(transactionStorageKey)
    if (!raw) {
      return undefined
    }

    const transaction = JSON.parse(raw) as PkceTransaction
    if (!transaction.createdAt || Date.now() - transaction.createdAt > transactionMaxAgeMs) {
      clearPkceTransaction()
      return undefined
    }
    return transaction
  } catch {
    clearPkceTransaction()
    return undefined
  }
}

export function consumePkceTransaction(expectedState?: string) {
  const transaction = readPkceTransaction()
  clearPkceTransaction()
  if (!transaction) {
    throw new PkceError('登录状态已过期，请重新开始登录。')
  }
  if (expectedState && transaction.state !== expectedState) {
    throw new PkceError('登录状态校验失败，请重新开始登录。')
  }
  return transaction
}

export function clearPkceTransaction() {
  try {
    sessionStorageSafe()?.removeItem(transactionStorageKey)
  } catch {
    // Ignore storage failures; the transaction is short-lived by design.
  }
}

export function validateAuthorizationState(expectedState: string, returnedState: string | null | undefined) {
  if (!returnedState || returnedState !== expectedState) {
    throw new PkceError('登录状态校验失败，请重新开始登录。')
  }
  return true
}

export function validateAuthorizationIssuer(
  expectedIssuer: string | null | undefined,
  returnedIssuer: string | null | undefined,
) {
  const expected = expectedIssuer?.trim().replace(/\/+$/, '')
  if (!expected) return true

  const returned = returnedIssuer?.trim().replace(/\/+$/, '')
  if (!returned || returned !== expected) {
    throw new PkceError('授权服务器校验失败，请重新开始登录。')
  }
  return true
}

export interface AuthorizationUrlOptions {
  issuer: string
  clientId: string
  redirectUri: string
  scope?: string
  prompt?: string
  loginHint?: string
  tenant?: string
}

export function buildAuthorizationUrl(
  options: AuthorizationUrlOptions,
  transaction: Pick<PkceTransaction, 'state' | 'codeChallenge' | 'nonce'>,
) {
  const issuer = options.issuer.replace(/\/+$/, '')
  const url = new URL(`${issuer}/connect/authorize`)
  url.searchParams.set('client_id', options.clientId)
  url.searchParams.set('response_type', 'code')
  url.searchParams.set('redirect_uri', options.redirectUri)
  url.searchParams.set('scope', options.scope || 'openid profile offline_access permission-system-api')
  url.searchParams.set('state', transaction.state)
  url.searchParams.set('code_challenge', transaction.codeChallenge)
  url.searchParams.set('code_challenge_method', 'S256')
  if (transaction.nonce) {
    url.searchParams.set('nonce', transaction.nonce)
  }
  if (options.prompt) {
    url.searchParams.set('prompt', options.prompt)
  }
  if (options.loginHint) {
    url.searchParams.set('login_hint', options.loginHint)
  }
  if (options.tenant) {
    url.searchParams.set('tenant', options.tenant)
  }
  return url.toString()
}

export async function createAuthorizationTransaction(
  options: Pick<AuthorizationUrlOptions, 'redirectUri'> & { returnPath?: string },
) {
  const pair = await createPkcePair()
  const transaction: PkceTransaction = {
    ...pair,
    state: createState(),
    nonce: createRandomString(32),
    redirectUri: options.redirectUri,
    createdAt: Date.now(),
    returnPath: validateReturnPath(options.returnPath),
  }
  savePkceTransaction(transaction)
  return transaction
}

export const pkceStorageKey = transactionStorageKey
