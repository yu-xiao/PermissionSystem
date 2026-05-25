<script setup lang="ts">
import { Lock } from '@element-plus/icons-vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { changeMyPassword } from '../../api/me'
import { useAuthStore } from '../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()
const visible = ref(false)
const submitting = ref(false)
const formRef = ref<FormInstance>()

const form = reactive({
  oldPassword: '',
  newPassword: '',
  confirmPassword: '',
})

const passwordPattern = /^(?=.*[A-Za-z])(?=.*\d).{8,}$/

const rules: FormRules = {
  oldPassword: [{ required: true, message: '请输入旧密码', trigger: 'blur' }],
  newPassword: [
    { required: true, message: '请输入新密码', trigger: 'blur' },
    {
      validator: (_rule, value: string, callback) => {
        if (!passwordPattern.test(value)) {
          callback(new Error('新密码至少 8 位，且包含字母和数字'))
          return
        }

        callback()
      },
      trigger: 'blur',
    },
  ],
  confirmPassword: [
    { required: true, message: '请再次输入新密码', trigger: 'blur' },
    {
      validator: (_rule, value: string, callback) => {
        if (value !== form.newPassword) {
          callback(new Error('两次输入的新密码不一致'))
          return
        }

        callback()
      },
      trigger: 'blur',
    },
  ],
}

function open() {
  Object.assign(form, {
    oldPassword: '',
    newPassword: '',
    confirmPassword: '',
  })
  visible.value = true
}

async function submit() {
  await formRef.value?.validate()
  submitting.value = true
  try {
    const result = await changeMyPassword({ ...form })
    authStore.clearSession()
    visible.value = false
    await router.replace('/login')
    ElMessage.success(result.message || '密码修改成功，请重新登录')
  } finally {
    submitting.value = false
  }
}

defineExpose({
  open,
})
</script>

<template>
  <el-dialog v-model="visible" title="修改密码" width="420px" destroy-on-close>
    <el-form ref="formRef" :model="form" :rules="rules" label-width="92px">
      <el-form-item label="旧密码" prop="oldPassword">
        <el-input v-model="form.oldPassword" type="password" show-password autocomplete="current-password" />
      </el-form-item>
      <el-form-item label="新密码" prop="newPassword">
        <el-input v-model="form.newPassword" type="password" show-password autocomplete="new-password" />
      </el-form-item>
      <el-form-item label="确认密码" prop="confirmPassword">
        <el-input v-model="form.confirmPassword" type="password" show-password autocomplete="new-password" />
      </el-form-item>
    </el-form>
    <template #footer>
      <el-button @click="visible = false">取消</el-button>
      <el-button type="primary" :loading="submitting" :icon="Lock" @click="submit">确认修改</el-button>
    </template>
  </el-dialog>
</template>
