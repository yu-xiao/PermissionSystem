<script setup lang="ts">
defineOptions({
  name: 'SsoLoginLog',
})

import { reactive, ref } from 'vue'
import { SsoProviderType } from '../../../api/ssoProvider'
import {
  getSsoLoginLog,
  getSsoLoginLogs,
  SsoLoginResult,
  type SsoLoginLogItem,
} from '../../../api/ssoLoginLog'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<SsoLoginLogItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<SsoLoginLogItem>()
const dateRange = ref<string[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  providerCode: '',
  providerType: undefined as SsoProviderType | undefined,
  loginResult: undefined as SsoLoginResult | undefined,
  startAt: undefined as string | undefined,
  endAt: undefined as string | undefined,
})

async function loadData() {
  syncDateRange()
  loading.value = true
  try {
    const result = await getSsoLoginLogs(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: SsoLoginLogItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getSsoLoginLog(row.id)
  } finally {
    detailLoading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    providerCode: '',
    providerType: undefined,
    loginResult: undefined,
    startAt: undefined,
    endAt: undefined,
  })
  dateRange.value = []
  loadData()
}

function syncDateRange() {
  query.startAt = dateRange.value[0]
  query.endAt = dateRange.value[1]
}

function providerTypeText(value: SsoProviderType) {
  return value === SsoProviderType.Oidc ? 'OIDC' : value === SsoProviderType.Saml ? 'SAML2' : 'OAuth2'
}

function resultText(value: SsoLoginResult) {
  const map: Record<SsoLoginResult, string> = {
    [SsoLoginResult.Success]: '成功',
    [SsoLoginResult.Failed]: '失败',
    [SsoLoginResult.UserDisabled]: '用户禁用',
    [SsoLoginResult.TenantDisabled]: '租户禁用',
    [SsoLoginResult.BindingFailed]: '绑定失败',
    [SsoLoginResult.AutoCreateFailed]: '创建失败',
  }
  return map[value] ?? '未知'
}

function resultType(value: SsoLoginResult) {
  return value === SsoLoginResult.Success ? 'success' : value === SsoLoginResult.Failed ? 'danger' : 'warning'
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="SSO 登录日志" description="查看外部 SSO 登录审计记录和失败原因。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="用户 / IP / TraceId" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.providerCode" clearable placeholder="ProviderCode" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.providerType" clearable placeholder="类型" style="width: 130px">
          <el-option label="OIDC" :value="SsoProviderType.Oidc" />
          <el-option label="SAML2" :value="SsoProviderType.Saml" />
          <el-option label="OAuth2" :value="SsoProviderType.OAuth2" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.loginResult" clearable placeholder="结果" style="width: 140px">
          <el-option label="成功" :value="SsoLoginResult.Success" />
          <el-option label="失败" :value="SsoLoginResult.Failed" />
          <el-option label="用户禁用" :value="SsoLoginResult.UserDisabled" />
          <el-option label="租户禁用" :value="SsoLoginResult.TenantDisabled" />
          <el-option label="绑定失败" :value="SsoLoginResult.BindingFailed" />
          <el-option label="创建失败" :value="SsoLoginResult.AutoCreateFailed" />
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
        <el-button v-permission="'sso:login-log:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="providerCode" label="Provider" min-width="140" />
      <el-table-column label="类型" width="100">
        <template #default="{ row }">{{ providerTypeText(row.providerType) }}</template>
      </el-table-column>
      <el-table-column prop="externalUserName" label="外部用户" min-width="150" />
      <el-table-column prop="localUserName" label="本地用户" min-width="140" />
      <el-table-column label="结果" width="120">
        <template #default="{ row }">
          <el-tag :type="resultType(row.loginResult)">{{ resultText(row.loginResult) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="failureReason" label="失败原因" min-width="220" show-overflow-tooltip />
      <el-table-column prop="ipAddress" label="IP" min-width="130" />
      <el-table-column prop="traceId" label="TraceId" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="100" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'sso:login-log:view'" link type="primary" @click="openDetail(row)">详情</el-button>
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

    <el-dialog v-model="detailVisible" title="SSO 登录日志详情" width="820px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="Provider">{{ detail.providerName || detail.providerCode }}</el-descriptions-item>
          <el-descriptions-item label="类型">{{ providerTypeText(detail.providerType) }}</el-descriptions-item>
          <el-descriptions-item label="结果">
            <el-tag :type="resultType(detail.loginResult)">{{ resultText(detail.loginResult) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="时间">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="外部用户ID">{{ detail.externalUserId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="外部用户名">{{ detail.externalUserName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="本地用户ID">{{ detail.localUserId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="本地用户名">{{ detail.localUserName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="TraceId">{{ detail.traceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="UserAgent" :span="2">{{ detail.userAgent || '-' }}</el-descriptions-item>
          <el-descriptions-item label="失败原因" :span="2">{{ detail.failureReason || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </PageContainer>
</template>
