<script setup lang="ts">
import StatusTag from './StatusTag.vue'

export interface TaskCardItem {
  id: string
  instanceId?: string
  businessTitle?: string
  title?: string
  nodeName?: string
  definitionName?: string
  starterUserName?: string
  assignedAt?: string
  completedAt?: string
  dueAt?: string
  status?: string | number
  statusLabel?: string
  statusTone?: 'neutral' | 'primary' | 'success' | 'warning' | 'danger'
}

defineProps<{ item: TaskCardItem; done?: boolean }>()
const emit = defineEmits<{ open: [item: TaskCardItem] }>()

function formatDate(value?: string) {
  if (!value) return '时间未提供'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date)
}
</script>

<template>
  <button class="surface list-card task-card" type="button" @click="emit('open', item)">
    <div class="list-card__top">
      <span class="list-card__title">{{ item.businessTitle || item.title || '未命名任务' }}</span>
      <StatusTag v-if="item.statusLabel" :label="item.statusLabel" :tone="item.statusTone || (done ? 'success' : 'warning')" />
    </div>
    <div class="list-card__meta">
      <span>{{ item.definitionName || '审批流程' }}</span>
      <span>{{ item.nodeName || (done ? '已处理' : '待处理') }}</span>
      <span v-if="item.starterUserName">发起人 {{ item.starterUserName }}</span>
    </div>
    <div class="list-card__footer">
      <span>{{ done ? '处理于' : '到达于' }} {{ formatDate(item.completedAt || item.assignedAt) }}</span>
      <span aria-hidden="true">›</span>
    </div>
  </button>
</template>

<style scoped>
.task-card { width: 100%; padding: 14px; color: inherit; text-align: left; }
.task-card:hover { border-color: color-mix(in srgb, var(--mobile-primary) 35%, var(--mobile-border)); }
</style>
