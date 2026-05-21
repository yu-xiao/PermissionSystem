<script setup lang="ts">
import { reactive, ref } from 'vue'
import {
  getOperationLogDetail,
  getOperationLogs,
  type OperationLogDetail,
  type OperationLogItem,
} from '../../../api/operation-logs'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<OperationLogItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<OperationLogDetail>()
const dateRange = ref<string[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  userName: '',
  module: '',
  requestMethod: '',
  statusCode: undefined as number | undefined,
  traceId: '',
  startTime: undefined as string | undefined,
  endTime: undefined as string | undefined,
})

async function loadData() {
  syncDateRange()
  loading.value = true
  try {
    const result = await getOperationLogs(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: OperationLogItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getOperationLogDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    userName: '',
    module: '',
    requestMethod: '',
    statusCode: undefined,
    traceId: '',
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

function statusType(statusCode: number) {
  if (statusCode >= 500) {
    return 'danger'
  }

  if (statusCode >= 400) {
    return 'warning'
  }

  return 'success'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Keyword / path / trace" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.userName" clearable placeholder="User name" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.module" clearable placeholder="Module" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.requestMethod" clearable placeholder="Method" style="width: 120px">
          <el-option label="GET" value="GET" />
          <el-option label="POST" value="POST" />
          <el-option label="PUT" value="PUT" />
          <el-option label="PATCH" value="PATCH" />
          <el-option label="DELETE" value="DELETE" />
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
        <el-button v-permission="'system:operation-log:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="Time" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="userName" label="User" min-width="120" />
      <el-table-column prop="module" label="Module" min-width="120" />
      <el-table-column prop="action" label="Action" min-width="120" />
      <el-table-column prop="requestMethod" label="Method" width="100" />
      <el-table-column prop="requestPath" label="Path" min-width="220" show-overflow-tooltip />
      <el-table-column prop="statusCode" label="Status" width="100">
        <template #default="{ row }">
          <el-tag :type="statusType(row.statusCode)">{{ row.statusCode }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="elapsedMilliseconds" label="Elapsed" width="110">
        <template #default="{ row }">{{ row.elapsedMilliseconds }} ms</template>
      </el-table-column>
      <el-table-column prop="ipAddress" label="IP" min-width="130" />
      <el-table-column prop="traceId" label="TraceId" min-width="180" show-overflow-tooltip />
      <el-table-column label="Actions" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:operation-log:view'" link type="primary" @click="openDetail(row)">Detail</el-button>
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

    <el-dialog v-model="detailVisible" title="Operation Log Detail" width="860px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="User">{{ detail.userName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="Tenant">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="Module">{{ detail.module }}</el-descriptions-item>
          <el-descriptions-item label="Action">{{ detail.action }}</el-descriptions-item>
          <el-descriptions-item label="Method">{{ detail.requestMethod }}</el-descriptions-item>
          <el-descriptions-item label="Status">{{ detail.statusCode }}</el-descriptions-item>
          <el-descriptions-item label="Elapsed">{{ detail.elapsedMilliseconds }} ms</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="Path" :span="2">{{ detail.requestPath || '-' }}</el-descriptions-item>
          <el-descriptions-item label="TraceId" :span="2">{{ detail.traceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="UserAgent" :span="2">{{ detail.userAgent || '-' }}</el-descriptions-item>
          <el-descriptions-item label="RequestBody" :span="2">
            <pre class="log-body">{{ detail.requestBody || '-' }}</pre>
          </el-descriptions-item>
          <el-descriptions-item label="ResponseBody" :span="2">
            <pre class="log-body">{{ detail.responseBody || '-' }}</pre>
          </el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>

<style scoped>
.log-body {
  max-height: 240px;
  margin: 0;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.5;
}
</style>
