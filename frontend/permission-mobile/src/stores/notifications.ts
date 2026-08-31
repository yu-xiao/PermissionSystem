import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  deleteMyNotification,
  getMyNotifications,
  getMyUnreadNotificationCount,
  markAllNotificationsRead,
  markNotificationRead,
  type NotificationItem,
  type NotificationQuery,
  type NotificationRealtimeMessage,
} from '../api/notifications'

const defaultPollingInterval = 60_000

export const useNotificationStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  const latest = ref<NotificationItem[]>([])
  const items = ref<NotificationItem[]>([])
  const loading = ref(false)
  const loadingMore = ref(false)
  const error = ref<string>()
  const pageIndex = ref(1)
  const hasNextPage = ref(false)
  const connected = ref(false)
  const lastUpdatedAt = ref<number>()
  let pollingTimer: ReturnType<typeof setTimeout> | undefined
  let pollingInterval = defaultPollingInterval
  let visibilityHandler: (() => void) | undefined

  const hasUnread = computed(() => unreadCount.value > 0)

  async function loadUnreadCount() {
    const count = await getMyUnreadNotificationCount()
    unreadCount.value = Math.max(0, count || 0)
    lastUpdatedAt.value = Date.now()
    return unreadCount.value
  }

  async function loadLatest(limit = 6) {
    const result = await getMyNotifications({ pageIndex: 1, pageSize: limit })
    latest.value = result.items
    await loadUnreadCount()
    return latest.value
  }

  async function load(query: NotificationQuery = {}, append = false) {
    if (append) {
      loadingMore.value = true
    } else {
      loading.value = true
      pageIndex.value = query.pageIndex || 1
    }
    error.value = undefined
    try {
      const result = await getMyNotifications({
        ...query,
        pageIndex: append ? pageIndex.value : query.pageIndex || 1,
      })
      items.value = append ? [...items.value, ...result.items] : result.items
      pageIndex.value = result.pageIndex
      hasNextPage.value = result.hasNextPage
      if (!append) {
        latest.value = result.items.slice(0, 6)
      }
      await loadUnreadCount()
      return result
    } catch (reason) {
      error.value = reason instanceof Error ? reason.message : '通知加载失败。'
      throw reason
    } finally {
      loading.value = false
      loadingMore.value = false
    }
  }

  async function loadMore(query: Omit<NotificationQuery, 'pageIndex'> = {}) {
    if (!hasNextPage.value || loadingMore.value) {
      return undefined
    }
    pageIndex.value += 1
    return load({ ...query, pageIndex: pageIndex.value }, true)
  }

  async function markRead(id: string) {
    await markNotificationRead(id)
    updateReadState(id)
    return loadUnreadCount()
  }

  async function markAllRead() {
    await markAllNotificationsRead()
    items.value = items.value.map((item) => ({ ...item, isRead: true, readAt: new Date().toISOString() }))
    latest.value = latest.value.map((item) => ({ ...item, isRead: true, readAt: new Date().toISOString() }))
    unreadCount.value = 0
  }

  async function remove(id: string) {
    await deleteMyNotification(id)
    items.value = items.value.filter((item) => item.id !== id && item.notificationId !== id)
    latest.value = latest.value.filter((item) => item.id !== id && item.notificationId !== id)
    await loadUnreadCount()
  }

  function updateReadState(id: string) {
    const now = new Date().toISOString()
    const update = (item: NotificationItem) =>
      item.id === id || item.notificationId === id ? { ...item, isRead: true, readAt: now } : item
    items.value = items.value.map(update)
    latest.value = latest.value.map(update)
  }

  function mergeRealtime(message: NotificationRealtimeMessage) {
    const item: NotificationItem = { ...message, isRead: false }
    const existing = new Set([message.id, message.notificationId])
    latest.value = [item, ...latest.value.filter((candidate) => !existing.has(candidate.id) && !existing.has(candidate.notificationId))].slice(0, 6)
    items.value = [item, ...items.value.filter((candidate) => !existing.has(candidate.id) && !existing.has(candidate.notificationId))]
    unreadCount.value += 1
    lastUpdatedAt.value = Date.now()
  }

  function schedulePolling() {
    if (!connected.value || pollingTimer || (typeof document !== 'undefined' && document.visibilityState === 'hidden')) {
      return
    }
    pollingTimer = setTimeout(async () => {
      pollingTimer = undefined
      try {
        await loadUnreadCount()
        pollingInterval = defaultPollingInterval
      } catch {
        pollingInterval = Math.min(pollingInterval * 2, 10 * 60_000)
      } finally {
        schedulePolling()
      }
    }, pollingInterval)
  }

  function startPolling(interval = defaultPollingInterval) {
    stopPolling()
    pollingInterval = interval
    connected.value = true
    if (typeof document !== 'undefined') {
      visibilityHandler = () => {
        if (document.visibilityState === 'visible') {
          void loadUnreadCount().catch(() => undefined)
          schedulePolling()
        } else {
          clearPollingTimer()
        }
      }
      document.addEventListener('visibilitychange', visibilityHandler)
    }
    schedulePolling()
  }

  function clearPollingTimer() {
    if (pollingTimer) {
      clearTimeout(pollingTimer)
      pollingTimer = undefined
    }
  }

  function stopPolling() {
    clearPollingTimer()
    connected.value = false
    if (visibilityHandler && typeof document !== 'undefined') {
      document.removeEventListener('visibilitychange', visibilityHandler)
      visibilityHandler = undefined
    }
  }

  // Alias used by the shell and by the admin-compatible API.
  const ensureStarted = async () => {
    if (!connected.value) {
      startPolling()
    }
    await loadUnreadCount().catch(() => undefined)
  }

  function stop() {
    stopPolling()
    unreadCount.value = 0
    latest.value = []
    items.value = []
    error.value = undefined
  }

  return {
    unreadCount,
    latest,
    items,
    loading,
    loadingMore,
    error,
    pageIndex,
    hasNextPage,
    connected,
    hasUnread,
    lastUpdatedAt,
    loadUnreadCount,
    loadLatest,
    load,
    loadMore,
    markRead,
    markAllRead,
    remove,
    mergeRealtime,
    startPolling,
    ensureStarted,
    stopPolling,
    stop,
  }
})

