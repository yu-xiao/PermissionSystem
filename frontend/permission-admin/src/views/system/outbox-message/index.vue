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
        <el-input v-model="query.keyword" clearable placeholder="MessageId / exchange / type" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.messageType" clearable placeholder="Message type" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.routingKey" clearable placeholder="Routing key" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="Status" style="width: 140px">
          <el-option label="Pending" value="Pending" />
          <el-option label="Processing" value="Processing" />
          <el-option label="Published" value="Published" />
          <el-option label="Failed" value="Failed" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-date-picker
          v-model="dateRange"
          type="datetimerange"
          value-format="YYYY-MM-DDTHH:mm:ssZ"
          start-placeholder="Start"
          end-placeholder="End"
        />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:outbox:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="Created" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="messageId" label="MessageId" min-width="180" show-overflow-tooltip />
      <el-table-column prop="messageType" label="Type" min-width="220" show-overflow-tooltip />
      <el-table-column prop="exchange" label="Exchange" min-width="150" show-overflow-tooltip />
      <el-table-column prop="routingKey" label="RoutingKey" min-width="180" show-overflow-tooltip />
      <el-table-column prop="status" label="Status" width="120">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="retryCount" label="Retries" width="90" />
      <el-table-column prop="nextRetryAt" label="Next Retry" width="180">
        <template #default="{ row }">{{ formatDate(row.nextRetryAt) }}</template>
      </el-table-column>
      <el-table-column prop="processedAt" label="Processed" width="180">
        <template #default="{ row }">{{ formatDate(row.processedAt) }}</template>
      </el-table-column>
      <el-table-column label="Actions" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:outbox:view'" link type="primary" @click="openDetail(row)">Detail</el-button>
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

    <el-dialog v-model="detailVisible" title="Outbox Message Detail" width="860px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="MessageId" :span="2">{{ detail.messageId }}</el-descriptions-item>
          <el-descriptions-item label="Tenant">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="Status">{{ detail.status }}</el-descriptions-item>
          <el-descriptions-item label="Exchange">{{ detail.exchange }}</el-descriptions-item>
          <el-descriptions-item label="RoutingKey">{{ detail.routingKey }}</el-descriptions-item>
          <el-descriptions-item label="Type" :span="2">{{ detail.messageType }}</el-descriptions-item>
          <el-descriptions-item label="RetryCount">{{ detail.retryCount }}</el-descriptions-item>
          <el-descriptions-item label="NextRetryAt">{{ formatDate(detail.nextRetryAt) }}</el-descriptions-item>
          <el-descriptions-item label="CreatedAt">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="ProcessedAt">{{ formatDate(detail.processedAt) }}</el-descriptions-item>
          <el-descriptions-item label="Headers" :span="2">
            <pre class="message-body">{{ detail.headers || '-' }}</pre>
          </el-descriptions-item>
          <el-descriptions-item label="Error" :span="2">
            <pre class="message-body">{{ detail.errorMessage || '-' }}</pre>
          </el-descriptions-item>
          <el-descriptions-item label="Payload" :span="2">
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
