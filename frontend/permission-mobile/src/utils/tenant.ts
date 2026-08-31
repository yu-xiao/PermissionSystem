const tenantStoragePrefix = 'permission_mobile_tenant:'
let storageScope = 'anonymous'
let activeTenantId: string | null = null

function storageKey(userId = storageScope) {
  return `${tenantStoragePrefix}${userId || 'anonymous'}`
}

function localStorageSafe() {
  return typeof window === 'undefined' ? undefined : window.localStorage
}

export function setTenantStorageScope(userId?: string | null) {
  storageScope = userId || 'anonymous'
  activeTenantId = getTargetTenantId()
}

export function getTargetTenantId(userId?: string | null) {
  if (activeTenantId && (!userId || userId === storageScope)) {
    return activeTenantId
  }

  try {
    return localStorageSafe()?.getItem(storageKey(userId || storageScope)) ?? null
  } catch {
    return null
  }
}

export function setTargetTenantId(tenantId?: string | null, userId?: string | null) {
  const key = storageKey(userId || storageScope)
  activeTenantId = tenantId || null
  try {
    if (tenantId) {
      localStorageSafe()?.setItem(key, tenantId)
    } else {
      localStorageSafe()?.removeItem(key)
    }
  } catch {
    // Tenant selection remains available in memory when storage is blocked.
  }
}

export function clearTenantStorage(userId?: string | null) {
  try {
    localStorageSafe()?.removeItem(storageKey(userId || storageScope))
  } catch {
    // Ignore storage failures while clearing tenant-scoped state.
  }
  activeTenantId = null
}

export function createTenantCacheKey(resource: string, tenantId?: string | null, userId?: string | null) {
  return `${userId || storageScope}:${tenantId || getTargetTenantId() || 'none'}:${resource}`
}

