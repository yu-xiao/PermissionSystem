<script setup lang="ts">
defineOptions({
  name: 'AccountProfile',
})

import { RefreshLeft, SwitchButton } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getMyProfile, logoutAll, updateMyProfile, type MyProfileResponse } from '../../../api/me'
import PageContainer from '../../../components/PageContainer/index.vue'
import { useAuthStore } from '../../../stores/auth'

const router = useRouter()
const authStore = useAuthStore()
const loading = ref(false)
const saving = ref(false)
const profile = ref<MyProfileResponse>()
const formRef = ref<FormInstance>()

const form = reactive({
  nickName: '',
  realName: '',
  avatar: '',
  email: '',
  phoneNumber: '',
})

const rules: FormRules = {
  nickName: [{ required: true, message: '请输入昵称', trigger: 'blur' }],
  email: [{ type: 'email', message: '请输入正确的邮箱地址', trigger: 'blur' }],
}

const avatarText = computed(() => (form.nickName || profile.value?.userName || 'U').slice(0, 1).toUpperCase())
const roleText = computed(() => profile.value?.roles.join(' / ') || '-')
const permissionCount = computed(() => profile.value?.permissions.length ?? 0)

onMounted(loadProfile)

async function loadProfile() {
  loading.value = true
  try {
    profile.value = await getMyProfile()
    Object.assign(form, {
      nickName: profile.value.nickName,
      realName: profile.value.realName,
      avatar: profile.value.avatar ?? '',
      email: profile.value.email ?? '',
      phoneNumber: profile.value.phoneNumber ?? '',
    })
  } finally {
    loading.value = false
  }
}

async function saveProfile() {
  await formRef.value?.validate()
  saving.value = true
  try {
    profile.value = await updateMyProfile({
      nickName: form.nickName,
      realName: form.realName,
      avatar: form.avatar,
      email: form.email,
      phoneNumber: form.phoneNumber,
    })
    await authStore.loadMyProfile()
    ElMessage.success('个人资料已保存')
  } finally {
    saving.value = false
  }
}

async function handleLogoutAll() {
  await ElMessageBox.confirm('确认退出所有设备吗？当前会话也会立即失效。', '退出所有设备', {
    confirmButtonText: '退出所有设备',
    cancelButtonText: '取消',
    type: 'warning',
  })
  await logoutAll()
  authStore.clearSession()
  await router.replace('/login')
  ElMessage.success('已退出所有设备，请重新登录')
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}
</script>

<template>
  <PageContainer title="个人中心" description="查看并维护当前登录用户的基础资料。">
    <template #actions>
      <el-button :icon="RefreshLeft" @click="loadProfile">刷新</el-button>
      <el-button type="danger" plain :icon="SwitchButton" @click="handleLogoutAll">退出所有设备</el-button>
    </template>

    <div v-loading="loading" class="profile-page">
      <section class="profile-summary">
        <span class="profile-summary__avatar">{{ avatarText }}</span>
        <div class="profile-summary__main">
          <h2>{{ profile?.nickName || profile?.userName || '-' }}</h2>
          <p>{{ profile?.tenantName || '-' }} · {{ roleText }}</p>
        </div>
      </section>

      <el-row :gutter="16">
        <el-col :xs="24" :lg="10">
          <el-descriptions class="profile-descriptions" :column="1" border>
            <el-descriptions-item label="用户名">{{ profile?.userName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="租户">{{ profile?.tenantName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="部门">{{ profile?.departmentName || '-' }}</el-descriptions-item>
            <el-descriptions-item label="角色">{{ roleText }}</el-descriptions-item>
            <el-descriptions-item label="权限数">{{ permissionCount }}</el-descriptions-item>
            <el-descriptions-item label="最近登录">{{ formatDate(profile?.lastLoginTime) }}</el-descriptions-item>
            <el-descriptions-item label="创建时间">{{ formatDate(profile?.createdAt) }}</el-descriptions-item>
          </el-descriptions>
        </el-col>

        <el-col :xs="24" :lg="14">
          <el-form ref="formRef" class="profile-form" :model="form" :rules="rules" label-width="88px">
            <el-form-item label="昵称" prop="nickName">
              <el-input v-model="form.nickName" maxlength="128" show-word-limit />
            </el-form-item>
            <el-form-item label="真实姓名" prop="realName">
              <el-input v-model="form.realName" maxlength="128" show-word-limit />
            </el-form-item>
            <el-form-item label="头像 URL" prop="avatar">
              <el-input v-model="form.avatar" maxlength="512" clearable />
            </el-form-item>
            <el-form-item label="邮箱" prop="email">
              <el-input v-model="form.email" maxlength="256" clearable />
            </el-form-item>
            <el-form-item label="手机号" prop="phoneNumber">
              <el-input v-model="form.phoneNumber" maxlength="32" clearable />
            </el-form-item>
            <el-form-item>
              <el-button type="primary" :loading="saving" @click="saveProfile">保存资料</el-button>
            </el-form-item>
          </el-form>
        </el-col>
      </el-row>
    </div>
  </PageContainer>
</template>

<style scoped>
.profile-page {
  display: grid;
  gap: 16px;
}

.profile-summary {
  display: flex;
  align-items: center;
  gap: 14px;
  padding-bottom: 16px;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.profile-summary__avatar {
  display: inline-flex;
  width: 56px;
  height: 56px;
  align-items: center;
  justify-content: center;
  flex: 0 0 auto;
  border-radius: 50%;
  background: var(--el-color-primary-light-8);
  color: var(--el-color-primary);
  font-size: 22px;
  font-weight: 700;
}

.profile-summary__main {
  min-width: 0;
}

.profile-summary__main h2 {
  margin: 0 0 4px;
  font-size: 18px;
  font-weight: 700;
}

.profile-summary__main p {
  margin: 0;
  color: var(--el-text-color-secondary);
}

.profile-descriptions,
.profile-form {
  width: 100%;
}

@media (max-width: 1200px) {
  .profile-form {
    margin-top: 16px;
  }
}
</style>
