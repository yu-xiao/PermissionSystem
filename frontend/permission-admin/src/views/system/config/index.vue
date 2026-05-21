<script setup lang="ts">
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createSystemConfig,
  deleteSystemConfig,
  getSystemConfigs,
  updateSystemConfig,
  type SystemConfigItem,
  type SystemConfigStatus,
  type SystemConfigType,
  type UpdateSystemConfigRequest,
} from '../../../api/system-configs'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')

const loading = ref(false)
const tableData = ref<SystemConfigItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingRow = ref<SystemConfigItem>()

const statusOptions: SystemConfigStatus[] = ['Enabled', 'Disabled']
const typeOptions: SystemConfigType[] = ['String', 'Number', 'Boolean', 'Json']

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  groupCode: '',
  configType: '',
  status: undefined as SystemConfigStatus | undefined,
  isEncrypted: undefined as boolean | undefined,
})

const form = reactive({
  configKey: '',
  configValue: '',
  configType: 'String',
  groupCode: '',
  name: '',
  description: '',
  isEncrypted: false,
  isSystem: false,
  status: 'Enabled' as SystemConfigStatus,
  sort: 0,
})

const rules: FormRules = {
  configKey: [{ required: true, message: 'Please enter config key', trigger: 'blur' }],
  configType: [{ required: true, message: 'Please select config type', trigger: 'change' }],
  groupCode: [{ required: true, message: 'Please enter group code', trigger: 'blur' }],
  name: [{ required: true, message: 'Please enter config name', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getSystemConfigs(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingRow.value = undefined
  Object.assign(form, {
    configKey: '',
    configValue: '',
    configType: 'String',
    groupCode: '',
    name: '',
    description: '',
    isEncrypted: false,
    isSystem: false,
    status: 'Enabled',
    sort: 0,
  })
  dialogVisible.value = true
}

function openEdit(row: SystemConfigItem) {
  editingRow.value = row
  Object.assign(form, {
    configKey: row.configKey,
    configValue: row.isEncrypted ? '' : row.configValue,
    configType: row.configType,
    groupCode: row.groupCode,
    name: row.name,
    description: row.description ?? '',
    isEncrypted: row.isEncrypted,
    isSystem: row.isSystem,
    status: row.status,
    sort: row.sort,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()

  if (editingRow.value) {
    const payload: UpdateSystemConfigRequest = {
      configType: form.configType,
      groupCode: form.groupCode,
      name: form.name,
      description: form.description,
      isEncrypted: form.isEncrypted,
      isSystem: form.isSystem,
      status: form.status,
      sort: form.sort,
    }

    if (form.configValue !== '') {
      payload.configValue = form.configValue
    }

    await updateSystemConfig(editingRow.value.id, payload)
  } else {
    await createSystemConfig({
      tenantId: tenantId.value,
      configKey: form.configKey,
      configValue: form.configValue,
      configType: form.configType,
      groupCode: form.groupCode,
      name: form.name,
      description: form.description,
      isEncrypted: form.isEncrypted,
      isSystem: form.isSystem,
      status: form.status,
      sort: form.sort,
    })
  }

  ElMessage.success('Saved successfully')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: SystemConfigItem) {
  await ElMessageBox.confirm(`Delete config ${row.configKey}?`, 'Confirm delete')
  await deleteSystemConfig(row.id)
  ElMessage.success('Deleted successfully')
  await loadData()
}

async function toggleStatus(row: SystemConfigItem) {
  await updateSystemConfig(row.id, {
    configType: row.configType,
    groupCode: row.groupCode,
    name: row.name,
    description: row.description,
    isEncrypted: row.isEncrypted,
    isSystem: row.isSystem,
    status: row.status === 'Enabled' ? 'Disabled' : 'Enabled',
    sort: row.sort,
  })
  ElMessage.success('Status updated')
  await loadData()
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    groupCode: '',
    configType: '',
    status: undefined,
    isEncrypted: undefined,
  })
  loadData()
}

function statusTagType(status: SystemConfigStatus) {
  return status === 'Enabled' ? 'success' : 'info'
}

function displayValue(row: SystemConfigItem) {
  return row.isEncrypted ? '******' : row.configValue || '-'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Key / name / group" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.groupCode" clearable placeholder="Group" style="width: 140px" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.configType" clearable placeholder="Type" style="width: 130px">
          <el-option v-for="type in typeOptions" :key="type" :label="type" :value="type" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="Status" style="width: 130px">
          <el-option v-for="status in statusOptions" :key="status" :label="status" :value="status" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEncrypted" clearable placeholder="Sensitive" style="width: 136px">
          <el-option label="Encrypted" :value="true" />
          <el-option label="Plain" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:config:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
        <el-button v-permission="'system:config:create'" @click="openCreate">Create</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="configKey" label="Key" min-width="180" show-overflow-tooltip />
      <el-table-column prop="name" label="Name" min-width="160" show-overflow-tooltip />
      <el-table-column prop="configValue" label="Value" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          <span :class="{ 'masked-value': row.isEncrypted }">{{ displayValue(row) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="configType" label="Type" width="100" />
      <el-table-column prop="groupCode" label="Group" width="130" show-overflow-tooltip />
      <el-table-column prop="isEncrypted" label="Sensitive" width="104">
        <template #default="{ row }">
          <el-tag :type="row.isEncrypted ? 'warning' : 'info'">{{ row.isEncrypted ? 'Yes' : 'No' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="isSystem" label="System" width="92">
        <template #default="{ row }">
          <el-tag v-if="row.isSystem" type="primary">Yes</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="Status" width="100">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="sort" label="Sort" width="72" />
      <el-table-column prop="description" label="Description" min-width="180" show-overflow-tooltip />
      <el-table-column label="Actions" width="190" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:config:update'" link type="primary" @click="openEdit(row)">Edit</el-button>
          <el-button v-permission="'system:config:update'" link type="primary" @click="toggleStatus(row)">
            {{ row.status === 'Enabled' ? 'Disable' : 'Enable' }}
          </el-button>
          <el-button
            v-permission="'system:config:delete'"
            link
            type="danger"
            :disabled="row.isSystem"
            @click="remove(row)"
          >
            Delete
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

    <el-dialog v-model="dialogVisible" :title="editingRow ? 'Edit System Config' : 'Create System Config'" width="640px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="130px">
        <el-form-item label="Config Key" prop="configKey">
          <el-input v-model="form.configKey" :disabled="Boolean(editingRow)" />
        </el-form-item>
        <el-form-item label="Name" prop="name">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="Value">
          <el-input
            v-model="form.configValue"
            :type="form.configType === 'Json' ? 'textarea' : form.isEncrypted ? 'password' : 'text'"
            :autosize="{ minRows: 3, maxRows: 8 }"
            show-password
            :placeholder="editingRow?.isEncrypted ? 'Leave blank to keep current encrypted value' : ''"
          />
        </el-form-item>
        <el-form-item label="Type" prop="configType">
          <el-select v-model="form.configType">
            <el-option v-for="type in typeOptions" :key="type" :label="type" :value="type" />
          </el-select>
        </el-form-item>
        <el-form-item label="Group Code" prop="groupCode">
          <el-input v-model="form.groupCode" />
        </el-form-item>
        <el-form-item label="Sensitive">
          <el-switch v-model="form.isEncrypted" />
        </el-form-item>
        <el-form-item label="System">
          <el-switch v-model="form.isSystem" />
        </el-form-item>
        <el-form-item label="Status">
          <el-select v-model="form.status">
            <el-option v-for="status in statusOptions" :key="status" :label="status" :value="status" />
          </el-select>
        </el-form-item>
        <el-form-item label="Sort">
          <el-input-number v-model="form.sort" :min="0" />
        </el-form-item>
        <el-form-item label="Description">
          <el-input v-model="form.description" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="save">Save</el-button>
      </template>
    </el-dialog>
  </section>
</template>

<style scoped>
.masked-value {
  color: var(--el-text-color-secondary);
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace;
}
</style>
