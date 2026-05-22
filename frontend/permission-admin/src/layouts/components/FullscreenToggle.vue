<script setup lang="ts">
import { FullScreen } from '@element-plus/icons-vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'

const fullscreen = ref(false)

function syncFullscreenState() {
  fullscreen.value = Boolean(document.fullscreenElement)
}

async function toggleFullscreen() {
  if (document.fullscreenElement) {
    await document.exitFullscreen()
    return
  }

  await document.documentElement.requestFullscreen()
}

onMounted(() => {
  syncFullscreenState()
  document.addEventListener('fullscreenchange', syncFullscreenState)
})

onBeforeUnmount(() => {
  document.removeEventListener('fullscreenchange', syncFullscreenState)
})
</script>

<template>
  <el-tooltip :content="fullscreen ? '退出全屏' : '全屏显示'" placement="bottom">
    <el-button class="header-icon-button" text :icon="FullScreen" @click="toggleFullscreen" />
  </el-tooltip>
</template>
