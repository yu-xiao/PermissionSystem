<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { RouterView, useRoute } from 'vue-router'
import BottomTabs from '../components/BottomTabs.vue'
import MobileTopBar from '../components/MobileTopBar.vue'
import { useNotificationStore } from '../stores/notifications'
import { usePermissionStore } from '../stores/permission'
import { getTodoTasks } from '../api/workflowTask'

const route = useRoute()
const notificationStore = useNotificationStore()
const permission = usePermissionStore()
const todoCount = ref(0)

const title = computed(() => String(route.meta.title || '权限工作台'))
const showBottomTabs = computed(() => route.meta.hideBottomTabs !== true)
const unreadCount = computed(() => {
  const store = notificationStore as unknown as Record<string, unknown>
  const value = store.unreadCount ?? store.unread ?? store.count
  return typeof value === 'number' ? value : 0
})

onMounted(() => {
  if (permission.hasPermission('workflow:task:todo')) {
    void getTodoTasks({ pageIndex: 1, pageSize: 1 })
      .then((page) => { todoCount.value = page.totalCount })
      .catch(() => undefined)
  }
  if (!permission.hasPermission('system:notification:view')) return
  const store = notificationStore as unknown as Record<string, unknown>
  const load = store.loadUnreadCount ?? store.fetchUnreadCount
  if (typeof load === 'function') void (load as () => Promise<unknown>)()
})
</script>

<template>
  <div class="mobile-app">
    <MobileTopBar :title="title" :show-back="route.meta.showBack === true" :back-to="typeof route.meta.backTo === 'string' ? route.meta.backTo : ''" />
    <main class="mobile-page" :class="{ 'mobile-page--detail': route.meta.showBack === true }">
      <RouterView />
    </main>
    <BottomTabs v-if="showBottomTabs" :unread="unreadCount" :todo-count="todoCount" />
  </div>
</template>

<style scoped>
.mobile-page--detail { padding-bottom: calc(var(--mobile-tab-height) + 30px + env(safe-area-inset-bottom)); }
</style>
