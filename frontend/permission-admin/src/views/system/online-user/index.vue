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
  await ElMessageBox.confirm(`确认强制用户 ${row.userName} 下线？`, '确认强制下线')
  await kickoutOnlineUser(row.id, '管理员强制下线。')
  ElMessage.success('用户会话已撤销')
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
        <el-input v-model="query.keyword" clearable placeholder="用户 / 会话 / IP" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isRevoked" clearable placeholder="状态" style="width: 140px">
          <el-option label="在线" :value="false" />
          <el-option label="已撤销" :value="true" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:online-user:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="userName" label="用户" min-width="150" />
      <el-table-column prop="ipAddress" label="IP" min-width="140">
        <template #default="{ row }">{{ row.ipAddress || '-' }}</template>
      </el-table-column>
      <el-table-column prop="sessionId" label="会话" min-width="220" show-overflow-tooltip />
      <el-table-column prop="loginAt" label="登录时间" width="180">
        <template #default="{ row }">{{ formatDate(row.loginAt) }}</template>
      </el-table-column>
      <el-table-column prop="lastActiveAt" label="最后活跃" width="180">
        <template #default="{ row }">{{ formatDate(row.lastActiveAt) }}</template>
      </el-table-column>
      <el-table-column prop="expiresAt" label="过期时间" width="180">
        <template #default="{ row }">{{ formatDate(row.expiresAt) }}</template>
      </el-table-column>
      <el-table-column prop="isRevoked" label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isRevoked ? 'danger' : 'success'">{{ row.isRevoked ? '已撤销' : '在线' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:online-user:view'" link type="primary" @click="openDetail(row)">详情</el-button>
          <el-button
            v-if="!row.isRevoked"
            v-permission="'system:online-user:kickout'"
            link
            type="danger"
            @click="kickout(row)"
          >
            强制下线
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

    <el-dialog v-model="detailVisible" title="会话详情" width="760px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="1" border>
          <el-descriptions-item label="用户">{{ detail.userName }}</el-descriptions-item>
          <el-descriptions-item label="租户">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="会话">{{ detail.sessionId }}</el-descriptions-item>
          <el-descriptions-item label="IP">{{ detail.ipAddress || '-' }}</el-descriptions-item>
          <el-descriptions-item label="用户代理">{{ detail.userAgent || '-' }}</el-descriptions-item>
          <el-descriptions-item label="登录时间">{{ formatDate(detail.loginAt) }}</el-descriptions-item>
          <el-descriptions-item label="最后活跃">{{ formatDate(detail.lastActiveAt) }}</el-descriptions-item>
          <el-descriptions-item label="过期时间">{{ formatDate(detail.expiresAt) }}</el-descriptions-item>
          <el-descriptions-item label="已撤销">{{ detail.isRevoked ? '是' : '否' }}</el-descriptions-item>
          <el-descriptions-item label="原因">{{ detail.revokedReason || '-' }}</el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>
