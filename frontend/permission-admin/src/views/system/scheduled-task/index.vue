<script setup lang="ts">
defineOptions({
  name: 'SystemScheduledTask',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createScheduledTask,
  deleteScheduledTask,
  disableScheduledTask,
  enableScheduledTask,
  getScheduledTaskLogs,
  getScheduledTasks,
  syncScheduledTasks,
  triggerScheduledTask,
  updateScheduledTask,
  type ScheduledTaskItem,
  type ScheduledTaskLogItem,
} from '../../../api/scheduled-tasks'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.effectiveTenantId)
const loading = ref(false)
const logLoading = ref(false)
const tableData = ref<ScheduledTaskItem[]>([])
const logData = ref<ScheduledTaskLogItem[]>([])
const total = ref(0)
const logTotal = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const logDialogVisible = ref(false)
const editingId = ref('')
const editingTask = ref<ScheduledTaskItem | null>(null)
const currentLogTask = ref<ScheduledTaskItem>()
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '', jobType: '', isEnabled: undefined as boolean | undefined })
const logQuery = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })
const form = reactive({
  code: '',
  name: '',
  jobType: 'DemoLog',
  cronExpression: '* * * * *',
  queue: 'default',
  description: '',
  parametersJson: '{"source":"frontend-demo"}',
  isEnabled: true,
})

const rules: FormRules = {
  code: [{ required: true, message: '请输入任务编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入任务名称', trigger: 'blur' }],
  jobType: [{ required: true, message: '请选择任务类型', trigger: 'change' }],
  cronExpression: [{ required: true, message: '请输入 Cron 表达式', trigger: 'blur' }],
  queue: [{ required: true, message: '请输入队列', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const params = {
      ...query,
      jobType: query.jobType || undefined,
      isEnabled: query.isEnabled,
    }
    const result = await getScheduledTasks(params)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  editingTask.value = null
  Object.assign(form, {
    code: `demo-${Date.now()}`,
    name: 'Demo scheduled task',
    jobType: 'DemoLog',
    cronExpression: '* * * * *',
    queue: 'default',
    description: 'Frontend configured demo task.',
    parametersJson: '{"source":"frontend-demo"}',
    isEnabled: true,
  })
  dialogVisible.value = true
}

function openEdit(row: ScheduledTaskItem) {
  editingId.value = row.id
  editingTask.value = row
  Object.assign(form, row)
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (editingId.value) {
    await updateScheduledTask(editingId.value, {
      ...form,
      concurrencyToken: editingTask.value?.concurrencyToken,
    })
  } else {
    await createScheduledTask({ tenantId: tenantId.value, ...form })
  }
  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: ScheduledTaskItem) {
  await ElMessageBox.confirm(`确定删除任务「${row.code}」吗？`, '确认删除')
  await deleteScheduledTask(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function toggleEnabled(row: ScheduledTaskItem) {
  if (row.isEnabled) {
    await disableScheduledTask(row.id)
    ElMessage.success('已停用')
  } else {
    await enableScheduledTask(row.id)
    ElMessage.success('已启用')
  }
  await loadData()
}

async function trigger(row: ScheduledTaskItem) {
  await triggerScheduledTask(row.id)
  ElMessage.success('已触发，稍后刷新日志查看结果')
}

async function syncJobs() {
  await syncScheduledTasks()
  ElMessage.success('同步成功')
}

async function openLogs(row: ScheduledTaskItem) {
  currentLogTask.value = row
  logQuery.pageIndex = 1
  logDialogVisible.value = true
  await loadLogs()
}

async function loadLogs() {
  if (!currentLogTask.value) {
    return
  }

  logLoading.value = true
  try {
    const result = await getScheduledTaskLogs(currentLogTask.value.id, logQuery)
    logData.value = result.items
    logTotal.value = result.totalCount
  } finally {
    logLoading.value = false
  }
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function isReservedTask(row: ScheduledTaskItem) {
  return row.jobType.trim() !== 'DemoLog'
}

loadData()
</script>

<template>
  <PageContainer title="定时任务" description="仅维护受控 DemoLog 演示任务；不支持生产业务作业或自定义处理器。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="任务编码 / 名称 / 类型" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 120px">
          <el-option label="启用" :value="true" />
          <el-option label="停用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button v-permission="'system:scheduled-task:create'" @click="openCreate">新增</el-button>
        <el-button v-permission="'system:scheduled-task:update'" @click="syncJobs">同步</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="任务编码" min-width="170" />
      <el-table-column prop="name" label="任务名称" min-width="170" />
      <el-table-column label="任务类型" width="150">
        <template #default="{ row }">
          <el-tag :type="isReservedTask(row) ? 'info' : 'warning'">
            {{ row.jobType }} {{ isReservedTask(row) ? '(Reserved)' : '(Demo)' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="cronExpression" label="Cron 表达式" width="130" />
      <el-table-column prop="queue" label="队列" width="100" />
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '停用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="最近执行" min-width="180">
        <template #default="{ row }">
          {{ formatTime(row.lastRunAt) }}
        </template>
      </el-table-column>
      <el-table-column label="结果" min-width="170">
        <template #default="{ row }">
          <el-tag v-if="row.lastRunSucceeded === true" type="success">成功</el-tag>
          <el-tag v-else-if="row.lastRunSucceeded === false" type="danger">失败</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="260" fixed="right">
        <template #default="{ row }">
          <el-button v-if="!isReservedTask(row)" v-permission="'system:scheduled-task:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="!isReservedTask(row) || row.isEnabled" v-permission="'system:scheduled-task:update'" link type="primary" @click="toggleEnabled(row)">{{ row.isEnabled ? '停用' : '启用' }}</el-button>
          <el-button v-if="!isReservedTask(row)" v-permission="'system:scheduled-task:trigger'" link type="primary" @click="trigger(row)">执行</el-button>
          <el-button link type="primary" @click="openLogs(row)">日志</el-button>
          <el-button v-permission="'system:scheduled-task:delete'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑定时任务' : '新增定时任务'" width="640px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="任务编码" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="任务名称" prop="name">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="任务类型" prop="jobType">
          <el-input v-model="form.jobType" disabled />
        </el-form-item>
        <el-form-item label="Cron 表达式" prop="cronExpression">
          <el-input v-model="form.cronExpression" placeholder="* * * * *" />
        </el-form-item>
        <el-form-item label="队列" prop="queue">
          <el-input v-model="form.queue" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="参数 JSON">
          <el-input v-model="form.parametersJson" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="2" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="logDialogVisible" :title="`执行日志 - ${currentLogTask?.code ?? ''}`" width="820px">
      <el-table v-loading="logLoading" :data="logData" border>
        <el-table-column label="开始时间" width="180">
          <template #default="{ row }">{{ formatTime(row.startedAt) }}</template>
        </el-table-column>
        <el-table-column label="结束时间" width="180">
          <template #default="{ row }">{{ formatTime(row.finishedAt) }}</template>
        </el-table-column>
        <el-table-column label="结果" width="90">
          <template #default="{ row }">
            <el-tag :type="row.succeeded ? 'success' : 'danger'">{{ row.succeeded ? '成功' : '失败' }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="message" label="消息" min-width="260" />
      </el-table>
      <el-pagination
        v-model:current-page="logQuery.pageIndex"
        v-model:page-size="logQuery.pageSize"
        class="pager"
        background
        layout="total, prev, pager, next"
        :total="logTotal"
        @change="loadLogs"
      />
    </el-dialog>
  </PageContainer>
</template>
