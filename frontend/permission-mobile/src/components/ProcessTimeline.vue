<script setup lang="ts">
export interface ProcessStep {
  id?: string
  name?: string
  nodeName?: string
  actorName?: string
  status?: string | number
  statusLabel?: string
  comment?: string
  completedAt?: string
  createdAt?: string
}

defineProps<{ items: ProcessStep[] }>()

function formatDate(value?: string) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}
</script>

<template>
  <div v-if="items.length" class="timeline">
    <div v-for="(step, index) in items" :key="step.id || `${step.name || step.nodeName}-${index}`" class="timeline__item" :class="{ 'timeline__item--done': index < items.length - 1 || step.statusLabel === '已完成' }">
      <div class="timeline__rail"><span class="timeline__dot" /></div>
      <div class="timeline__content">
        <div class="timeline__name">{{ step.name || step.nodeName || '流程节点' }} <span v-if="step.actorName">· {{ step.actorName }}</span></div>
        <div v-if="step.comment" class="timeline__desc">{{ step.comment }}</div>
        <div v-else-if="step.statusLabel" class="timeline__desc">{{ step.statusLabel }}</div>
        <div v-if="step.completedAt || step.createdAt" class="timeline__time">{{ formatDate(step.completedAt || step.createdAt) }}</div>
      </div>
    </div>
  </div>
  <div v-else class="state-box"><span class="state-box__icon" aria-hidden="true">∅</span><span class="state-box__hint">暂无流程轨迹</span></div>
</template>
