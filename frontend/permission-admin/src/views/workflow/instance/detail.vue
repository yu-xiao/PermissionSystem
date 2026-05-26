<script setup lang="ts">
defineOptions({ name: 'WorkflowInstanceDetail' })

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getUsers, type UserItem } from '../../../api/users'
import {
  getInstanceDetail,
  getInstanceRecords,
  withdrawInstance,
  WorkflowActionType,
  type WorkflowInstanceDetail,
  type WorkflowRecordItem,
} from '../../../api/workflowInstance'
import {
  addSignTask,
  approveTask,
  rejectTask,
  transferTask,
  WorkflowInstanceStatus,
  WorkflowTaskStatus,
  type WorkflowTaskItem,
} from '../../../api/workflowTask'
import PageContainer from '../../../components/PageContainer/index.vue'
import { useAuthStore } from '../../../stores/auth'

type ActionType = 'approve' | 'reject' | 'transfer' | 'addSign'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const loading = ref(false)
const submitting = ref(false)
const actionVisible = ref(false)
const actionType = ref<ActionType>('approve')
const currentTask = ref<WorkflowTaskItem>()
const detail = ref<WorkflowInstanceDetail>()
const records = ref<WorkflowRecordItem[]>([])
const users = ref<UserItem[]>([])
const formRef = ref<FormInstance>()
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

const instanceId = computed(() => String(route.params.id ?? ''))
const currentUserId = computed(() => authStore.currentUser?.userId?.toLowerCase())
const currentTodoTask = computed(() =>
  detail.value?.tasks.find(
    (task) =>
      task.status === WorkflowTaskStatus.Pending &&
      task.approverUserId.toLowerCase() === currentUserId.value,
  ),
)
const canWithdraw = computed(
  () =>
    detail.value?.status === WorkflowInstanceStatus.Running &&
    detail.value.starterUserId.toLowerCase() === currentUserId.value,
)
const prettyFormData = computed(() => {
  const value = detail.value?.formDataJson
  if (!value) {
    return '-'
  }

  try {
    return JSON.stringify(JSON.parse(value), null, 2)
  } catch {
    return value
  }
})

async function loadData() {
  if (!instanceId.value) {
    return
  }

  loading.value = true
  try {
    const [detailResult, recordResult] = await Promise.all([
      getInstanceDetail(instanceId.value),
      getInstanceRecords(instanceId.value),
    ])
    detail.value = detailResult
    records.value = recordResult
  } finally {
    loading.value = false
  }
}

async function loadUsers() {
  const result = await getUsers({ pageIndex: 1, pageSize: 200, keyword: '', isEnabled: true })
  users.value = result.items
}

function openAction(type: ActionType, task?: WorkflowTaskItem) {
  const targetTask = task ?? currentTodoTask.value
  if (!targetTask) {
    return
  }

  currentTask.value = targetTask
  actionType.value = type
  Object.assign(actionForm, { comment: '', targetUserId: '' })
  actionVisible.value = true
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
    actionVisible.value = false
    await loadData()
  } finally {
    submitting.value = false
  }
}

async function withdraw() {
  await ElMessageBox.prompt('请输入撤回原因', '撤回流程', {
    confirmButtonText: '确定',
    cancelButtonText: '取消',
    inputType: 'textarea',
    inputPattern: /\S+/,
    inputErrorMessage: '请输入撤回原因',
  }).then(async ({ value }) => {
    await withdrawInstance(instanceId.value, value)
    ElMessage.success('撤回成功')
    await loadData()
  })
}

function goBack() {
  router.back()
}

