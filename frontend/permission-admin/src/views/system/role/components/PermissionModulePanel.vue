<script setup lang="ts">
import { computed } from 'vue'
import type { PermissionMenuRow, PermissionModule } from '../../../../api/roles'
import PermissionMenuRowPanel from './PermissionMenuRow.vue'

const props = defineProps<{
  module: PermissionModule
  onlyChecked: boolean
}>()

const emit = defineEmits<{
  toggle: [moduleId: string]
  setModuleChecked: [moduleId: string, checked: boolean]
  setRowChecked: [menuId: string, checked: boolean]
  setPermissionChecked: [menuId: string, permissionId: string, checked: boolean]
  openDataScope: [row: PermissionMenuRow]
}>()

const visibleMenus = computed(() => {
  if (!props.onlyChecked) {
    return props.module.menus
  }

  return props.module.menus.filter(
    (row) => row.checked || row.indeterminate || row.permissions.some((permission) => permission.checked),
  )
})
</script>

<template>
  <section v-if="visibleMenus.length > 0" class="permission-module">
    <header class="module-header">
      <div class="module-title">
        <el-checkbox
          :model-value="module.checked"
          :indeterminate="module.indeterminate"
          @change="(value: boolean) => emit('setModuleChecked', module.moduleId, value)"
        />
        <span>{{ module.moduleName }}</span>
      </div>
      <el-button link type="primary" @click="emit('toggle', module.moduleId)">
        {{ module.expanded ? '收起' : '展开' }}
      </el-button>
    </header>

    <div v-show="module.expanded" class="module-body">
      <PermissionMenuRowPanel
        v-for="row in visibleMenus"
        :key="row.menuId"
        :row="row"
        @set-row-checked="(menuId, checked) => emit('setRowChecked', menuId, checked)"
        @set-permission-checked="
          (menuId, permissionId, checked) => emit('setPermissionChecked', menuId, permissionId, checked)
        "
        @open-data-scope="(menuRow) => emit('openDataScope', menuRow)"
      />
    </div>
  </section>
</template>

<style scoped>
.permission-module {
  overflow: hidden;
  border: 1px solid var(--el-border-color);
  border-radius: 8px;
  background: var(--el-bg-color);
}

.module-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 48px;
  padding: 0 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-fill-color-light);
}

.module-title {
  display: flex;
  align-items: center;
  min-width: 0;
  gap: 10px;
  color: var(--el-text-color-primary);
  font-weight: 700;
}

.module-title span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.module-body {
  background: var(--el-bg-color);
}
</style>
