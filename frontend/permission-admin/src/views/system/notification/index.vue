<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  deleteMyNotification,
  getMyNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type NotificationItem,
} from '../../../api/notifications'
import { useNotificationStore } from '../../../stores/notifications'

const notificationStore = useNotificationStore()
const loading = ref(false)
const detailVisible = ref(false)
const tableData = ref<NotificationItem[]>([])
const total = ref(0)
const current = ref<NotificationItem>()

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  type: '',
  isRead: undefined as boolean | undefined,
})

async function loadData() {
  loading.value = true
  try {
    const result = await getMyNotifications({
      ...query,
      type: query.type || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
    await notificationStore.loadUnreadCount()
  } finally {
    loading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    type: '',
    isRead: undefined,
  })
  loadData()
}

async function openDetail(row: NotificationItem) {
  current.value = row
  detailVisible.value = true
  if (!row.isRead) {
    await markNotificationRead(row.id)
    row.isRead = true
    await notificationStore.loadUnreadCount()
  }
}

async function markAllRead() {
  await markAllNotificationsRead()
  ElMessage.success('All notifications marked as read.')
  await loadData()
}

async function remove(row: NotificationItem) {
  await ElMessageBox.confirm(`Delete notification "${row.title}"?`, 'Confirm Delete')
  await deleteMyNotification(row.id)
  ElMessage.success('Notification deleted.')
  await loadData()
}

function statusType(type: string) {
  if (type === 'Security') {
    return 'danger'
  }

  if (type === 'Task') {
    return 'warning'
  }

  if (type === 'Approval') {
    return 'success'
  }

  return 'info'
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Title / content" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.type" clearable placeholder="Type" style="width: 140px">
          <el-option label="System" value="System" />
          <el-option label="Security" value="Security" />
          <el-option label="Task" value="Task" />
          <el-option label="Approval" value="Approval" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isRead" clearable placeholder="Read status" style="width: 140px">
          <el-option label="Unread" :value="false" />
          <el-option label="Read" :value="true" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:notification:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
        <el-button @click="markAllRead">Mark all read</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="Created" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="type" label="Type" width="120">
        <template #default="{ row }">
          <el-tag :type="statusType(row.type)">{{ row.type }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="title" label="Title" min-width="220" show-overflow-tooltip />
      <el-table-column prop="content" label="Content" min-width="260" show-overflow-tooltip />
      <el-table-column prop="isRead" label="Status" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isRead ? 'info' : 'danger'">{{ row.isRead ? 'Read' : 'Unread' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Actions" width="150" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row)">Detail</el-button>
          <el-button link type="danger" @click="remove(row)">Delete</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pager"
      background
      layout="total, sizes, prev, pager, next"
      :total="total"
      @change="loadData"
    />

    <el-dialog v-model="detailVisible" title="Notification Detail" width="640px">
      <el-descriptions v-if="current" :column="1" border>
        <el-descriptions-item label="Type">{{ current.type }}</el-descriptions-item>
        <el-descriptions-item label="Title">{{ current.title }}</el-descriptions-item>
        <el-descriptions-item label="Content">{{ current.content }}</el-descriptions-item>
        <el-descriptions-item label="Sender">{{ current.senderName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="Link">{{ current.linkUrl || '-' }}</el-descriptions-item>
        <el-descriptions-item label="Created">{{ formatDate(current.createdAt) }}</el-descriptions-item>
      </el-descriptions>
    </el-dialog>
  </section>
</template>
