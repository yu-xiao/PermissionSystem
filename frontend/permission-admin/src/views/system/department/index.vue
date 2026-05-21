<script setup lang="ts">
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  createDepartment,
  deleteDepartment,
  getDepartmentTree,
  setDepartmentEnabled,
  updateDepartment,
  type DepartmentItem,
} from '../../../api/departments'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const tableData = ref<DepartmentItem[]>([])
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')

const form = reactive({
  parentId: undefined as string | undefined,
  code: '',
  name: '',
  sort: 0,
  status: 'Enabled',
})

const rules: FormRules = {
  code: [{ required: true, message: 'Please enter department code', trigger: 'blur' }],
  name: [{ required: true, message: 'Please enter department name', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    tableData.value = await getDepartmentTree(tenantId.value)
  } finally {
    loading.value = false
  }
}

function openCreate(parent?: DepartmentItem) {
  editingId.value = ''
  Object.assign(form, {
    parentId: parent?.id,
    code: '',
    name: '',
    sort: 0,
    status: 'Enabled',
  })
  dialogVisible.value = true
}

function openEdit(row: DepartmentItem) {
  editingId.value = row.id
  Object.assign(form, {
    parentId: row.parentId,
    code: row.code,
    name: row.name,
    sort: row.sort,
    status: row.status,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const payload = { ...form, parentId: form.parentId || undefined }
  if (editingId.value) {
    await updateDepartment(editingId.value, payload)
  } else {
    await createDepartment({ tenantId: tenantId.value, ...payload })
  }
  ElMessage.success('Saved successfully')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: DepartmentItem) {
  await ElMessageBox.confirm(`Delete department ${row.name}?`, 'Confirm delete')
  await deleteDepartment(row.id)
  ElMessage.success('Deleted successfully')
  await loadData()
}

async function toggleEnabled(row: DepartmentItem) {
  await setDepartmentEnabled(row.id, !row.isEnabled)
  ElMessage.success(row.isEnabled ? 'Department disabled' : 'Department enabled')
  await loadData()
}

loadData()
</script>

<template>
  <section class="page">
    <div class="toolbar">
      <el-button type="primary" @click="loadData">Refresh</el-button>
      <el-button v-permission="'system:department:create'" @click="openCreate()">Create</el-button>
    </div>

    <el-table v-loading="loading" :data="tableData" row-key="id" border default-expand-all>
      <el-table-column prop="name" label="Name" min-width="180" />
      <el-table-column prop="code" label="Code" min-width="140" />
      <el-table-column prop="sort" label="Sort" width="90" />
      <el-table-column prop="status" label="Status" width="120">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="treePath" label="Tree Path" min-width="220" show-overflow-tooltip />
      <el-table-column label="Actions" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:department:create'" link @click="openCreate(row)">Create child</el-button>
          <el-button v-permission="'system:department:update'" link type="primary" @click="openEdit(row)">Edit</el-button>
          <el-button v-permission="'system:department:update'" link type="primary" @click="toggleEnabled(row)">
            {{ row.isEnabled ? 'Disable' : 'Enable' }}
          </el-button>
          <el-button v-permission="'system:department:delete'" link type="danger" @click="remove(row)">Delete</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? 'Edit Department' : 'Create Department'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="Parent">
          <el-tree-select
            v-model="form.parentId"
            :data="tableData"
            clearable
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            class="full-width"
          />
        </el-form-item>
        <el-form-item label="Code" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="Name" prop="name"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="Sort"><el-input-number v-model="form.sort" :min="0" /></el-form-item>
        <el-form-item label="Status">
          <el-select v-model="form.status" class="full-width">
            <el-option label="Enabled" value="Enabled" />
            <el-option label="Disabled" value="Disabled" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancel</el-button>
        <el-button type="primary" @click="save">Save</el-button>
      </template>
    </el-dialog>
  </section>
</template>
