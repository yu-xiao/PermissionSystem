import { defineStore } from 'pinia'
import { ref } from 'vue'
import { ElMessage } from 'element-plus'
import {
  getMyNotifications,
  getMyUnreadNotificationCount,
  type NotificationItem,
  type NotificationRealtimeMessage,
} from '../api/notifications'
import { startNotificationConnection, type SignalRLiteConnection } from '../utils/signalr-lite'

export const useNotificationStore = defineStore('notifications', () => {
  const unreadCount = ref(0)
  const latest = ref<NotificationItem[]>([])
  const connected = ref(false)
  let connection: SignalRLiteConnection | undefined
  let starting: Promise<void> | undefined

  async function loadUnreadCount() {
    unreadCount.value = await getMyUnreadNotificationCount()
  }

  async function loadLatest() {
    const result = await getMyNotifications({ pageIndex: 1, pageSize: 6 })
    latest.value = result.items
    unreadCount.value = result.items.filter((item) => !item.isRead).length
    await loadUnreadCount()
  }

  async function ensureStarted() {
    if (connection || starting) {
      return starting
    }

    starting = startNotificationConnection((payload) => {
      const message = payload as NotificationRealtimeMessage
      unreadCount.value += 1
      latest.value = [
        {
          ...message,
          senderName: 'System',
          isRead: false,
        },
        ...latest.value,
      ].slice(0, 6)
      ElMessage.info(message.title)
    })
      .then((created) => {
        connection = created
        connected.value = Boolean(created)
      })
      .finally(() => {
        starting = undefined
      })

    return starting
  }

  function stop() {
    connection?.stop()
    connection = undefined
    connected.value = false
  }

  return {
    unreadCount,
    latest,
    connected,
    loadUnreadCount,
    loadLatest,
    ensureStarted,
    stop,
  }
})
