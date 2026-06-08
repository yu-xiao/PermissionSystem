<script setup lang="ts">
defineOptions({
  name: 'SsoProvider',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import { getRoles, type RoleItem } from '../../../api/roles'
import {
  createSsoProvider,
  deleteSsoProvider,
  disableSsoProvider,
  enableSsoProvider,
  getSsoProvider,
  getSsoProviders,
  SsoProviderType,
  testSsoProvider,
  updateSsoProvider,
  type SaveSsoProviderRequest,
  type SsoProviderDetail,
  type SsoProviderListItem,
} from '../../../api/ssoProvider'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.currentUser?.tenantId ?? '')
const loading = ref(false)
const saving = ref(false)
const testing = ref(false)
const tableData = ref<SsoProviderListItem[]>([])
const roles = ref<RoleItem[]>([])
const total = ref(0)
const dialogVisible = ref(false)
const detailVisible = ref(false)
const formRef = ref<FormInstance>()
const editingId = ref('')
const selectedRoleIds = ref<string[]>([])
const detail = ref<SsoProviderDetail>()

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  providerType: undefined as SsoProviderType | undefined,
  enabled: undefined as boolean | undefined,
})

const form = reactive({
  providerCode: '',
  providerName: '',
  providerType: SsoProviderType.Oidc as SsoProviderType,
  enabled: true,
  authority: '',
  metadataAddress: '',
  clientId: '',
  clientSecret: '',
  scopes: 'openid profile email',
  callbackPath: '/api/sso/oidc/callback',
  responseType: 'code',
  usePkce: true,
  getClaimsFromUserInfoEndpoint: true,
  userIdClaim: 'sub',
  userNameClaim: 'preferred_username',
  emailClaim: 'email',
  phoneClaim: 'phone_number',
  displayNameClaim: 'name',
  roleClaim: 'roles',
  departmentClaim: 'department',
  autoCreateUser: true,
  autoBindUser: true,
  allowLocalLoginFallback: true,
  logoutRedirectUri: '',
  remark: '',
})

