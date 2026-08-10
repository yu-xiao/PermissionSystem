<script setup lang="ts">
defineOptions({ name: 'WorkflowBusinessBinding' })

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createWorkflowBusinessBinding,
  deleteWorkflowBusinessBinding,
  disableWorkflowBusinessBinding,
  enableWorkflowBusinessBinding,
  getWorkflowBusinessBindings,
  updateWorkflowBusinessBinding,
  type WorkflowBusinessBindingItem,
} from '../../../api/workflowBusinessBinding'
import {
  getWorkflowDefinitions,
  WorkflowDefinitionStatus,
  type WorkflowDefinitionItem,
} from '../../../api/workflowDefinition'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref('')
const editingBinding = ref<WorkflowBusinessBindingItem | null>(null)
const tableData = ref<WorkflowBusinessBindingItem[]>([])
const definitions = ref<WorkflowDefinitionItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  isEnabled: undefined as boolean | undefined,
})
const form = reactive({
  businessType: '',
  businessName: '',
  definitionId: '',
  isEnabled: true,
  remark: '',
})

const rules: FormRules = {
  businessType: [{ required: true, message: '请输入业务类型', trigger: 'blur' }],
  businessName: [{ required: true, message: '请输入业务名称', trigger: 'blur' }],
  definitionId: [{ required: true, message: '请选择流程定义', trigger: 'change' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getWorkflowBusinessBindings({
      ...query,
      keyword: query.keyword || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadDefinitions() {
  const result = await getWorkflowDefinitions({
    pageIndex: 1,
    pageSize: 200,
    keyword: '',
    status: WorkflowDefinitionStatus.Published,
    isPublished: true,
  })
  definitions.value = result.items
}

function resetPageAndLoad() {
  query.pageIndex = 1
  void loadData()
}

function openCreate() {
  editingId.value = ''
  editingBinding.value = null
  Object.assign(form, {
    businessType: '',
    businessName: '',
    definitionId: '',
    isEnabled: true,
    remark: '',
  })
  dialogVisible.value = true
}

function openEdit(row: WorkflowBusinessBindingItem) {
  editingId.value = row.id
  editingBinding.value = row
  Object.assign(form, {
    businessType: row.businessType,
    businessName: row.businessName,
    definitionId: row.definitionId,
    isEnabled: row.isEnabled,
    remark: row.remark ?? '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    if (editingId.value) {
      await updateWorkflowBusinessBinding(editingId.value, {
        businessType: form.businessType,
        businessName: form.businessName,
        definitionId: form.definitionId,
        remark: form.remark,
        concurrencyToken: editingBinding.value?.concurrencyToken,
      })
    } else {
      await createWorkflowBusinessBinding({
        tenantId: tenantId.value,
        businessType: form.businessType,
        businessName: form.businessName,
        definitionId: form.definitionId,
        isEnabled: form.isEnabled,
        remark: form.remark,
      })
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: WorkflowBusinessBindingItem) {
  await ElMessageBox.confirm(`确定删除业务流程绑定“${row.businessName}”吗？`, '确认删除')
  await deleteWorkflowBusinessBinding(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function enable(row: WorkflowBusinessBindingItem) {
  await enableWorkflowBusinessBinding(row.id)
  ElMessage.success('启用成功')
  await loadData()
}

async function disable(row: WorkflowBusinessBindingItem) {
  await disableWorkflowBusinessBinding(row.id)
  ElMessage.success('禁用成功')
  await loadData()
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadDefinitions()
loadData()
</script>

<template>
  <PageContainer title="业务流程绑定" description="维护业务类型和已发布审批流程之间的接入关系。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="业务类型 / 业务名称 / 流程名称" @keyup.enter="resetPageAndLoad" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="启用状态" style="width: 140px">
          <el-option label="已启用" :value="true" />
          <el-option label="已禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'workflow:business-binding:view'" type="primary" @click="resetPageAndLoad">查询</el-button>
        <el-button v-permission="'workflow:business-binding:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="businessType" label="BusinessType" min-width="170" />
      <el-table-column prop="businessName" label="业务名称" min-width="150" />
      <el-table-column label="流程定义" min-width="220">
        <template #default="{ row }">
          {{ row.definitionName }} v{{ row.definitionVersion }}
        </template>
      </el-table-column>
      <el-table-column prop="definitionCode" label="流程编码" min-width="150" />
      <el-table-column label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '已启用' : '已禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="创建时间" width="180">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="210" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'workflow:business-binding:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="!row.isEnabled" v-permission="'workflow:business-binding:enable'" link type="success" @click="enable(row)">启用</el-button>
          <el-button v-if="row.isEnabled" v-permission="'workflow:business-binding:disable'" link type="warning" @click="disable(row)">禁用</el-button>
          <el-button v-permission="'workflow:business-binding:delete'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑业务流程绑定' : '新增业务流程绑定'" width="620px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="BusinessType" prop="businessType">
          <el-input v-model="form.businessType" placeholder="例如：DemoApprovalOrder" />
        </el-form-item>
        <el-form-item label="业务名称" prop="businessName">
          <el-input v-model="form.businessName" placeholder="例如：Demo 审批单" />
        </el-form-item>
        <el-form-item label="流程定义" prop="definitionId">
          <el-select v-model="form.definitionId" filterable class="full-width">
            <el-option
              v-for="definition in definitions"
              :key="definition.id"
              :label="`${definition.name}（${definition.code} v${definition.version}）`"
              :value="definition.id"
            />
          </el-select>
        </el-form-item>
        <el-form-item v-if="!editingId" label="立即启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="form.remark" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}
</style>
