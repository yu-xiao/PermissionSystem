<script setup lang="ts">
import { Connection, Lock, User } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { onMounted, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { challengeOidc } from '../../api/ssoAuth'
import { getEnabledSsoProviders, SsoProviderType, type SsoProviderListItem } from '../../api/ssoProvider'
import { useAuthStore } from '../../stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const loading = ref(false)
const ssoLoading = ref('')
const providers = ref<SsoProviderListItem[]>([])

const form = reactive({
  username: 'admin',
  password: '',
})

async function submit() {
  loading.value = true

  try {
    await authStore.login(form.username, form.password)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
    await router.replace(redirect)
  } catch {
    ElMessage.error('登录失败，请检查用户名和密码后重试')
  } finally {
    loading.value = false
  }
}

async function loadSsoProviders() {
  try {
    providers.value = (await getEnabledSsoProviders()).filter((provider) => provider.providerType === SsoProviderType.Oidc)
  } catch {
    providers.value = []
  }
}

async function startSsoLogin(provider: SsoProviderListItem) {
  ssoLoading.value = provider.providerCode
  try {
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
    const callbackReturnUrl = `/sso/callback?redirect=${encodeURIComponent(redirect)}`
    const result = await challengeOidc(provider.providerCode, callbackReturnUrl)
    window.location.href = result.redirectUrl
  } finally {
    ssoLoading.value = ''
  }
}

onMounted(loadSsoProviders)
</script>

<template>
  <main class="login-page">
    <section class="login-panel">
      <div class="login-panel__title">
        <h1>权限管理系统</h1>
        <p>企业级权限管理平台</p>
      </div>

      <el-form class="login-panel__form" @submit.prevent="submit">
        <el-form-item>
          <el-input v-model="form.username" size="large" placeholder="用户名" :prefix-icon="User" />
        </el-form-item>
        <el-form-item>
          <el-input
            v-model="form.password"
            size="large"
            type="password"
            show-password
            placeholder="密码"
            :prefix-icon="Lock"
            @keyup.enter="submit"
          />
        </el-form-item>
        <el-button class="login-panel__submit" size="large" type="primary" :loading="loading" @click="submit">
          登录
        </el-button>
      </el-form>

      <div v-if="providers.length > 0" class="sso-login">
        <el-divider>SSO 登录</el-divider>
        <el-button
          v-for="provider in providers"
          :key="provider.id"
          class="sso-login__button"
          size="large"
          :icon="Connection"
          :loading="ssoLoading === provider.providerCode"
          @click="startSsoLogin(provider)"
        >
          {{ provider.providerName }}
        </el-button>
      </div>
    </section>
  </main>
</template>

<style scoped>
.login-page {
  display: grid;
  min-height: 100vh;
  place-items: center;
  padding: 24px;
  background:
    linear-gradient(135deg, color-mix(in srgb, var(--app-primary) 10%, transparent), rgba(20, 184, 166, 0.08)),
    var(--app-bg);
}

.login-panel {
  width: min(420px, 100%);
  padding: 32px;
  border: 1px solid #d9e2ef;
  border-radius: 8px;
  background: var(--app-surface);
  box-shadow: var(--app-shadow-soft);
}

.login-panel__title {
  margin-bottom: 28px;
}

.login-panel__title h1 {
  margin: 0;
  color: var(--app-text);
  font-size: 26px;
  line-height: 1.2;
}

.login-panel__title p {
  margin: 8px 0 0;
  color: var(--app-text-secondary);
  font-size: 14px;
}

.login-panel__form {
  width: 100%;
}

.login-panel__submit,
.sso-login__button {
  width: 100%;
}

.sso-login {
  margin-top: 20px;
}

.sso-login__button + .sso-login__button {
  margin-top: 10px;
}
</style>
