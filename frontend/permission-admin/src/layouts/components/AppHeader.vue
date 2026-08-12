<script setup lang="ts">
import { Expand, Fold } from '@element-plus/icons-vue'
import { ref } from 'vue'
import ChangePasswordDialog from '../../components/ChangePasswordDialog/index.vue'
import NotificationBell from '../../components/NotificationBell.vue'
import Breadcrumb from './Breadcrumb.vue'
import FullscreenToggle from './FullscreenToggle.vue'
import ThemeSwitch from './ThemeSwitch.vue'
import TenantSwitcher from './TenantSwitcher.vue'
import UserDropdown from './UserDropdown.vue'

defineProps<{
  collapsed: boolean
}>()

defineEmits<{
  toggleSidebar: []
}>()

const changePasswordDialogRef = ref<InstanceType<typeof ChangePasswordDialog>>()

function openChangePasswordDialog() {
  changePasswordDialogRef.value?.open()
}
</script>

<template>
  <header class="app-header">
    <div class="app-header__left">
      <el-tooltip :content="collapsed ? '展开菜单' : '折叠菜单'" placement="bottom">
        <el-button
          class="header-icon-button"
          text
          :icon="collapsed ? Expand : Fold"
          @click="$emit('toggleSidebar')"
        />
      </el-tooltip>
      <Breadcrumb />
    </div>

    <div class="app-header__right">
      <FullscreenToggle />
      <ThemeSwitch />
      <TenantSwitcher />
      <NotificationBell />
      <UserDropdown @change-password="openChangePasswordDialog" />
    </div>
  </header>

  <ChangePasswordDialog ref="changePasswordDialogRef" />
</template>
