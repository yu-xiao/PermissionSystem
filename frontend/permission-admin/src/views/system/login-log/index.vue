<script setup lang="ts">
import { reactive, ref } from 'vue'
import {
  getLoginLogDetail,
  getLoginLogs,
  type LoginLogItem,
} from '../../../api/login-logs'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<LoginLogItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<LoginLogItem>()
const dateRange = ref<string[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  userName: '',
  loginType: '',
  loginResult: '',
  traceId: '',
  startTime: undefined as string | undefined,
  endTime: undefined as string | undefined,
})

async function loadData() {
  syncDateRange()
  loading.value = true
  try {
    const result = await getLoginLogs(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: LoginLogItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getLoginLogDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    userName: '',
    loginType: '',
    loginResult: '',
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

function resultType(result: string) {
  return result === 'Succeeded' ? 'success' : 'danger'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Keyword / IP / trace" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.userName" clearable placeholder="User name" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.loginResult" clearable placeholder="Result" style="width: 140px">
          <el-option label="Succeeded" value="Succeeded" />
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
        <el-button v-permission="'system:login-log:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="Time" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="userName" label="User" min-width="140" />
      <el-table-column prop="loginType" label="Login Type" width="130" />
      <el-table-column prop="loginResult" label="Result" width="120">
        <template #default="{ row }">
          <el-tag :type="resultType(row.loginResult)">{{ row.loginResult }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="failureReason" label="Failure Reason" min-width="220" show-overflow-tooltip />
      <el-table-column prop="ipAddress" label="IP" min-width="130" />
      <el-table-column prop="traceId" label="TraceId" min-width="180" show-overflow-tooltip />
      <el-table-column label="Actions" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:login-log:view'" link type="primary" @click="openDetail(row)">Detail</el-button>
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

    <el-dialog v-model="detailVisible" title="Login Log Detail" width="760px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="User">{{ detail.userName }}</el-descriptions-item>
          <el-descriptions-item label="Tenant">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="Login Type">{{ detail.loginType }}</el-descriptions-item>
          <el-descriptions-item label="Result">{{ detail.loginResult }}</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="Time">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="TraceId" :span="2">{{ detail.traceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="Failure Reason" :span="2">{{ detail.failureReason || '-' }}</el-descriptions-item>
          <el-descriptions-item label="UserAgent" :span="2">{{ detail.userAgent || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>
