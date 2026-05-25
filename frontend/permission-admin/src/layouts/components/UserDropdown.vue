<script setup lang="ts">
import { ArrowDown, Lock, SwitchButton, User } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()
const emit = defineEmits<{
  changePassword: []
}>()

const profile = computed(() => authStore.currentProfile)
const displayName = computed(() => profile.value?.nickName || authStore.currentUser?.username || '管理员')
const username = computed(() => profile.value?.userName || authStore.currentUser?.username || 'admin')
const avatarText = computed(() => displayName.value.slice(0, 1).toUpperCase())
const tenantName = computed(() => profile.value?.tenantName || '默认租户')
const roleText = computed(() => profile.value?.roles?.join(' / ') || '未分配角色')

onMounted(() => {
  authStore.loadMyProfile().catch(() => undefined)
})

function goProfile() {
  router.push('/account/profile')
}

function openChangePassword() {
  emit('changePassword')
}

function handleCommand(command: string) {
  if (command === 'profile') {
    goProfile()
    return
  }

  if (command === 'changePassword') {
    openChangePassword()
    return
  }

  if (command === 'logout') {
    handleLogout()
  }
}

async function handleLogout() {
  await ElMessageBox.confirm('确认退出当前登录吗？', '退出登录', {
    confirmButtonText: '退出',
    cancelButtonText: '取消',
    type: 'warning',
  })
  await authStore.logout()
  router.replace('/login')
  ElMessage.success('退出登录成功')
}
</script>

<template>
  <el-dropdown trigger="click" @command="handleCommand">
    <button class="user-dropdown" type="button">
      <span class="user-dropdown__avatar">{{ avatarText }}</span>
      <span class="user-dropdown__name">{{ displayName }}</span>
      <el-icon><ArrowDown /></el-icon>
    </button>
    <template #dropdown>
      <el-dropdown-menu>
        <div class="user-dropdown-card">
          <span class="user-dropdown-card__avatar">{{ avatarText }}</span>
          <div class="user-dropdown-card__meta">
            <strong>{{ displayName }}</strong>
            <span>{{ username }}</span>
            <span>{{ tenantName }} · {{ roleText }}</span>
          </div>
        </div>
        <el-dropdown-item command="profile" :icon="User">个人中心</el-dropdown-item>
        <el-dropdown-item command="changePassword" :icon="Lock">修改密码</el-dropdown-item>
        <el-dropdown-item command="logout" divided :icon="SwitchButton">退出登录</el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<style scoped>
.user-dropdown {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  border: 0;
  background: transparent;
  color: var(--el-text-color-primary);
  cursor: pointer;
}

.user-dropdown__avatar {
  display: inline-flex;
  width: 28px;
  height: 28px;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: var(--el-color-primary-light-8);
  color: var(--el-color-primary);
  font-size: 13px;
  font-weight: 700;
}

.user-dropdown__name {
  max-width: 120px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.user-dropdown-card {
  display: flex;
  gap: 10px;
  min-width: 220px;
  padding: 10px 14px 12px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.user-dropdown-card__avatar {
  display: inline-flex;
  width: 36px;
  height: 36px;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  border-radius: 50%;
  background: var(--el-color-primary-light-8);
  color: var(--el-color-primary);
  font-weight: 700;
}

.user-dropdown-card__meta {
  display: grid;
  min-width: 0;
  gap: 3px;
  color: var(--el-text-color-regular);
  font-size: 12px;
}

.user-dropdown-card__meta strong {
  color: var(--el-text-color-primary);
  font-size: 14px;
}

.user-dropdown-card__meta span {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
