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
  ElMessage.success('所有通知已标记为已读')
  await loadData()
}

async function remove(row: NotificationItem) {
  await ElMessageBox.confirm(`确认删除通知 ${row.title}？`, '确认删除')
  await deleteMyNotification(row.id)
  ElMessage.success('通知已删除')
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
        <el-input v-model="query.keyword" clearable placeholder="标题 / 内容" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.type" clearable placeholder="类型" style="width: 140px">
          <el-option label="系统" value="System" />
          <el-option label="安全" value="Security" />
          <el-option label="任务" value="Task" />
          <el-option label="审批" value="Approval" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isRead" clearable placeholder="阅读状态" style="width: 140px">
          <el-option label="未读" :value="false" />
          <el-option label="已读" :value="true" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:notification:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button @click="markAllRead">全部标为已读</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="type" label="类型" width="120">
        <template #default="{ row }">
          <el-tag :type="statusType(row.type)">{{ $displayText(row.type) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="title" label="标题" min-width="220" show-overflow-tooltip />
      <el-table-column prop="content" label="内容" min-width="260" show-overflow-tooltip />
      <el-table-column prop="isRead" label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isRead ? 'info' : 'danger'">{{ row.isRead ? '已读' : '未读' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="openDetail(row)">详情</el-button>
          <el-button link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="detailVisible" title="通知详情" width="640px">
      <el-descriptions v-if="current" :column="1" border>
        <el-descriptions-item label="类型">{{ $displayText(current.type) }}</el-descriptions-item>
        <el-descriptions-item label="标题">{{ current.title }}</el-descriptions-item>
        <el-descriptions-item label="内容">{{ current.content }}</el-descriptions-item>
        <el-descriptions-item label="发送人">{{ current.senderName || '-' }}</el-descriptions-item>
        <el-descriptions-item label="链接">{{ current.linkUrl || '-' }}</el-descriptions-item>
        <el-descriptions-item label="创建时间">{{ formatDate(current.createdAt) }}</el-descriptions-item>
      </el-descriptions>
    </el-dialog>
  </section>
</template>
