<script setup lang="ts">
defineOptions({
  name: 'SystemTenant',
})

import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  createTenant,
  getTenants,
  setTenantEnabled,
  updateTenant,
  type TenantItem,
} from '../../../api/tenants'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

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
  code: [{ required: true, message: '请输入租户编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入租户名称', trigger: 'blur' }],
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

  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function toggleEnabled(row: TenantItem) {
  await setTenantEnabled(row.id, !row.isEnabled)
  ElMessage.success(row.isEnabled ? '租户已禁用' : '租户已启用')
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
  <PageContainer title="租户管理" description="维护租户编码、租户状态和基础信息。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="编码 / 名称 / 描述" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 140px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
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
      <el-table-column prop="isEnabled" label="状态" width="110">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="180" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:tenant:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:tenant:disable'" link type="primary" @click="toggleEnabled(row)">
            {{ row.isEnabled ? '禁用' : '启用' }}
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
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
