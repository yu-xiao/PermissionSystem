<script setup lang="ts">
defineOptions({ name: 'WorkflowTaskDone' })

import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getDoneTasks, WorkflowTaskStatus, type WorkflowTaskItem } from '../../../api/workflowTask'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const router = useRouter()
const loading = ref(false)
const tableData = ref<WorkflowTaskItem[]>([])
const total = ref(0)
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })

async function loadData() {
  loading.value = true
  try {
    const result = await getDoneTasks({ ...query, keyword: query.keyword || undefined })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function resetPageAndLoad() {
  query.pageIndex = 1
  void loadData()
}

function statusText(status: number) {
  const map: Record<number, string> = {
    [WorkflowTaskStatus.Approved]: '已同意',
    [WorkflowTaskStatus.Rejected]: '已拒绝',
    [WorkflowTaskStatus.Transferred]: '已转交',
    [WorkflowTaskStatus.Added]: '已加签',
    [WorkflowTaskStatus.Canceled]: '已取消',
    [WorkflowTaskStatus.Expired]: '已超时',
  }
  return map[status] ?? '已处理'
}

function statusType(status: number) {
  return status === WorkflowTaskStatus.Approved ? 'success' : status === WorkflowTaskStatus.Rejected ? 'danger' : 'info'
}

function viewDetail(row: WorkflowTaskItem) {
  void router.push(`/workflow/instances/${row.instanceId}`)
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="我已审批" description="查看当前用户已处理的审批任务。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="业务标题 / 单据编号 / 节点" @keyup.enter="resetPageAndLoad" />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'workflow:task:todo'" type="primary" @click="resetPageAndLoad">查询</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="definitionName" label="流程名称" min-width="150" />
      <el-table-column prop="businessTitle" label="业务标题" min-width="180" />
      <el-table-column prop="nodeName" label="审批节点" min-width="140" />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ statusText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="处理时间" width="180">
        <template #default="{ row }">{{ formatTime(row.completedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="90" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="viewDetail(row)">查看</el-button>
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
  </PageContainer>
</template>
