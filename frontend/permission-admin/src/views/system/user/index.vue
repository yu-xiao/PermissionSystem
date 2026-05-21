<script setup lang="ts">
import { Download, Upload } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules, type UploadRequestOptions } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { getRoles, type RoleItem } from '../../../api/roles'
import {
  assignUserRoles,
  createUser,
  deleteUser,
  downloadUserImportTemplate,
  exportUsers,
  getUsers,
  importUsers,
  resetUserPassword,
  setUserEnabled,
  updateUser,
  type ImportResult,
  type UserImportRow,
  type UserItem,
} from '../../../api/users'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const importing = ref(false)
const saving = ref(false)
const tableData = ref<UserItem[]>([])
const total = ref(0)
const roles = ref<RoleItem[]>([])
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const roleDialogVisible = ref(false)
const importResultVisible = ref(false)
const importResult = ref<ImportResult<UserImportRow>>()
const editingId = ref('')

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  isEnabled: undefined as boolean | undefined,
})

const form = reactive({
  userName: '',
  password: '',
  displayName: '',
  email: '',
  phoneNumber: '',
  isEnabled: true,
})

const roleForm = reactive({ userId: '', roleIds: [] as string[] })

const rules: FormRules = {
  userName: [{ required: true, message: 'Please enter username', trigger: 'blur' }],
  password: [{ required: true, message: 'Please enter password', trigger: 'blur' }],
  displayName: [{ required: true, message: 'Please enter display name', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getUsers(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadRoles() {
  const result = await getRoles({ pageIndex: 1, pageSize: 200 })
  roles.value = result.items
}

function openCreate() {
  editingId.value = ''
  Object.assign(form, {
    userName: '',
    password: '',
    displayName: '',
    email: '',
    phoneNumber: '',
    isEnabled: true,
  })
  dialogVisible.value = true
}

function openEdit(row: UserItem) {
  editingId.value = row.id
  Object.assign(form, {
    userName: row.userName,
    password: '',
    displayName: row.displayName,
    email: row.email ?? '',
    phoneNumber: row.phoneNumber ?? '',
    isEnabled: row.isEnabled,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    if (editingId.value) {
      await updateUser(editingId.value, {
        displayName: form.displayName,
        email: form.email,
        phoneNumber: form.phoneNumber,
        isEnabled: form.isEnabled,
      })
    } else {
      await createUser({ tenantId: tenantId.value, ...form })
    }

    ElMessage.success('Saved successfully')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: UserItem) {
  await ElMessageBox.confirm(`Delete user ${row.userName}?`, 'Confirm delete')
  await deleteUser(row.id)
  ElMessage.success('Deleted successfully')
  await loadData()
}

async function toggle(row: UserItem) {
  await setUserEnabled(row.id, !row.isEnabled)
  await loadData()
}

async function resetPassword(row: UserItem) {
  const { value } = await ElMessageBox.prompt('Please enter the new password', 'Reset Password', {
    inputType: 'password',
    inputPattern: /^.{6,}$/,
    inputErrorMessage: 'Password must be at least 6 characters',
  })
  await resetUserPassword(row.id, value)
  ElMessage.success('Password reset')
}

async function openRoles(row: UserItem) {
  await loadRoles()
  roleForm.userId = row.id
  roleForm.roleIds = [...row.roleIds]
  roleDialogVisible.value = true
}

async function saveRoles() {
  await assignUserRoles(roleForm.userId, roleForm.roleIds)
  ElMessage.success('Saved successfully')
  roleDialogVisible.value = false
  await loadData()
}

async function exportData() {
  const response = await exportUsers(query)
  saveBlob(response.data, `users-${Date.now()}.xlsx`)
}

async function downloadTemplate() {
  const response = await downloadUserImportTemplate()
  saveBlob(response.data, 'user-import-template.xlsx')
}

async function uploadImportFile(options: UploadRequestOptions) {
  importing.value = true
  try {
    importResult.value = await importUsers(options.file)
    importResultVisible.value = true
    options.onSuccess(importResult.value)
  } catch (error) {
    ;(options.onError as (error: unknown) => void)(error)
  } finally {
    importing.value = false
  }
}

function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    isEnabled: undefined,
  })
  loadData()
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="Username / display name / email" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="Status" style="width: 130px">
          <el-option label="Enabled" :value="true" />
          <el-option label="Disabled" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">Search</el-button>
        <el-button @click="resetQuery">Reset</el-button>
        <el-button v-permission="'system:user:create'" @click="openCreate">Create</el-button>
        <el-button v-permission="'system:user:export'" :icon="Download" @click="exportData">Export</el-button>
        <el-button v-permission="'system:user:import'" @click="downloadTemplate">Template</el-button>
        <el-upload :http-request="uploadImportFile" :show-file-list="false" accept=".xlsx">
          <el-button v-permission="'system:user:import'" :icon="Upload" :loading="importing">Import</el-button>
        </el-upload>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="userName" label="Username" min-width="140" />
      <el-table-column prop="displayName" label="Display Name" min-width="160" />
      <el-table-column prop="email" label="Email" min-width="180" />
      <el-table-column prop="phoneNumber" label="Phone" min-width="140" />
      <el-table-column prop="isEnabled" label="Status" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? 'Enabled' : 'Disabled' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Actions" width="330" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:user:update'" link type="primary" @click="openEdit(row)">Edit</el-button>
          <el-button v-permission="'system:user:update'" link @click="toggle(row)">
            {{ row.isEnabled ? 'Disable' : 'Enable' }}
          </el-button>
          <el-button v-permission="'system:user:update'" link @click="resetPassword(row)">Reset password</el-button>
          <el-button v-permission="'system:user:update'" link @click="openRoles(row)">Roles</el-button>
          <el-button v-permission="'system:user:delete'" link type="danger" @click="remove(row)">Delete</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? 'Edit User' : 'Create User'" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="Username" prop="userName">
          <el-input v-model="form.userName" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item v-if="!editingId" label="Password" prop="password">
          <el-input v-model="form.password" type="password" show-password />
        </el-form-item>
        <el-form-item label="Display Name" prop="displayName">
          <el-input v-model="form.displayName" />
        </el-form-item>
        <el-form-item label="Email">
          <el-input v-model="form.email" />
        </el-form-item>
        <el-form-item label="Phone">
          <el-input v-model="form.phoneNumber" />
        </el-form-item>
        <el-form-item label="Enabled">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancel</el-button>
        <el-button type="primary" :loading="saving" @click="save">Save</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="roleDialogVisible" title="Assign Roles" width="520px">
      <el-select v-model="roleForm.roleIds" multiple filterable class="full-width">
        <el-option v-for="role in roles" :key="role.id" :label="role.name" :value="role.id" />
      </el-select>
      <template #footer>
        <el-button @click="roleDialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="saveRoles">Save</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="importResultVisible" title="Import Result" width="720px">
      <el-descriptions v-if="importResult" :column="3" border>
        <el-descriptions-item label="Total">{{ importResult.totalRows }}</el-descriptions-item>
        <el-descriptions-item label="Valid">{{ importResult.successRows }}</el-descriptions-item>
        <el-descriptions-item label="Failed">{{ importResult.failedRows }}</el-descriptions-item>
      </el-descriptions>
      <el-table v-if="importResult?.errors.length" :data="importResult.errors" border class="import-errors">
        <el-table-column prop="rowNumber" label="Row" width="80" />
        <el-table-column prop="columnName" label="Column" width="160" />
        <el-table-column prop="rawValue" label="Value" min-width="160" show-overflow-tooltip />
        <el-table-column prop="message" label="Message" min-width="240" show-overflow-tooltip />
      </el-table>
      <el-empty v-else description="No import errors" />
    </el-dialog>
  </section>
</template>

<style scoped>
.import-errors {
  margin-top: 16px;
}
</style>
