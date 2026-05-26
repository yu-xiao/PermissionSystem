<script setup lang="ts">
defineOptions({ name: 'WorkflowTaskTodo' })

import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getUsers, type UserItem } from '../../../api/users'
import {
  addSignTask,
  approveTask,
  getTodoTasks,
  rejectTask,
  transferTask,
  type WorkflowTaskItem,
} from '../../../api/workflowTask'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

type ActionType = 'approve' | 'reject' | 'transfer' | 'addSign'

const router = useRouter()
const loading = ref(false)
const submitting = ref(false)
const dialogVisible = ref(false)
const actionType = ref<ActionType>('approve')
const currentTask = ref<WorkflowTaskItem>()
const tableData = ref<WorkflowTaskItem[]>([])
const users = ref<UserItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })
const actionForm = reactive({ comment: '', targetUserId: '' })

const rules: FormRules = {
  comment: [{ required: true, message: '请输入处理意见', trigger: 'blur' }],
  targetUserId: [{ required: true, message: '请选择用户', trigger: 'change' }],
}

const actionTitles: Record<ActionType, string> = {
  approve: '同意审批',
  reject: '拒绝审批',
  transfer: '转交审批',
  addSign: '加签审批',
}

async function loadData() {
  loading.value = true
  try {
    const result = await getTodoTasks({ ...query, keyword: query.keyword || undefined })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  const result = await getUsers({ pageIndex: 1, pageSize: 200, keyword: '', isEnabled: true })
  users.value = result.items
}

function resetPageAndLoad() {
  query.pageIndex = 1
  void loadData()
}

function openAction(row: WorkflowTaskItem, type: ActionType) {
  currentTask.value = row
  actionType.value = type
  Object.assign(actionForm, { comment: '', targetUserId: '' })
  dialogVisible.value = true
}

async function submitAction() {
  await formRef.value?.validate()
  if (!currentTask.value) {
    return
  }

  submitting.value = true
  try {
    if (actionType.value === 'approve') {
      await approveTask(currentTask.value.id, { comment: actionForm.comment })
    } else if (actionType.value === 'reject') {
      await rejectTask(currentTask.value.id, { comment: actionForm.comment })
    } else if (actionType.value === 'transfer') {
      await transferTask(currentTask.value.id, {
        targetUserId: actionForm.targetUserId,
        comment: actionForm.comment,
      })
    } else {
      await addSignTask(currentTask.value.id, {
        targetUserId: actionForm.targetUserId,
        comment: actionForm.comment,
      })
    }

    ElMessage.success('处理成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    submitting.value = false
  }
}

function viewDetail(row: WorkflowTaskItem) {
  void router.push(`/workflow/instances/${row.instanceId}`)
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadUsers()
loadData()
</script>

<template>
  <PageContainer title="待我审批" description="处理分配给当前用户的审批任务。">
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
      <el-table-column prop="nodeName" label="当前节点" min-width="140" />
      <el-table-column prop="starterUserName" label="发起人" width="120" />
      <el-table-column label="发起时间" width="180">
        <template #default="{ row }">{{ formatTime(row.startedAt) }}</template>
      </el-table-column>
      <el-table-column label="到达时间" width="180">
        <template #default="{ row }">{{ formatTime(row.assignedAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button link type="primary" @click="viewDetail(row)">查看</el-button>
          <el-button v-permission="'workflow:task:approve'" link type="success" @click="openAction(row, 'approve')">同意</el-button>
          <el-button v-permission="'workflow:task:reject'" link type="danger" @click="openAction(row, 'reject')">拒绝</el-button>
          <el-button v-permission="'workflow:task:transfer'" link type="primary" @click="openAction(row, 'transfer')">转交</el-button>
          <el-button v-permission="'workflow:task:add-sign'" link type="primary" @click="openAction(row, 'addSign')">加签</el-button>
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

    <el-dialog v-model="dialogVisible" :title="actionTitles[actionType]" width="520px">
      <el-form ref="formRef" :model="actionForm" :rules="rules" label-width="100px">
        <el-form-item v-if="actionType === 'transfer' || actionType === 'addSign'" label="选择用户" prop="targetUserId">
          <el-select v-model="actionForm.targetUserId" filterable class="full-width">
            <el-option
              v-for="user in users"
              :key="user.id"
              :label="`${user.displayName}（${user.userName}）`"
              :value="user.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item :label="actionType === 'reject' ? '拒绝原因' : '处理意见'" prop="comment">
          <el-input v-model="actionForm.comment" type="textarea" :rows="4" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitAction">确定</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}
</style>
