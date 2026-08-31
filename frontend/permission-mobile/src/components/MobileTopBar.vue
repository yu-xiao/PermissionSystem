<script setup lang="ts">
import { useRouter } from 'vue-router'

const props = withDefaults(
  defineProps<{
    title: string
    showBack?: boolean
    backTo?: string
    actionLabel?: string
    actionIcon?: string
  }>(),
  { showBack: false, backTo: '', actionLabel: '', actionIcon: '' },
)

const emit = defineEmits<{ action: [] }>()
const router = useRouter()

function goBack() {
  if (props.backTo) {
    void router.push(props.backTo)
    return
  }
  router.back()
}
</script>

<template>
  <header class="mobile-topbar">
    <div class="mobile-topbar__inner">
      <button
        v-if="showBack"
        class="icon-button"
        type="button"
        aria-label="返回"
        title="返回"
        @click="goBack"
      >
        <span aria-hidden="true">‹</span>
      </button>
      <span v-else class="mobile-topbar__spacer" aria-hidden="true" />

      <h1 class="mobile-topbar__title">{{ title }}</h1>

      <div class="mobile-topbar__actions">
        <button
          v-if="actionLabel || actionIcon"
          class="icon-button"
          type="button"
          :aria-label="actionLabel || '操作'"
          :title="actionLabel || '操作'"
          @click="emit('action')"
        >
          <span aria-hidden="true">{{ actionIcon || '⋯' }}</span>
        </button>
        <span v-else class="mobile-topbar__spacer" aria-hidden="true" />
      </div>
    </div>
  </header>
</template>

<style scoped>
.mobile-topbar__spacer {
  display: inline-block;
  width: 40px;
  height: 40px;
}

.mobile-topbar__actions,
.mobile-topbar__inner > .icon-button { flex: 0 0 40px; }

.mobile-topbar__title { text-align: center; }
</style>
