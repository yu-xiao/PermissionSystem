<script setup lang="ts">
import { Lock, User } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../../stores/auth'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()
const loading = ref(false)

const form = reactive({
  username: 'admin',
  password: 'admin123456',
})

async function submit() {
  loading.value = true

  try {
    await authStore.login(form.username, form.password)
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/dashboard'
    await router.replace(redirect)
  } catch {
    ElMessage.error('登录失败')
  } finally {
    loading.value = false
  }
}
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
    linear-gradient(135deg, rgba(45, 92, 255, 0.08), rgba(20, 184, 166, 0.08)),
    #f5f7fb;
}

.login-panel {
  width: min(420px, 100%);
  padding: 32px;
  border: 1px solid #d9e2ef;
  border-radius: 8px;
  background: #ffffff;
  box-shadow: 0 18px 42px rgba(15, 23, 42, 0.08);
}

.login-panel__title {
  margin-bottom: 28px;
}

.login-panel__title h1 {
  margin: 0;
  color: #111827;
  font-size: 26px;
  line-height: 1.2;
}

.login-panel__title p {
  margin: 8px 0 0;
  color: #64748b;
  font-size: 14px;
}

.login-panel__form {
  width: 100%;
}

.login-panel__submit {
  width: 100%;
}
</style>
