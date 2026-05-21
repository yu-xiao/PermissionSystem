<script setup lang="ts">
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  createTenant,
  getTenants,
  setTenantEnabled,
  updateTenant,
  type TenantItem,
} from '../../../api/tenants'

const loading = ref(false)
const tableData = ref<TenantItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  isEnabled: undefined as boolean | undefined,
})

const form = reactive({
  code: '',
  name: '',
  description: '',
  isEnabled: true,
})

const rules: FormRules = {
  code: [{ required: true, message: 'Please enter tenant code', trigger: 'blur' }],
  name: [{ required: true, message: 'Please enter tenant name', trigger: 'blur' }],
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
  Object.assign(form, { code: '', name: '', description: '', isEnabled: true })
  dialogVisible.value = true
}

function openEdit(row: TenantItem) {
  editingId.value = row.id
  Object.assign(form, {
    code: row.code,
    name: row.name,
    description: row.description ?? '',
    isEnabled: row.isEnabled,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (editingId.value) {
    await updateTenant(editingId.value, {
      name: form.name,
      description: form.description,
      isEnabled: form.isEnabled,
    })
  } else {
    await createTenant(form)
  }

  ElMessage.success('Saved successfully')
  dialogVisible.value = false
  await loadData()
}

async function toggleEnabled(row: TenantItem) {
  await setTenantEnabled(row.id, !row.isEnabled)
  ElMessage.success(row.isEnabled ? 'Tenant disabled' : 'Tenant enabled')
  await loadData()
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    isEnabled: undefined,
  })
  loadData()
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Code / name / description" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="Status" style="width: 140px">
          <el-option label="Enabled" :value="true" />
          <el-option label="Disabled" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:tenant:view'" type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
        <el-button v-permission="'system:tenant:create'" @click="openCreate">Create</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="Code" min-width="140" />
      <el-table-column prop="name" label="Name" min-width="160" />
      <el-table-column prop="description" label="Description" min-width="220" show-overflow-tooltip />
      <el-table-column prop="isEnabled" label="Status" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? 'Enabled' : 'Disabled' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="Created At" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="Actions" width="180" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:tenant:update'" link type="primary" @click="openEdit(row)">Edit</el-button>
          <el-button v-permission="'system:tenant:disable'" link type="primary" @click="toggleEnabled(row)">
            {{ row.isEnabled ? 'Disable' : 'Enable' }}
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

    <el-dialog v-model="dialogVisible" :title="editingId ? 'Edit Tenant' : 'Create Tenant'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="Code" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="Name" prop="name">
          <el-input v-model="form.name" />
        </el-form-item>
        <el-form-item label="Description">
          <el-input v-model="form.description" type="textarea" />
        </el-form-item>
        <el-form-item label="Enabled">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="save">Save</el-button>
      </template>
    </el-dialog>
  </section>
</template>
