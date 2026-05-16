<script setup lang="ts">
import { ArrowDown, Fold, HomeFilled, Lock, Menu as MenuIcon, SwitchButton } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import { usePermissionStore } from '../stores/permission'

const router = useRouter()
const authStore = useAuthStore()
const permissionStore = usePermissionStore()

function handleLogout() {
  authStore.logout()
  router.replace('/login')
}
</script>

<template>
  <el-container class="admin-layout">
    <el-aside class="admin-layout__aside" width="232px">
      <div class="admin-layout__brand">
        <el-icon><Lock /></el-icon>
        <span>权限管理系统</span>
      </div>
      <el-menu class="admin-layout__menu" router default-active="/dashboard">
        <el-menu-item index="/dashboard">
          <el-icon><HomeFilled /></el-icon>
          <span>仪表盘</span>
        </el-menu-item>
        <template v-for="menu in permissionStore.menus" :key="menu.id">
          <el-sub-menu v-if="menu.children?.length" :index="menu.path || menu.id">
            <template #title>
              <el-icon><MenuIcon /></el-icon>
              <span>{{ menu.name }}</span>
            </template>
            <el-menu-item
              v-for="child in menu.children"
              :key="child.id"
              :index="child.path || child.id"
            >
              {{ child.name }}
            </el-menu-item>
          </el-sub-menu>
          <el-menu-item v-else-if="menu.path" :index="menu.path">
            <el-icon><MenuIcon /></el-icon>
            <span>{{ menu.name }}</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="admin-layout__header">
        <el-button text :icon="Fold" />
        <el-dropdown>
          <span class="admin-layout__user">
            {{ authStore.currentUser?.username ?? '管理员' }}
            <el-icon><ArrowDown /></el-icon>
          </span>
          <template #dropdown>
            <el-dropdown-menu>
              <el-dropdown-item :icon="SwitchButton" @click="handleLogout">退出登录</el-dropdown-item>
            </el-dropdown-menu>
          </template>
        </el-dropdown>
      </el-header>

      <el-main class="admin-layout__main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<style scoped>
.admin-layout {
  min-height: 100vh;
}

.admin-layout__aside {
  border-right: 1px solid #d9e2ef;
  background: #ffffff;
}

.admin-layout__brand {
  display: flex;
  align-items: center;
  gap: 10px;
  height: 56px;
  padding: 0 18px;
  border-bottom: 1px solid #d9e2ef;
  color: #111827;
  font-size: 17px;
  font-weight: 650;
}

.admin-layout__menu {
  border-right: 0;
}

.admin-layout__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  border-bottom: 1px solid #d9e2ef;
  background: #ffffff;
}

.admin-layout__user {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #1f2937;
  cursor: pointer;
}

.admin-layout__main {
  padding: 24px;
  background: #f5f7fb;
}
</style>
