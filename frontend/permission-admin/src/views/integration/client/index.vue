<script setup lang="ts">
defineOptions({
  name: 'IntegrationClient',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { MoreFilled } from '@element-plus/icons-vue'
import { reactive, ref } from 'vue'
import {
  createApiClient,
  deleteApiClient,
  disableApiClient,
  enableApiClient,
  generateApiClientSecret,
  getApiClients,
  updateApiClient,
  type ApiClientItem,
  type CreateApiClientRequest,
} from '../../../api/integration'
import PageContainer from '../../../components/PageContainer/index.vue'
import SensitiveVerificationDialog from '../../../components/SensitiveVerificationDialog/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const secretDialogVisible = ref(false)
const editingId = ref('')
const editingClient = ref<ApiClientItem | null>(null)
const formRef = ref<FormInstance>()
const sensitiveVerificationRef = ref<InstanceType<typeof SensitiveVerificationDialog>>()
const tableData = ref<ApiClientItem[]>([])
const total = ref(0)
const generatedSecret = ref({ apiKey: '', apiSecret: '' })

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  isEnabled: undefined as boolean | undefined,
})

const form = reactive<CreateApiClientRequest>({
  clientCode: '',
  clientName: '',
  description: '',
  isEnabled: true,
  allowedScopes: '',
  allowedIpList: '',
  rateLimitPerMinute: 60,
})

const rules: FormRules = {
  clientCode: [{ required: true, message: '请输入客户端编码', trigger: 'blur' }],
  clientName: [{ required: true, message: '请输入客户端名称', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getApiClients(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  editingClient.value = null
  Object.assign(form, {
    clientCode: '',
    clientName: '',
    description: '',
    isEnabled: true,
    allowedScopes: '',
    allowedIpList: '',
    rateLimitPerMinute: 60,
  })
  dialogVisible.value = true
}

function openEdit(row: ApiClientItem) {
  editingId.value = row.id
  editingClient.value = row
  Object.assign(form, {
    clientCode: row.clientCode,
    clientName: row.clientName,
    description: row.description ?? '',
    isEnabled: row.isEnabled,
    allowedScopes: row.allowedScopes ?? '',
    allowedIpList: row.allowedIpList ?? '',
    rateLimitPerMinute: row.rateLimitPerMinute,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  const stepUpTicket = await requestSensitiveVerification(
    editingId.value ? 'integration:client:update' : 'integration:client:create',
  )
  saving.value = true
  try {
    if (editingId.value) {
      await updateApiClient(editingId.value, {
        clientName: form.clientName,
        description: form.description,
        allowedScopes: form.allowedScopes,
        allowedIpList: form.allowedIpList,
        rateLimitPerMinute: form.rateLimitPerMinute,
        concurrencyToken: editingClient.value?.concurrencyToken,
      }, stepUpTicket)
    } else {
      await createApiClient(form, stepUpTicket)
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: ApiClientItem) {
  await ElMessageBox.confirm(`确认删除 API 客户端 ${row.clientName}？`, '确认删除')
  const stepUpTicket = await requestSensitiveVerification('integration:client:delete')
  await deleteApiClient(row.id, stepUpTicket)
  ElMessage.success('删除成功')
  await loadData()
}

async function toggle(row: ApiClientItem) {
  const stepUpTicket = await requestSensitiveVerification(
    row.isEnabled ? 'integration:client:disable' : 'integration:client:enable',
  )
  if (row.isEnabled) {
    await disableApiClient(row.id, stepUpTicket)
  } else {
    await enableApiClient(row.id, stepUpTicket)
  }

  await loadData()
}

async function generateSecret(row: ApiClientItem) {
  const stepUpTicket = await requestSensitiveVerification('integration:client:secret')
  const result = await generateApiClientSecret(row.id, stepUpTicket)
  generatedSecret.value = {
    apiKey: result.apiKey,
    apiSecret: result.apiSecret,
  }
  secretDialogVisible.value = true
}

function hasMoreClientActions() {
  return (
    authStore.hasPermission('integration:client:update') ||
    authStore.hasPermission('integration:client:secret') ||
    authStore.hasPermission('integration:client:delete')
  )
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    isEnabled: undefined,
  })
  loadData()
}

async function requestSensitiveVerification(operationCode: string) {
  const code = await sensitiveVerificationRef.value?.open(operationCode)
  if (!code) {
    throw new Error('Sensitive operation verification was cancelled.')
  }

  return code
}

loadData()
</script>

<template>
  <PageContainer title="API 客户端" description="维护外部系统 API Key、Scope、IP 白名单和限流策略。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input v-model="query.keyword" clearable placeholder="编码 / 名称" />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 130px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'integration:client:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="clientCode" label="编码" min-width="150" />
      <el-table-column prop="clientName" label="名称" min-width="180" />
      <el-table-column prop="allowedScopes" label="Scope" min-width="180" show-overflow-tooltip />
      <el-table-column prop="allowedIpList" label="IP 白名单" min-width="180" show-overflow-tooltip />
      <el-table-column prop="rateLimitPerMinute" label="每分钟限流" width="120" />
      <el-table-column prop="isEnabled" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="170" fixed="right">
        <template #default="{ row }">
          <div class="table-actions">
            <el-button v-permission="'integration:client:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
            <el-dropdown v-if="hasMoreClientActions()" trigger="click">
              <el-button link type="primary" :icon="MoreFilled">更多</el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item v-permission="'integration:client:update'" @click="toggle(row)">
                    {{ row.isEnabled ? '禁用' : '启用' }}
                  </el-dropdown-item>
                  <el-dropdown-item v-permission="'integration:client:secret'" @click="generateSecret(row)">
                    生成密钥
                  </el-dropdown-item>
                  <el-dropdown-item v-permission="'integration:client:delete'" divided @click="remove(row)">
                    删除
                  </el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 API 客户端' : '新增 API 客户端'" width="620px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="130px">
        <el-form-item label="客户端编码" prop="clientCode">
          <el-input v-model="form.clientCode" :disabled="Boolean(editingId)" />
        </el-form-item>
        <el-form-item label="客户端名称" prop="clientName">
          <el-input v-model="form.clientName" />
        </el-form-item>
        <el-form-item label="Scope">
          <el-input v-model="form.allowedScopes" placeholder="user.read,workflow.read" />
        </el-form-item>
        <el-form-item label="IP 白名单">
          <el-input v-model="form.allowedIpList" placeholder="192.168.1.*,10.0.0.8" />
        </el-form-item>
        <el-form-item label="每分钟限流">
          <el-input-number v-model="form.rateLimitPerMinute" :min="0" :max="10000" />
        </el-form-item>
        <el-form-item v-if="!editingId" label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
        <el-form-item label="描述">
          <el-input v-model="form.description" type="textarea" :rows="3" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="secretDialogVisible" title="客户端密钥" width="620px">
      <el-alert type="warning" show-icon title="Secret 只显示一次，请立即保存到外部系统的安全配置中。" />
      <el-descriptions class="secret-box" :column="1" border>
        <el-descriptions-item label="X-Api-Key">{{ generatedSecret.apiKey }}</el-descriptions-item>
        <el-descriptions-item label="X-Api-Secret">{{ generatedSecret.apiSecret }}</el-descriptions-item>
      </el-descriptions>
      <template #footer>
        <el-button type="primary" @click="secretDialogVisible = false">我已记录</el-button>
      </template>
    </el-dialog>

    <SensitiveVerificationDialog ref="sensitiveVerificationRef" />
  </PageContainer>
</template>

<style scoped>
.secret-box {
  margin-top: 14px;
}
</style>
