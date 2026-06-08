<script setup lang="ts">
defineOptions({
  name: 'SecurityLoginFailure',
})

import { reactive, ref } from 'vue'
import { getLoginFailures, type LoginFailureRecordItem } from '../../../api/security'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const tableData = ref<LoginFailureRecordItem[]>([])
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
})

async function loadData() {
  loading.value = true
  try {
    const result = await getLoginFailures(query)
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
  })
  loadData()
}

function lockStatus(row: LoginFailureRecordItem) {
  if (!row.lockedUntil) {
    return '未锁定'
  }

  return new Date(row.lockedUntil).getTime() > Date.now() ? '锁定中' : '已过期'
}

loadData()
</script>

<template>
  <PageContainer title="登录失败记录" description="查看账号和 IP 的登录失败计数与锁定状态。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="用户名 / IP" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="userName" label="用户名" min-width="160" />
      <el-table-column prop="ipAddress" label="IP" min-width="150" />
      <el-table-column prop="failureCount" label="失败次数" width="110" />
      <el-table-column label="锁定状态" width="120">
        <template #default="{ row }">
          <el-tag :type="lockStatus(row) === '锁定中' ? 'danger' : 'info'">{{ lockStatus(row) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="lockedUntil" label="锁定至" min-width="180" />
      <el-table-column prop="lastFailureAt" label="最后失败时间" min-width="180" />
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
  </PageContainer>
</template>
