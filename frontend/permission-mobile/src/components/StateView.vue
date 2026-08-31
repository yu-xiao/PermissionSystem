<script setup lang="ts">
withDefaults(
  defineProps<{
    kind?: 'loading' | 'empty' | 'error'
    title?: string
    hint?: string
    actionLabel?: string
  }>(),
  { kind: 'empty', title: '', hint: '', actionLabel: '' },
)

const emit = defineEmits<{ action: [] }>()
</script>

<template>
  <div class="surface state-box" role="status">
    <span v-if="kind === 'loading'" class="spinner" aria-label="加载中" />
    <span v-else class="state-box__icon" aria-hidden="true">{{ kind === 'error' ? '!' : '∅' }}</span>
    <strong class="state-box__title">{{ title || (kind === 'loading' ? '正在加载' : kind === 'error' ? '加载失败' : '暂无内容') }}</strong>
    <span v-if="hint" class="state-box__hint">{{ hint }}</span>
    <button v-if="actionLabel" class="button button--secondary" type="button" @click="emit('action')">{{ actionLabel }}</button>
  </div>
</template>
