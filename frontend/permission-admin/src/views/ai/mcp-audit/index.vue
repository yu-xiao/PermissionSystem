<script setup lang="ts">
defineOptions({ name: 'AiMcpAudit' })

import { Refresh, Search } from '@element-plus/icons-vue'
import { reactive, ref } from 'vue'
import { getMcpInvocationLogs, type McpInvocationLog } from '../../../api/mcp'
import PageContainer from '../../../components/PageContainer/index.vue'

const loading = ref(false)
const rows = ref<McpInvocationLog[]>([])
const total = ref(0)
const query = reactive({
  pageIndex: 1,
  pageSize: 20,
  clientBindingId: '',
  datasetCode: '',
  status: undefined as number | undefined,
})
const statuses = [
  { value: 1, label: '成功', type: 'success' },
  { value: 2, label: '拒绝', type: 'warning' },
  { value: 3, label: '失败', type: 'danger' },
] as const

async function loadData() {
  loading.value = true
  try {
    const result = await getMcpInvocationLogs({
      ...query,
      clientBindingId: query.clientBindingId || undefined,
      datasetCode: query.datasetCode || undefined,
    })
    rows.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function statusMeta(status: number) {
  return statuses.find((item) => item.value === status) ?? { label: `状态 ${status}`, type: 'info' as const }
}

function reset() {
  Object.assign(query, { pageIndex: 1, clientBindingId: '', datasetCode: '', status: undefined })
  loadData()
}

loadData()
</script>

<template>
  <PageContainer title="MCP 调用审计">
    <template #actions>
      <el-tooltip content="刷新"><el-button :icon="Refresh" circle @click="loadData" /></el-tooltip>
    </template>
    <el-form class="toolbar" inline @submit.prevent="loadData">
      <el-form-item><el-input v-model="query.datasetCode" clearable placeholder="数据集编码" /></el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="结果" style="width: 120px">
          <el-option v-for="status in statuses" :key="status.value" :label="status.label" :value="status.value" />
        </el-select>
      </el-form-item>
      <el-form-item><el-button type="primary" :icon="Search" @click="loadData">查询</el-button><el-button @click="reset">重置</el-button></el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="rows" border>
      <el-table-column prop="createdAt" label="时间" min-width="180" />
      <el-table-column label="调用方" min-width="170"><template #default="{ row }">{{ row.callerType === 2 ? row.oauthClientId : '委托用户' }}</template></el-table-column>
      <el-table-column prop="toolName" label="工具" min-width="150" />
      <el-table-column prop="datasetCode" label="数据集" min-width="170" />
      <el-table-column label="结果" width="90"><template #default="{ row }"><el-tag :type="statusMeta(row.status).type">{{ statusMeta(row.status).label }}</el-tag></template></el-table-column>
      <el-table-column prop="rowCount" label="行数" width="80" />
      <el-table-column label="截断" width="75"><template #default="{ row }">{{ row.isTruncated ? '是' : '否' }}</template></el-table-column>
      <el-table-column prop="durationMilliseconds" label="耗时(ms)" width="100" />
      <el-table-column prop="ipAddress" label="IP" min-width="130" />
      <el-table-column prop="traceId" label="Trace ID" min-width="230" show-overflow-tooltip />
      <el-table-column prop="errorSummary" label="错误摘要" min-width="220" show-overflow-tooltip />
    </el-table>
    <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" class="pager" background layout="total, sizes, prev, pager, next" :total="total" @change="loadData" />
  </PageContainer>
</template>
