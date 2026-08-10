<script setup lang="ts">
defineOptions({
  name: 'SystemDepartment',
})

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
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const tableData = ref<DepartmentItem[]>([])
const formRef = ref<FormInstance>()
const dialogVisible = ref(false)
const editingId = ref('')
const editingDepartment = ref<DepartmentItem | null>(null)

const form = reactive({
  parentId: undefined as string | undefined,
  code: '',
  name: '',
  sort: 0,
  status: 'Enabled',
})

const rules: FormRules = {
  code: [{ required: true, message: '请输入部门编码', trigger: 'blur' }],
  name: [{ required: true, message: '请输入部门名称', trigger: 'blur' }],
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
  editingDepartment.value = null
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
  editingDepartment.value = row
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
    await updateDepartment(editingId.value, {
      ...payload,
      concurrencyToken: editingDepartment.value?.concurrencyToken,
    })
  } else {
    await createDepartment({ tenantId: tenantId.value, ...payload })
  }
  ElMessage.success('保存成功')
  dialogVisible.value = false
  await loadData()
}

async function remove(row: DepartmentItem) {
  await ElMessageBox.confirm(`确认删除部门 ${row.name}？`, '确认删除')
  await deleteDepartment(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function toggleEnabled(row: DepartmentItem) {
  await setDepartmentEnabled(row.id, !row.isEnabled)
  ElMessage.success(row.isEnabled ? '部门已禁用' : '部门已启用')
  await loadData()
}

loadData()
</script>

<template>
  <PageContainer title="部门管理" description="维护组织架构、部门层级和部门启停状态。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <div class="toolbar">
      <el-button type="primary" @click="loadData">刷新</el-button>
      <el-button v-permission="'system:department:create'" @click="openCreate()">新增</el-button>
    </div>

    <el-table v-loading="loading" :data="tableData" row-key="id" border default-expand-all>
      <el-table-column prop="name" label="名称" min-width="180" />
      <el-table-column prop="code" label="编码" min-width="140" />
      <el-table-column prop="sort" label="排序" width="90" />
      <el-table-column prop="status" label="状态" width="120">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ $displayText(row.status) }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="treePath" label="树路径" min-width="220" show-overflow-tooltip />
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:department:create'" link @click="openCreate(row)">新增子部门</el-button>
          <el-button v-permission="'system:department:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'system:department:update'" link type="primary" @click="toggleEnabled(row)">
            {{ row.isEnabled ? '禁用' : '启用' }}
          </el-button>
          <el-button v-permission="'system:department:delete'" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑部门' : '新增部门'" width="560px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="上级">
          <el-tree-select
            v-model="form.parentId"
            :data="tableData"
            clearable
            node-key="id"
            :props="{ label: 'name', children: 'children' }"
            class="full-width"
          />
        </el-form-item>
        <el-form-item label="编码" prop="code">
          <el-input v-model="form.code" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="名称" prop="name"><el-input v-model="form.name" /></el-form-item>
        <el-form-item label="排序"><el-input-number v-model="form.sort" :min="0" /></el-form-item>
        <el-form-item label="状态">
          <el-select v-model="form.status" class="full-width">
            <el-option label="启用" value="Enabled" />
            <el-option label="禁用" value="Disabled" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
