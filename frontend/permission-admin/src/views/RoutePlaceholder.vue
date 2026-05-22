<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'

defineOptions({
  name: 'RoutePlaceholder',
})

const route = useRoute()
const title = computed(() => String(route.meta.title ?? route.name ?? '页面'))
</script>

<template>
  <el-empty :description="title" />
</template>
