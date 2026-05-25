<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed, ref, watch } from 'vue'
import {
  DataScopeType,
  getRolePermissionMatrix,
  saveRolePermissionMatrix,
  type PermissionItem,
  type PermissionMenuRow,
  type RoleItem,
  type RoleMenuDataScopeRequest,
  type RolePermissionMatrix,
  type SaveRolePermissionMatrixRequest,
  type DataScopeType as DataScopeTypeValue,
} from '../../../../api/roles'
import DataScopeDialog from './DataScopeDialog.vue'
import FieldPermissionDialog from './FieldPermissionDialog.vue'
import PermissionModulePanel from './PermissionModulePanel.vue'

const props = defineProps<{
  modelValue: boolean
  role: RoleItem | null
  tenantId?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: []
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
})
const title = computed(() => `为角色【${props.role?.name ?? ''}】分配权限`)

const loading = ref(false)
const saving = ref(false)
const onlyChecked = ref(false)
const matrix = ref<RolePermissionMatrix>()
const dataScopeVisible = ref(false)
const fieldPermissionVisible = ref(false)
const selectedRow = ref<PermissionMenuRow>()
const roleDataScopeDraft = ref<Omit<RoleMenuDataScopeRequest, 'menuId'> | null>(null)

watch(
  () => [props.modelValue, props.role?.id] as const,
  async ([isOpen, roleId]) => {
    if (isOpen && roleId) {
      await loadMatrix(roleId)
    }
  },
)

async function loadMatrix(roleId = props.role?.id) {
  if (!roleId) {
    return
  }

  loading.value = true
  try {
    matrix.value = await getRolePermissionMatrix(roleId)
    roleDataScopeDraft.value = null
    recomputeMatrix()
  } finally {
    loading.value = false
  }
}

function toggleModule(moduleId: string) {
  const module = findModule(moduleId)
  if (module) {
    module.expanded = !module.expanded
  }
}

function setModuleChecked(moduleId: string, checked: boolean) {
  const module = findModule(moduleId)
  if (!module) {
    return
  }

  for (const row of module.menus) {
    setRowState(row, checked)
  }
  recomputeMatrix()
}

function setRowChecked(menuId: string, checked: boolean) {
  const row = findRow(menuId)
  if (!row) {
    return
  }

  setRowState(row, checked)
  recomputeMatrix()
}

function setPermissionChecked(menuId: string, permissionId: string, checked: boolean) {
  const row = findRow(menuId)
  const permission = row?.permissions.find((item) => item.permissionId === permissionId)
  if (!row || !permission) {
    return
  }

  permission.checked = checked

  if (checked) {
    row.checked = true
    ensureViewPermission(row, permission)
  } else if (row.permissions.every((item) => !item.checked)) {
    row.checked = false
  }

  recomputeMatrix()
}

function selectAll() {
  for (const module of matrix.value?.modules ?? []) {
    for (const row of module.menus) {
      setRowState(row, true)
    }
  }
  recomputeMatrix()
}

function clearAll() {
  for (const module of matrix.value?.modules ?? []) {
    for (const row of module.menus) {
      setRowState(row, false)
    }
  }
  recomputeMatrix()
}

function expandAll() {
  for (const module of matrix.value?.modules ?? []) {
    module.expanded = true
  }
}

function collapseAll() {
  for (const module of matrix.value?.modules ?? []) {
    module.expanded = false
  }
}

function openDataScope(row: PermissionMenuRow) {
  selectedRow.value = row
  dataScopeVisible.value = true
}

function openFieldPermission(row: PermissionMenuRow) {
  selectedRow.value = row
  fieldPermissionVisible.value = true
}

function saveDataScope(value: { scopeType: DataScopeTypeValue; departmentIds: string[] }) {
  if (!selectedRow.value) {
    return
  }

  roleDataScopeDraft.value = {
    scopeType: value.scopeType,
    departmentIds: value.departmentIds,
  }

  for (const row of matrix.value?.modules.flatMap((module) => module.menus) ?? []) {
    row.dataScopeSummary = getDataScopeText(value.scopeType)
  }
}

async function save() {
  if (!props.role || !matrix.value) {
    return
  }

  const payload: SaveRolePermissionMatrixRequest = {
    menuIds: collectMenuIds(),
    permissionIds: collectPermissionIds(),
    dataScopes: collectDataScopes(),
    fieldPermissions: [],
  }

  saving.value = true
  try {
    await saveRolePermissionMatrix(props.role.id, payload)
    ElMessage.success('保存成功')
    visible.value = false
    emit('saved')
  } finally {
    saving.value = false
  }
}

function close() {
  if (saving.value) {
    return
  }

  visible.value = false
}

function collectMenuIds() {
  return (matrix.value?.modules ?? []).flatMap((module) =>
    module.menus.filter((row) => row.checked).map((row) => row.menuId),
  )
}

function collectPermissionIds() {
  return (matrix.value?.modules ?? []).flatMap((module) =>
    module.menus.flatMap((row) =>
      row.permissions.filter((permission) => permission.checked).map((permission) => permission.permissionId),
    ),
  )
}

function collectDataScopes() {
  if (!roleDataScopeDraft.value || !matrix.value) {
    return []
  }

  const targetRow =
    matrix.value.modules.flatMap((module) => module.menus).find((row) => row.checked) ??
    matrix.value.modules.flatMap((module) => module.menus)[0]

  return targetRow
    ? [
        {
          menuId: targetRow.menuId,
          scopeType: roleDataScopeDraft.value.scopeType,
          departmentIds: roleDataScopeDraft.value.departmentIds,
        },
      ]
    : []
}

