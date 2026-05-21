<script setup lang="ts">
import { reactive, ref } from 'vue'
import {
  getInboxMessageDetail,
  getInboxMessages,
  type InboxMessageDetail,
  type InboxMessageItem,
} from '../../../api/inbox-messages'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<InboxMessageItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<InboxMessageDetail>()
const dateRange = ref<string[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  consumer: '',
  status: '',
  messageType: '',
  startTime: undefined as string | undefined,
  endTime: undefined as string | undefined,
})

async function loadData() {
  syncDateRange()
  loading.value = true
  try {
    const result = await getInboxMessages(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: InboxMessageItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getInboxMessageDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    consumer: '',
    status: '',
    messageType: '',
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
  if (status === 'Processed') {
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
        <el-input v-model="query.keyword" clearable placeholder="MessageId / consumer / type" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.consumer" clearable placeholder="Consumer" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.messageType" clearable placeholder="Message type" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="Status" style="width: 140px">
          <el-option label="Processing" value="Processing" />
          <el-option label="Processed" value="Processed" />
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
        <el-button v-permission="'system:inbox:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="Created" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="messageId" label="MessageId" min-width="180" show-overflow-tooltip />
      <el-table-column prop="consumer" label="Consumer" min-width="160" show-overflow-tooltip />
      <el-table-column prop="messageType" label="Type" min-width="220" show-overflow-tooltip />
      <el-table-column prop="payloadHash" label="PayloadHash" min-width="180" show-overflow-tooltip />
      <el-table-column prop="status" label="Status" width="120">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="processedAt" label="Processed" width="180">
        <template #default="{ row }">{{ formatDate(row.processedAt) }}</template>
      </el-table-column>
      <el-table-column label="Actions" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:inbox:view'" link type="primary" @click="openDetail(row)">Detail</el-button>
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

    <el-dialog v-model="detailVisible" title="Inbox Message Detail" width="760px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="MessageId" :span="2">{{ detail.messageId }}</el-descriptions-item>
          <el-descriptions-item label="Tenant">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="Status">{{ detail.status }}</el-descriptions-item>
          <el-descriptions-item label="Consumer">{{ detail.consumer }}</el-descriptions-item>
          <el-descriptions-item label="Type">{{ detail.messageType }}</el-descriptions-item>
          <el-descriptions-item label="PayloadHash" :span="2">{{ detail.payloadHash }}</el-descriptions-item>
          <el-descriptions-item label="CreatedAt">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="ProcessedAt">{{ formatDate(detail.processedAt) }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>
