import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import { getTenants, type TenantItem } from '../api/tenants'
import {
  clearTenantStorage,
  getTargetTenantId,
  setTargetTenantId,
  setTenantStorageScope,
} from '../utils/tenant'
import { useNotificationStore } from './notifications'
import { useOrderStore } from './orders'
import { useTaskStore } from './tasks'

type TenantChangedHandler = (tenantId: string | null, previousTenantId: string | null) => void

export const useTenantStore = defineStore('tenant', () => {
  const targetTenantId = ref<string | null>(getTargetTenantId())
  const tenants = ref<TenantItem[]>([])
  const loading = ref(false)
  const initialized = ref(false)
  let changeHandler: TenantChangedHandler | undefined

  function clearTenantScopedStores() {
    useTaskStore().reset()
    useOrderStore().reset()
    useNotificationStore().stop()
  }

  const currentTenant = computed(() =>
    tenants.value.find((tenant) => tenant.tenantId === targetTenantId.value || tenant.id === targetTenantId.value),
  )

  function registerChangeHandler(handler?: TenantChangedHandler) {
    changeHandler = handler
  }

  function setUserScope(userId?: string | null) {
    setTenantStorageScope(userId)
    targetTenantId.value = getTargetTenantId()
  }

  function selectTenant(tenantId: string) {
    if (!tenantId) {
      throw new Error('租户标识不能为空。')
    }

    // A loaded tenant list is authoritative. Do not allow arbitrary IDs to be
    // written into the request context from the UI.
    if (tenants.value.length > 0 && !tenants.value.some((tenant) => tenant.tenantId === tenantId || tenant.id === tenantId)) {
      throw new Error('当前用户无权访问该租户。')
    }

    const previousTenantId = targetTenantId.value
    targetTenantId.value = tenantId
    setTargetTenantId(tenantId)
    if (previousTenantId !== tenantId) {
      clearTenantScopedStores()
      changeHandler?.(tenantId, previousTenantId)
    }
  }

  function clearTarget() {
    const previousTenantId = targetTenantId.value
    targetTenantId.value = null
    clearTenantStorage()
    if (previousTenantId) {
      clearTenantScopedStores()
      changeHandler?.(null, previousTenantId)
    }
  }

  function syncCurrentTenant(tenantId?: string | null) {
    if (!tenantId) {
      return
    }
    if (!targetTenantId.value) {
      targetTenantId.value = tenantId
      setTargetTenantId(tenantId)
    }
  }

  async function loadTenants() {
    loading.value = true
    try {
      const result = await getTenants({ pageIndex: 1, pageSize: 200, isEnabled: true })
      tenants.value = result.items
      initialized.value = true
      if (!targetTenantId.value && result.items.length === 1) {
        selectTenant(result.items[0].tenantId || result.items[0].id)
      }
      return tenants.value
    } finally {
      loading.value = false
    }
  }

  function reset() {
    tenants.value = []
    initialized.value = false
    clearTarget()
  }

  return {
    targetTenantId,
    tenants,
    loading,
    initialized,
    currentTenant,
    registerChangeHandler,
    setUserScope,
    selectTenant,
    syncCurrentTenant,
    clearTarget,
    loadTenants,
    reset,
  }
})