function setRowState(row: PermissionMenuRow, checked: boolean) {
  row.checked = checked
  for (const permission of row.permissions) {
    permission.checked = checked
  }
}

function ensureViewPermission(row: PermissionMenuRow, changedPermission: PermissionItem) {
  if (changedPermission.permissionType === 'view') {
    return
  }

  const viewPermission = row.permissions.find((permission) => permission.permissionType === 'view')
  if (viewPermission) {
    viewPermission.checked = true
  }
}

function recomputeMatrix() {
  for (const module of matrix.value?.modules ?? []) {
    for (const row of module.menus) {
      const total = 1 + row.permissions.length
      const checked = (row.checked ? 1 : 0) + row.permissions.filter((permission) => permission.checked).length
      row.indeterminate = checked > 0 && checked < total
    }

    const total = module.menus.reduce((sum, row) => sum + 1 + row.permissions.length, 0)
    const checked = module.menus.reduce(
      (sum, row) => sum + (row.checked ? 1 : 0) + row.permissions.filter((permission) => permission.checked).length,
      0,
    )
    module.checked = total > 0 && checked === total
    module.indeterminate = checked > 0 && checked < total
  }
}

function findModule(moduleId: string) {
  return matrix.value?.modules.find((module) => module.moduleId === moduleId)
}

function findRow(menuId: string) {
  return matrix.value?.modules.flatMap((module) => module.menus).find((row) => row.menuId === menuId)
}

function getInitialScopeType() {
  return roleDataScopeDraft.value?.scopeType ?? getScopeTypeFromSummary(selectedRow.value?.dataScopeSummary)
}

function getInitialDepartmentIds() {
  return roleDataScopeDraft.value?.departmentIds ?? []
}

function getScopeTypeFromSummary(summary?: string): DataScopeTypeValue {
  const key = (summary ?? '').trim()
  const map: Record<string, DataScopeTypeValue> = {
    All: DataScopeType.All,
    CurrentUser: DataScopeType.CurrentUser,
    CurrentDepartment: DataScopeType.CurrentDepartment,
    CurrentDepartmentAndChildren: DataScopeType.CurrentDepartmentAndChildren,
    CustomDepartments: DataScopeType.CustomDepartments,
  }

  return map[key] ?? DataScopeType.All
}

function getDataScopeText(scopeType: DataScopeTypeValue) {
  const map: Record<DataScopeTypeValue, string> = {
    [DataScopeType.All]: 'All',
    [DataScopeType.CurrentUser]: 'CurrentUser',
    [DataScopeType.CurrentDepartment]: 'CurrentDepartment',
    [DataScopeType.CurrentDepartmentAndChildren]: 'CurrentDepartmentAndChildren',
    [DataScopeType.CustomDepartments]: 'CustomDepartments',
  }

  return map[scopeType]
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :close-on-click-modal="false"
    :title="title"
    class="role-permission-matrix-dialog"
    width="90vw"
    top="5vh"
  >
    <div class="matrix-toolbar">
      <div class="toolbar-left">
        <el-button type="primary" plain @click="selectAll">全选</el-button>
        <el-button plain @click="clearAll">取消全选</el-button>
        <el-button plain @click="expandAll">展开全部</el-button>
        <el-button plain @click="collapseAll">折叠全部</el-button>
        <el-checkbox v-model="onlyChecked">只看已选</el-checkbox>
      </div>
      <el-button :loading="loading" @click="loadMatrix()">刷新</el-button>
    </div>

    <div v-loading="loading" class="matrix-body">
      <template v-if="matrix && matrix.modules.length > 0">
        <PermissionModulePanel
          v-for="module in matrix.modules"
          :key="module.moduleId"
          :module="module"
          :only-checked="onlyChecked"
          @toggle="toggleModule"
          @set-module-checked="setModuleChecked"
          @set-row-checked="setRowChecked"
          @set-permission-checked="setPermissionChecked"
          @open-data-scope="openDataScope"
          @open-field-permission="openFieldPermission"
        />
      </template>
      <el-empty v-else description="暂无权限矩阵数据" />
    </div>

    <template #footer>
      <el-button @click="close">取消</el-button>
      <el-button type="primary" :loading="saving" @click="save">保存</el-button>
    </template>

    <DataScopeDialog
      v-model="dataScopeVisible"
      :tenant-id="tenantId"
      :menu-name="selectedRow?.menuName"
      :scope-type="getInitialScopeType()"
      :department-ids="getInitialDepartmentIds()"
      @save="saveDataScope"
    />
    <FieldPermissionDialog v-model="fieldPermissionVisible" :menu-name="selectedRow?.menuName" />
  </el-dialog>
</template>

<style scoped>
.matrix-toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding-bottom: 14px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.toolbar-left {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 10px;
}

.toolbar-left :deep(.el-button + .el-button) {
  margin-left: 0;
}

.matrix-body {
  display: flex;
  flex-direction: column;
  gap: 14px;
  flex: 1;
  min-height: 0;
  overflow-x: hidden;
  overflow-y: auto;
  padding: 16px 4px 4px;
}

:deep(.role-permission-matrix-dialog) {
  display: flex;
  flex-direction: column;
  height: 90vh;
  max-width: 1280px;
}

:deep(.role-permission-matrix-dialog .el-dialog__body) {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  overflow: hidden;
  padding-top: 12px;
}

:deep(.role-permission-matrix-dialog .el-dialog__footer) {
  flex-shrink: 0;
}
</style>
