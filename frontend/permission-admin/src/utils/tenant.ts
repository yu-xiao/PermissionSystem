const targetTenantStorageKey = 'permission_system_target_tenant_id'

export function getTargetTenantId() {
  return localStorage.getItem(targetTenantStorageKey)
}

export function setTargetTenantId(tenantId?: string | null) {
  if (!tenantId) {
    localStorage.removeItem(targetTenantStorageKey)
    return
  }

  localStorage.setItem(targetTenantStorageKey, tenantId)
}
