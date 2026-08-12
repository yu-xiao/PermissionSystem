<script setup lang="ts">
import { OfficeBuilding } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { useNotificationStore } from '../../stores/notifications'
import { useTabsViewStore } from '../../stores/tabsView'
import { useTenantStore } from '../../stores/tenant'

const router = useRouter()
const authStore = useAuthStore()
const tenantStore = useTenantStore()
const notificationStore = useNotificationStore()
const tabsViewStore = useTabsViewStore()
const switching = ref(false)
const activeTenants = computed(() => tenantStore.tenants.filter((tenant) => tenant.isEnabled))

onMounted(loadTenants)

async function loadTenants() {
  if (!authStore.isSuperAdmin) {
    return
  }

  try {
    await tenantStore.loadTenants()
    const selectedIsAvailable = activeTenants.value.some(
      (tenant) => tenant.tenantId === tenantStore.targetTenantId,
    )

    if (!selectedIsAvailable && authStore.currentUser?.tenantId) {
      const currentTenantIsAvailable = activeTenants.value.some(
        (tenant) => tenant.tenantId === authStore.currentUser?.tenantId,
      )
      if (currentTenantIsAvailable) {
        tenantStore.selectTenant(authStore.currentUser.tenantId)
      }
    }
  } catch {
    ElMessage.error('租户列表加载失败')
  }
}

async function switchTenant(tenantId: string) {
  if (!tenantId || tenantId === tenantStore.targetTenantId || switching.value) {
    return
  }

  const previousTenantId = tenantStore.targetTenantId
  tenantStore.selectTenant(tenantId)
  switching.value = true

  try {
    await authStore.reloadAuthorizationState()
    tabsViewStore.reset()
    await router.replace('/dashboard')
    await notificationStore.loadLatest().catch(() => undefined)
    const selected = tenantStore.tenants.find((tenant) => tenant.tenantId === tenantId)
    ElMessage.success(`已切换到${selected?.name ?? '目标租户'}`)
  } catch {
    if (previousTenantId) {
      tenantStore.selectTenant(previousTenantId)
    } else {
      tenantStore.clearTarget()
    }
    ElMessage.error('租户切换失败，已恢复原租户')
  } finally {
    switching.value = false
  }
}
</script>

<template>
  <el-select
    v-if="authStore.isSuperAdmin"
    class="tenant-switcher"
    :model-value="tenantStore.targetTenantId"
    :loading="tenantStore.loading || switching"
    :prefix-icon="OfficeBuilding"
    size="small"
    filterable
    @change="switchTenant"
  >
    <el-option
      v-for="tenant in activeTenants"
      :key="tenant.tenantId"
      :label="`${tenant.name} (${tenant.code})`"
      :value="tenant.tenantId"
    />
  </el-select>
</template>

<style scoped>
.tenant-switcher {
  width: 190px;
}

@media (max-width: 900px) {
  .tenant-switcher {
    width: 150px;
  }
}
</style>
