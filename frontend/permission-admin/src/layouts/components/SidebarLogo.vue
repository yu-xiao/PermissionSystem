<script setup lang="ts">
import { Lock } from '@element-plus/icons-vue'

defineProps<{
  collapsed: boolean
}>()
</script>

<template>
  <div class="sidebar-logo" :class="{ 'is-collapsed': collapsed }">
    <div class="sidebar-logo__icon">
      <el-icon><Lock /></el-icon>
    </div>
    <div v-show="!collapsed" class="sidebar-logo__text">
      <strong>权限管理系统</strong>
      <span>Permission Admin</span>
    </div>
  </div>
</template>
