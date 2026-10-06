<script setup lang="ts">
import type { AiStatisticsData } from '../api/ai'
defineProps<{ statistics: AiStatisticsData }>()
const labels: Record<string, string> = {
  byResult: '登录结果',
  byType: '登录类型',
  byStatus: 'HTTP 状态',
  byModule: '操作模块',
}
</script>

<template>
  <section aria-label="日志统计摘要">
    <p>匹配日志总量：{{ statistics.totalCount }} 条。</p>
    <section v-for="group in statistics.groups" :key="group.code" class="statistics-group">
      <strong>{{ labels[group.code] ?? '统计分组' }}</strong>
      <p>
        共 {{ group.totalGroupCount }} 组，展示 {{ group.displayedGroupCount }} 组<span
          v-if="group.isTruncated"
          >（已截断）</span
        >。
      </p>
      <ul>
        <li v-for="(item, index) in group.items" :key="index">
          {{ item.key || '空值' }}：{{ item.count }} 条
        </li>
      </ul>
    </section>
  </section>
</template>

<style scoped>
.statistics-group {
  margin: 12px 0;
}
ul {
  padding-left: 20px;
  line-height: 1.7;
}
</style>
