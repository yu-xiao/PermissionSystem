<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()
const loading = ref(false)
const error = ref('')

async function logoutAll() {
  loading.value = true; error.value = ''
  try { await authStore.logoutAll(); await router.replace('/login') } catch (reason) { error.value = reason instanceof Error ? reason.message : '退出所有设备失败。' } finally { loading.value = false }
}
</script>

<template>
  <section class="sessions-view">
    <div class="surface session-card"><div class="session-card__icon" aria-hidden="true">▣</div><div class="session-card__body"><strong>当前浏览器</strong><span>本设备 · 活跃</span><small>当前会话通过安全令牌保护。</small></div><span class="tag tag--success">当前</span></div>
    <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    <div class="session-note">退出所有设备会撤销其他浏览器上的登录状态。</div>
    <button class="button button--danger button--block" type="button" :disabled="loading" @click="logoutAll">{{ loading ? '处理中…' : '退出所有设备' }}</button>
    <button class="button button--ghost button--block back-button" type="button" @click="router.back()">返回个人中心</button>
  </section>
</template>

<style scoped>
.session-card { display: flex; align-items: center; gap: 11px; padding: 15px; }
.session-card__icon { display: grid; flex: 0 0 auto; width: 38px; height: 38px; place-items: center; border-radius: 10px; color: var(--mobile-primary); background: var(--mobile-primary-soft); font-size: 20px; }
.session-card__body { display: grid; min-width: 0; flex: 1; gap: 3px; }
.session-card__body strong { font-size: 14px; }
.session-card__body span, .session-card__body small { color: var(--mobile-text-secondary); font-size: 11px; }
.session-note { margin: 14px 0; color: var(--mobile-text-secondary); font-size: 12px; line-height: 1.5; }
.inline-error { margin: 12px 0; padding: 9px 10px; border-radius: 8px; color: var(--mobile-danger); background: var(--mobile-danger-soft); font-size: 12px; }
.back-button { margin-top: 9px; }
</style>
