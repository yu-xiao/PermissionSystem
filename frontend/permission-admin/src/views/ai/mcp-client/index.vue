<script setup lang="ts">
defineOptions({ name: 'AiMcpClient' })

import { Connection, Edit, Key, Plus, Refresh, Switch } from '@element-plus/icons-vue'
import { ElMessage, type FormInstance, type FormRules } from 'element-plus'
import { reactive, ref } from 'vue'
import {
  createMcpClient,
  getMcpClients,
  getMcpDatasets,
  mcpToolScopes,
  rotateMcpClientSecret,
  setMcpClientEnabled,
  updateMcpClient,
  type McpClient,
  type McpDataset,
} from '../../../api/mcp'
import PageContainer from '../../../components/PageContainer/index.vue'
import SensitiveVerificationDialog from '../../../components/SensitiveVerificationDialog/index.vue'

const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const credentialVisible = ref(false)
const formRef = ref<FormInstance>()
const verificationRef = ref<InstanceType<typeof SensitiveVerificationDialog>>()
const rows = ref<McpClient[]>([])
const datasets = ref<McpDataset[]>([])
const total = ref(0)
const editing = ref<McpClient>()
const credential = reactive({ oauthClientId: '', clientSecret: '' })
const query = reactive({ pageIndex: 1, pageSize: 20, keyword: '', isEnabled: undefined as boolean | undefined })
const form = reactive({
  clientCode: '',
  clientName: '',
  description: '',
  allowedScopes: mcpToolScopes.map((scope) => scope.value) as string[],
  allowedIpList: '',
  rateLimitPerMinute: 60,
  grants: {} as Record<string, string[]>,
})
const rules: FormRules = {
  clientCode: [{ required: true, message: '请输入客户端编码', trigger: 'blur' }],
  clientName: [{ required: true, message: '请输入客户端名称', trigger: 'blur' }],
  allowedIpList: [{ required: true, message: '请输入 IP 白名单', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const [clientResult, datasetResult] = await Promise.all([getMcpClients(query), getMcpDatasets()])
    rows.value = clientResult.items
    total.value = clientResult.totalCount
    datasets.value = datasetResult
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editing.value = undefined
  Object.assign(form, {
    clientCode: '', clientName: '', description: '',
    allowedScopes: mcpToolScopes.map((scope) => scope.value),
    allowedIpList: '', rateLimitPerMinute: 60, grants: {},
  })
  for (const dataset of datasets.value) {
    form.grants[dataset.id] = dataset.fields.filter((field) => field.isDefault).map((field) => field.fieldCode)
  }
  dialogVisible.value = true
}

function openEdit(row: McpClient) {
  editing.value = row
  Object.assign(form, {
    clientCode: row.clientCode,
    clientName: row.clientName,
    description: row.description ?? '',
    allowedScopes: [...row.allowedScopes],
    allowedIpList: row.allowedIpList,
    rateLimitPerMinute: row.rateLimitPerMinute,
    grants: Object.fromEntries(row.datasetGrants.map((grant) => [grant.datasetId, [...grant.allowedFields]])),
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  if (form.allowedScopes.length === 0) {
    ElMessage.warning('请至少选择一个 Scope')
    return
  }
  const datasetGrants = datasets.value
    .filter((dataset) => (form.grants[dataset.id]?.length ?? 0) > 0)
    .map((dataset) => ({ datasetId: dataset.id, allowedFields: form.grants[dataset.id] }))
  if (datasetGrants.length === 0) {
    ElMessage.warning('请至少授权一个数据集字段')
    return
  }

  const operation = editing.value ? 'ai:mcp-client:update' : 'ai:mcp-client:create'
  const ticket = await verificationRef.value?.open(operation)
  if (!ticket) return
  saving.value = true
  try {
    if (editing.value) {
      await updateMcpClient(editing.value.id, {
        clientName: form.clientName,
        description: form.description,
        allowedScopes: form.allowedScopes,
        allowedIpList: form.allowedIpList,
        rateLimitPerMinute: form.rateLimitPerMinute,
        datasetGrants,
        concurrencyToken: editing.value.concurrencyToken,
      }, ticket)
      ElMessage.success('客户端已更新')
    } else {
      const result = await createMcpClient({
        clientCode: form.clientCode,
        clientName: form.clientName,
        description: form.description,
        allowedScopes: form.allowedScopes,
        allowedIpList: form.allowedIpList,
        rateLimitPerMinute: form.rateLimitPerMinute,
        datasetGrants,
      }, ticket)
      Object.assign(credential, { oauthClientId: result.client.oauthClientId, clientSecret: result.clientSecret })
      credentialVisible.value = true
    }
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function toggle(row: McpClient) {
  const ticket = await verificationRef.value?.open('ai:mcp-client:status')
  if (!ticket) return
  await setMcpClientEnabled(row.id, !row.isEnabled, row.concurrencyToken, ticket)
  ElMessage.success(row.isEnabled ? '客户端已禁用' : '客户端已启用')
  await loadData()
}

async function rotateSecret(row: McpClient) {
  const ticket = await verificationRef.value?.open('ai:mcp-client:secret')
  if (!ticket) return
  const result = await rotateMcpClientSecret(row.id, row.concurrencyToken, ticket)
  Object.assign(credential, { oauthClientId: result.client.oauthClientId, clientSecret: result.clientSecret })
  credentialVisible.value = true
}

function closeCredential() {
  Object.assign(credential, { oauthClientId: '', clientSecret: '' })
  credentialVisible.value = false
}

loadData()
</script>

<template>
  <PageContainer title="MCP 客户端">
    <template #actions>
      <el-tooltip content="刷新">
        <el-button :icon="Refresh" circle @click="loadData" />
      </el-tooltip>
      <el-button v-permission="'ai:mcp-client:manage'" type="primary" :icon="Plus" @click="openCreate">新增客户端</el-button>
    </template>

    <el-form class="toolbar" inline @submit.prevent="loadData">
      <el-form-item><el-input v-model="query.keyword" clearable placeholder="编码 / 名称 / OAuth Client ID" /></el-form-item>
      <el-form-item>
        <el-select v-model="query.isEnabled" clearable placeholder="状态" style="width: 120px">
          <el-option label="启用" :value="true" /><el-option label="禁用" :value="false" />
        </el-select>
      </el-form-item>
      <el-form-item><el-button type="primary" :icon="Connection" @click="loadData">查询</el-button></el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="rows" border>
      <el-table-column prop="clientCode" label="编码" min-width="140" />
      <el-table-column prop="clientName" label="名称" min-width="150" />
      <el-table-column prop="oauthClientId" label="OAuth Client ID" min-width="280" show-overflow-tooltip />
      <el-table-column label="Scope" min-width="200">
        <template #default="{ row }"><el-tag v-for="scope in row.allowedScopes" :key="scope" class="scope-tag" type="info">{{ scope }}</el-tag></template>
      </el-table-column>
      <el-table-column prop="allowedIpList" label="IP 白名单" min-width="170" show-overflow-tooltip />
      <el-table-column prop="rateLimitPerMinute" label="每分钟限流" width="110" />
      <el-table-column label="状态" width="90"><template #default="{ row }"><el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag></template></el-table-column>
      <el-table-column label="操作" width="220" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'ai:mcp-client:manage'" link type="primary" :icon="Edit" @click="openEdit(row)">编辑</el-button>
          <el-button v-permission="'ai:mcp-client:manage'" link :icon="Switch" @click="toggle(row)">{{ row.isEnabled ? '禁用' : '启用' }}</el-button>
          <el-button v-permission="'ai:mcp-client:secret'" link :icon="Key" @click="rotateSecret(row)">轮换密钥</el-button>
        </template>
      </el-table-column>
    </el-table>
    <el-pagination v-model:current-page="query.pageIndex" v-model:page-size="query.pageSize" class="pager" background layout="total, sizes, prev, pager, next" :total="total" @change="loadData" />

    <el-dialog v-model="dialogVisible" :title="editing ? '编辑 MCP 客户端' : '新增 MCP 客户端'" width="760px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="120px">
        <el-form-item label="客户端编码" prop="clientCode"><el-input v-model="form.clientCode" :disabled="Boolean(editing)" /></el-form-item>
        <el-form-item label="客户端名称" prop="clientName"><el-input v-model="form.clientName" /></el-form-item>
        <el-form-item label="Scope"><el-checkbox-group v-model="form.allowedScopes"><el-checkbox v-for="scope in mcpToolScopes" :key="scope.value" :label="scope.value">{{ scope.label }}</el-checkbox></el-checkbox-group></el-form-item>
        <el-form-item label="IP 白名单" prop="allowedIpList"><el-input v-model="form.allowedIpList" placeholder="10.0.0.0/24, 192.168.1.8" /></el-form-item>
        <el-form-item label="每分钟限流"><el-input-number v-model="form.rateLimitPerMinute" :min="1" :max="1000" /></el-form-item>
        <el-form-item label="数据集授权">
          <div class="dataset-list">
            <div v-for="dataset in datasets" :key="dataset.id" class="dataset-row">
              <div class="dataset-name"><span>{{ dataset.datasetName }}</span><el-tag size="small" type="info">{{ dataset.dataClassification }}</el-tag></div>
              <el-checkbox-group v-model="form.grants[dataset.id]">
                <el-checkbox v-for="field in dataset.fields" :key="field.fieldCode" :label="field.fieldCode">{{ field.displayName }}</el-checkbox>
              </el-checkbox-group>
            </div>
          </div>
        </el-form-item>
        <el-form-item label="描述"><el-input v-model="form.description" type="textarea" :rows="2" /></el-form-item>
      </el-form>
      <template #footer><el-button @click="dialogVisible = false">取消</el-button><el-button type="primary" :loading="saving" @click="save">保存</el-button></template>
    </el-dialog>

    <el-dialog v-model="credentialVisible" title="OAuth 客户端凭据" width="660px" :close-on-click-modal="false" @closed="closeCredential">
      <el-alert type="warning" show-icon title="Client Secret 仅本次显示" />
      <el-descriptions class="credential" :column="1" border>
        <el-descriptions-item label="Client ID">{{ credential.oauthClientId }}</el-descriptions-item>
        <el-descriptions-item label="Client Secret"><span class="secret">{{ credential.clientSecret }}</span></el-descriptions-item>
      </el-descriptions>
      <template #footer><el-button type="primary" @click="closeCredential">关闭</el-button></template>
    </el-dialog>
    <SensitiveVerificationDialog ref="verificationRef" />
  </PageContainer>
</template>

<style scoped>
.scope-tag { margin: 2px 4px 2px 0; }
.dataset-list { width: 100%; }
.dataset-row { padding: 10px 0; border-bottom: 1px solid var(--el-border-color-lighter); }
.dataset-row:last-child { border-bottom: 0; }
.dataset-name { display: flex; align-items: center; gap: 8px; margin-bottom: 6px; font-weight: 600; }
.credential { margin-top: 14px; }
.secret { overflow-wrap: anywhere; font-family: ui-monospace, SFMono-Regular, Consolas, monospace; }
</style>
