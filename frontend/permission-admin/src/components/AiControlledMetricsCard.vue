<script setup lang="ts">
import type { AiControlledUserMetrics } from '../api/ai'
defineProps<{ metrics: AiControlledUserMetrics }>()
function groupLabel(key: string | null, dimension: AiControlledUserMetrics['dimension']) {
  if (dimension === 'DepartmentId') return key === null ? '未分配部门' : `部门 ${key}`
  return key === 'true' ? '启用' : '停用'
}
</script>

<template>
  <section aria-label="受控用户指标">
    <p>匹配用户总数：{{ metrics.totals.userCount }} 人。</p>
    <p>
      启用：{{ metrics.totals.enabledUserCount }} 人；停用：{{ metrics.totals.disabledUserCount }}
      人。
    </p>
    <dl>
      <template v-for="definition in metrics.definitions" :key="definition.code">
        <dt>{{ definition.name }}（{{ definition.unit }}）</dt>
        <dd>{{ definition.definition }}</dd>
      </template>
    </dl>
    <template v-if="metrics.dimension !== 'None'">
      <p>
        共 {{ metrics.totalGroupCount }} 组，展示 {{ metrics.displayedGroupCount }} 组<span
          v-if="metrics.isTruncated"
          >（已截断，指标总数仍为完整匹配集）</span
        >。
      </p>
      <ul>
        <li v-for="(group, index) in metrics.groups" :key="index">
          {{ groupLabel(group.key, metrics.dimension) }}：总数 {{ group.values.userCount }} 人，启用
          {{ group.values.enabledUserCount }} 人，停用 {{ group.values.disabledUserCount }} 人。
        </li>
      </ul>
    </template>
  </section>
</template>

<style scoped>
dl {
  margin: 12px 0;
}
dd {
  margin-bottom: 8px;
}
ul {
  padding-left: 20px;
  line-height: 1.7;
}
</style>
