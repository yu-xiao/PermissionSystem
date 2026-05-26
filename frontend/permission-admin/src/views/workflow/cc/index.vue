<script setup lang="ts">
defineOptions({ name: 'WorkflowCc' })

import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  getMyCc,
  markWorkflowCcRead,
  type WorkflowCcItem,
} from '../../../api/workflowInstance'
import { WorkflowInstanceStatus } from '../../../api/workflowTask'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const router = useRouter()
const loading = ref(false)
const tableData = ref<WorkflowCcItem[]>([])
const total = ref(0)
const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  isRead: undefined as boolean | undefined,
})

async function loadData() {
  loading.value = true
  try {
    const result = await getMyCc({
      ...query,
      keyword: query.keyword || undefined,
    })
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

function viewDetail(row: WorkflowCcItem) {
  void router.push(`/workflow/instances/${row.instanceId}`)
}

async function markRead(row: WorkflowCcItem) {
  await markWorkflowCcRead(row.id)
  ElMessage.success('已标记为已读')
  await loadData()
}

function statusText(status: WorkflowInstanceStatus) {
  const map: Record<WorkflowInstanceStatus, string> = {
    [WorkflowInstanceStatus.Running]: '审批中',
    [WorkflowInstanceStatus.Approved]: '已完成',
    [WorkflowInstanceStatus.Rejected]: '已拒绝',
    [WorkflowInstanceStatus.Withdrawn]: '已撤回',
    [WorkflowInstanceStatus.Canceled]: '已取消',
    [WorkflowInstanceStatus.Exception]: '异常',
  }

  return map[status] ?? '未知'
}

function statusType(status: WorkflowInstanceStatus) {
  if (status === WorkflowInstanceStatus.Running) {
    return 'primary'
  }

  if (status === WorkflowInstanceStatus.Approved) {
    return 'success'
  }

  if (status === WorkflowInstanceStatus.Rejected || status === WorkflowInstanceStatus.Exception) {
    return 'danger'
  }

  return 'info'
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="抄送我的" description="查看抄送给当前用户的审批流程。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="流程名称 / 业务标题 / 发起人" @keyup.enter="resetPageAndLoad" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isRead" clearable placeholder="阅读状态" style="width: 140px">
          <el-option label="未读" :value="false" />
          <el-option label="已读" :value="true" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'workflow:cc:view'" type="primary" @click="resetPageAndLoad">查询</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="definitionName" label="流程名称" min-width="150" />
      <el-table-column prop="businessTitle" label="业务标题" min-width="180" show-overflow-tooltip />
      <el-table-column prop="starterUserName" label="发起人" width="120" />
      <el-table-column label="流程状态" width="110">
        <template #default="{ row }">
          <el-tag :type="statusType(row.instanceStatus)">{{ statusText(row.instanceStatus) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="阅读状态" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isRead ? 'info' : 'danger'">{{ row.isRead ? '已读' : '未读' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="抄送时间" width="180">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="160" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'workflow:cc:view'" link type="primary" @click="viewDetail(row)">查看</el-button>
          <el-button v-if="!row.isRead" v-permission="'workflow:cc:view'" link type="success" @click="markRead(row)">标记已读</el-button>
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
