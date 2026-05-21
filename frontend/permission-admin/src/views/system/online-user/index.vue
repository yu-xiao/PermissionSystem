<script setup lang="ts">
import { ElMessage, ElMessageBox } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  getOnlineUserDetail,
  getOnlineUsers,
  kickoutOnlineUser,
  type OnlineUserItem,
} from '../../../api/online-users'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<OnlineUserItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<OnlineUserItem>()

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  isRevoked: undefined as boolean | undefined,
})

async function loadData() {
  loading.value = true
  try {
    const result = await getOnlineUsers(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    isRevoked: undefined,
  })
  loadData()
}

async function openDetail(row: OnlineUserItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getOnlineUserDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

async function kickout(row: OnlineUserItem) {
  await ElMessageBox.confirm(`Force logout user "${row.userName}"?`, 'Confirm Kickout')
  await kickoutOnlineUser(row.id, 'Force logout by administrator.')
  ElMessage.success('User session has been revoked.')
  await loadData()
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
        <el-input v-model="query.keyword" clearable placeholder="User / session / IP" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isRevoked" clearable placeholder="Status" style="width: 140px">
          <el-option label="Online" :value="false" />
          <el-option label="Revoked" :value="true" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:online-user:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="userName" label="User" min-width="150" />
      <el-table-column prop="ipAddress" label="IP" min-width="140">
        <template #default="{ row }">{{ row.ipAddress || '-' }}</template>
      </el-table-column>
      <el-table-column prop="sessionId" label="Session" min-width="220" show-overflow-tooltip />
      <el-table-column prop="loginAt" label="Login At" width="180">
        <template #default="{ row }">{{ formatDate(row.loginAt) }}</template>
      </el-table-column>
      <el-table-column prop="lastActiveAt" label="Last Active" width="180">
        <template #default="{ row }">{{ formatDate(row.lastActiveAt) }}</template>
      </el-table-column>
      <el-table-column prop="expiresAt" label="Expires" width="180">
        <template #default="{ row }">{{ formatDate(row.expiresAt) }}</template>
      </el-table-column>
      <el-table-column prop="isRevoked" label="Status" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isRevoked ? 'danger' : 'success'">{{ row.isRevoked ? 'Revoked' : 'Online' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Actions" width="150" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:online-user:view'" link type="primary" @click="openDetail(row)">Detail</el-button>
          <el-button
            v-if="!row.isRevoked"
            v-permission="'system:online-user:kickout'"
            link
            type="danger"
            @click="kickout(row)"
          >
            Kickout
          </el-button>
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

    <el-dialog v-model="detailVisible" title="Session Detail" width="760px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="1" border>
          <el-descriptions-item label="User">{{ detail.userName }}</el-descriptions-item>
          <el-descriptions-item label="Tenant">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="Session">{{ detail.sessionId }}</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="User Agent">{{ detail.userAgent || '-' }}</el-descriptions-item>
          <el-descriptions-item label="Login At">{{ formatDate(detail.loginAt) }}</el-descriptions-item>
          <el-descriptions-item label="Last Active">{{ formatDate(detail.lastActiveAt) }}</el-descriptions-item>
          <el-descriptions-item label="Expires">{{ formatDate(detail.expiresAt) }}</el-descriptions-item>
          <el-descriptions-item label="Revoked">{{ detail.isRevoked ? 'Yes' : 'No' }}</el-descriptions-item>
          <el-descriptions-item label="Reason">{{ detail.revokedReason || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>
