<script setup lang="ts">
import { Bell } from '@element-plus/icons-vue'
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { markNotificationRead, type NotificationItem } from '../api/notifications'
import { useNotificationStore } from '../stores/notifications'

const router = useRouter()
const notificationStore = useNotificationStore()
const detailVisible = ref(false)
const current = ref<NotificationItem>()

onMounted(async () => {
  await notificationStore.loadLatest()
  await notificationStore.ensureStarted()
})

async function openNotification(item: NotificationItem) {
  current.value = item
  detailVisible.value = true
  if (!item.isRead) {
    await markNotificationRead(item.id)
    item.isRead = true
    notificationStore.unreadCount = Math.max(0, notificationStore.unreadCount - 1)
  }
}

function goToNotifications() {
  router.push('/system/notifications')
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}
</script>

<template>
  <div>
    <el-popover placement="bottom-end" width="360" trigger="click" @show="notificationStore.loadLatest">
      <template #reference>
        <el-badge :value="notificationStore.unreadCount" :hidden="notificationStore.unreadCount === 0" :max="99">
          <el-button text :icon="Bell" />
        </el-badge>
      </template>

      <div class="notification-popover">
        <div class="notification-popover__header">
          <strong>通知</strong>
          <el-button link type="primary" @click="goToNotifications">查看全部</el-button>
        </div>
        <el-empty v-if="notificationStore.latest.length === 0" :image-size="72" description="暂无通知" />
        <button
          v-for="item in notificationStore.latest"
          v-else
          :key="item.id"
          class="notification-item"
          type="button"
          @click="openNotification(item)"
        >
          <span class="notification-item__title">
            <el-tag v-if="!item.isRead" size="small" type="danger">新</el-tag>
            {{ item.title }}
          </span>
          <span class="notification-item__content">{{ item.content }}</span>
          <span class="notification-item__time">{{ formatDate(item.createdAt) }}</span>
        </button>
      </div>
    </el-popover>

    <el-dialog v-model="detailVisible" title="通知详情" width="560px">
      <el-descriptions v-if="current" :column="1" border>
        <el-descriptions-item label="类型">{{ $displayText(current.type) }}</el-descriptions-item>
        <el-descriptions-item label="标题">{{ current.title }}</el-descriptions-item>
        <el-descriptions-item label="内容">{{ current.content }}</el-descriptions-item>
        <el-descriptions-item label="创建时间">{{ formatDate(current.createdAt) }}</el-descriptions-item>
      </el-descriptions>
    </el-dialog>
  </div>
</template>

<style scoped>
.notification-popover {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.notification-popover__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.notification-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
  width: 100%;
  padding: 10px;
  border: 1px solid #e4e7ed;
  border-radius: 6px;
  background: #fff;
  color: inherit;
  text-align: left;
  cursor: pointer;
}

.notification-item:hover {
  background: #f5f7fb;
}

.notification-item__title {
  display: flex;
  align-items: center;
  gap: 6px;
  font-weight: 600;
}

.notification-item__content {
  overflow: hidden;
  color: #606266;
  font-size: 13px;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.notification-item__time {
  color: #909399;
  font-size: 12px;
}
</style>
