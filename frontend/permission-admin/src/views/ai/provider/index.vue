<script setup lang="ts">
defineOptions({ name: 'AiProvider' })

import { MoreFilled, Plus } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { computed, reactive, ref } from 'vue'
import {
  AiProviderType,
  createAiProvider,
  deleteAiProvider,
  getAiProvider,
  getAiProviders,
  setAiProviderEnabled,
  setAiProviderCompliance,
  setDefaultAiProvider,
  testAiProvider,
  updateAiProvider,
  type AiProviderDetail,
  type AiProviderListItem,
  type SaveAiProviderRequest,
} from '../../../api/ai'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const authStore = useAuthStore()
const tenantId = computed(() => authStore.effectiveTenantId)
const loading = ref(false)
const saving = ref(false)
const testingId = ref('')
const dialogVisible = ref(false)
const formRef = ref<FormInstance>()
const editingId = ref('')
const detail = ref<AiProviderDetail>()
const rows = ref<AiProviderListItem[]>([])
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  enabled: undefined as boolean | undefined,
})

const form = reactive({
  providerCode: '',
  providerName: '',
  baseUrl: '',
  chatCompletionsPath: 'v1/chat/completions',
  apiKey: '',
  modelName: '',
  isDefault: false,
  isEnabled: true,
  timeoutSeconds: 30,
  temperature: undefined as number | undefined,
  maxTokens: undefined as number | undefined,
  allowInsecureHttp: false,
  allowPrivateNetwork: false,
  allowedHostsText: '',
  dataResidency: '',
  supportsTools: true,
  supportsJsonSchema: false,
  inputTokenPricePerMillion: undefined as number | undefined,
  outputTokenPricePerMillion: undefined as number | undefined,
  pricingCurrency: '',
  remark: '',
})

