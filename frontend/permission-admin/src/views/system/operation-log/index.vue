<script setup lang="ts">
defineOptions({
  name: 'SystemOperationLog',
})

import { reactive, ref } from 'vue'
import {
  getOperationLogDetail,
  getOperationLogs,
  type OperationLogDetail,
  type OperationLogItem,
} from '../../../api/operation-logs'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

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
  <PageContainer title="操作日志" description="查看用户操作行为、请求路径、耗时和追踪信息。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="关键词 / 路径 / 追踪ID" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.userName" clearable placeholder="用户名" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.module" clearable placeholder="模块" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.requestMethod" clearable placeholder="方法" style="width: 120px">
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
          start-placeholder="开始时间"
          end-placeholder="结束时间"
        />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:operation-log:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="userName" label="用户" min-width="120" />
      <el-table-column prop="module" label="模块" min-width="120" />
      <el-table-column prop="action" label="动作" min-width="120" />
      <el-table-column prop="requestMethod" label="方法" width="100" />
      <el-table-column prop="requestPath" label="路径" min-width="220" show-overflow-tooltip />
      <el-table-column prop="statusCode" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="statusType(row.statusCode)">{{ row.statusCode }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="elapsedMilliseconds" label="耗时" width="110">
        <template #default="{ row }">{{ row.elapsedMilliseconds }} ms</template>
      </el-table-column>
      <el-table-column prop="ipAddress" label="IP" min-width="130" />
      <el-table-column prop="traceId" label="追踪ID" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:operation-log:view'" link type="primary" @click="openDetail(row)">详情</el-button>
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

    <el-dialog v-model="detailVisible" title="操作日志详情" width="860px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="用户">{{ detail.userName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="租户">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="模块">{{ detail.module }}</el-descriptions-item>
          <el-descriptions-item label="动作">{{ detail.action }}</el-descriptions-item>
          <el-descriptions-item label="方法">{{ detail.requestMethod }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ detail.statusCode }}</el-descriptions-item>
          <el-descriptions-item label="耗时">{{ detail.elapsedMilliseconds }} ms</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="路径" :span="2">{{ detail.requestPath || '-' }}</el-descriptions-item>
          <el-descriptions-item label="追踪ID" :span="2">{{ detail.traceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="用户代理" :span="2">{{ detail.userAgent || '-' }}</el-descriptions-item>
          <el-descriptions-item label="请求体" :span="2">
            <pre class="log-body">{{ detail.requestBody || '-' }}</pre>
          </el-descriptions-item>
          <el-descriptions-item label="响应体" :span="2">
            <pre class="log-body">{{ detail.responseBody || '-' }}</pre>
          </el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </PageContainer>
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
