<script setup lang="ts">
defineOptions({
  name: 'SecurityPolicy',
})

import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  getSecurityPolicy,
  updateSecurityPolicy,
  type UpdateSecurityPolicyRequest,
} from '../../../api/security'
import PageContainer from '../../../components/PageContainer/index.vue'
import SensitiveVerificationDialog from '../../../components/SensitiveVerificationDialog/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const loading = ref(false)
const saving = ref(false)
const formRef = ref<FormInstance>()
const sensitiveVerificationRef = ref<InstanceType<typeof SensitiveVerificationDialog>>()

const form = reactive<UpdateSecurityPolicyRequest>({
  passwordMinLength: 8,
  requireDigit: true,
  requireUppercase: false,
  requireLowercase: true,
  requireSpecialChar: false,
  passwordExpireDays: 0,
  loginFailureLockThreshold: 5,
  loginFailureLockMinutes: 15,
  enableMfa: false,
  enableSensitiveOperationVerify: false,
  enableIpWhitelist: false,
  enableIpBlacklist: false,
})

const rules: FormRules = {
  passwordMinLength: [{ required: true, message: '请输入密码最小长度', trigger: 'blur' }],
  loginFailureLockThreshold: [{ required: true, message: '请输入失败锁定阈值', trigger: 'blur' }],
  loginFailureLockMinutes: [{ required: true, message: '请输入锁定分钟数', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    Object.assign(form, await getSecurityPolicy())
  } finally {
    loading.value = false
  }
}

async function save() {
  await formRef.value?.validate()
  const stepUpTicket = await requestSensitiveVerification()
  saving.value = true
  try {
    Object.assign(form, await updateSecurityPolicy(form, stepUpTicket))
    ElMessage.success('保存成功')
  } finally {
    saving.value = false
  }
}

async function requestSensitiveVerification() {
  try {
    const code = await sensitiveVerificationRef.value?.open('security:policy:update')
    if (!code) {
      throw new Error('Sensitive operation verification was cancelled.')
    }

    return code
  } catch (error) {
    if (error instanceof Error && error.message === 'Sensitive operation verification was cancelled.') {
      throw error
    }

    return undefined
  }
}

loadData()
</script>

<template>
  <PageContainer title="安全策略" description="统一维护密码复杂度、登录失败锁定、二次验证和 IP 访问控制。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-skeleton v-if="loading" :rows="8" animated />
    <el-form v-else ref="formRef" :model="form" :rules="rules" label-width="180px" class="policy-form">
      <el-card shadow="never">
        <template #header>密码策略</template>
        <el-form-item label="密码最小长度" prop="passwordMinLength">
          <el-input-number v-model="form.passwordMinLength" :min="6" :max="128" />
        </el-form-item>
        <el-form-item label="密码过期天数">
          <el-input-number v-model="form.passwordExpireDays" :min="0" :max="3650" />
        </el-form-item>
        <el-form-item label="复杂度要求">
          <el-checkbox v-model="form.requireDigit">数字</el-checkbox>
          <el-checkbox v-model="form.requireUppercase">大写字母</el-checkbox>
          <el-checkbox v-model="form.requireLowercase">小写字母</el-checkbox>
          <el-checkbox v-model="form.requireSpecialChar">特殊字符</el-checkbox>
        </el-form-item>
      </el-card>

      <el-card shadow="never">
        <template #header>登录保护</template>
        <el-form-item label="失败锁定阈值" prop="loginFailureLockThreshold">
          <el-input-number v-model="form.loginFailureLockThreshold" :min="1" :max="50" />
        </el-form-item>
        <el-form-item label="锁定分钟数" prop="loginFailureLockMinutes">
          <el-input-number v-model="form.loginFailureLockMinutes" :min="1" :max="1440" />
        </el-form-item>
      </el-card>

      <el-card shadow="never">
        <template #header>安全开关</template>
        <el-form-item label="启用 MFA">
          <el-switch v-model="form.enableMfa" />
        </el-form-item>
        <el-form-item label="敏感操作二次验证">
          <el-switch v-model="form.enableSensitiveOperationVerify" />
        </el-form-item>
        <el-form-item label="启用 IP 白名单">
          <el-switch v-model="form.enableIpWhitelist" />
        </el-form-item>
        <el-form-item label="启用 IP 黑名单">
          <el-switch v-model="form.enableIpBlacklist" />
        </el-form-item>
      </el-card>

      <el-form-item>
        <el-button v-permission="'security:policy:update'" type="primary" :loading="saving" @click="save">
          保存
        </el-button>
      </el-form-item>
    </el-form>

    <SensitiveVerificationDialog ref="sensitiveVerificationRef" />
  </PageContainer>
</template>

<style scoped>
.policy-form {
  max-width: 920px;
}

.policy-form :deep(.el-card) {
  margin-bottom: 16px;
  border-radius: 8px;
}
</style>
