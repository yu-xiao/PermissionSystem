<script setup lang="ts">
defineOptions({
  name: 'SystemTenant',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  createTenant,
  disableTenant,
  getTenants,
  restoreTenant,
  retryTenantInitialization,
  updateTenant,
  type TenantItem,
  TenantStatus,
} from '../../../api/tenants'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const tableData = ref<TenantItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')
const editingTenant = ref<TenantItem | null>(null)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  status: undefined as TenantStatus | undefined,
})

const form = reactive({
  code: '',
  name: '',
  description: '',
  administratorUserName: 'admin',
  administratorDisplayName: '租户管理员',
  administratorPassword: '',
})

const rules: FormRules = {
  code: [{ required: true, message: '请输入租户编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入租户名称', trigger: 'blur' }],
  administratorUserName: [{ required: true, message: '请输入管理员用户名', trigger: 'blur' }],
  administratorDisplayName: [{ required: true, message: '请输入管理员显示名', trigger: 'blur' }],
  administratorPassword: [
    { required: true, message: '请输入管理员初始密码', trigger: 'blur' },
    { min: 8, message: '密码至少 8 位', trigger: 'blur' },
  ],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getTenants(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  editingTenant.value = null
  Object.assign(form, {
    code: '',
    name: '',
    description: '',
    administratorUserName: 'admin',
    administratorDisplayName: '租户管理员',
    administratorPassword: '',
  })
  dialogVisible.value = true
}

function openEdit(row: TenantItem) {
  editingId.value = row.id
  editingTenant.value = row
  Object.assign(form, {
    code: row.code,
    name: row.name,
    description: row.description ?? '',
    administratorUserName: '',
    administratorDisplayName: '',
    administratorPassword: '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (editingId.value) {
    await updateTenant(editingId.value, {
      name: form.name,
      description: form.description,
      concurrencyToken: editingTenant.value?.concurrencyToken,
    })
  } else {
    await createTenant(form)
  }

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function disable(row: TenantItem) {
  await ElMessageBox.confirm(`停用租户“${row.name}”后，现有会话、Token 和后台任务将立即失效。`, '确认停用', {
    type: 'warning',
    confirmButtonText: '停用',
  })
  await disableTenant(row.id)
  ElMessage.success('租户已停用')
  await loadData()
}

async function restore(row: TenantItem) {
  await restoreTenant(row.id)
  ElMessage.success('租户已恢复，原会话不会恢复')
  await loadData()
}

async function retryInitialization(row: TenantItem) {
  await retryTenantInitialization(row.id)
  ElMessage.success('初始化任务已重新提交')
  await loadData()
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    status: undefined,
  })
  loadData()
}

const statusMeta: Record<TenantStatus, { label: string; type: 'success' | 'info' | 'warning' | 'danger' }> = {
  [TenantStatus.Initializing]: { label: '初始化中', type: 'warning' },
  [TenantStatus.Active]: { label: '运行中', type: 'success' },
  [TenantStatus.Disabled]: { label: '已停用', type: 'info' },
  [TenantStatus.Failed]: { label: '初始化失败', type: 'danger' },
  [TenantStatus.Archived]: { label: '已归档', type: 'info' },
}

function getStatusMeta(status: TenantStatus) {
  return statusMeta[status] ?? { label: '未知', type: 'info' as const }
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="租户管理" description="维护租户编码、租户状态和基础信息。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="编码 / 名称 / 描述" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="生命周期状态" style="width: 160px">
          <el-option v-for="(meta, value) in statusMeta" :key="value" :label="meta.label" :value="Number(value)" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:tenant:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'system:tenant:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="name" label="名称" min-width="160" />
      <el-table-column prop="description" label="描述" min-width="220" show-overflow-tooltip />
      <el-table-column prop="status" label="状态" width="120">
        <template #default="{ row }">
          <el-tag :type="getStatusMeta(row.status).type">{{ getStatusMeta(row.status).label }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="初始化" min-width="170">
        <template #default="{ row }">
          <el-progress
            v-if="row.status === TenantStatus.Initializing"
            :percentage="row.initializationProgress"
            :stroke-width="8"
          />
          <el-text v-else-if="row.status === TenantStatus.Failed" type="danger" truncated>
            {{ row.initializationError || row.initializationStep || '初始化失败' }}
          </el-text>
          <span v-else>{{ row.initializedAt ? formatDate(row.initializedAt) : '-' }}</span>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="240" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:tenant:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button
            v-if="row.status === TenantStatus.Active && row.code !== 'default'"
            v-permission="'system:tenant:disable'"
            link
            type="warning"
            @click="disable(row)"
          >
            停用
          </el-button>
          <el-button
            v-if="row.status === TenantStatus.Disabled"
            v-permission="'system:tenant:disable'"
            link
            type="success"
            @click="restore(row)"
          >
            恢复
          </el-button>
          <el-button
            v-if="row.status === TenantStatus.Failed || row.status === TenantStatus.Initializing"
            v-permission="'system:tenant:create'"
            link
            type="primary"
            @click="retryInitialization(row)"
          >
            重试初始化
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑租户' : '新增租户'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="编码" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="名称" prop="name">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" />
        </el-form-item>
        <template v-if="!editingId">
          <el-form-item label="管理员用户名" prop="administratorUserName">
            <el-input v-model="form.administratorUserName" autocomplete="off" />
          </el-form-item>
          <el-form-item label="管理员显示名" prop="administratorDisplayName">
            <el-input v-model="form.administratorDisplayName" />
          </el-form-item>
          <el-form-item label="管理员初始密码" prop="administratorPassword">
            <el-input v-model="form.administratorPassword" type="password" show-password autocomplete="new-password" />
          </el-form-item>
        </template>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
