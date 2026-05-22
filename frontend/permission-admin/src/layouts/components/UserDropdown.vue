<script setup lang="ts">
import { ArrowDown, Lock, SwitchButton, User } from '@element-plus/icons-vue'
import { computed } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()

const username = computed(() => authStore.currentUser?.username ?? '管理员')

function handleLogout() {
  authStore.logout()
  router.replace('/login')
}
</script>

<template>
  <el-dropdown trigger="click">
    <button class="user-dropdown" type="button">
      <span class="user-dropdown__avatar">{{ username.slice(0, 1).toUpperCase() }}</span>
      <span class="user-dropdown__name">{{ username }}</span>
      <el-icon><ArrowDown /></el-icon>
    </button>
    <template #dropdown>
      <el-dropdown-menu>
        <el-dropdown-item disabled :icon="User">当前用户：{{ username }}</el-dropdown-item>
        <el-dropdown-item disabled :icon="User">个人中心</el-dropdown-item>
        <el-dropdown-item disabled :icon="Lock">修改密码</el-dropdown-item>
        <el-dropdown-item divided :icon="SwitchButton" @click="handleLogout">退出登录</el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>
