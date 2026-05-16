<script setup lang="ts">
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createPermission,
  deletePermission,
  getPermissions,
  updatePermission,
  type PermissionItem,
} from '../../../api/permissions'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const tableData = ref<PermissionItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '', group: '' })
const form = reactive({ code: '', name: '', group: '', description: '', resource: '', action: '' })

const rules: FormRules = {
  code: [{ required: true, message: '请输入权限编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入权限名称', trigger: 'blur' }],
  group: [{ required: true, message: '请输入权限分组', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getPermissions(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  Object.assign(form, { code: '', name: '', group: '', description: '', resource: '', action: '' })
  dialogVisible.value = true
}

function openEdit(row: PermissionItem) {
  editingId.value = row.id
  Object.assign(form, row)
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (editingId.value) {
    await updatePermission(editingId.value, form)
  } else {
    await createPermission({ tenantId: tenantId.value, ...form })
  }
  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: PermissionItem) {
  await ElMessageBox.confirm(`确定删除权限「${row.code}」吗？`, '确认删除')
  await deletePermission(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item><el-input v-model="query.keyword" clearable placeholder="权限编码 / 名称 / 分组" /></el-form-item>
      <el-form-item><el-input v-model="query.group" clearable placeholder="权限分组" /></el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button v-permission="'system:permission:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="权限编码" min-width="180" />
      <el-table-column prop="name" label="权限名称" min-width="150" />
      <el-table-column prop="group" label="权限分组" min-width="150" />
      <el-table-column prop="resource" label="资源" min-width="140" />
      <el-table-column prop="action" label="操作类型" width="120" />
      <el-table-column prop="description" label="描述" min-width="180" />
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:permission:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:permission:delete'" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" class="pager" background layout="total, sizes, prev, pager, next" :total="total" @change="loadData" />

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑权限' : '新增权限'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="权限编码" prop="code"><el-input v-model="form.code" :disabled="Boolean(editingId)" /></el-form-item>
        <el-form-item label="权限名称" prop="name"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="权限分组" prop="group"><el-input v-model="form.group" /></el-form-item>
        <el-form-item label="资源"><el-input v-model="form.resource" /></el-form-item>
        <el-form-item label="操作类型"><el-input v-model="form.action" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>
  </section>
</template>