const rules: FormRules = {
  providerCode: [{ required: true, message: '请输入 ProviderCode', trigger: 'blur' }],
  providerName: [{ required: true, message: '请输入名称', trigger: 'blur' }],
  baseUrl: [{ required: true, message: '请输入 BaseUrl', trigger: 'blur' }],
  apiKey: [{ required: true, message: '请输入 API Key', trigger: 'blur' }],
  modelName: [{ required: true, message: '请输入模型名称', trigger: 'blur' }],
  allowedHostsText: [{ required: true, message: '请输入允许的主机名', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getAiProviders(query)
    rows.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function resetForm() {
  detail.value = undefined
  Object.assign(form, {
    providerCode: '',
    providerName: '',
    baseUrl: '',
    chatCompletionsPath: 'v1/chat/completions',
    apiKey: '',
    modelName: '',
    isDefault: false,
    isEnabled: true,
    timeoutSeconds: 30,
    temperature: undefined,
    maxTokens: undefined,
    allowInsecureHttp: false,
    allowPrivateNetwork: false,
    allowedHostsText: '',
    dataResidency: '',
    supportsTools: true,
    supportsJsonSchema: false,
    inputTokenPricePerMillion: undefined,
    outputTokenPricePerMillion: undefined,
    pricingCurrency: '',
    remark: '',
  })
}

function openCreate() {
  editingId.value = ''
  resetForm()
  dialogVisible.value = true
}

async function openEdit(row: AiProviderListItem) {
  editingId.value = row.id
  detail.value = await getAiProvider(row.id)
  const item = detail.value
  Object.assign(form, {
    providerCode: item.providerCode,
    providerName: item.providerName,
    baseUrl: item.baseUrl,
    chatCompletionsPath: item.chatCompletionsPath,
    apiKey: item.apiKey,
    modelName: item.modelName,
    isDefault: item.isDefault,
    isEnabled: item.isEnabled,
    timeoutSeconds: item.timeoutSeconds,
    temperature: item.temperature,
    maxTokens: item.maxTokens,
    allowInsecureHttp: item.allowInsecureHttp,
    allowPrivateNetwork: item.allowPrivateNetwork,
    allowedHostsText: item.allowedHosts.join('\n'),
    dataResidency: item.dataResidency ?? '',
    supportsTools: item.supportsTools,
    supportsJsonSchema: item.supportsJsonSchema,
    inputTokenPricePerMillion: item.inputTokenPricePerMillion,
    outputTokenPricePerMillion: item.outputTokenPricePerMillion,
    pricingCurrency: item.pricingCurrency ?? '',
    remark: item.remark ?? '',
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    const payload: SaveAiProviderRequest = {
      tenantId: tenantId.value || undefined,
      providerCode: form.providerCode,
      providerName: form.providerName,
      providerType: AiProviderType.OpenAiCompatible,
      baseUrl: form.baseUrl,
      chatCompletionsPath: form.chatCompletionsPath,
      apiKey: form.apiKey,
      modelName: form.modelName,
      isDefault: form.isDefault,
      isEnabled: form.isEnabled,
      timeoutSeconds: form.timeoutSeconds,
      temperature: form.temperature,
      maxTokens: form.maxTokens,
      allowInsecureHttp: form.allowInsecureHttp,
      allowPrivateNetwork: form.allowPrivateNetwork,
      allowedHosts: splitHosts(form.allowedHostsText),
      dataResidency: form.dataResidency || undefined,
      supportsTools: form.supportsTools,
      supportsJsonSchema: form.supportsJsonSchema,
      inputTokenPricePerMillion: form.inputTokenPricePerMillion,
      outputTokenPricePerMillion: form.outputTokenPricePerMillion,
      pricingCurrency: form.pricingCurrency.trim().toUpperCase() || undefined,
      remark: form.remark || undefined,
      concurrencyToken: detail.value?.concurrencyToken,
    }
    if (editingId.value) {
      await updateAiProvider(editingId.value, payload)
    } else {
      await createAiProvider(payload)
    }
    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function toggle(row: AiProviderListItem) {
  await setAiProviderEnabled(row.id, !row.isEnabled, row.concurrencyToken)
  ElMessage.success(row.isEnabled ? '已禁用' : '已启用')
  await loadData()
}

async function makeDefault(row: AiProviderListItem) {
  await setDefaultAiProvider(row.id)
  ElMessage.success('已设为默认 Provider')
  await loadData()
}

async function testConnection(row: AiProviderListItem) {
  testingId.value = row.id
  try {
    const result = await testAiProvider(row.id)
    ElMessage[result.succeeded ? 'success' : 'warning'](result.message)
  } finally {
    testingId.value = ''
  }
}

async function setCompliance(row: AiProviderListItem) {
  const confirmed = Boolean(row.complianceConfirmedAt)
  await ElMessageBox.confirm(
    confirmed
      ? `确认撤销 Provider “${row.providerName}”的合规确认？撤销后模型调用将被阻止。`
      : `确认 Provider “${row.providerName}”已完成合规审查？`,
    confirmed ? '撤销合规确认' : '确认合规',
  )
  await setAiProviderCompliance(row.id, !confirmed, row.concurrencyToken)
  ElMessage.success(confirmed ? '已撤销合规确认' : '已确认合规')
  await loadData()
}

async function remove(row: AiProviderListItem) {
  await ElMessageBox.confirm(`确认删除 AI Provider “${row.providerName}”？`, '删除 Provider')
  await deleteAiProvider(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

function resetQuery() {
  query.keyword = ''
  query.enabled = undefined
  query.pageIndex = 1
  loadData()
}

function splitHosts(value: string) {
  return value
    .split(/[\n,;]+/)
    .map((item) => item.trim())
    .filter(Boolean)
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="AI 模型配置">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-input
          v-model="query.keyword"
          clearable
          placeholder="ProviderCode / 名称 / 模型"
          @keyup.enter="loadData"
        />
      </el-form-item>
      <el-form-item>
        <el-select v-model="query.enabled" clearable placeholder="状态" style="width: 120px">
          <el-option label="启用" :value="true" />
          <el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'ai:provider:view'" type="primary" @click="loadData"
          >查询</el-button
        >
        <el-button @click="resetQuery">重置</el-button>
        <el-button v-permission="'ai:provider:create'" :icon="Plus" @click="openCreate"
          >新增</el-button
        >
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="rows" border>
      <el-table-column prop="providerCode" label="ProviderCode" min-width="150" />
      <el-table-column prop="providerName" label="名称" min-width="160" />
      <el-table-column prop="modelName" label="模型" min-width="160" />
      <el-table-column prop="baseUrl" label="BaseUrl" min-width="230" show-overflow-tooltip />
      <el-table-column label="默认" width="80" align="center">
        <template #default="{ row }"
          ><el-tag v-if="row.isDefault" type="success">默认</el-tag></template
        >
      </el-table-column>
      <el-table-column label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{
            row.isEnabled ? '启用' : '禁用'
          }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="合规" width="100">
        <template #default="{ row }">
          <el-tag :type="row.complianceConfirmedAt ? 'success' : 'warning'">
            {{ row.complianceConfirmedAt ? '已确认' : '未确认' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="dataResidency" label="数据驻留" width="120" />
      <el-table-column label="创建时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="150" fixed="right">
        <template #default="{ row }">
          <div class="table-actions">
            <el-button v-permission="'ai:provider:view'" link type="primary" @click="openEdit(row)"
              >详情</el-button
            >
            <el-dropdown trigger="click">
              <el-button link type="primary" :icon="MoreFilled">更多</el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item v-permission="'ai:provider:update'" @click="openEdit(row)"
                    >编辑</el-dropdown-item
                  >
                  <el-dropdown-item v-permission="'ai:provider:update'" @click="toggle(row)">
                    {{ row.isEnabled ? '禁用' : '启用' }}
                  </el-dropdown-item>
                  <el-dropdown-item
                    v-if="row.isEnabled && !row.isDefault"
                    v-permission="'ai:provider:update'"
                    @click="makeDefault(row)"
                  >
                    设为默认
                  </el-dropdown-item>
                  <el-dropdown-item
                    v-permission="'ai:provider:test'"
                    :disabled="testingId === row.id"
                    @click="testConnection(row)"
                  >
                    测试连接
                  </el-dropdown-item>
                  <el-dropdown-item
                    v-permission="'ai:provider:compliance'"
                    @click="setCompliance(row)"
                  >
                    {{ row.complianceConfirmedAt ? '撤销合规' : '确认合规' }}
                  </el-dropdown-item>
                  <el-dropdown-item v-permission="'ai:provider:delete'" divided @click="remove(row)"
                    >删除</el-dropdown-item
                  >
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

    <el-dialog
      v-model="dialogVisible"
      :title="editingId ? 'AI Provider 详情' : '新增 AI Provider'"
      width="860px"
    >
      <el-form ref="formRef" :model="form" :rules="rules" label-width="140px">
        <el-row :gutter="16">
          <el-col :span="12">
            <el-form-item label="ProviderCode" prop="providerCode">
              <el-input v-model="form.providerCode" :disabled="Boolean(editingId)" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="名称" prop="providerName"
              ><el-input v-model="form.providerName"
            /></el-form-item>
          </el-col>
          <el-col :span="16">
            <el-form-item label="BaseUrl" prop="baseUrl"
              ><el-input v-model="form.baseUrl"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="接口路径"
              ><el-input v-model="form.chatCompletionsPath"
            /></el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="API Key" prop="apiKey">
              <el-input
                v-model="form.apiKey"
                type="password"
                show-password
                autocomplete="new-password"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="模型名称" prop="modelName"
              ><el-input v-model="form.modelName"
            /></el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="允许主机" prop="allowedHostsText">
              <el-input
                v-model="form.allowedHostsText"
                type="textarea"
                :rows="3"
                placeholder="api.example.com"
              />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="备注"
              ><el-input v-model="form.remark" type="textarea" :rows="3"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="超时（秒）"
              ><el-input-number v-model="form.timeoutSeconds" :min="1" :max="120"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="Temperature"
              ><el-input-number v-model="form.temperature" :min="0" :max="2" :step="0.1"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="MaxTokens"
              ><el-input-number v-model="form.maxTokens" :min="1" :max="128000"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="启用"><el-switch v-model="form.isEnabled" /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="默认"
              ><el-switch v-model="form.isDefault" :disabled="Boolean(editingId)"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="数据驻留"><el-input v-model="form.dataResidency" /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="Tool Calling"
              ><el-switch v-model="form.supportsTools"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="JSON Schema"
              ><el-switch v-model="form.supportsJsonSchema"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="计价币种"
              ><el-input v-model="form.pricingCurrency" maxlength="3" placeholder="CNY"
            /></el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="输入价/百万 Token">
              <el-input-number v-model="form.inputTokenPricePerMillion" :min="0" :precision="6" />
            </el-form-item>
          </el-col>
          <el-col :span="8">
            <el-form-item label="输出价/百万 Token">
              <el-input-number v-model="form.outputTokenPricePerMillion" :min="0" :precision="6" />
            </el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="允许 HTTP"
              ><el-switch v-model="form.allowInsecureHttp"
            /></el-form-item>
          </el-col>
          <el-col :span="12">
            <el-form-item label="允许私网"
              ><el-switch v-model="form.allowPrivateNetwork"
            /></el-form-item>
          </el-col>
        </el-row>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">关闭</el-button>
        <el-button
          v-if="
            editingId
              ? authStore.hasPermission('ai:provider:update')
              : authStore.hasPermission('ai:provider:create')
          "
          type="primary"
          :loading="saving"
          @click="save"
        >
          保存
        </el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
