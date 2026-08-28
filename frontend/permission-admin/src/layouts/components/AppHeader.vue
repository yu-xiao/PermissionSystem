<script setup lang="ts">
import { ChatDotRound, Expand, Fold } from '@element-plus/icons-vue'
import { computed, ref } from 'vue'
import AiChatDialog from '../../components/AiChatDialog.vue'
import ChangePasswordDialog from '../../components/ChangePasswordDialog/index.vue'
import NotificationBell from '../../components/NotificationBell.vue'
import Breadcrumb from './Breadcrumb.vue'
import FullscreenToggle from './FullscreenToggle.vue'
import ThemeSwitch from './ThemeSwitch.vue'
import TenantSwitcher from './TenantSwitcher.vue'
import UserDropdown from './UserDropdown.vue'
import { useAuthStore } from '../../stores/auth'

defineProps<{
  collapsed: boolean
}>()

defineEmits<{
  toggleSidebar: []
}>()

const changePasswordDialogRef = ref<InstanceType<typeof ChangePasswordDialog>>()
const aiChatDialogRef = ref<InstanceType<typeof AiChatDialog>>()
const authStore = useAuthStore()
const canUseAi = computed(
  () => authStore.hasPermission('ai:chat:use') && authStore.hasPermission('ai:conversation:view'),
)

function openChangePasswordDialog() {
  changePasswordDialogRef.value?.open()
}

function openAiChat() {
  aiChatDialogRef.value?.open()
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
      <el-tooltip v-if="canUseAi" content="AI 中心" placement="bottom">
        <el-button class="header-icon-button" text :icon="ChatDotRound" @click="openAiChat" />
      </el-tooltip>
      <FullscreenToggle />
      <ThemeSwitch />
      <TenantSwitcher />
      <NotificationBell />
      <UserDropdown @change-password="openChangePasswordDialog" />
    </div>
  </header>

  <ChangePasswordDialog ref="changePasswordDialogRef" />
  <AiChatDialog ref="aiChatDialogRef" />
</template>
