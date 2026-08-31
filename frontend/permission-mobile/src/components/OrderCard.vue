<script setup lang="ts">
import StatusTag from './StatusTag.vue'

export interface OrderCardItem {
  id: string
  orderNo?: string
  title?: string
  customerName?: string
  amount?: number
  approvalStatus?: string | number
  statusLabel?: string
  statusTone?: 'neutral' | 'primary' | 'success' | 'warning' | 'danger'
  createdAt?: string
}

defineProps<{ item: OrderCardItem }>()
const emit = defineEmits<{ open: [id: string] }>()

function formatAmount(value?: number) {
  if (typeof value !== 'number') return '--'
  return new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY', maximumFractionDigits: 2 }).format(value)
}

function formatDate(value?: string) {
  if (!value) return '--'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value
  return new Intl.DateTimeFormat('zh-CN', { month: '2-digit', day: '2-digit' }).format(date)
}
</script>

<template>
  <button class="surface list-card order-card" type="button" @click="emit('open', item.id)">
    <div class="list-card__top">
      <span class="list-card__title">{{ item.title || '未命名单据' }}</span>
      <StatusTag v-if="item.statusLabel" :label="item.statusLabel" :tone="item.statusTone || 'neutral'" />
    </div>
    <div class="list-card__meta">
      <span v-if="item.orderNo">单号 <strong>{{ item.orderNo }}</strong></span>
      <span v-if="item.customerName">客户 {{ item.customerName }}</span>
      <span>金额 <strong>{{ formatAmount(item.amount) }}</strong></span>
    </div>
    <div class="list-card__footer"><span>{{ formatDate(item.createdAt) }}</span><span aria-hidden="true">›</span></div>
  </button>
</template>

<style scoped>
.order-card { width: 100%; padding: 14px; color: inherit; text-align: left; }
.order-card:hover { border-color: color-mix(in srgb, var(--mobile-primary) 35%, var(--mobile-border)); }
</style>
