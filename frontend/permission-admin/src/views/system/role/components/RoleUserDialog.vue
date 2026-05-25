<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { computed, nextTick, reactive, ref, watch } from 'vue'
import {
  getRoleUsers,
  saveRoleUsers,
  type RoleUserItem,
} from '../../../../api/roles'

const props = defineProps<{
  modelValue: boolean
  roleId?: string
  roleName?: string
}>()

const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  saved: []
}>()

const visible = computed({
  get: () => props.modelValue,
  set: (value: boolean) => emit('update:modelValue', value),
})
const title = computed(() => `为角色【${props.roleName ?? ''}】关联用户`)

const tableRef = ref()
const loading = ref(false)
const saving = ref(false)
const syncingSelection = ref(false)
const userData = ref<RoleUserItem[]>([])
const total = ref(0)
const selectedUserIds = ref<string[]>([])
const query = reactive({ pageIndex: 1, pageSize: 10, keyword: '' })

watch(
  () => [props.modelValue, props.roleId] as const,
  async ([isOpen, roleId]) => {
    if (!isOpen || !roleId) {
      return
    }

    query.pageIndex = 1
    query.keyword = ''
    selectedUserIds.value = []
    await loadUsers(true)
  },
)

async function loadUsers(resetSelection = false) {
  if (!props.roleId) {
    return
  }

  loading.value = true
  try {
    const result = await getRoleUsers(props.roleId, query)
    userData.value = result.users.items
    total.value = result.users.totalCount
    if (resetSelection) {
      selectedUserIds.value = [...result.selectedUserIds]
    }
    await syncTableSelection()
  } finally {
    loading.value = false
  }
}

async function syncTableSelection() {
  await nextTick()
  syncingSelection.value = true
  try {
    tableRef.value?.clearSelection()
    for (const row of userData.value) {
      if (selectedUserIds.value.includes(row.userId)) {
        tableRef.value?.toggleRowSelection(row, true)
      }
    }
  } finally {
    syncingSelection.value = false
  }
}

function handleSelectionChange(rows: RoleUserItem[]) {
  if (syncingSelection.value) {
    return
  }

  const currentPageIds = new Set(userData.value.map((item) => item.userId))
  const selectedOnPageIds = new Set(rows.map((item) => item.userId))
  const nextIds = selectedUserIds.value.filter((id) => !currentPageIds.has(id))

  for (const id of selectedOnPageIds) {
    if (!nextIds.includes(id)) {
      nextIds.push(id)
    }
  }

  selectedUserIds.value = nextIds
}

function isSelectable(row: RoleUserItem) {
  return row.status === 'Enabled' || selectedUserIds.value.includes(row.userId)
}

function search() {
  query.pageIndex = 1
  loadUsers()
}

function reset() {
  query.pageIndex = 1
  query.keyword = ''
  loadUsers()
}

async function save() {
  if (!props.roleId) {
    return
  }

  saving.value = true
  try {
    await saveRoleUsers(props.roleId, { userIds: selectedUserIds.value })
    ElMessage.success('保存成功')
    visible.value = false
    emit('saved')
  } finally {
    saving.value = false
  }
}
</script>

<template>
  <el-dialog
    v-model="visible"
    :close-on-click-modal="false"
    :title="title"
    class="role-user-dialog"
    width="960px"
  >
    <el-form class="dialog-toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input
          v-model="query.keyword"
          clearable
          placeholder="用户名 / 昵称 / 手机号 / 邮箱"
          @keyup.enter="search"
        />
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="search">查询</el-button>
        <el-button @click="reset">重置</el-button>
        <el-button :loading="loading" @click="loadUsers()">刷新</el-button>
      </el-form-item>
    </el-form>

    <el-table
      ref="tableRef"
      v-loading="loading"
      :data="userData"
      border
      row-key="userId"
      max-height="460"
      @selection-change="handleSelectionChange"
    >
      <el-table-column type="selection" width="48" :selectable="isSelectable" />
      <el-table-column prop="userName" label="用户账号" min-width="140" />
      <el-table-column label="用户姓名 / 昵称" min-width="150">
        <template #default="{ row }">
          {{ row.realName || row.nickName || '-' }}
        </template>
      </el-table-column>
      <el-table-column prop="phoneNumber" label="手机号" min-width="130" />
      <el-table-column prop="email" label="邮箱" min-width="180" show-overflow-tooltip />
      <el-table-column prop="departmentName" label="部门" min-width="140" show-overflow-tooltip />
      <el-table-column prop="status" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.status === 'Enabled' ? 'success' : 'info'">
            {{ row.status === 'Enabled' ? '启用' : '禁用' }}
          </el-tag>
        </template>
      </el-table-column>
    </el-table>

    <div class="dialog-footer-bar">
      <span>已选择 {{ selectedUserIds.length }} 人</span>
      <el-pagination
        v-model:current-page="query.pageIndex"
        v-model:page-size="query.pageSize"
        background
        layout="total, sizes, prev, pager, next"
        :total="total"
        @change="loadUsers()"
      />
    </div>

    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="saving" @click="save">保存</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.dialog-toolbar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 12px;
  margin-bottom: 12px;
}

.dialog-toolbar :deep(.el-form-item) {
  margin-right: 0;
  margin-bottom: 0;
}

.dialog-footer-bar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding-top: 14px;
}

:deep(.role-user-dialog) {
  max-width: calc(100vw - 32px);
}
</style>
