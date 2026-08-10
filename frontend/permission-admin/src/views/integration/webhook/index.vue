<script setup lang="ts">
defineOptions({
  name: 'IntegrationWebhook',
})

import { ElMessage, ElMessageBox, type FormInstance, type FormRules } from 'element-plus'
import { MoreFilled } from '@element-plus/icons-vue'
import { reactive, ref } from 'vue'
import {
  createWebhook,
  deleteWebhook,
  getWebhooks,
  testWebhook,
  updateWebhook,
  type SaveWebhookRequest,
  type WebhookItem,
} from '../../../api/integration'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'
import { useAuthStore } from '../../../stores/auth'

const eventOptions = ['user.created', 'workflow.approved', 'workflow.rejected', 'notification.created']
const authStore = useAuthStore()
const loading = ref(false)
const saving = ref(false)
const dialogVisible = ref(false)
const editingId = ref('')
const editingWebhook = ref<WebhookItem | null>(null)
const formRef = ref<FormInstance>()
const tableData = ref<WebhookItem[]>([])
const total = ref(0)

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  eventType: '',
  isEnabled: undefined as boolean | undefined,
})

const form = reactive<SaveWebhookRequest>({
  eventType: 'user.created',
  targetUrl: '',
  secret: '',
  isEnabled: true,
  retryCount: 3,
})

const rules: FormRules = {
  eventType: [{ required: true, message: '请选择事件类型', trigger: 'change' }],
  targetUrl: [{ required: true, message: '请输入目标地址', trigger: 'blur' }],
}

async function loadData() {
  loading.value = true
  try {
    const result = await getWebhooks(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = ''
  editingWebhook.value = null
  Object.assign(form, {
    eventType: 'user.created',
    targetUrl: '',
    secret: '',
    isEnabled: true,
    retryCount: 3,
  })
  dialogVisible.value = true
}

function openEdit(row: WebhookItem) {
  editingId.value = row.id
  editingWebhook.value = row
  Object.assign(form, {
    eventType: row.eventType,
    targetUrl: row.targetUrl,
    secret: '',
    isEnabled: row.isEnabled,
    retryCount: row.retryCount,
  })
  dialogVisible.value = true
}

async function save() {
  await formRef.value?.validate()
  saving.value = true
  try {
    if (editingId.value) {
      await updateWebhook(editingId.value, {
        ...form,
        concurrencyToken: editingWebhook.value?.concurrencyToken,
      })
    } else {
      await createWebhook(form)
    }

    ElMessage.success('保存成功')
    dialogVisible.value = false
    await loadData()
  } finally {
    saving.value = false
  }
}

async function remove(row: WebhookItem) {
  await ElMessageBox.confirm(`确认删除 Webhook 订阅 ${row.eventType}？`, '确认删除')
  await deleteWebhook(row.id)
  ElMessage.success('删除成功')
  await loadData()
}

async function sendTest(row: WebhookItem) {
  await testWebhook(row.id)
  ElMessage.success('测试投递任务已提交')
}

function hasMoreWebhookActions() {
  return authStore.hasPermission('integration:webhook:test') || authStore.hasPermission('integration:webhook:delete')
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    eventType: '',
    isEnabled: undefined,
  })
  loadData()
}

loadData()
</script>

<template>
  <PageContainer title="Webhook 订阅" description="按事件类型向外部系统投递签名 Webhook。">
    <template #actions>
      <TableToolbar @refresh="loadData" />
    </template>

    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item>
        <el-select v-model="query.eventType" clearable filterable placeholder="事件类型" style="width: 210px">
          <el-option v-for="event in eventOptions" :key="event" :label="event" :value="event" />
        </el-select>
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
        <el-button v-permission="'integration:webhook:create'" @click="openCreate">新增</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="eventType" label="事件类型" min-width="180" />
      <el-table-column prop="targetUrl" label="目标地址" min-width="280" show-overflow-tooltip />
      <el-table-column prop="secret" label="Secret" width="120" />
      <el-table-column prop="retryCount" label="重试次数" width="100" />
      <el-table-column prop="isEnabled" label="状态" width="100">
        <template #default="{ row }">
          <el-tag :type="row.isEnabled ? 'success' : 'info'">{{ row.isEnabled ? '启用' : '禁用' }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="操作" width="170" fixed="right">
        <template #default="{ row }">
          <div class="table-actions">
            <el-button v-permission="'integration:webhook:update'" link type="primary" @click="openEdit(row)">编辑</el-button>
            <el-dropdown v-if="hasMoreWebhookActions()" trigger="click">
              <el-button link type="primary" :icon="MoreFilled">更多</el-button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item v-permission="'integration:webhook:test'" @click="sendTest(row)">测试</el-dropdown-item>
                  <el-dropdown-item v-permission="'integration:webhook:delete'" divided @click="remove(row)">删除</el-dropdown-item>
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

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑 Webhook' : '新增 Webhook'" width="680px">
      <el-form ref="formRef" :model="form" :rules="rules" label-width="110px">
        <el-form-item label="事件类型" prop="eventType">
          <el-select v-model="form.eventType" filterable allow-create class="full-width">
            <el-option v-for="event in eventOptions" :key="event" :label="event" :value="event" />
          </el-select>
        </el-form-item>
        <el-form-item label="目标地址" prop="targetUrl">
          <el-input v-model="form.targetUrl" placeholder="https://example.com/webhooks/permission-system" />
        </el-form-item>
        <el-form-item label="Secret">
          <el-input v-model="form.secret" show-password placeholder="留空则自动生成或保留原值" />
        </el-form-item>
        <el-form-item label="重试次数">
          <el-input-number v-model="form.retryCount" :min="0" :max="10" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isEnabled" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="save">保存</el-button>
      </template>
    </el-dialog>
  </PageContainer>
</template>
