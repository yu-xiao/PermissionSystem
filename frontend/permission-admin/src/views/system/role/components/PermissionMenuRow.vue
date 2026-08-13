<script setup lang="ts">
import { computed } from 'vue'
import type { PermissionMenuRow } from '../../../../api/roles'

const props = defineProps<{
  row: PermissionMenuRow
}>()

const emit = defineEmits<{
  setRowChecked: [menuId: string, checked: boolean]
  setPermissionChecked: [menuId: string, permissionId: string, checked: boolean]
  openDataScope: [row: PermissionMenuRow]
}>()

const rowAllChecked = computed(() => {
  if (props.row.permissions.length === 0) {
    return props.row.checked
  }

  return props.row.checked && props.row.permissions.every((permission) => permission.checked)
})

function getPermissionLabel(type: string) {
  const labels: Record<string, string> = {
    view: '查看',
    create: '新增',
    update: '修改',
    delete: '删除',
    import: '导入',
    export: '导出',
    audit: '审核',
    print: '打印',
    attachment: '附件',
    upload: '上传',
    download: '下载',
    trigger: '触发',
    'data-scope': '数据范围',
    'permission-matrix': '权限矩阵',
    'assign-permission': '分配权限',
  }

  return labels[type] ?? (type || '其他')
}
</script>

<template>
  <div class="permission-menu-row">
    <div class="menu-cell">
      <div class="menu-title">{{ row.menuName }}</div>
      <el-checkbox
        :model-value="rowAllChecked"
        :indeterminate="row.indeterminate"
        @change="(value: boolean) => emit('setRowChecked', row.menuId, value)"
      >
        全选
      </el-checkbox>
    </div>

    <div class="permission-cell">
      <el-checkbox
        v-for="permission in row.permissions"
        :key="permission.permissionId"
        :model-value="permission.checked"
        @change="(value: boolean) => emit('setPermissionChecked', row.menuId, permission.permissionId, value)"
      >
        <span class="permission-label">{{ getPermissionLabel(permission.permissionType) }}</span>
        <span class="permission-name">{{ permission.permissionName }}</span>
      </el-checkbox>
      <el-empty v-if="row.permissions.length === 0" description="暂无功能权限" :image-size="42" />
    </div>

    <div class="action-cell">
      <el-link
        :disabled="!row.dataScopeEnabled"
        type="primary"
        underline="never"
        @click="emit('openDataScope', row)"
      >
        数据范围
      </el-link>
    </div>
  </div>
</template>

<style scoped>
.permission-menu-row {
  display: grid;
  grid-template-columns: 210px minmax(0, 1fr) 156px;
  min-height: 72px;
  border-top: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}

.menu-cell {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 8px;
  padding: 12px 16px;
  border-right: 1px solid var(--el-border-color-lighter);
  background: var(--el-fill-color-extra-light);
}

.menu-title {
  overflow: hidden;
  color: var(--el-text-color-primary);
  font-weight: 600;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.permission-cell {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(148px, 1fr));
  align-content: center;
  gap: 8px 12px;
  padding: 12px 16px;
}

.permission-cell :deep(.el-checkbox) {
  display: inline-flex;
  height: 28px;
  min-width: 0;
  margin-right: 0;
}

.permission-cell :deep(.el-checkbox__label) {
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.permission-label {
  color: var(--el-text-color-primary);
}

.permission-name {
  margin-left: 4px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.action-cell {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 14px;
  padding: 12px;
  border-left: 1px solid var(--el-border-color-lighter);
}

@media (max-width: 900px) {
  .permission-menu-row {
    grid-template-columns: 150px minmax(0, 1fr);
  }

  .action-cell {
    grid-column: 1 / -1;
    justify-content: flex-end;
    border-top: 1px solid var(--el-border-color-lighter);
    border-left: 0;
  }
}
</style>
