<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import EmptyState from '../components/EmptyState/index.vue'
import PageContainer from '../components/PageContainer/index.vue'

defineOptions({
  name: 'RoutePlaceholder',
})

const route = useRoute()
const title = computed(() => String(route.meta.title ?? route.name ?? '页面'))
</script>

<template>
  <PageContainer :title="title" description="该菜单已接入路由，前端页面仍在建设中。">
    <div class="route-placeholder">
      <EmptyState title="页面建设中" description="当前菜单暂未配置对应前端视图，请联系管理员确认菜单组件路径。" />
    </div>
  </PageContainer>
</template>
