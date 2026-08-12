<script setup lang="ts">
defineOptions({
  name: 'SystemConfig',
})

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
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.effectiveTenantId)

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
  configKey: [{ required: true, message: '请输入配置键', trigger: 'blur' }],
  configType: [{ required: true, message: '请选择配置类型', trigger: 'change' }],
  groupCode: [{ required: true, message: '请输入分组编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入配置名称', trigger: 'blur' }],
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
      concurrencyToken: editingRow.value.concurrencyToken,
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

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: SystemConfigItem) {
  await ElMessageBox.confirm(`确认删除配置 ${row.configKey}？`, '确认删除')
  await deleteSystemConfig(row.id)
  ElMessage.success('删除成功')
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
    concurrencyToken: row.concurrencyToken,
  })
  ElMessage.success('状态已更新')
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
  <PageContainer title="参数配置" description="维护系统参数、敏感配置、分组和启停状态。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="键 / 名称 / 分组" />
      </el-form-item>
      <el-form-item>
        <el-input v-model="query.groupCode" clearable placeholder="分组" style="width: 140px" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.configType" clearable placeholder="类型" style="width: 130px">
          <el-option v-for="type in typeOptions" :key="type" :label="$displayText(type)" :value="type" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="状态" style="width: 130px">
          <el-option v-for="status in statusOptions" :key="status" :label="$displayText(status)" :value="status" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEncrypted" clearable placeholder="敏感" style="width: 136px">
          <el-option label="加密" :value="true" />
          <el-option label="明文" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:config:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'system:config:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="configKey" label="键" min-width="180" show-overflow-tooltip />
      <el-table-column prop="name" label="名称" min-width="160" show-overflow-tooltip />
      <el-table-column prop="configValue" label="值" min-width="220" show-overflow-tooltip>
        <template #default="{ row }">
          <span :class="{ 'masked-value': row.isEncrypted }">{{ displayValue(row) }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="configType" label="类型" width="100" />
      <el-table-column prop="groupCode" label="分组" width="130" show-overflow-tooltip />
      <el-table-column prop="isEncrypted" label="敏感" width="104">
        <template #default="{ row }">
          <el-tag :type="row.isEncrypted ? 'warning' : 'info'">{{ row.isEncrypted ? '是' : '否' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="isSystem" label="系统" width="92">
        <template #default="{ row }">
          <el-tag v-if="row.isSystem" type="primary">是</el-tag>
          <span v-else>-</span>
        </template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)">{{ $displayText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="sort" label="排序" width="72" />
      <el-table-column prop="description" label="描述" min-width="180" show-overflow-tooltip />
      <el-table-column label="操作" width="190" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:config:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:config:update'" link type="primary" @click="toggleStatus(row)">
            {{ row.status === 'Enabled' ? '禁用' : '启用' }}
          </el-button>
          <el-button
            v-permission="'system:config:delete'"
            link
            type="danger"
            :disabled="row.isSystem"
            @click="remove(row)"
          >
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

    <el-dialog v-model="dialogVisible" :title="editingRow ? '编辑系统配置' : '新增系统配置'" width="640px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="130px">
        <el-form-item label="配置键" prop="configKey">
          <el-input v-model="form.configKey" :disabled="Boolean(editingRow)" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="值">
          <el-input
            v-model="form.configValue"
            :type="form.configType === 'Json' ? 'textarea' : form.isEncrypted ? 'password' : 'text'"
            :autosize="{ minRows: 3, maxRows: 8 }"
            show-password
            :placeholder="editingRow?.isEncrypted ? '留空表示保留当前加密值' : ''"
          />
        </el-form-item>
        <el-form-item label="类型" prop="configType">
          <el-select v-model="form.configType">
            <el-option v-for="type in typeOptions" :key="type" :label="$displayText(type)" :value="type" />
          </el-select>
        </el-form-item>
        <el-form-item label="分组编码" prop="groupCode">
          <el-input v-model="form.groupCode" />
        </el-form-item>
        <el-form-item label="敏感">
          <el-switch v-model="form.isEncrypted" />
        </el-form-item>
        <el-form-item label="系统">
          <el-switch v-model="form.isSystem" />
        </el-form-item>
        <el-form-item label="状态">
          <el-select v-model="form.status">
            <el-option v-for="status in statusOptions" :key="status" :label="$displayText(status)" :value="status" />
          </el-select>
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="form.sort" :min="0" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.masked-value {
  color: var(--el-text-color-secondary);
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", monospace;
}
</style>
