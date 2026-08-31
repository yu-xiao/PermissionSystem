<script setup lang="ts">
import { ref } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const route = useRoute()
const loading = ref(false)
const error = ref('')
const tenant = ref(import.meta.env.VITE_DEFAULT_TENANT_CODE || '')
const auth = useAuthStore()

async function login() {
  loading.value = true
  error.value = ''
  try {
    if (!tenant.value.trim()) throw new Error('请输入租户代码。')
    const returnPath = typeof route.query.redirect === 'string' ? route.query.redirect : '/home'
    const url = await auth.startLogin({ returnPath, tenant: tenant.value.trim() })
    // The authorization endpoint owns credential entry and consent; no secret is kept in this client.
    window.location.assign(url)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '暂时无法连接认证服务，请稍后重试。'
    loading.value = false
  }
}
</script>

<template>
  <main class="login-shell">
    <section class="login-card" aria-labelledby="login-title">
      <div class="login-brand">
        <div class="login-brand__mark" aria-hidden="true">P</div>
        <div><h1>PermissionSystem</h1><p>移动工作台</p></div>
      </div>
      <h2 id="login-title" class="login-card__title">登录工作台</h2>
      <p class="login-card__hint">使用企业账号完成安全授权后继续。</p>
      <div class="form-field login-card__tenant"><label for="tenant-code">租户代码</label><input id="tenant-code" v-model="tenant" type="text" autocomplete="organization" placeholder="请输入租户代码" @keyup.enter="login" /></div>
      <p v-if="error" class="login-card__error" role="alert">{{ error }}</p>
      <button class="button button--primary button--block" type="button" :disabled="loading" @click="login">
        <span v-if="loading" class="spinner spinner--inverse" aria-hidden="true" />
        <span>{{ loading ? '正在跳转…' : '使用企业账号登录' }}</span>
      </button>
      <p class="login-card__foot">登录后将按当前租户和权限展示可用功能。</p>
      <p v-if="route.query.redirect" class="login-card__redirect">登录后返回原页面</p>
    </section>
  </main>
</template>

<style scoped>
.spinner--inverse { width: 17px; height: 17px; border-color: rgba(255,255,255,.4); border-top-color: #fff; }
.login-card__redirect { margin: 8px 0 0; color: var(--mobile-primary); font-size: 11px; text-align: center; }
.login-card__tenant { margin: 18px 0 12px; text-align: left; }
</style>
