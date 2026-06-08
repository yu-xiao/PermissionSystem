<script setup lang="ts">
import { ElMessage } from 'element-plus'
import { reactive, ref } from 'vue'
import { sendSensitiveVerification } from '../../api/security'

const visible = ref(false)
const sending = ref(false)
const resolving = ref<((code: string | undefined) => void) | null>(null)

const form = reactive({
  operationCode: '',
  verifyCode: '',
  expiresAt: '',
})

async function open(operationCode: string) {
  form.operationCode = operationCode
  form.verifyCode = ''
  form.expiresAt = ''
  visible.value = true
  sending.value = true
  try {
    const result = await sendSensitiveVerification({ operationCode })
    form.expiresAt = result.expiresAt
    ElMessage.success(result.deliveryMessage || '验证码已发送，请完成二次验证')
  } finally {
    sending.value = false
  }

  return new Promise<string | undefined>((resolve) => {
    resolving.value = resolve
  })
}

function confirm() {
  if (!form.verifyCode.trim()) {
    ElMessage.warning('请输入验证码')
    return
  }

  resolving.value?.(form.verifyCode.trim())
  resolving.value = null
  visible.value = false
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
    <el-form label-width="110px">
      <el-form-item label="操作编码">
        <el-input v-model="form.operationCode" disabled />
      </el-form-item>
      <el-form-item label="验证码">
        <el-input v-model="form.verifyCode" :disabled="sending" maxlength="6" />
      </el-form-item>
      <el-form-item v-if="form.expiresAt" label="有效期至">
        <el-text>{{ form.expiresAt }}</el-text>
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="cancel">取消</el-button>
      <el-button type="primary" :loading="sending" @click="confirm">确认</el-button>
    </template>
  </el-dialog>
</template>
