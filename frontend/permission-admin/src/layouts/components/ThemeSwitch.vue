<script setup lang="ts">
import { Moon, Sunny } from '@element-plus/icons-vue'
import { useAppStore } from '../../stores/app'

const appStore = useAppStore()
</script>

<template>
  <el-tooltip :content="appStore.isDark ? '切换为浅色主题' : '切换为深色主题'" placement="bottom">
    <el-button
      class="header-icon-button"
      text
      :icon="appStore.isDark ? Sunny : Moon"
      @click="appStore.toggleTheme"
    />
  </el-tooltip>
</template>
