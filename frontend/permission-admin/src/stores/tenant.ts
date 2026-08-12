import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { getTenants, type TenantItem } from '../api/tenants'
import { getTargetTenantId, setTargetTenantId } from '../utils/tenant'

export const useTenantStore = defineStore('tenant', () => {
  const targetTenantId = ref<string | undefined>(getTargetTenantId() ?? undefined)
  const tenants = ref<TenantItem[]>([])
  const loading = ref(false)
  const currentTenant = computed(() =>
    tenants.value.find((tenant) => tenant.tenantId === targetTenantId.value),
  )

  function selectTenant(tenantId: string) {
    targetTenantId.value = tenantId
    setTargetTenantId(tenantId)
  }

  function clearTarget() {
    targetTenantId.value = undefined
    setTargetTenantId()
  }

  async function loadTenants() {
    loading.value = true
    try {
      const result = await getTenants({
        pageIndex: 1,
        pageSize: 200,
        isEnabled: true,
      })
      tenants.value = result.items
      return tenants.value
    } finally {
      loading.value = false
    }
  }

  return {
    targetTenantId,
    tenants,
    loading,
    currentTenant,
    selectTenant,
    clearTarget,
    loadTenants,
  }
})
