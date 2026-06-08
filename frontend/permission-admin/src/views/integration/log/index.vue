<script setup lang="ts">
defineOptions({
  name: 'IntegrationLog',
})

import { reactive, ref } from 'vue'
import {
  getApiCallLogs,
  getWebhookLogs,
  type ApiCallLogItem,
  type WebhookLogItem,
} from '../../../api/integration'
import PageContainer from '../../../components/PageContainer/index.vue'
import TableToolbar from '../../../components/TableToolbar/index.vue'

const activeTab = ref('api')
const apiLoading = ref(false)
const webhookLoading = ref(false)
const apiRows = ref<ApiCallLogItem[]>([])
const webhookRows = ref<WebhookLogItem[]>([])
const apiTotal = ref(0)
const webhookTotal = ref(0)

const apiQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  path: '',
})

const webhookQuery = reactive({
  pageIndex: 1,
  pageSize: 10,
  eventType: '',
  status: '',
})

async function loadApiLogs() {
  apiLoading.value = true
  try {
    const result = await getApiCallLogs(apiQuery)
    apiRows.value = result.items
    apiTotal.value = result.totalCount
  } finally {
    apiLoading.value = false
  }
}

async function loadWebhookLogs() {
  webhookLoading.value = true
  try {
    const result = await getWebhookLogs(webhookQuery)
    webhookRows.value = result.items
    webhookTotal.value = result.totalCount
  } finally {
    webhookLoading.value = false
  }
}

function refresh() {
  if (activeTab.value === 'api') {
    loadApiLogs()
  } else {
    loadWebhookLogs()
  }
}

function resetApiQuery() {
  Object.assign(apiQuery, {
    pageIndex: 1,
    path: '',
  })
  loadApiLogs()
}

function resetWebhookQuery() {
  Object.assign(webhookQuery, {
    pageIndex: 1,
    eventType: '',
    status: '',
  })
  loadWebhookLogs()
}

loadApiLogs()
loadWebhookLogs()
</script>

<template>
  <PageContainer title="调用日志" description="查看 API Key 外部调用日志和 Webhook 投递日志。">
    <template #actions>
      <TableToolbar @refresh="refresh" />
    </template>

    <el-tabs v-model="activeTab">
      <el-tab-pane label="API 调用" name="api">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="apiQuery.path" clearable placeholder="请求路径" />
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="loadApiLogs">查询</el-button>
            <el-button @click="resetApiQuery">重置</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="apiLoading" :data="apiRows" border>
          <el-table-column prop="clientCode" label="客户端" width="160" />
          <el-table-column prop="method" label="方法" width="90" />
          <el-table-column prop="path" label="路径" min-width="260" show-overflow-tooltip />
          <el-table-column prop="ipAddress" label="IP" width="150" />
          <el-table-column prop="statusCode" label="状态码" width="100" />
          <el-table-column prop="elapsedMilliseconds" label="耗时(ms)" width="110" />
          <el-table-column prop="createdAt" label="时间" min-width="180" />
        </el-table>

        <el-pagination
          v-model:current-page="apiQuery.pageIndex"
          v-model:page-size="apiQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="apiTotal"
          @change="loadApiLogs"
        />
      </el-tab-pane>

      <el-tab-pane label="Webhook 投递" name="webhook">
        <el-form class="toolbar" inline @submit.prevent>
          <el-form-item>
            <el-input v-model="webhookQuery.eventType" clearable placeholder="事件类型" />
          </el-form-item>
          <el-form-item>
            <el-select v-model="webhookQuery.status" clearable placeholder="状态" style="width: 140px">
              <el-option label="成功" value="Succeeded" />
              <el-option label="失败" value="Failed" />
            </el-select>
          </el-form-item>
          <el-form-item>
            <el-button type="primary" @click="loadWebhookLogs">查询</el-button>
            <el-button @click="resetWebhookQuery">重置</el-button>
          </el-form-item>
        </el-form>

        <el-table v-loading="webhookLoading" :data="webhookRows" border>
          <el-table-column prop="eventType" label="事件类型" width="180" />
          <el-table-column prop="status" label="状态" width="100">
            <template #default="{ row }">
              <el-tag :type="row.status === 'Succeeded' ? 'success' : 'danger'">{{ row.status }}</el-tag>
            </template>
          </el-table-column>
          <el-table-column prop="responseStatusCode" label="响应码" width="100" />
          <el-table-column prop="retryCount" label="重试序号" width="100" />
          <el-table-column prop="responseBody" label="响应内容" min-width="220" show-overflow-tooltip />
          <el-table-column prop="createdAt" label="时间" min-width="180" />
        </el-table>

        <el-pagination
          v-model:current-page="webhookQuery.pageIndex"
          v-model:page-size="webhookQuery.pageSize"
          class="pager"
          background
          layout="total, sizes, prev, pager, next"
          :total="webhookTotal"
          @change="loadWebhookLogs"
        />
      </el-tab-pane>
    </el-tabs>
  </PageContainer>
</template>
