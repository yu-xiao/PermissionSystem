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
  userName: [{ required: true, message: '请输入用户名', trigger: 'blur' }],
  password: [{ required: true, message: '请输入密码', trigger: 'blur' }],
  displayName: [{ required: true, message: '请输入显示名称', trigger: 'blur' }],
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

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: UserItem) {
  await ElMessageBox.confirm(`确认删除用户 ${row.userName}？`, '确认删除')
  await deleteUser(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function toggle(row: UserItem) {
  await setUserEnabled(row.id, !row.isEnabled)
  await loadData()
}

async function resetPassword(row: UserItem) {
  const { value } = await ElMessageBox.prompt('请输入新密码', '重置密码', {
    inputType: 'password',
    inputPattern: /^.{6,}$/,
    inputErrorMessage: '密码至少 6 个字符',
  })
  await resetUserPassword(row.id, value)
  ElMessage.success('密码已重置')
}

async function openRoles(row: UserItem) {
  await loadRoles()
  roleForm.userId = row.id
  roleForm.roleIds = [...row.roleIds]
  roleDialogVisible.value = true
}

async function saveRoles() {
  await assignUserRoles(roleForm.userId, roleForm.roleIds)
  ElMessage.success('保存成功')
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
        <el-input v-model="query.keyword" clearable placeholder="用户名 / 显示名 / 邮箱" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 130px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'system:user:create'" @click="openCreate">新增</el-button>
        <el-button v-permission="'system:user:export'" :icon="Download" @click="exportData">导出</el-button>
        <el-button v-permission="'system:user:import'" @click="downloadTemplate">模板</el-button>
        <el-upload :http-request="uploadImportFile" :show-file-list="false" accept=".xlsx">
          <el-button v-permission="'system:user:import'" :icon="Upload" :loading="importing">导入</el-button>
        </el-upload>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="userName" label="用户名" min-width="140" />
      <el-table-column prop="displayName" label="显示名称" min-width="160" />
      <el-table-column prop="email" label="邮箱" min-width="180" />
      <el-table-column prop="phoneNumber" label="手机号" min-width="140" />
      <el-table-column prop="isEnabled" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="330" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:user:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:user:update'" link @click="toggle(row)">
            {{ row.isEnabled ? '禁用' : '启用' }}
          </el-button>
          <el-button v-permission="'system:user:update'" link @click="resetPassword(row)">重置密码</el-button>
          <el-button v-permission="'system:user:update'" link @click="openRoles(row)">角色</el-button>
          <el-button v-permission="'system:user:delete'" link type="danger" @click="remove(row)">删除</el-button>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑用户' : '新增用户'" width="520px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="用户名" prop="userName">
          <el-input v-model="form.userName" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item v-if="!editingId" label="密码" prop="password">
          <el-input v-model="form.password" type="password" show-password />
        </el-form-item>
        <el-form-item label="显示名称" prop="displayName">
          <el-input v-model="form.displayName" />
        </el-form-item>
        <el-form-item label="邮箱">
          <el-input v-model="form.email" />
        </el-form-item>
        <el-form-item label="手机号">
          <el-input v-model="form.phoneNumber" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="roleDialogVisible" title="分配角色" width="520px">
      <el-select v-model="roleForm.roleIds" multiple filterable class="full-width">
        <el-option v-for="role in roles" :key="role.id" :label="role.name" :value="role.id" />
      </el-select>
      <template #footer>
        <el-button @click="roleDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="saveRoles">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="importResultVisible" title="导入结果" width="720px">
      <el-descriptions v-if="importResult" :column="3" border>
        <el-descriptions-item label="总数">{{ importResult.totalRows }}</el-descriptions-item>
        <el-descriptions-item label="有效">{{ importResult.successRows }}</el-descriptions-item>
        <el-descriptions-item label="失败">{{ importResult.failedRows }}</el-descriptions-item>
      </el-descriptions>
      <el-table v-if="importResult?.errors.length" :data="importResult.errors" border class="import-errors">
        <el-table-column prop="rowNumber" label="行号" width="80" />
        <el-table-column prop="columnName" label="列" width="160" />
        <el-table-column prop="rawValue" label="值" min-width="160" show-overflow-tooltip />
        <el-table-column prop="message" label="消息" min-width="240" show-overflow-tooltip />
      </el-table>
      <el-empty v-else description="没有导入错误" />
    </el-dialog>
  </section>
</template>

<style scoped>
.import-errors {
  margin-top: 16px;
}
</style>
