<script setup lang="ts">
defineOptions({
  name: 'SystemDeadLetterMessage',
})

import { reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  discardDeadLetterMessage,
  getDeadLetterMessageDetail,
  getDeadLetterMessages,
  replayDeadLetterMessage,
  type DeadLetterMessageDetail,
  type DeadLetterMessageItem,
} from '../../../api/dead-letter-messages'

const loading = ref(false)
const detailLoading = ref(false)
const tableData = ref<DeadLetterMessageItem[]>([])
const total = ref(0)
const detailVisible = ref(false)
const detail = ref<DeadLetterMessageDetail>()
const dateRange = ref<string[]>([])

const query = reactive({
  pageIndex: 1,
  pageSize: 10,
  keyword: '',
  consumer: '',
  sourceQueue: '',
  status: '',
  startTime: undefined as string | undefined,
  endTime: undefined as string | undefined,
})

async function loadData() {
  syncDateRange()
  loading.value = true
  try {
    const result = await getDeadLetterMessages(query)
    tableData.value = result.items
    total.value = result.totalCount
  } finally {
    loading.value = false
  }
}

async function openDetail(row: DeadLetterMessageItem) {
  detailVisible.value = true
  detailLoading.value = true
  try {
    detail.value = await getDeadLetterMessageDetail(row.id)
  } finally {
    detailLoading.value = false
  }
}

async function replay(row: DeadLetterMessageItem) {
  await ElMessageBox.confirm('确认重放该死信消息？', '重放确认', { type: 'warning' })
  await replayDeadLetterMessage(row.id)
  ElMessage.success('消息已提交重放')
  await loadData()
}

async function discard(row: DeadLetterMessageItem) {
  const result = await ElMessageBox.prompt('请输入放弃原因', '人工放弃', {
    inputPattern: /\S+/,
    inputErrorMessage: '放弃原因不能为空',
    inputPlaceholder: '例如：业务数据已由人工补录',
  })
  await discardDeadLetterMessage(row.id, result.value)
  ElMessage.success('消息已标记为放弃')
  await loadData()
}

function resetQuery() {
  Object.assign(query, {
    pageIndex: 1,
    keyword: '',
    consumer: '',
    sourceQueue: '',
    status: '',
    startTime: undefined,
    endTime: undefined,
  })
  dateRange.value = []
  loadData()
}

function syncDateRange() {
  query.startTime = dateRange.value[0]
  query.endTime = dateRange.value[1]
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

function statusType(status: string) {
  if (status === 'Replayed') return 'success'
  if (status === 'Discarded') return 'info'
  return 'danger'
}

loadData()
</script>

<template>
  <section class="page">
    <el-form class="toolbar" inline @submit.prevent>
      <el-form-item><el-input v-model="query.keyword" clearable placeholder="消息ID / 类型 / 原因" /></el-form-item>
      <el-form-item><el-input v-model="query.consumer" clearable placeholder="消费者" /></el-form-item>
      <el-form-item><el-input v-model="query.sourceQueue" clearable placeholder="来源队列" /></el-form-item>
      <el-form-item>
        <el-select v-model="query.status" clearable placeholder="状态" style="width: 140px">
          <el-option label="待处理" value="Pending" />
          <el-option label="已重放" value="Replayed" />
          <el-option label="已放弃" value="Discarded" />
        </el-select>
      </el-form-item>
      <el-form-item>
        <el-date-picker
          v-model="dateRange"
          type="datetimerange"
          value-format="YYYY-MM-DDTHH:mm:ssZ"
          start-placeholder="开始时间"
          end-placeholder="结束时间"
        />
      </el-form-item>
      <el-form-item>
        <el-button v-permission="'system:dead-letter:view'" type="primary" @click="loadData">查询</el-button>
        <el-button @click="resetQuery">重置</el-button>
      </el-form-item>
    </el-form>

    <el-table v-loading="loading" :data="tableData" border>
      <el-table-column prop="createdAt" label="进入时间" width="180">
        <template #default="{ row }">{{ formatDate(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column prop="messageId" label="消息ID" min-width="180" show-overflow-tooltip />
      <el-table-column prop="sourceQueue" label="来源队列" min-width="180" show-overflow-tooltip />
      <el-table-column prop="messageType" label="类型" min-width="220" show-overflow-tooltip />
      <el-table-column prop="retryCount" label="重试次数" width="90" />
      <el-table-column prop="status" label="状态" width="100">
        <template #default="{ row }"><el-tag :type="statusType(row.status)">{{ $displayText(row.status) }}</el-tag></template>
      </el-table-column>
      <el-table-column prop="failureReason" label="失败原因" min-width="220" show-overflow-tooltip />
      <el-table-column label="操作" width="190" fixed="right">
        <template #default="{ row }">
          <el-button v-permission="'system:dead-letter:view'" link type="primary" @click="openDetail(row)">详情</el-button>
          <el-button
            v-if="row.status === 'Pending'"
            v-permission="'system:dead-letter:replay'"
            link
            type="primary"
            @click="replay(row)"
          >重放</el-button>
          <el-button
            v-if="row.status === 'Pending'"
            v-permission="'system:dead-letter:discard'"
            link
            type="danger"
            @click="discard(row)"
          >放弃</el-button>
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

    <el-dialog v-model="detailVisible" title="死信消息详情" width="860px">
      <div v-loading="detailLoading">
        <el-descriptions v-if="detail" :column="2" border>
          <el-descriptions-item label="消息ID" :span="2">{{ detail.messageId }}</el-descriptions-item>
          <el-descriptions-item label="租户">{{ detail.tenantId }}</el-descriptions-item>
          <el-descriptions-item label="状态">{{ $displayText(detail.status) }}</el-descriptions-item>
          <el-descriptions-item label="消费者">{{ detail.consumer }}</el-descriptions-item>
          <el-descriptions-item label="来源队列">{{ detail.sourceQueue }}</el-descriptions-item>
          <el-descriptions-item label="交换机">{{ detail.exchange }}</el-descriptions-item>
          <el-descriptions-item label="路由键">{{ detail.routingKey }}</el-descriptions-item>
          <el-descriptions-item label="失败原因" :span="2">{{ detail.failureReason }}</el-descriptions-item>
          <el-descriptions-item label="处置说明" :span="2">{{ detail.dispositionRemark || '-' }}</el-descriptions-item>
          <el-descriptions-item label="请求头" :span="2"><pre class="message-body">{{ detail.headers || '-' }}</pre></el-descriptions-item>
          <el-descriptions-item label="载荷" :span="2"><pre class="message-body">{{ detail.payload || '-' }}</pre></el-descriptions-item>
        </el-descriptions>
      </div>
    </el-dialog>
  </section>
</template>

<style scoped>
.message-body {
  max-height: 260px;
  margin: 0;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  font-family: Consolas, 'Courier New', monospace;
  font-size: 12px;
  line-height: 1.5;
}
</style>
