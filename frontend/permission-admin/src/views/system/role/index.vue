<script setup lang="ts">
defineOptions({
  name: 'SystemRole',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { getDepartmentTree, type DepartmentItem } from '../../../api/departments'
import {
  createRole,
  DataScopeType,
  deleteRole,
  getRoleDataScope,
  getRoles,
  setRoleDataScope,
  updateRole,
  type DataScopeType as DataScopeTypeValue,
  type RoleItem,
} from '../../../api/roles'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'
import RolePermissionMatrixDialog from './components/RolePermissionMatrixDialog.vue'
import RoleUserDialog from './components/RoleUserDialog.vue'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const isSuperAdmin = computed(() => authStore.isSuperAdmin)
const loading = ref(false)
const saving = ref(false)
const tableData = ref<RoleItem[]>([])
const total = ref(0)
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const dataScopeDialogVisible = ref(false)
const permissionMatrixDialogVisible = ref(false)
const roleUserDialogVisible = ref(false)
const editingId = ref('')
const selectedRole = ref<RoleItem | null>(null)
const departments = ref<DepartmentItem[]>([])
const dataScopeForm = reactive({
  roleId: '',
  scopeType: DataScopeType.All as DataScopeTypeValue,
  departmentIds: [] as string[],
})
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })
const form = reactive({ code: '', name: '', description: '', isEnabled: true, sort: 0 })

function isProtectedRole(row: RoleItem) {
  return row.isBuiltin || row.isSuperAdminRole || row.code === 'SuperAdmin'
}

function canEditRole(row: RoleItem) {
  return !isProtectedRole(row) || isSuperAdmin.value
}

function canAssignRolePermissions(row: RoleItem) {
  return !isProtectedRole(row) || (isSuperAdmin.value && (row.isSuperAdminRole || row.code === 'SuperAdmin'))
}

function canAssignRoleUsers(row: RoleItem) {
  return !isProtectedRole(row) || isSuperAdmin.value
}

function canSetRoleDataScope(row: RoleItem) {
  return !isProtectedRole(row)
}

function canDeleteRole(row: RoleItem) {
  return !isProtectedRole(row)
}

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
  selectedRole.value = null
  Object.assign(form, { code: '', name: '', description: '', isEnabled: true, sort: 0 })
  dialogVisible.value = true
}

function openEdit(row: RoleItem) {
  editingId.value = row.id
  selectedRole.value = row
  Object.assign(form, row)
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    if (editingId.value) {
      await updateRole(editingId.value, form)
    } else {
      await createRole({ tenantId: tenantId.value, ...form })
    }
    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: RoleItem) {
  await ElMessageBox.confirm(`确认删除角色 ${row.name}？`, '确认删除')
  await deleteRole(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function openDataScope(row: RoleItem) {
  const [departmentTree, dataScope] = await Promise.all([
    getDepartmentTree(tenantId.value),
    getRoleDataScope(row.id),
  ])
  departments.value = departmentTree
  dataScopeForm.roleId = row.id
  dataScopeForm.scopeType = dataScope.scopeType
  dataScopeForm.departmentIds = [...dataScope.departmentIds]
  dataScopeDialogVisible.value = true
}

function openPermissionMatrix(row: RoleItem) {
  selectedRole.value = row
  permissionMatrixDialogVisible.value = true
}

function openRoleUsers(row: RoleItem) {
  selectedRole.value = row
  roleUserDialogVisible.value = true
}

async function saveDataScope() {
  const departmentIds =
    dataScopeForm.scopeType === DataScopeType.CustomDepartments ? dataScopeForm.departmentIds : []
  await setRoleDataScope(dataScopeForm.roleId, dataScopeForm.scopeType, departmentIds)
  ElMessage.success('保存成功')
  dataScopeDialogVisible.value = false
}

loadData()
</script>

<template>
  <PageContainer title="角色管理" description="维护角色、菜单授权、权限授权和数据范围。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="角色编码 / 名称" />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button v-permission="'system:role:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="name" label="名称" min-width="160" />
      <el-table-column label="标识" width="180">
        <template #default="{ row }">
          <el-tag v-if="row.isBuiltin" type="warning">系统内置</el-tag>
          <el-tag v-if="row.isSuperAdminRole || row.code === 'SuperAdmin'" type="danger" class="role-flag">超级管理员</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="description" label="描述" min-width="180" />
      <el-table-column prop="sort" label="排序" width="90" />
      <el-table-column prop="isEnabled" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="420" fixed="right">
        <template #default="{ row }">
          <el-button v-if="canEditRole(row)" v-permission="'system:role:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-if="canAssignRolePermissions(row)" v-permission="'system:role:assign-permission'" link type="primary" @click="openPermissionMatrix(row)">
            分配权限
          </el-button>
          <el-button v-if="canAssignRoleUsers(row)" v-permission="'system:role:assign-user'" link type="primary" @click="openRoleUsers(row)">
            关联用户
          </el-button>
          <el-button v-if="canSetRoleDataScope(row)" v-permission="'system:role:data-scope'" link @click="openDataScope(row)">数据范围</el-button>
          <el-button v-if="canDeleteRole(row)" v-permission="'system:role:delete'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑角色' : '新增角色'" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="编码" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="名称" prop="name"><el-input v-model="form.name" :disabled="Boolean(selectedRole && isProtectedRole(selectedRole))" /></el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" /></el-form-item>
        <el-form-item label="排序"><el-input-number v-model="form.sort" :min="0" /></el-form-item>
        <el-form-item label="启用"><el-switch v-model="form.isEnabled" :disabled="Boolean(selectedRole && isProtectedRole(selectedRole))" /></el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="dataScopeDialogVisible" title="数据范围" width="640px">
      <el-form label-width="150px">
        <el-form-item label="范围">
          <el-radio-group v-model="dataScopeForm.scopeType">
            <el-radio :value="DataScopeType.All">全部</el-radio>
            <el-radio :value="DataScopeType.CurrentUser">当前用户</el-radio>
            <el-radio :value="DataScopeType.CurrentDepartment">当前部门</el-radio>
            <el-radio :value="DataScopeType.CurrentDepartmentAndChildren">当前部门及子部门</el-radio>
            <el-radio :value="DataScopeType.CustomDepartments">自定义部门</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-form-item v-if="dataScopeForm.scopeType === DataScopeType.CustomDepartments" label="部门">
          <el-tree-select
            v-model="dataScopeForm.departmentIds"
            :data="departments"
            multiple
            show-checkbox
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            class="full-width"
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dataScopeDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveDataScope">保存</el-button>
      </template>
    </el-dialog>

    <RolePermissionMatrixDialog
      v-model="permissionMatrixDialogVisible"
      :role="selectedRole"
      :tenant-id="tenantId"
      @saved="loadData"
    />

    <RoleUserDialog
      v-model="roleUserDialogVisible"
      :role-id="selectedRole?.id"
      :role-name="selectedRole?.name"
      :requires-sensitive-verification="Boolean(selectedRole?.isSuperAdminRole || selectedRole?.code === 'SuperAdmin')"
      @saved="loadData"
    />
  </PageContainer>
</template>

<style scoped>
.role-flag {
  margin-left: 6px;
}
</style>