function instanceStatusText(status?: WorkflowInstanceStatus) {
  if (status === undefined) {
    return '-'
  }

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

function instanceStatusType(status?: WorkflowInstanceStatus) {
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

function taskStatusText(status: WorkflowTaskStatus) {
  const map: Record<WorkflowTaskStatus, string> = {
    [WorkflowTaskStatus.Pending]: '待处理',
    [WorkflowTaskStatus.Approved]: '已同意',
    [WorkflowTaskStatus.Rejected]: '已拒绝',
    [WorkflowTaskStatus.Transferred]: '已转交',
    [WorkflowTaskStatus.Added]: '已加签',
    [WorkflowTaskStatus.Canceled]: '已取消',
    [WorkflowTaskStatus.Expired]: '已超时',
  }

  return map[status] ?? '未知'
}

function taskStatusType(status: WorkflowTaskStatus) {
  if (status === WorkflowTaskStatus.Pending) {
    return 'warning'
  }

  if (status === WorkflowTaskStatus.Approved) {
    return 'success'
  }

  if (status === WorkflowTaskStatus.Rejected) {
    return 'danger'
  }

  return 'info'
}

function actionText(action: WorkflowActionType) {
  const map: Record<WorkflowActionType, string> = {
    [WorkflowActionType.Start]: '发起',
    [WorkflowActionType.Approve]: '同意',
    [WorkflowActionType.Reject]: '拒绝',
    [WorkflowActionType.Withdraw]: '撤回',
    [WorkflowActionType.Transfer]: '转交',
    [WorkflowActionType.AddSign]: '加签',
    [WorkflowActionType.Cc]: '抄送',
    [WorkflowActionType.Complete]: '完成',
    [WorkflowActionType.System]: '系统',
  }

  return map[action] ?? '未知'
}

function timelineType(action: WorkflowActionType) {
  if (action === WorkflowActionType.Approve || action === WorkflowActionType.Complete) {
    return 'success'
  }

  if (action === WorkflowActionType.Reject) {
    return 'danger'
  }

  if (action === WorkflowActionType.Withdraw || action === WorkflowActionType.Transfer) {
    return 'warning'
  }

  return 'primary'
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadUsers()
loadData()
</script>

<template>
  <PageContainer title="审批详情" description="查看流程基本信息、表单数据和审批记录。">
    <template #actions>
      <el-button @click="goBack">返回</el-button>
      <el-button
        v-if="currentTodoTask"
        v-permission="'workflow:task:approve'"
        type="success"
        @click="openAction('approve')"
      >
        同意
      </el-button>
      <el-button
        v-if="currentTodoTask"
        v-permission="'workflow:task:reject'"
        type="danger"
        @click="openAction('reject')"
      >
        拒绝
      </el-button>
      <el-button
        v-if="currentTodoTask"
        v-permission="'workflow:task:transfer'"
        @click="openAction('transfer')"
      >
        转交
      </el-button>
      <el-button
        v-if="currentTodoTask"
        v-permission="'workflow:task:add-sign'"
        @click="openAction('addSign')"
      >
        加签
      </el-button>
      <el-button v-if="canWithdraw" v-permission="'workflow:instance:withdraw'" type="warning" @click="withdraw">
        撤回
      </el-button>
    </template>

    <div v-loading="loading" class="detail-layout">
      <el-card shadow="never">
        <template #header>
          <span>基本信息</span>
        </template>
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="流程名称">{{ detail.definitionName }}</el-descriptions-item>
          <el-descriptions-item label="业务标题">{{ detail.businessTitle }}</el-descriptions-item>
          <el-descriptions-item label="业务类型">{{ detail.businessType }}</el-descriptions-item>
          <el-descriptions-item label="业务编号">{{ detail.businessId }}</el-descriptions-item>
          <el-descriptions-item label="发起人">{{ detail.starterUserName }}</el-descriptions-item>
          <el-descriptions-item label="当前节点">{{ detail.currentNodeKey || '-' }}</el-descriptions-item>
          <el-descriptions-item label="流程状态">
            <el-tag :type="instanceStatusType(detail.status)">{{ instanceStatusText(detail.status) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="发起时间">{{ formatTime(detail.startedAt) }}</el-descriptions-item>
          <el-descriptions-item label="完成时间">{{ formatTime(detail.completedAt) }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <el-card shadow="never">
        <template #header>
          <span>表单数据</span>
        </template>
        <pre class="json-preview">{{ prettyFormData }}</pre>
      </el-card>

      <el-card shadow="never">
        <template #header>
          <span>审批进度</span>
        </template>
        <el-table :data="detail?.tasks ?? []" border>
          <el-table-column prop="nodeName" label="节点" min-width="150" />
          <el-table-column prop="approverUserName" label="审批人" width="150" />
          <el-table-column label="状态" width="110">
            <template #default="{ row }">
              <el-tag :type="taskStatusType(row.status)">{{ taskStatusText(row.status) }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="到达时间" width="180">
            <template #default="{ row }">{{ formatTime(row.assignedAt) }}</template>
          </el-table-column>
          <el-table-column label="完成时间" width="180">
            <template #default="{ row }">{{ formatTime(row.completedAt) }}</template>
          </el-table-column>
        </el-table>
      </el-card>

      <el-card shadow="never">
        <template #header>
          <span>审批记录</span>
        </template>
        <el-empty v-if="!records.length" description="暂无审批记录" />
        <el-timeline v-else>
          <el-timeline-item
            v-for="record in records"
            :key="record.id"
            :timestamp="formatTime(record.operatedAt)"
            :type="timelineType(record.action)"
          >
            <div class="record-title">
              <span>{{ actionText(record.action) }}</span>
              <span>{{ record.operatorUserName || '系统' }}</span>
              <span v-if="record.nodeName">({{ record.nodeName }})</span>
            </div>
            <div v-if="record.comment" class="record-comment">{{ record.comment }}</div>
          </el-timeline-item>
        </el-timeline>
      </el-card>
    </div>

    <el-dialog v-model="actionVisible" :title="actionTitles[actionType]" width="520px">
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
        <el-button @click="actionVisible = false">取消</el-button>
        <el-button type="primary" :loading="submitting" @click="submitAction">确定</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.detail-layout {
  display: grid;
  gap: 16px;
}

.json-preview {
  max-height: 320px;
  padding: 12px;
  margin: 0;
  overflow: auto;
  line-height: 1.6;
  white-space: pre-wrap;
  word-break: break-word;
  background: var(--el-fill-color-light);
  border: 1px solid var(--el-border-color-lighter);
  border-radius: 6px;
}

.record-title {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  font-weight: 600;
}

.record-comment {
  margin-top: 6px;
  color: var(--el-text-color-regular);
}

.full-width {
  width: 100%;
}
</style>
