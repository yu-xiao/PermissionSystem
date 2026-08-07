<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { sendSensitiveVerification, verifySensitiveOperation } from '../../api/security'

const visible = ref(false)
const loading = ref(false)
const resolving = ref<((ticket: string | undefined) => void) | null>(null)

const form = reactive({
  challengeId: '',
  operationCode: '',
  password: '',
  expiresAt: '',
})

async function open(operationCode: string) {
  form.challengeId = ''
  form.operationCode = operationCode
  form.password = ''
  form.expiresAt = ''
  visible.value = true
  loading.value = true

  try {
    const result = await sendSensitiveVerification({ operationCode })
    form.challengeId = result.challengeId
    form.expiresAt = result.expiresAt
  } catch {
    visible.value = false
    return undefined
  } finally {
    loading.value = false
  }

  return new Promise<string | undefined>((resolve) => {
    resolving.value = resolve
  })
}

async function confirm() {
  if (!form.password) {
    ElMessage.warning('请输入当前登录密码')
    return
  }

  loading.value = true
  try {
    const result = await verifySensitiveOperation({
      challengeId: form.challengeId,
      password: form.password,
    })
    resolving.value?.(result.stepUpTicket)
    resolving.value = null
    visible.value = false
  } finally {
    loading.value = false
  }
}

function cancel() {
  resolving.value?.(undefined)
  resolving.value = null
  visible.value = false
}

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" title="敏感操作二次验证" width="420px" @close="cancel">
    <el-form label-width="110px" @submit.prevent="confirm">
      <el-form-item label="操作编码">
        <el-input v-model="form.operationCode" disabled />
      </el-form-item>
      <el-form-item label="当前密码">
        <el-input
          v-model="form.password"
          type="password"
          show-password
          autocomplete="current-password"
          :disabled="loading"
          @keyup.enter="confirm"
        />
      </el-form-item>
      <el-form-item v-if="form.expiresAt" label="挑战有效期至">
        <el-text>{{ form.expiresAt }}</el-text>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button :disabled="loading" @click="cancel">取消</el-button>
      <el-button type="primary" :loading="loading" @click="confirm">验证</el-button>
    </template>
  </el-dialog>
</template>
