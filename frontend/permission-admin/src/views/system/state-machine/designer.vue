<script setup lang="ts">
defineOptions({
  name: 'SystemStateMachineDesigner',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  createState,
  createTransition,
  deleteState,
  deleteTransition,
  getStates,
  getTransitions,
  updateState,
  updateTransition,
  type StateDefinitionItem,
  type StateTransitionItem,
} from '../../../api/stateMachine'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const route = useRoute()
const router = useRouter()
const machineId = computed(() => String(route.params.id ?? ''))
const machineName = computed(() => String(route.query.name ?? '状态机设计'))
const businessType = computed(() => String(route.query.businessType ?? ''))

const loading = ref(false)
const stateDialogVisible = ref(false)
const transitionDialogVisible = ref(false)
const editingState = ref<StateDefinitionItem>()
const editingTransition = ref<StateTransitionItem>()
const stateFormRef = ref<FormInstance>()
const transitionFormRef = ref<FormInstance>()
const states = ref<StateDefinitionItem[]>([])
const transitions = ref<StateTransitionItem[]>([])

const stateForm = reactive({
  stateCode: '',
  stateName: '',
  stateType: 'Normal',
  color: '#409EFF',
  sort: 0,
  isInitial: false,
  isFinal: false,
})

const transitionForm = reactive({
  fromState: '',
  toState: '',
  actionCode: '',
  actionName: '',
  requiredPermission: '',
  conditionJson: '',
  isEnabled: true,
  sort: 0,
})

const stateRules: FormRules = {
  stateCode: [{ required: true, message: '请输入状态编码', trigger: 'blur' }],
  stateName: [{ required: true, message: '请输入状态名称', trigger: 'blur' }],
  stateType: [{ required: true, message: '请输入状态类型', trigger: 'blur' }],
}

const transitionRules: FormRules = {
  fromState: [{ required: true, message: '请选择来源状态', trigger: 'change' }],
  toState: [{ required: true, message: '请选择目标状态', trigger: 'change' }],
  actionCode: [{ required: true, message: '请输入操作编码', trigger: 'blur' }],
  actionName: [{ required: true, message: '请输入操作名称', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const [stateResult, transitionResult] = await Promise.all([
      getStates(machineId.value),
      getTransitions(machineId.value),
    ])
    states.value = stateResult
    transitions.value = transitionResult
  } finally {
    loading.value = false
  }
}

function openCreateState() {
  editingState.value = undefined
  Object.assign(stateForm, {
    stateCode: '',
    stateName: '',
    stateType: 'Normal',
    color: '#409EFF',
    sort: states.value.length + 1,
    isInitial: false,
    isFinal: false,
  })
  stateDialogVisible.value = true
}

function openEditState(row: StateDefinitionItem) {
  editingState.value = row
  Object.assign(stateForm, {
    stateCode: row.stateCode,
    stateName: row.stateName,
    stateType: row.stateType,
    color: row.color ?? '#409EFF',
    sort: row.sort,
    isInitial: row.isInitial,
    isFinal: row.isFinal,
  })
  stateDialogVisible.value = true
}

async function saveState() {
  await stateFormRef.value?.validate()
  const payload = {
    stateCode: stateForm.stateCode.trim(),
    stateName: stateForm.stateName.trim(),
    stateType: stateForm.stateType.trim(),
    color: stateForm.color,
    sort: stateForm.sort,
    isInitial: stateForm.isInitial,
    isFinal: stateForm.isFinal,
  }

  if (editingState.value) {
    await updateState(machineId.value, editingState.value.id, {
      ...payload,
      concurrencyToken: editingState.value.concurrencyToken,
    })
  } else {
    await createState(machineId.value, payload)
  }

  ElMessage.success('保存成功')
  stateDialogVisible.value = false
  await loadData()
}

async function removeState(row: StateDefinitionItem) {
  await ElMessageBox.confirm(`确认删除状态 ${row.stateName}？`, '确认删除')
  await deleteState(machineId.value, row.id)
  ElMessage.success('删除成功')
  await loadData()
}

function openCreateTransition() {
  editingTransition.value = undefined
  Object.assign(transitionForm, {
    fromState: '',
    toState: '',
    actionCode: '',
    actionName: '',
    requiredPermission: '',
    conditionJson: '',
    isEnabled: true,
    sort: transitions.value.length + 1,
  })
  transitionDialogVisible.value = true
}

function openEditTransition(row: StateTransitionItem) {
  editingTransition.value = row
  Object.assign(transitionForm, {
    fromState: row.fromState,
    toState: row.toState,
    actionCode: row.actionCode,
    actionName: row.actionName,
    requiredPermission: row.requiredPermission ?? '',
    conditionJson: row.conditionJson ?? '',
    isEnabled: row.isEnabled,
    sort: row.sort,
  })
  transitionDialogVisible.value = true
}

async function saveTransition() {
  await transitionFormRef.value?.validate()
  const payload = {
    fromState: transitionForm.fromState,
    toState: transitionForm.toState,
    actionCode: transitionForm.actionCode.trim(),
    actionName: transitionForm.actionName.trim(),
    requiredPermission: transitionForm.requiredPermission.trim(),
    conditionJson: transitionForm.conditionJson.trim(),
    isEnabled: transitionForm.isEnabled,
    sort: transitionForm.sort,
  }

  if (editingTransition.value) {
    await updateTransition(machineId.value, editingTransition.value.id, {
      ...payload,
      concurrencyToken: editingTransition.value.concurrencyToken,
    })
  } else {
    await createTransition(machineId.value, payload)
  }

  ElMessage.success('保存成功')
  transitionDialogVisible.value = false
  await loadData()
}

async function removeTransition(row: StateTransitionItem) {
  await ElMessageBox.confirm(`确认删除流转动作 ${row.actionName}？`, '确认删除')
  await deleteTransition(machineId.value, row.id)
  ElMessage.success('删除成功')
  await loadData()
}

function stateName(code: string) {
  return states.value.find((item) => item.stateCode === code)?.stateName ?? code
}

loadData()
</script>

<template>
  <PageContainer :title="machineName" :description="`业务类型：${businessType || '-'}`">
    <template #actions>
      <el-button @click="router.back()">返回</el-button>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-row :gutter="16">
      <el-col :xs="24" :lg="10">
        <div class="section-header">
          <span>状态配置</span>
          <el-button v-permission="'system:state-machine:update'" type="primary" @click="openCreateState">
            新增状态
          </el-button>
        </div>
        <el-table v-loading="loading" :data="states" border>
          <el-table-column prop="stateCode" label="编码" min-width="110" />
          <el-table-column prop="stateName" label="名称" min-width="110" />
          <el-table-column prop="color" label="颜色" width="82">
            <template #default="{ row }">
              <span class="color-dot" :style="{ backgroundColor: row.color || '#909399' }" />
            </template>
          </el-table-column>
          <el-table-column label="标记" width="120">
            <template #default="{ row }">
              <el-tag v-if="row.isInitial" size="small">初始</el-tag>
              <el-tag v-if="row.isFinal" size="small" type="success">最终</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:state-machine:update'" link type="primary" @click="openEditState(row)">
                编辑
              </el-button>
              <el-button v-permission="'system:state-machine:update'" link type="danger" @click="removeState(row)">
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-col>

      <el-col :xs="24" :lg="14">
        <div class="section-header">
          <span>流转配置</span>
          <el-button v-permission="'system:state-machine:update'" type="primary" @click="openCreateTransition">
            新增流转
          </el-button>
        </div>
        <el-table v-loading="loading" :data="transitions" border>
          <el-table-column label="来源" min-width="110">
            <template #default="{ row }">{{ stateName(row.fromState) }}</template>
          </el-table-column>
          <el-table-column label="目标" min-width="110">
            <template #default="{ row }">{{ stateName(row.toState) }}</template>
          </el-table-column>
          <el-table-column prop="actionCode" label="动作编码" min-width="130" />
          <el-table-column prop="actionName" label="动作名称" min-width="130" />
          <el-table-column prop="requiredPermission" label="所需权限" min-width="180" show-overflow-tooltip />
          <el-table-column label="状态" width="86">
            <template #default="{ row }">
              <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="操作" width="130" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:state-machine:update'" link type="primary" @click="openEditTransition(row)">
                编辑
              </el-button>
              <el-button v-permission="'system:state-machine:update'" link type="danger" @click="removeTransition(row)">
                删除
              </el-button>
            </template>
          </el-table-column>
        </el-table>
      </el-col>
    </el-row>

    <el-dialog v-model="stateDialogVisible" :title="editingState ? '编辑状态' : '新增状态'" width="560px">
      <el-form ref="stateFormRef" :model="stateForm" :rules="stateRules" label-width="110px">
        <el-form-item label="状态编码" prop="stateCode">
          <el-input v-model="stateForm.stateCode" />
        </el-form-item>
        <el-form-item label="状态名称" prop="stateName">
          <el-input v-model="stateForm.stateName" />
        </el-form-item>
        <el-form-item label="状态类型" prop="stateType">
          <el-input v-model="stateForm.stateType" placeholder="Initial / Normal / Final" />
        </el-form-item>
        <el-form-item label="颜色">
          <el-color-picker v-model="stateForm.color" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="stateForm.sort" :min="0" />
        </el-form-item>
        <el-form-item label="初始状态">
          <el-switch v-model="stateForm.isInitial" />
        </el-form-item>
        <el-form-item label="最终状态">
          <el-switch v-model="stateForm.isFinal" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="stateDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveState">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="transitionDialogVisible" :title="editingTransition ? '编辑流转' : '新增流转'" width="640px">
      <el-form ref="transitionFormRef" :model="transitionForm" :rules="transitionRules" label-width="110px">
        <el-form-item label="来源状态" prop="fromState">
          <el-select v-model="transitionForm.fromState" class="full-width">
            <el-option v-for="state in states" :key="state.id" :label="state.stateName" :value="state.stateCode" />
          </el-select>
        </el-form-item>
        <el-form-item label="目标状态" prop="toState">
          <el-select v-model="transitionForm.toState" class="full-width">
            <el-option v-for="state in states" :key="state.id" :label="state.stateName" :value="state.stateCode" />
          </el-select>
        </el-form-item>
        <el-form-item label="操作编码" prop="actionCode">
          <el-input v-model="transitionForm.actionCode" />
        </el-form-item>
        <el-form-item label="操作名称" prop="actionName">
          <el-input v-model="transitionForm.actionName" />
        </el-form-item>
        <el-form-item label="所需权限">
          <el-input v-model="transitionForm.requiredPermission" placeholder="例如 system:state-machine:transition" />
        </el-form-item>
        <el-form-item label="条件 JSON">
          <el-input v-model="transitionForm.conditionJson" type="textarea" :rows="3" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="transitionForm.isEnabled" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="transitionForm.sort" :min="0" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="transitionDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveTransition">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.section-header {
  align-items: center;
  display: flex;
  justify-content: space-between;
  margin-bottom: 12px;
}

.color-dot {
  border: 1px solid var(--el-border-color);
  border-radius: 50%;
  display: inline-block;
  height: 16px;
  width: 16px;
}

.full-width {
  width: 100%;
}
</style>
