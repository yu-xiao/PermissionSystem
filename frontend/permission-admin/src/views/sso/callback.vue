<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { exchangeSsoLoginCode } from '../../api/ssoAuth'
import { useAuthStore } from '../../stores/auth'
import { setTokens } from '../../utils/token'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const message = ref('正在完成 SSO 登录...')

onMounted(async () => {
  const error = typeof route.query.error === 'string' ? route.query.error : ''
  const loginCode = typeof route.query.login_code === 'string' ? route.query.login_code : ''
  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'

  if (error || !loginCode) {
    ElMessage.error(error || 'SSO 登录失败')
    await router.replace('/login')
    return
  }

  try {
    const token = await exchangeSsoLoginCode(loginCode)
    setTokens({
      accessToken: token.access_token,
      refreshToken: token.refresh_token,
    })
    await authStore.loadCurrentUser()
    await router.replace(redirect)
  } catch {
    message.value = 'SSO 登录失败'
    ElMessage.error('SSO 登录失败，请重新登录')
    await router.replace('/login')
  }
})
</script>

<template>
  <main class="sso-callback">
    <el-result icon="info" title="SSO 登录" :sub-title="message" />
  </main>
</template>

<style scoped>
.sso-callback {
  display: grid;
  min-height: 100vh;
  place-items: center;
  background: var(--app-bg);
}
</style>
