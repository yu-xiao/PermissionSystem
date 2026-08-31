<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { deleteMyNotification, getMyNotifications, getMyUnreadNotificationCount, markAllNotificationsRead, markNotificationRead, type NotificationItem } from '../../api/notifications'
import StateView from '../../components/StateView.vue'
import StatusTag from '../../components/StatusTag.vue'

const loading = ref(true)
const loadingMore = ref(false)
const saving = ref(false)
const error = ref('')
const items = ref<NotificationItem[]>([])
const unreadCount = ref(0)
const pageIndex = ref(1)
const hasNext = ref(false)
const activeFilter = ref<'all' | 'unread'>('all')

const visibleItems = computed(() => activeFilter.value === 'unread' ? items.value.filter((item) => !item.isRead) : items.value)
const filters = [{ key: 'all', label: '全部' }, { key: 'unread', label: '未读' }] as const

function typeLabel(type: string) {
  const labels: Record<string, string> = { System: '系统', Security: '安全', Task: '任务', Approval: '审批' }
  return labels[type] || '通知'
}
function typeTone(type: string): 'neutral' | 'primary' | 'success' | 'warning' {
  if (type === 'Approval') return 'primary'
  if (type === 'Task') return 'warning'
  if (type === 'Security') return 'success'
  return 'neutral'
}
function formatDate(value: string) {
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date)
}

async function load(reset = true) {
  if (reset) { pageIndex.value = 1; loading.value = true } else loadingMore.value = true
  error.value = ''
  try {
    const page = await getMyNotifications({ pageIndex: pageIndex.value, pageSize: 20 })
    if (reset) items.value = page?.items || []
    else items.value = [...items.value, ...(page?.items || [])]
    hasNext.value = Boolean(page?.hasNextPage)
    unreadCount.value = await getMyUnreadNotificationCount()
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '通知加载失败。'
  } finally { loading.value = false; loadingMore.value = false }
}
async function markRead(item: NotificationItem) {
  if (item.isRead || saving.value) return
  saving.value = true
  try { await markNotificationRead(item.id); item.isRead = true; unreadCount.value = Math.max(0, unreadCount.value - 1) } catch (reason) { error.value = reason instanceof Error ? reason.message : '标记已读失败。' } finally { saving.value = false }
}
async function markAll() {
  if (!unreadCount.value || saving.value) return
  saving.value = true
  try { await markAllNotificationsRead(); items.value.forEach((item) => { item.isRead = true }); unreadCount.value = 0 } catch (reason) { error.value = reason instanceof Error ? reason.message : '操作失败。' } finally { saving.value = false }
}
async function remove(item: NotificationItem) {
  if (saving.value) return
  saving.value = true
  try { await deleteMyNotification(item.id); items.value = items.value.filter((candidate) => candidate.id !== item.id); if (!item.isRead) unreadCount.value = Math.max(0, unreadCount.value - 1) } catch (reason) { error.value = reason instanceof Error ? reason.message : '删除失败。' } finally { saving.value = false }
}
async function loadMore() { if (!hasNext.value || loadingMore.value) return; pageIndex.value += 1; await load(false) }
onMounted(() => void load())
</script>

<template>
  <section class="notification-view">
    <div class="notification-summary surface"><div><span class="notification-summary__label">未读通知</span><strong>{{ unreadCount }}</strong></div><button class="button button--text" type="button" :disabled="!unreadCount || saving" @click="markAll">全部已读</button></div>
    <div class="filter-row notification-filters" role="tablist">
      <button v-for="filter in filters" :key="filter.key" class="filter-chip" :class="{ 'filter-chip--active': activeFilter === filter.key }" type="button" role="tab" :aria-selected="activeFilter === filter.key" @click="activeFilter = filter.key">{{ filter.label }}</button>
    </div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <StateView v-if="loading" kind="loading" title="正在加载通知" />
    <StateView v-else-if="!visibleItems.length" kind="empty" :title="activeFilter === 'unread' ? '没有未读通知' : '暂无通知'" hint="新的系统动态会显示在这里。" />
    <div v-else class="notification-list">
      <article v-for="item in visibleItems" :key="item.id" class="surface notification-item" :class="{ 'notification-item--unread': !item.isRead }">
        <button class="notification-item__content" type="button" @click="markRead(item)">
          <div class="notification-item__heading"><strong>{{ item.title }}</strong><StatusTag :label="typeLabel(item.type)" :tone="typeTone(item.type)" /></div>
          <p>{{ item.content }}</p>
          <time>{{ formatDate(item.createdAt) }}</time>
        </button>
        <button class="icon-button notification-item__delete" type="button" aria-label="删除通知" title="删除通知" :disabled="saving" @click="remove(item)">×</button>
      </article>
      <button v-if="hasNext" class="button button--ghost button--block" type="button" :disabled="loadingMore" @click="loadMore">{{ loadingMore ? '正在加载…' : '加载更多' }}</button>
    </div>
  </section>
</template>

<style scoped>
.notification-summary { display: flex; align-items: center; justify-content: space-between; padding: 14px 16px; }
.notification-summary__label { display: block; color: var(--mobile-text-secondary); font-size: 12px; }
.notification-summary strong { display: block; margin-top: 4px; font-size: 26px; line-height: 1; }
.notification-filters { margin: 14px 0; }
.notification-list { display: grid; gap: 9px; }
.notification-item { display: flex; align-items: stretch; padding: 0 4px 0 0; overflow: hidden; }
.notification-item--unread { border-left: 3px solid var(--mobile-primary); }
.notification-item__content { display: block; min-width: 0; flex: 1; padding: 14px 8px 14px 14px; border: 0; color: inherit; background: transparent; text-align: left; }
.notification-item__heading { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.notification-item__heading strong { min-width: 0; overflow: hidden; color: var(--mobile-text); font-size: 14px; text-overflow: ellipsis; white-space: nowrap; }
.notification-item__content p { margin: 8px 0; color: var(--mobile-text-secondary); font-size: 12px; line-height: 1.5; }
.notification-item__content time { color: var(--mobile-text-muted); font-size: 11px; }
.notification-item__delete { align-self: center; width: 36px; height: 36px; font-size: 18px; }
.inline-error { margin: 0 0 12px; padding: 9px 10px; border-radius: 8px; color: var(--mobile-danger); background: var(--mobile-danger-soft); font-size: 12px; }
</style>
