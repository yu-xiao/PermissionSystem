<script setup lang="ts">
defineOptions({
  name: 'SsoUserBinding',
})

import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import { getSsoProviders, type SsoProviderListItem } from '../../../api/ssoProvider'
import {
  deleteSsoUserBinding,
  getSsoUserBinding,
  getSsoUserBindings,
  unbindSsoUser,
  type SsoUserBindingDetail,
  type SsoUserBindingItem,
} from '../../../api/ssoUserBinding'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<SsoUserBindingItem[]>([])
const providers = ref<SsoProviderListItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<SsoUserBindingDetail>()

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  providerId: undefined as string | undefined,
})

async function loadProviders() {
  const result = await getSsoProviders({ pageIndex: 1, pageSize: 500 })
  providers.value = result.items
}

async function loadData() {
  loading.value = true
  try {
    const result = await getSsoUserBindings(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: SsoUserBindingItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getSsoUserBinding(row.id)
  } finally {
    detailLoading.value = false
  }
}

async function unbind(row: SsoUserBindingItem) {
  await ElMessageBox.confirm(`确认解绑外部用户 ${row.externalUserName || row.externalUserId}？`, '确认解绑')
  await unbindSsoUser(row.id)
  ElMessage.success('解绑成功')
  await loadData()
}

async function remove(row: SsoUserBindingItem) {
  await ElMessageBox.confirm(`确认删除绑定 ${row.externalUserName || row.externalUserId}？`, '确认删除')
  await deleteSsoUserBinding(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    providerId: undefined,
  })
  loadData()
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadProviders()
loadData()
</script>

<template>
  <PageContainer title="SSO 用户绑定" description="查看外部账号与本地用户的绑定关系。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-select v-model="query.providerId" clearable filterable placeholder="SSO Provider" style="width: 220px">
          <el-option
            v-for="provider in providers"
            :key="provider.id"
            :label="`${provider.providerName} (${provider.providerCode})`"
            :value="provider.id"
          />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="外部用户 / 邮箱 / 手机" />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'sso:user-binding:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="providerCode" label="Provider" min-width="140" />
      <el-table-column prop="externalUserId" label="外部用户ID" min-width="180" show-overflow-tooltip />
      <el-table-column prop="externalUserName" label="外部用户名" min-width="140" />
      <el-table-column prop="externalEmail" label="外部邮箱" min-width="180" show-overflow-tooltip />
      <el-table-column prop="externalPhone" label="外部手机" min-width="130" />
      <el-table-column prop="localUserName" label="本地用户" min-width="140" />
      <el-table-column prop="localDisplayName" label="显示名" min-width="140" />
      <el-table-column prop="lastLoginAt" label="最近登录" width="180">
        <template #default="{ row }">{{ formatDate(row.lastLoginAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="190" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'sso:user-binding:view'" link type="primary" @click="openDetail(row)">详情</el-button>
          <el-button v-permission="'sso:user-binding:unbind'" link type="warning" @click="unbind(row)">解绑</el-button>
          <el-button v-permission="'sso:user-binding:unbind'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="detailVisible" title="绑定详情" width="820px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="Provider">{{ detail.providerName || detail.providerCode }}</el-descriptions-item>
          <el-descriptions-item label="TenantId">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="外部用户ID">{{ detail.externalUserId }}</el-descriptions-item>
          <el-descriptions-item label="外部用户名">{{ detail.externalUserName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="外部邮箱">{{ detail.externalEmail || '-' }}</el-descriptions-item>
          <el-descriptions-item label="外部手机">{{ detail.externalPhone || '-' }}</el-descriptions-item>
          <el-descriptions-item label="本地用户">{{ detail.localUserName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="显示名">{{ detail.localDisplayName || '-' }}</el-descriptions-item>
          <el-descriptions-item label="最近登录">{{ formatDate(detail.lastLoginAt) }}</el-descriptions-item>
          <el-descriptions-item label="创建时间">{{ formatDate(detail.createdAt) }}</el-descriptions-item>
          <el-descriptions-item label="Claims" :span="2">
            <pre class="claims-json">{{ detail.claimsJson || '-' }}</pre>
          </el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.claims-json {
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
