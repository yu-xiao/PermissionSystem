<script setup lang="ts">
defineOptions({ name: 'DemoApprovalOrderDetail' })

import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import {
  ApprovalStatus,
  getDemoApprovalOrder,
  type ApprovalStatus as ApprovalStatusValue,
  type DemoApprovalOrderItem,
} from '../../../api/demoApprovalOrder'
import { getInstanceDetail, type WorkflowInstanceDetail } from '../../../api/workflowInstance'
import PageContainer from '../../../components/PageContainer/index.vue'

const route = useRoute()
const router = useRouter()
const loading = ref(false)
const order = ref<DemoApprovalOrderItem>()
const workflow = ref<WorkflowInstanceDetail>()
const orderId = computed(() => String(route.params.id ?? ''))

async function loadData() {
  loading.value = true
  try {
    order.value = await getDemoApprovalOrder(orderId.value)
    workflow.value = order.value.workflowInstanceId
      ? await getInstanceDetail(order.value.workflowInstanceId)
      : undefined
  } finally {
    loading.value = false
  }
}

function goBack() {
  router.back()
}

function openWorkflowDetail() {
  if (order.value?.workflowInstanceId) {
    void router.push(`/workflow/instances/${order.value.workflowInstanceId}`)
  }
}

function statusText(status?: ApprovalStatusValue) {
  if (status === undefined) {
    return '-'
  }

  const map: Record<ApprovalStatusValue, string> = {
    [ApprovalStatus.Draft]: '草稿',
    [ApprovalStatus.Pending]: '审批中',
    [ApprovalStatus.Approved]: '已通过',
    [ApprovalStatus.Rejected]: '已驳回',
    [ApprovalStatus.Withdrawn]: '已撤回',
    [ApprovalStatus.Cancelled]: '已取消',
  }
  return map[status] ?? '未知'
}

function statusType(status?: ApprovalStatusValue) {
  if (status === ApprovalStatus.Pending) {
    return 'warning'
  }
  if (status === ApprovalStatus.Approved) {
    return 'success'
  }
  if (status === ApprovalStatus.Rejected) {
    return 'danger'
  }
  return 'info'
}

function formatTime(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

loadData()
</script>

<template>
  <PageContainer title="Demo 审批单详情" description="查看示例单据和关联审批实例。">
    <template #actions>
      <el-button @click="goBack">返回</el-button>
      <el-button v-if="order?.workflowInstanceId" type="primary" @click="openWorkflowDetail">审批实例详情</el-button>
    </template>

    <div v-loading="loading" class="detail-layout">
      <el-card shadow="never">
        <template #header>
          <span>单据信息</span>
        </template>
        <el-descriptions v-if="order" :column="2" border>
          <el-descriptions-item label="单据编号">{{ order.orderNo }}</el-descriptions-item>
          <el-descriptions-item label="标题">{{ order.title }}</el-descriptions-item>
          <el-descriptions-item label="金额">{{ order.amount }}</el-descriptions-item>
          <el-descriptions-item label="申请人">{{ order.applicantUserName }}</el-descriptions-item>
          <el-descriptions-item label="审批状态">
            <el-tag :type="statusType(order.approvalStatus)">{{ statusText(order.approvalStatus) }}</el-tag>
          </el-descriptions-item>
          <el-descriptions-item label="WorkflowInstanceId">{{ order.workflowInstanceId || '-' }}</el-descriptions-item>
          <el-descriptions-item label="提交时间">{{ formatTime(order.submittedAt) }}</el-descriptions-item>
          <el-descriptions-item label="通过时间">{{ formatTime(order.approvedAt) }}</el-descriptions-item>
          <el-descriptions-item label="驳回时间">{{ formatTime(order.rejectedAt) }}</el-descriptions-item>
          <el-descriptions-item label="撤回时间">{{ formatTime(order.withdrawnAt) }}</el-descriptions-item>
        </el-descriptions>
      </el-card>

      <el-card v-if="workflow" shadow="never">
        <template #header>
          <span>审批记录</span>
        </template>
        <el-timeline>
          <el-timeline-item
            v-for="record in workflow.records"
            :key="record.id"
            :timestamp="formatTime(record.operatedAt)"
          >
            <strong>{{ record.operatorUserName || '系统' }}</strong>
            <span> {{ record.nodeName || '' }}</span>
            <div v-if="record.comment">{{ record.comment }}</div>
          </el-timeline-item>
        </el-timeline>
      </el-card>
    </div>
  </PageContainer>
</template>

<style scoped>
.detail-layout {
  display: grid;
  gap: 16px;
}
</style>
