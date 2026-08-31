const databaseName = 'permission-mobile-security'
const keyStoreName = 'crypto-keys'
const encryptionKeyId = 'refresh-token-aes-gcm-v1'
const encryptedPrefix = 'enc:v1:'

export type SecureStorageMode = 'encrypted' | 'session-fallback' | 'unavailable'

interface EncryptedPayload {
  iv: string
  ciphertext: string
}

let storageMode: SecureStorageMode = 'unavailable'

function bytesToBase64(bytes: Uint8Array) {
  let binary = ''
  for (const byte of bytes) binary += String.fromCharCode(byte)
  return btoa(binary)
}

function base64ToBytes(value: string) {
  const binary = atob(value)
  return Uint8Array.from(binary, (character) => character.charCodeAt(0))
}

function getStorage(kind: 'local' | 'session') {
  if (typeof window === 'undefined') return undefined
  try {
    const storage = kind === 'local' ? window.localStorage : window.sessionStorage
    const probe = '__permission_mobile_storage_probe__'
    storage.setItem(probe, probe)
    storage.removeItem(probe)
    return storage
  } catch {
    return undefined
  }
}

function openKeyDatabase() {
  return new Promise<IDBDatabase>((resolve, reject) => {
    if (!globalThis.indexedDB) {
      reject(new Error('IndexedDB unavailable'))
      return
    }
    const request = globalThis.indexedDB.open(databaseName, 1)
    request.onupgradeneeded = () => {
      if (!request.result.objectStoreNames.contains(keyStoreName)) request.result.createObjectStore(keyStoreName)
    }
    request.onsuccess = () => resolve(request.result)
    request.onerror = () => reject(request.error ?? new Error('IndexedDB open failed'))
    request.onblocked = () => reject(new Error('IndexedDB open blocked'))
  })
}

async function getOrCreateEncryptionKey() {
  if (!globalThis.crypto?.subtle) throw new Error('Web Crypto unavailable')
  const database = await openKeyDatabase()
  try {
    const existing = await new Promise<CryptoKey | undefined>((resolve, reject) => {
      const request = database.transaction(keyStoreName, 'readonly').objectStore(keyStoreName).get(encryptionKeyId)
      request.onsuccess = () => resolve(request.result as CryptoKey | undefined)
      request.onerror = () => reject(request.error ?? new Error('Encryption key read failed'))
    })
    if (existing) return existing
    const key = await globalThis.crypto.subtle.generateKey({ name: 'AES-GCM', length: 256 }, false, ['encrypt', 'decrypt'])
    await new Promise<void>((resolve, reject) => {
      const transaction = database.transaction(keyStoreName, 'readwrite')
      transaction.objectStore(keyStoreName).put(key, encryptionKeyId)
      transaction.oncomplete = () => resolve()
      transaction.onerror = () => reject(transaction.error ?? new Error('Encryption key write failed'))
      transaction.onabort = () => reject(transaction.error ?? new Error('Encryption key write aborted'))
    })
    return key
  } finally {
    database.close()
  }
}

async function encrypt(value: string) {
  const key = await getOrCreateEncryptionKey()
  const iv = globalThis.crypto.getRandomValues(new Uint8Array(12))
  const ciphertext = await globalThis.crypto.subtle.encrypt({ name: 'AES-GCM', iv }, key, new TextEncoder().encode(value))
  const payload: EncryptedPayload = { iv: bytesToBase64(iv), ciphertext: bytesToBase64(new Uint8Array(ciphertext)) }
  return encryptedPrefix + JSON.stringify(payload)
}

async function decrypt(value: string) {
  if (!value.startsWith(encryptedPrefix)) throw new Error('Unsupported encrypted value')
  const payload = JSON.parse(value.slice(encryptedPrefix.length)) as EncryptedPayload
  const key = await getOrCreateEncryptionKey()
  const plaintext = await globalThis.crypto.subtle.decrypt(
    { name: 'AES-GCM', iv: base64ToBytes(payload.iv) },
    key,
    base64ToBytes(payload.ciphertext),
  )
  return new TextDecoder().decode(plaintext)
}

export async function writeSecureValue(key: string, value: string) {
  const persistent = getStorage('local')
  const session = getStorage('session')
  try {
    if (!persistent) throw new Error('Persistent storage unavailable')
    persistent.setItem(key, await encrypt(value))
    session?.removeItem(key)
    storageMode = 'encrypted'
  } catch {
    persistent?.removeItem(key)
    if (session) {
      session.setItem(key, value)
      storageMode = 'session-fallback'
    } else storageMode = 'unavailable'
  }
  return storageMode
}

export async function readSecureValue(key: string) {
  const persistent = getStorage('local')
  const session = getStorage('session')
  const encryptedValue = persistent?.getItem(key)
  if (encryptedValue) {
    try {
      const value = await decrypt(encryptedValue)
      storageMode = 'encrypted'
      return value
    } catch {
      persistent?.removeItem(key)
    }
  }
  const fallbackValue = session?.getItem(key) ?? null
  storageMode = fallbackValue ? 'session-fallback' : 'unavailable'
  return fallbackValue
}

export async function removeSecureValue(key: string) {
  getStorage('local')?.removeItem(key)
  getStorage('session')?.removeItem(key)
}

export function getSecureStorageMode() {
  return storageMode
}

export const secureStorageMetadata = { encryptedPrefix, databaseName, keyStoreName } as const
