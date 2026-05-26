<script setup lang="ts">
defineOptions({ name: 'WorkflowMyStarted' })

import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  getMyStartedInstances,
  withdrawInstance,
  type WorkflowInstanceItem,
} from '../../../api/workflowInstance'
import { WorkflowInstanceStatus } from '../../../api/workflowTask'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const router = useRouter()
const loading = ref(false)
const submitting = ref(false)
const withdrawVisible = ref(false)
const current = ref<WorkflowInstanceItem>()
const tableData = ref<WorkflowInstanceItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: undefined as WorkflowInstanceStatus | undefined,
})
const form = reactive({ comment: '' })

const rules: FormRules = {
  comment: [{ required: true, message: '请输入撤回原因', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getMyStartedInstances({
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

function viewDetail(row: WorkflowInstanceItem) {
  void router.push(`/workflow/instances/${row.id}`)
}

function openWithdraw(row: WorkflowInstanceItem) {
  current.value = row
  form.comment = ''
  withdrawVisible.value = true
}

async function submitWithdraw() {
  await formRef.value?.validate()
  if (!current.value) {
    return
  }

  submitting.value = true
  try {
    await withdrawInstance(current.value.id, form.comment)
    ElMessage.success('撤回成功')
    withdrawVisible.value = false
    await loadData()
  } finally {
    submitting.value = false
  }
}

function canWithdraw(row: WorkflowInstanceItem) {
  return row.status === WorkflowInstanceStatus.Running
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
  <PageContainer title="我发起的" description="查看自己发起的审批流程，并在流程未完成时撤回。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="流程名称 / 业务标题 / 单据编号" @keyup.enter="resetPageAndLoad" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="状态" style="width: 140px">
          <el-option label="审批中" :value="WorkflowInstanceStatus.Running" />
          <el-option label="已完成" :value="WorkflowInstanceStatus.Approved" />
          <el-option label="已拒绝" :value="WorkflowInstanceStatus.Rejected" />
          <el-option label="已撤回" :value="WorkflowInstanceStatus.Withdrawn" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'workflow:instance:view'" type="primary" @click="resetPageAndLoad">查询</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="definitionName" label="流程名称" min-width="150" />
      <el-table-column prop="businessTitle" label="业务标题" min-width="180" show-overflow-tooltip />
      <el-table-column prop="businessType" label="业务类型" min-width="130" />
      <el-table-column label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="statusType(row.status)">{{ statusText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="发起时间" width="180">
        <template #default="{ row }">{{ formatTime(row.startedAt) }}</template>
      </el-table-column>
      <el-table-column label="完成时间" width="180">
        <template #default="{ row }">{{ formatTime(row.completedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="140" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'workflow:instance:view'" link type="primary" @click="viewDetail(row)">查看</el-button>
          <el-button
            v-if="canWithdraw(row)"
            v-permission="'workflow:instance:withdraw'"
            link
            type="warning"
            @click="openWithdraw(row)"
          >
            撤回
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

    <el-dialog v-model="withdrawVisible" title="撤回流程" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="90px">
        <el-form-item label="撤回原因" prop="comment">
          <el-input v-model="form.comment" type="textarea" :rows="4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="withdrawVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitWithdraw">确定</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