const rules: FormRules = {
  providerCode: [{ required: true, message: '请输入 ProviderCode', trigger: 'blur' }],
  providerName: [{ required: true, message: '请输入 ProviderName', trigger: 'blur' }],
  providerType: [{ required: true, message: '请选择 ProviderType', trigger: 'change' }],
  userIdClaim: [{ required: true, message: '请输入用户 ID Claim', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getSsoProviders(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function loadRoles() {
  const result = await getRoles({ pageIndex: 1, pageSize: 500, isEnabled: true })
  roles.value = result.items.filter((role) => !role.isSuperAdminRole && role.code !== 'SuperAdmin')
}

async function openCreate() {
  editingId.value = ''
  selectedRoleIds.value = []
  Object.assign(form, {
    providerCode: '',
    providerName: '',
    providerType: SsoProviderType.Oidc,
    enabled: true,
    authority: '',
    metadataAddress: '',
    clientId: '',
    clientSecret: '',
    scopes: 'openid profile email',
    callbackPath: '/api/sso/oidc/callback',
    responseType: 'code',
    usePkce: true,
    getClaimsFromUserInfoEndpoint: true,
    userIdClaim: 'sub',
    userNameClaim: 'preferred_username',
    emailClaim: 'email',
    phoneClaim: 'phone_number',
    displayNameClaim: 'name',
    roleClaim: 'roles',
    departmentClaim: 'department',
    autoCreateUser: true,
    autoBindUser: true,
    allowLocalLoginFallback: true,
    logoutRedirectUri: '',
    remark: '',
  })
  await loadRoles()
  dialogVisible.value = true
}

async function openEdit(row: SsoProviderListItem) {
  editingId.value = row.id
  await loadRoles()
  const item = await getSsoProvider(row.id)
  selectedRoleIds.value = splitRoleIds(item.defaultRoleIds)
  Object.assign(form, {
    providerCode: item.providerCode,
    providerName: item.providerName,
    providerType: item.providerType,
    enabled: item.enabled,
    authority: item.authority ?? '',
    metadataAddress: item.metadataAddress ?? '',
    clientId: item.clientId ?? '',
    clientSecret: '',
    scopes: item.scopes ?? '',
    callbackPath: item.callbackPath,
    responseType: item.responseType,
    usePkce: item.usePkce,
    getClaimsFromUserInfoEndpoint: item.getClaimsFromUserInfoEndpoint,
    userIdClaim: item.userIdClaim,
    userNameClaim: item.userNameClaim,
    emailClaim: item.emailClaim,
    phoneClaim: item.phoneClaim,
    displayNameClaim: item.displayNameClaim,
    roleClaim: item.roleClaim,
    departmentClaim: item.departmentClaim,
    autoCreateUser: item.autoCreateUser,
    autoBindUser: item.autoBindUser,
    allowLocalLoginFallback: item.allowLocalLoginFallback,
    logoutRedirectUri: item.logoutRedirectUri ?? '',
    remark: item.remark ?? '',
  })
  detail.value = item
  dialogVisible.value = true
}

async function openDetail(row: SsoProviderListItem) {
  detail.value = await getSsoProvider(row.id)
  detailVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    const payload: SaveSsoProviderRequest = {
      ...form,
      tenantId: tenantId.value,
      defaultRoleIds: selectedRoleIds.value.join(','),
    }
    if (!payload.clientSecret) {
      delete payload.clientSecret
    }

    if (editingId.value) {
      await updateSsoProvider(editingId.value, payload)
    } else {
      await createSsoProvider(payload)
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: SsoProviderListItem) {
  await ElMessageBox.confirm(`确认删除 SSO Provider ${row.providerName}？`, '确认删除')
  await deleteSsoProvider(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function toggle(row: SsoProviderListItem) {
  if (row.enabled) {
    await disableSsoProvider(row.id)
  } else {
    await enableSsoProvider(row.id)
  }
  ElMessage.success(row.enabled ? '已禁用' : '已启用')
  await loadData()
}

async function test(row?: SsoProviderListItem) {
  testing.value = true
  try {
    const result = await testSsoProvider(row?.id ?? editingId.value, {
      authority: form.authority,
      metadataAddress: form.metadataAddress,
      clientId: form.clientId,
      clientSecret: form.clientSecret,
    })
    ElMessage[result.succeeded ? 'success' : 'warning'](result.message)
  } finally {
    testing.value = false
  }
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    providerType: undefined,
    enabled: undefined,
  })
  loadData()
}

function splitRoleIds(value?: string) {
  if (!value) {
    return []
  }

  return value.split(/[,\s;|]+/).filter(Boolean)
}

function providerTypeText(value: SsoProviderType) {
  return value === SsoProviderType.Oidc ? 'OIDC' : value === SsoProviderType.Saml ? 'SAML2' : 'OAuth2'
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="SSO 提供方" description="维护外部统一身份源配置，当前优先支持 OIDC。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="ProviderCode / 名称" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.providerType" clearable placeholder="类型" style="width: 130px">
          <el-option label="OIDC" :value="SsoProviderType.Oidc" />
          <el-option label="SAML2" :value="SsoProviderType.Saml" />
          <el-option label="OAuth2" :value="SsoProviderType.OAuth2" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.enabled" clearable placeholder="状态" style="width: 120px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'sso:provider:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'sso:provider:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="providerCode" label="ProviderCode" min-width="150" />
      <el-table-column prop="providerName" label="名称" min-width="160" />
      <el-table-column label="类型" width="100">
        <template #default="{ row }">{{ providerTypeText(row.providerType) }}</template>
      </el-table-column>
      <el-table-column prop="authority" label="Authority" min-width="220" show-overflow-tooltip />
      <el-table-column prop="scopes" label="Scopes" min-width="180" show-overflow-tooltip />
      <el-table-column label="PKCE" width="90">
        <template #default="{ row }">
          <el-tag :type="row.usePkce ? 'success' : 'info'">{{ row.usePkce ? '启用' : '关闭' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.enabled ? 'success' : 'info'">{{ row.enabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="300" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'sso:provider:view'" link type="primary" @click="openDetail(row)">详情</el-button>
          <el-button v-permission="'sso:provider:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
          <el-button
            v-permission="row.enabled ? 'sso:provider:disable' : 'sso:provider:enable'"
            link
            @click="toggle(row)"
          >
            {{ row.enabled ? '禁用' : '启用' }}
          </el-button>
          <el-button v-permission="'sso:provider:test'" link type="success" @click="test(row)">测试</el-button>
          <el-button v-permission="'sso:provider:delete'" link type="danger" @click="remove(row)">删除</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-pagination
      v-model:current-page="query.pageIndex"
      v-model:page-size="query.pageSize"
      class="pager"
      background
      layout="total, sizes, prev, pager, next"
      :total="total"
      @change="loadData"
    />

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 SSO Provider' : '新增 SSO Provider'" width="920px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="150px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="ProviderCode" prop="providerCode">
              <el-input v-model="form.providerCode" :disabled="Boolean(editingId)" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="ProviderName" prop="providerName">
              <el-input v-model="form.providerName" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="ProviderType" prop="providerType">
              <el-select v-model="form.providerType" class="full-width">
                <el-option label="OIDC" :value="SsoProviderType.Oidc" />
                <el-option label="SAML2" :value="SsoProviderType.Saml" />
                <el-option label="OAuth2" :value="SsoProviderType.OAuth2" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="Enabled">
              <el-switch v-model="form.enabled" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="Authority">
              <el-input v-model="form.authority" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="MetadataAddress">
              <el-input v-model="form.metadataAddress" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="ClientId">
              <el-input v-model="form.clientId" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="ClientSecret">
              <el-input
                v-model="form.clientSecret"
                type="password"
                show-password
                :placeholder="editingId && detail?.hasClientSecret ? '已设置，留空则不变' : '请输入 ClientSecret'"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="Scopes">
              <el-input v-model="form.scopes" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="CallbackPath">
              <el-input v-model="form.callbackPath" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="ResponseType">
              <el-input v-model="form.responseType" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="默认角色">
              <el-select v-model="selectedRoleIds" multiple filterable clearable class="full-width">
                <el-option v-for="role in roles" :key="role.id" :label="`${role.name} (${role.code})`" :value="role.id" />
              </el-select>
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="UserIdClaim" prop="userIdClaim">
              <el-input v-model="form.userIdClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="UserNameClaim">
              <el-input v-model="form.userNameClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="EmailClaim">
              <el-input v-model="form.emailClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="PhoneClaim">
              <el-input v-model="form.phoneClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="DisplayNameClaim">
              <el-input v-model="form.displayNameClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="RoleClaim">
              <el-input v-model="form.roleClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="DepartmentClaim">
              <el-input v-model="form.departmentClaim" />
            </el-form-item>
          </el-col>
          <el-col :span="24">
            <el-form-item label="策略">
              <el-space wrap>
                <el-checkbox v-model="form.usePkce">UsePkce</el-checkbox>
                <el-checkbox v-model="form.getClaimsFromUserInfoEndpoint">UserInfo</el-checkbox>
                <el-checkbox v-model="form.autoCreateUser">AutoCreateUser</el-checkbox>
                <el-checkbox v-model="form.autoBindUser">AutoBindUser</el-checkbox>
                <el-checkbox v-model="form.allowLocalLoginFallback">AllowLocalLoginFallback</el-checkbox>
              </el-space>
            </el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button v-permission="'sso:provider:test'" :disabled="!editingId" :loading="testing" @click="test()">测试</el-button>
        <el-button v-permission="editingId ? 'sso:provider:update' : 'sso:provider:create'" type="primary" :loading="saving" @click="save">
          保存
        </el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="detailVisible" title="SSO Provider 详情" width="760px">
      <el-descriptions v-if="detail" :column="2" border>
        <el-descriptions-item label="ProviderCode">{{ detail.providerCode }}</el-descriptions-item>
        <el-descriptions-item label="ProviderName">{{ detail.providerName }}</el-descriptions-item>
        <el-descriptions-item label="类型">{{ providerTypeText(detail.providerType) }}</el-descriptions-item>
        <el-descriptions-item label="状态">{{ detail.enabled ? '启用' : '禁用' }}</el-descriptions-item>
        <el-descriptions-item label="ClientId">{{ detail.clientId || '-' }}</el-descriptions-item>
        <el-descriptions-item label="ClientSecret">{{ detail.clientSecret || '-' }}</el-descriptions-item>
        <el-descriptions-item label="Authority" :span="2">{{ detail.authority || '-' }}</el-descriptions-item>
        <el-descriptions-item label="MetadataAddress" :span="2">{{ detail.metadataAddress || '-' }}</el-descriptions-item>
        <el-descriptions-item label="Scopes" :span="2">{{ detail.scopes || '-' }}</el-descriptions-item>
        <el-descriptions-item label="Claims" :span="2">
          {{ detail.userIdClaim }} / {{ detail.userNameClaim }} / {{ detail.emailClaim }} / {{ detail.roleClaim }} /
          {{ detail.departmentClaim }}
        </el-descriptions-item>
      </el-descriptions>
    </el-dialog>
  </PageContainer>
</template>

<style scoped>
.full-width {
  width: 100%;
}
</style>
