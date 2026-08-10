<script setup lang="ts">
defineOptions({
  name: 'WorkflowDefinition',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  copyWorkflowDefinition,
  createWorkflowDefinition,
  deleteWorkflowDefinition,
  disableWorkflowDefinition,
  getWorkflowDefinitions,
  publishWorkflowDefinition,
  updateWorkflowDefinition,
  WorkflowDefinitionStatus,
  type WorkflowDefinitionItem,
  type WorkflowDefinitionStatus as WorkflowDefinitionStatusValue,
} from '../../../api/workflowDefinition'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const router = useRouter()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const saving = ref(false)
const tableData = ref<WorkflowDefinitionItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')
const editingDefinition = ref<WorkflowDefinitionItem | null>(null)
const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: undefined as WorkflowDefinitionStatusValue | undefined,
  isPublished: undefined as boolean | undefined,
})
const form = reactive({
  code: '',
  name: '',
  description: '',
  businessType: '',
})

const rules: FormRules = {
  code: [{ required: true, message: '请输入流程编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入流程名称', trigger: 'blur' }],
  businessType: [{ required: true, message: '请输入业务类型', trigger: 'blur' }],
}

function buildQueryParams() {
  return {
    ...query,
    keyword: query.keyword || undefined,
    status: query.status,
    isPublished: query.isPublished,
  }
}

async function loadData() {
  loading.value = true
  try {
    const result = await getWorkflowDefinitions(buildQueryParams())
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

function openCreate() {
  editingId.value = ''
  editingDefinition.value = null
  Object.assign(form, {
    code: '',
    name: '',
    description: '',
    businessType: '',
  })
  dialogVisible.value = true
}

function openEdit(row: WorkflowDefinitionItem) {
  editingId.value = row.id
  editingDefinition.value = row
  Object.assign(form, {
    code: row.code,
    name: row.name,
    description: row.description ?? '',
    businessType: row.businessType ?? '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    if (editingId.value) {
      await updateWorkflowDefinition(editingId.value, {
        name: form.name,
        description: form.description,
        businessType: form.businessType,
        concurrencyToken: editingDefinition.value?.concurrencyToken,
      })
    } else {
      await createWorkflowDefinition({
        tenantId: tenantId.value,
        code: form.code,
        name: form.name,
        description: form.description,
        businessType: form.businessType,
      })
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: WorkflowDefinitionItem) {
  await ElMessageBox.confirm(`确定删除流程定义“${row.name}”吗？`, '确认删除')
  await deleteWorkflowDefinition(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function publish(row: WorkflowDefinitionItem) {
  await ElMessageBox.confirm(`确定发布流程定义“${row.name}”吗？`, '确认发布')
  await publishWorkflowDefinition(row.id)
  ElMessage.success('发布成功')
  await loadData()
}

async function disable(row: WorkflowDefinitionItem) {
  await ElMessageBox.confirm(`确定停用流程定义“${row.name}”吗？`, '确认停用')
  await disableWorkflowDefinition(row.id)
  ElMessage.success('停用成功')
  await loadData()
}

async function copyVersion(row: WorkflowDefinitionItem) {
  await copyWorkflowDefinition(row.id)
  ElMessage.success('复制成功')
  await loadData()
}

function openDesigner(row: WorkflowDefinitionItem) {
  void router.push(`/workflow/definition/${row.id}/designer`)
}

function canDelete(row: WorkflowDefinitionItem) {
  return !row.isPublished && row.status !== WorkflowDefinitionStatus.Published
}

function canPublish(row: WorkflowDefinitionItem) {
  return row.status !== WorkflowDefinitionStatus.Published && !row.isPublished
}

function canDisable(row: WorkflowDefinitionItem) {
  return row.status === WorkflowDefinitionStatus.Published || row.isPublished
}

function statusText(status: WorkflowDefinitionStatusValue) {
  const map: Record<WorkflowDefinitionStatusValue, string> = {
    [WorkflowDefinitionStatus.Draft]: '草稿',
    [WorkflowDefinitionStatus.Published]: '已发布',
    [WorkflowDefinitionStatus.Disabled]: '已停用',
    [WorkflowDefinitionStatus.Archived]: '已归档',
  }

  return map[status] ?? '未知'
}

function statusTagType(status: WorkflowDefinitionStatusValue) {
  const map: Record<WorkflowDefinitionStatusValue, 'primary' | 'success' | 'info' | 'warning'> = {
    [WorkflowDefinitionStatus.Draft]: 'info',
    [WorkflowDefinitionStatus.Published]: 'success',
    [WorkflowDefinitionStatus.Disabled]: 'warning',
    [WorkflowDefinitionStatus.Archived]: 'info',
  }

  return map[status] ?? 'info'
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="流程定义" description="维护审批流定义、版本、发布状态，并进入流程设计器。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="流程编码 / 流程名称" @keyup.enter="resetPageAndLoad" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="状态" style="width: 120px">
          <el-option label="草稿" :value="WorkflowDefinitionStatus.Draft" />
          <el-option label="已发布" :value="WorkflowDefinitionStatus.Published" />
          <el-option label="已停用" :value="WorkflowDefinitionStatus.Disabled" />
          <el-option label="已归档" :value="WorkflowDefinitionStatus.Archived" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isPublished" clearable placeholder="发布" style="width: 120px">
          <el-option label="已发布" :value="true" />
          <el-option label="未发布" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'workflow:definition:view'" type="primary" @click="resetPageAndLoad">查询</el-button>
        <el-button v-permission="'workflow:definition:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="流程编码" min-width="160" />
      <el-table-column prop="name" label="流程名称" min-width="180" />
      <el-table-column prop="businessType" label="业务类型" min-width="140" />
      <el-table-column prop="version" label="版本" width="90">
        <template #default="{ row }">v{{ row.version }}</template>
      </el-table-column>
      <el-table-column label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)">{{ statusText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="是否发布" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isPublished ? 'success' : 'info'">{{ row.isPublished ? '是' : '否' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="180">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'workflow:definition:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'workflow:definition:design'" link type="primary" @click="openDesigner(row)">设计</el-button>
          <el-button v-if="canPublish(row)" v-permission="'workflow:definition:publish'" link type="primary" @click="publish(row)">发布</el-button>
          <el-button v-if="canDisable(row)" v-permission="'workflow:definition:disable'" link type="warning" @click="disable(row)">停用</el-button>
          <el-button v-permission="'workflow:definition:create'" link type="primary" @click="copyVersion(row)">复制</el-button>
          <el-button v-if="canDelete(row)" v-permission="'workflow:definition:delete'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑流程定义' : '新增流程定义'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="Code" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" placeholder="例如：expense-approval" />
        </el-form-item>
        <el-form-item label="Name" prop="name">
          <el-input v-model="form.name" placeholder="请输入流程名称" />
        </el-form-item>
        <el-form-item label="BusinessType" prop="businessType">
          <el-input v-model="form.businessType" placeholder="例如：expense" />
        </el-form-item>
        <el-form-item label="Description">
          <el-input v-model="form.description" type="textarea" :rows="3" placeholder="请输入流程说明" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
