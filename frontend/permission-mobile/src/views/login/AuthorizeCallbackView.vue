<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'
import { clearPkceTransaction, readPkceTransaction, validateReturnPath } from '../../utils/pkce'

const route = useRoute()
const router = useRouter()
const error = ref('')
const loading = ref(true)
const auth = useAuthStore()

onMounted(async () => {
  const callbackError = typeof route.query.error === 'string' ? route.query.error : ''
  if (callbackError) {
    clearPkceTransaction()
    error.value = typeof route.query.error_description === 'string' ? route.query.error_description : '授权未完成。'
    loading.value = false
    return
  }
  try {
    const transaction = readPkceTransaction()
    if (!transaction) throw new Error('登录状态已过期，请重新开始登录。')
    await auth.handleAuthorizationCallback(new URLSearchParams(window.location.search))
    await router.replace(validateReturnPath(transaction.returnPath))
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '授权回调无效，请重新登录。'
    loading.value = false
  }
})
</script>

<template>
  <main class="login-shell">
    <section class="login-card callback-card" aria-live="polite">
      <div v-if="loading" class="state-box callback-state"><span class="spinner" /><strong class="state-box__title">正在完成登录</strong><span class="state-box__hint">请稍候，不要关闭页面。</span></div>
      <div v-else class="state-box callback-state"><span class="state-box__icon" aria-hidden="true">!</span><strong class="state-box__title">登录未完成</strong><span class="state-box__hint">{{ error }}</span><button class="button button--secondary" type="button" @click="router.replace('/login')">返回登录</button></div>
    </section>
  </main>
</template>

<style scoped>
.callback-card { padding: 10px; }
.callback-state { min-height: 230px; border: 0; box-shadow: none; }
</style>
