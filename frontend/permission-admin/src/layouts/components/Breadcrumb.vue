<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { HomeFilled } from '@element-plus/icons-vue'

const route = useRoute()
const router = useRouter()

const breadcrumbItems = computed(() =>
  route.matched
    .filter((item) => item.name !== 'AdminRoot' && item.path !== '/dashboard')
    .map((item) => ({
      path: item.path,
      title: String(item.meta.title ?? item.name ?? '页面'),
    })),
)

function goDashboard() {
  if (route.path !== '/dashboard') {
    router.push('/dashboard')
  }
}
</script>

<template>
  <el-breadcrumb class="app-breadcrumb" separator="/">
    <el-breadcrumb-item>
      <button class="app-breadcrumb__home" type="button" @click="goDashboard">
        <el-icon><HomeFilled /></el-icon>
        <span>首页</span>
      </button>
    </el-breadcrumb-item>
    <el-breadcrumb-item v-for="item in breadcrumbItems" :key="item.path">
      {{ item.title }}
    </el-breadcrumb-item>
  </el-breadcrumb>
</template>
