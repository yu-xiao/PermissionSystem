<script setup lang="ts">
withDefaults(
  defineProps<{
    title?: string
    description?: string
  }>(),
  {
    title: '暂无数据',
    description: '当前没有可展示的内容，请调整筛选条件后重试。',
  },
)
</script>

<template>
  <el-empty class="empty-state" :description="title">
    <template #description>
      <div class="empty-state__description">
        <strong>{{ title }}</strong>
        <span>{{ description }}</span>
      </div>
    </template>
    <slot />
  </el-empty>
</template>
