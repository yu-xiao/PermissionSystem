<script setup lang="ts">
import { reactive, ref } from 'vue'
import {
  getOutboxMessageDetail,
  getOutboxMessages,
  type OutboxMessageDetail,
  type OutboxMessageItem,
} from '../../../api/outbox-messages'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<OutboxMessageItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<OutboxMessageDetail>()
const dateRange = ref<string[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: '',
  messageType: '',
  routingKey: '',
  startTime: undefined as string | undefined,
  endTime: undefined as string | undefined,
})

async function loadData() {
  syncDateRange()
  loading.value = true
  try {
    const result = await getOutboxMessages(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: OutboxMessageItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getOutboxMessageDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    status: '',
    messageType: '',
    routingKey: '',
    startTime: undefined,
    endTime: undefined,
  })
  dateRange.value = []
  loadData()
}

function syncDateRange() {
  query.startTime = dateRange.value[0]
  query.endTime = dateRange.value[1]
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function statusType(status: string) {
  if (status === 'Published') {
    return 'success'
  }

  if (status === 'Failed') {
    return 'danger'
  }

  if (status === 'Processing') {
    return 'warning'
  }

  return 'info'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="消息ID / 交换机 / 类型" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.messageType" clearable placeholder="消息类型" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.routingKey" clearable placeholder="路由键" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="状态" style="width: 140px">
          <el-option label="待发送" value="Pending" />
          <el-option label="处理中" value="Processing" />
          <el-option label="已发布" value="Published" />
          <el-option label="失败" value="Failed" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-date-picker
          v-model="dateRange"
          type="datetimerange"
          value-format="YYYY-MM-DDTHH:mm:ssZ"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
        />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:outbox:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="messageId" label="消息ID" min-width="180" show-overflow-tooltip />
      <el-table-column prop="messageType" label="类型" min-width="220" show-overflow-tooltip />
      <el-table-column prop="exchange" label="交换机" min-width="150" show-overflow-tooltip />
      <el-table-column prop="routingKey" label="路由键" min-width="180" show-overflow-tooltip />
      <el-table-column prop="status" label="状态" width="120">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ $displayText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="retryCount" label="重试次数" width="90" />
      <el-table-column prop="nextRetryAt" label="下次重试" width="180">
        <template #default="{ row }">{{ formatDate(row.nextRetryAt) }}</template>
      </el-table-column>
      <el-table-column prop="processedAt" label="处理时间" width="180">
        <template #default="{ row }">{{ formatDate(row.processedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:outbox:view'" link type="primary" @click="openDetail(row)">详情</el-button>
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

    <el-dialog v-model="detailVisible" title="发件箱消息详情" width="860px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="消息ID" :span="2">{{ detail.messageId }}</el-descriptions-item>
          <el-descriptions-item label="租户">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ $displayText(detail.status) }}</el-descriptions-item>
          <el-descriptions-item label="交换机">{{ detail.exchange }}</el-descriptions-item>
          <el-descriptions-item label="路由键">{{ detail.routingKey }}</el-descriptions-item>
          <el-descriptions-item label="类型" :span="2">{{ detail.messageType }}</el-descriptions-item>
          <el-descriptions-item label="重试次数">{{ detail.retryCount }}</el-descriptions-item>
          <el-descriptions-item label="下次重试时间">{{ formatDate(detail.nextRetryAt) }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="处理时间">{{ formatDate(detail.processedAt) }}</el-descriptions-item>
          <el-descriptions-item label="请求头" :span="2">
            <pre class="message-body">{{ detail.headers || '-' }}</pre>
          </el-descriptions-item>
          <el-descriptions-item label="错误" :span="2">
            <pre class="message-body">{{ detail.errorMessage || '-' }}</pre>
          </el-descriptions-item>
          <el-descriptions-item label="载荷" :span="2">
            <pre class="message-body">{{ detail.payload || '-' }}</pre>
          </el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>

<style scoped>
.message-body {
  max-height: 260px;
  margin: 0;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.5;
}
</style>
