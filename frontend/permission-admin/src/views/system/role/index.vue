<script setup lang="ts">
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { getMenuTree, type MenuItem } from '../../../api/menus'
import { getPermissions, type PermissionItem } from '../../../api/permissions'
import {
  assignRoleMenus,
  assignRolePermissions,
  createRole,
  deleteRole,
  getRoles,
  updateRole,
  type RoleItem,
} from '../../../api/roles'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const tableData = ref<RoleItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const menuDialogVisible = ref(false)
const permissionDialogVisible = ref(false)
const editingId = ref('')
const menus = ref<MenuItem[]>([])
const permissions = ref<PermissionItem[]>([])
const relationForm = reactive({ roleId: '', menuIds: [] as string[], permissionIds: [] as string[] })
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })
const form = reactive({ code: '', name: '', description: '', isEnabled: true, sort: 0 })

const rules: FormRules = {
  code: [{ required: true, message: '请输入角色编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入角色名称', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getRoles(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  Object.assign(form, { code: '', name: '', description: '', isEnabled: true, sort: 0 })
  dialogVisible.value = true
}

function openEdit(row: RoleItem) {
  editingId.value = row.id
  Object.assign(form, row)
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (editingId.value) {
    await updateRole(editingId.value, form)
  } else {
    await createRole({ tenantId: tenantId.value, ...form })
  }
  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: RoleItem) {
  await ElMessageBox.confirm(`确定删除角色「${row.name}」吗？`, '确认删除')
  await deleteRole(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function openMenus(row: RoleItem) {
  menus.value = await getMenuTree(tenantId.value)
  relationForm.roleId = row.id
  relationForm.menuIds = []
  menuDialogVisible.value = true
}

async function openPermissions(row: RoleItem) {
  const result = await getPermissions({ pageIndex: 1, pageSize: 200 })
  permissions.value = result.items
  relationForm.roleId = row.id
  relationForm.permissionIds = []
  permissionDialogVisible.value = true
}

async function saveMenus() {
  await assignRoleMenus(relationForm.roleId, relationForm.menuIds)
  ElMessage.success('保存成功')
  menuDialogVisible.value = false
}

async function savePermissions() {
  await assignRolePermissions(relationForm.roleId, relationForm.permissionIds)
  ElMessage.success('保存成功')
  permissionDialogVisible.value = false
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item><el-input v-model="query.keyword" clearable placeholder="角色编码 / 角色名称" /></el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button v-permission="'system:role:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="角色编码" min-width="140" />
      <el-table-column prop="name" label="角色名称" min-width="160" />
      <el-table-column prop="description" label="描述" min-width="180" />
      <el-table-column prop="sort" label="排序" width="90" />
      <el-table-column prop="isEnabled" label="状态" width="100">
        <template #default="{ row }"><el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag></template>
      </el-table-column>
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:role:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:role:update'" link @click="openMenus(row)">菜单</el-button>
          <el-button v-permission="'system:role:update'" link @click="openPermissions(row)">权限</el-button>
          <el-button v-permission="'system:role:delete'" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>
    <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" class="pager" background layout="total, sizes, prev, pager, next" :total="total" @change="loadData" />

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑角色' : '新增角色'" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="角色编码" prop="code"><el-input v-model="form.code" :disabled="Boolean(editingId)" /></el-form-item>
        <el-form-item label="角色名称" prop="name"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" /></el-form-item>
        <el-form-item label="排序"><el-input-number v-model="form.sort" :min="0" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" @click="save">保存</el-button></template>
    </el-dialog>

    <el-dialog v-model="menuDialogVisible" title="分配菜单" width="520px">
      <el-tree-select v-model="relationForm.menuIds" :data="menus" multiple show-checkbox node-key="id" :props="{ label: 'name', children: 'children' }" class="full-width" />
      <template #footer><el-button @click="menuDialogVisible = false">取消</el-button><el-button type="primary" @click="saveMenus">保存</el-button></template>
    </el-dialog>

    <el-dialog v-model="permissionDialogVisible" title="分配权限" width="560px">
      <el-select v-model="relationForm.permissionIds" multiple filterable class="full-width">
        <el-option v-for="item in permissions" :key="item.id" :label="`${item.name} (${item.code})`" :value="item.id" />
      </el-select>
      <template #footer><el-button @click="permissionDialogVisible = false">取消</el-button><el-button type="primary" @click="savePermissions">保存</el-button></template>
    </el-dialog>
  </section>
</template>
