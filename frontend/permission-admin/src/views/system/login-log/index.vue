<script setup lang="ts">
defineOptions({
  name: 'SystemLoginLog',
})

import { reactive, ref } from 'vue'
import {
  getLoginLogDetail,
  getLoginLogs,
  type LoginLogItem,
} from '../../../api/login-logs'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

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
  <PageContainer title="登录日志" description="查看用户登录结果、失败原因、IP 和追踪信息。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="关键词 / IP / 追踪ID" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.userName" clearable placeholder="用户名" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.loginResult" clearable placeholder="结果" style="width: 140px">
          <el-option label="成功" value="Succeeded" />
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
        <el-button v-permission="'system:login-log:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="userName" label="用户" min-width="140" />
      <el-table-column prop="loginType" label="登录类型" width="130" />
      <el-table-column prop="loginResult" label="结果" width="120">
        <template #default="{ row }">
          <el-tag :type="resultType(row.loginResult)">{{ $displayText(row.loginResult) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="failureReason" label="失败原因" min-width="220" show-overflow-tooltip />
      <el-table-column prop="ipAddress" label="IP" min-width="130" />
      <el-table-column prop="traceId" label="追踪ID" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="110" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:login-log:view'" link type="primary" @click="openDetail(row)">详情</el-button>
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

    <el-dialog v-model="detailVisible" title="登录日志详情" width="760px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="用户">{{ detail.userName }}</el-descriptions-item>
          <el-descriptions-item label="租户">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="登录类型">{{ detail.loginType }}</el-descriptions-item>
          <el-descriptions-item label="结果">{{ $displayText(detail.loginResult) }}</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="时间">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="追踪ID" :span="2">{{ detail.traceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="失败原因" :span="2">{{ detail.failureReason || '-' }}</el-descriptions-item>
          <el-descriptions-item label="用户代理" :span="2">{{ detail.userAgent || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </PageContainer>
</template>
