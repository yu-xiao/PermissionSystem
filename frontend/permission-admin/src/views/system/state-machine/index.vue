<script setup lang="ts">
defineOptions({
  name: 'SystemStateMachine',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import {
  createStateMachine,
  deleteStateMachine,
  getStateMachines,
  getStateTransitionLogs,
  updateStateMachine,
  type StateMachineItem,
  type StateTransitionLogItem,
} from '../../../api/stateMachine'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const router = useRouter()
const activeTab = ref('machines')
const loading = ref(false)
const logLoading = ref(false)
const dialogVisible = ref(false)
const editingRow = ref<StateMachineItem>()
const formRef = ref<FormInstance>()
const tableData = ref<StateMachineItem[]>([])
const total = ref(0)
const logData = ref<StateTransitionLogItem[]>([])
const logTotal = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  businessType: '',
  isEnabled: undefined as boolean | undefined,
})

const logQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  businessType: '',
  businessId: '',
  actionCode: '',
})

const form = reactive({
  businessType: '',
  name: '',
  description: '',
  isEnabled: true,
})

const rules: FormRules = {
  businessType: [{ required: true, message: '请输入业务类型', trigger: 'blur' }],
  name: [{ required: true, message: '请输入状态机名称', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getStateMachines({
      ...query,
      keyword: query.keyword || undefined,
      businessType: query.businessType || undefined,
    })
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadLogs() {
  logLoading.value = true
  try {
    const result = await getStateTransitionLogs({
      ...logQuery,
      businessType: logQuery.businessType || undefined,
      businessId: logQuery.businessId || undefined,
      actionCode: logQuery.actionCode || undefined,
    })
    logData.value = result.items
    logTotal.value = result.totalCount
  } finally {
    logLoading.value = false
  }
}

function openCreate() {
  editingRow.value = undefined
  Object.assign(form, {
    businessType: '',
    name: '',
    description: '',
    isEnabled: true,
  })
  dialogVisible.value = true
}

function openEdit(row: StateMachineItem) {
  editingRow.value = row
  Object.assign(form, {
    businessType: row.businessType,
    name: row.name,
    description: row.description ?? '',
    isEnabled: row.isEnabled,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (editingRow.value) {
    await updateStateMachine(editingRow.value.id, {
      name: form.name.trim(),
      description: form.description.trim(),
      isEnabled: form.isEnabled,
      concurrencyToken: editingRow.value.concurrencyToken,
    })
  } else {
    await createStateMachine({
      businessType: form.businessType.trim(),
      name: form.name.trim(),
      description: form.description.trim(),
      isEnabled: form.isEnabled,
    })
  }

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: StateMachineItem) {
  await ElMessageBox.confirm(`确认删除状态机 ${row.name}？`, '确认删除')
  await deleteStateMachine(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

function openDesigner(row: StateMachineItem) {
  router.push({
    path: `/system/state-machines/${row.id}/designer`,
    query: {
      name: row.name,
      businessType: row.businessType,
    },
  })
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    businessType: '',
    isEnabled: undefined,
  })
  loadData()
}

function resetLogQuery() {
  Object.assign(logQuery, {
    pageIndex: 1,
    businessType: '',
    businessId: '',
    actionCode: '',
  })
  loadLogs()
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
loadLogs()
</script>

<template>
  <PageContainer title="状态机" description="维护平台通用业务状态、动作和流转日志。">
    <template #actions>
      <TableToolbar @refresh="activeTab === 'machines' ? loadData() : loadLogs()" />
    </template>

    <el-tabs v-model="activeTab">
      <el-tab-pane label="状态机配置" name="machines">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="query.keyword" clearable placeholder="业务类型 / 名称" @keyup.enter="loadData" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="query.businessType" clearable placeholder="业务类型" style="width: 160px" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 120px">
              <el-option label="启用" :value="true" />
              <el-option label="禁用" :value="false" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:state-machine:view'" type="primary" @click="loadData">查询</el-button>
            <el-button @click="resetQuery">重置</el-button>
            <el-button v-permission="'system:state-machine:create'" @click="openCreate">新增</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="loading" :data="tableData" border>
          <el-table-column prop="businessType" label="业务类型" min-width="180" show-overflow-tooltip />
          <el-table-column prop="name" label="名称" min-width="180" show-overflow-tooltip />
          <el-table-column prop="description" label="描述" min-width="220" show-overflow-tooltip />
          <el-table-column prop="isEnabled" label="状态" width="90">
            <template #default="{ row }">
              <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column label="创建时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
          <el-table-column label="操作" width="220" fixed="right">
            <template #default="{ row }">
              <el-button v-permission="'system:state-machine:update'" link type="primary" @click="openDesigner(row)">
                设计
              </el-button>
              <el-button v-permission="'system:state-machine:update'" link type="primary" @click="openEdit(row)">
                编辑
              </el-button>
              <el-button v-permission="'system:state-machine:delete'" link type="danger" @click="remove(row)">
                删除
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
      </el-tab-pane>

      <el-tab-pane label="流转日志" name="logs">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="logQuery.businessType" clearable placeholder="业务类型" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="logQuery.businessId" clearable placeholder="业务ID" />
          </el-form-item>
          <el-form-item>
            <el-input v-model="logQuery.actionCode" clearable placeholder="动作编码" />
          </el-form-item>
          <el-form-item>
            <el-button v-permission="'system:state-machine:log'" type="primary" @click="loadLogs">查询</el-button>
            <el-button @click="resetLogQuery">重置</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="logLoading" :data="logData" border>
          <el-table-column prop="businessType" label="业务类型" min-width="150" />
          <el-table-column prop="businessId" label="业务ID" min-width="220" show-overflow-tooltip />
          <el-table-column prop="fromState" label="来源状态" width="110" />
          <el-table-column prop="toState" label="目标状态" width="110" />
          <el-table-column prop="actionName" label="动作" min-width="130" />
          <el-table-column prop="operatorUserName" label="操作人" width="130" />
          <el-table-column prop="comment" label="备注" min-width="180" show-overflow-tooltip />
          <el-table-column label="时间" width="180">
            <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
          </el-table-column>
        </el-table>

        <el-pagination
          v-model:current-page="logQuery.pageIndex"
          v-model:page-size="logQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="logTotal"
          @change="loadLogs"
        />
      </el-tab-pane>
    </el-tabs>

    <el-dialog v-model="dialogVisible" :title="editingRow ? '编辑状态机' : '新增状态机'" width="620px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="业务类型" prop="businessType">
          <el-input v-model="form.businessType" :disabled="Boolean(editingRow)" placeholder="DemoApprovalOrder" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
