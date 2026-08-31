<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getDemoApprovalOrder, submitDemoApprovalOrder, withdrawDemoApprovalOrder, cancelDemoApprovalOrder, type DemoApprovalOrderItem } from '../../api/demoApprovalOrder'
import { cancelDemoBusinessOrder, getDemoBusinessOrder, getDemoBusinessOrderAttachments, submitDemoBusinessOrder, uploadDemoBusinessOrderAttachment, withdrawDemoBusinessOrder, type DemoBusinessOrderItem } from '../../api/demoBusinessOrder'
import { deleteFile, getFilesByBusiness, uploadFile, type FileResourceItem } from '../../api/files'
import StateView from '../../components/StateView.vue'
import StatusTag from '../../components/StatusTag.vue'
import AttachmentList from '../../components/AttachmentList.vue'
import type { AttachmentItem } from '../../components/AttachmentList.vue'
import { usePermissionStore } from '../../stores/permission'

const route = useRoute(); const router = useRouter(); const permission = usePermissionStore()
const loading = ref(true); const saving = ref(false); const uploading = ref(false); const error = ref(''); const notice = ref('')
const order = ref<DemoBusinessOrderItem | DemoApprovalOrderItem>(); const attachments = ref<FileResourceItem[]>([])
const kind = computed(() => route.query.kind === 'approval' ? 'approval' : 'business')
const id = computed(() => String(route.params.id || ''))
const status = computed(() => Number(order.value?.approvalStatus ?? -1))
const isDraft = computed(() => status.value === 0); const isPending = computed(() => status.value === 1)
const isBusiness = computed(() => order.value && 'customerName' in order.value)
const canUpdate = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:update' : 'demo-approval-order:update'))
const canSubmit = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:submit' : 'demo-approval-order:submit'))
const canWithdraw = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:withdraw' : 'demo-approval-order:withdraw'))
const canCancel = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:cancel' : 'demo-approval-order:cancel'))
const canUpload = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:attachment:upload' : 'system:file:upload'))
const canRemove = computed(() => permission.hasPermission('system:file:delete'))
const canViewAttachment = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:attachment:view' : 'system:file:view'))

function statusLabel(value: number) { return ({ 0: '草稿', 1: '审批中', 2: '已通过', 3: '已驳回', 4: '已撤回', 5: '已取消' } as Record<number, string>)[value] || '未知状态' }
function statusTone(value: number): 'neutral' | 'primary' | 'success' | 'warning' | 'danger' { if (value === 2) return 'success'; if (value === 3) return 'danger'; if (value === 1) return 'warning'; return 'neutral' }
function formatAmount(value?: number) { return typeof value === 'number' ? new Intl.NumberFormat('zh-CN', { style: 'currency', currency: 'CNY' }).format(value) : '--' }
function formatDate(value?: string) { if (!value) return '--'; const date = new Date(value); return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(date) }

async function load() {
  loading.value = true; error.value = ''; notice.value = ''
  try {
    order.value = kind.value === 'business' ? await getDemoBusinessOrder(id.value) : await getDemoApprovalOrder(id.value)
    if (canViewAttachment.value) {
      if (kind.value === 'business') attachments.value = await getDemoBusinessOrderAttachments(id.value)
      else attachments.value = await getFilesByBusiness('demo-approval-order', id.value)
    } else {
      attachments.value = []
    }
  } catch (reason) { error.value = reason instanceof Error ? reason.message : '单据详情加载失败。' } finally { loading.value = false }
}
async function runAction(action: 'submit' | 'withdraw' | 'cancel') {
  if (!order.value || saving.value) return
  const allowed = action === 'submit' ? canSubmit.value : action === 'withdraw' ? canWithdraw.value : canCancel.value
  if (!allowed) { error.value = '当前账号没有执行此操作的权限。'; return }
  saving.value = true; error.value = ''; notice.value = ''
  try {
    if (kind.value === 'business') {
      if (action === 'submit') order.value = await submitDemoBusinessOrder(id.value)
      if (action === 'withdraw') order.value = await withdrawDemoBusinessOrder(id.value)
      if (action === 'cancel') order.value = await cancelDemoBusinessOrder(id.value)
    } else {
      if (action === 'submit') order.value = await submitDemoApprovalOrder(id.value)
      if (action === 'withdraw') order.value = await withdrawDemoApprovalOrder(id.value)
      if (action === 'cancel') order.value = await cancelDemoApprovalOrder(id.value)
    }
    notice.value = action === 'submit' ? '单据已提交。' : action === 'withdraw' ? '单据已撤回。' : '单据已取消。'
    await load()
  } catch (reason) { error.value = reason instanceof Error ? reason.message : '操作失败，请稍后重试。' } finally { saving.value = false }
}
async function upload(file: File) {
  if (!canUpload.value) { error.value = '当前账号没有上传附件的权限。'; return }
  uploading.value = true; error.value = ''
  try { const item = kind.value === 'business' ? await uploadDemoBusinessOrderAttachment(id.value, file) : await uploadFile(file, 'demo-approval-order', id.value); attachments.value = [...attachments.value, item] } catch (reason) { error.value = reason instanceof Error ? reason.message : '附件上传失败。' } finally { uploading.value = false }
}
async function removeAttachment(fileId: string) {
  if (!canRemove.value) { error.value = '当前账号没有删除附件的权限。'; return }
  try { await deleteFile(fileId); attachments.value = attachments.value.filter((item) => item.id !== fileId) } catch (reason) { error.value = reason instanceof Error ? reason.message : '附件删除失败。' }
}
function openAttachment(item: AttachmentItem) { if (item.url) window.open(item.url, '_blank', 'noopener,noreferrer') }
onMounted(() => void load())
</script>

<template>
  <section class="order-detail-view">
    <StateView v-if="loading" kind="loading" title="正在加载单据" /><StateView v-else-if="error && !order" kind="error" :hint="error" action-label="重新加载" @action="load" /><template v-else-if="order">
      <div class="detail-hero surface"><div class="detail-hero__top"><span class="detail-hero__type">{{ isBusiness ? '业务订单' : '审批单' }}</span><StatusTag :label="statusLabel(status)" :tone="statusTone(status)" /></div><h2>{{ order.title }}</h2><p>{{ order.orderNo }} · {{ formatDate(order.createdAt) }}</p></div>
      <p v-if="notice" class="notice" role="status">{{ notice }}</p><p v-if="error" class="inline-error" role="alert">{{ error }}</p>
      <section class="surface detail-section"><h2>单据信息</h2><dl class="detail-grid"><dt>单据编号</dt><dd>{{ order.orderNo }}</dd><dt>申请人</dt><dd>{{ isBusiness ? (order as DemoBusinessOrderItem).ownerUserName : (order as DemoApprovalOrderItem).applicantUserName }}</dd><dt>金额</dt><dd>{{ formatAmount(order.amount) }}</dd><dt v-if="isBusiness">客户</dt><dd v-if="isBusiness">{{ (order as DemoBusinessOrderItem).customerName }}</dd><dt>创建时间</dt><dd>{{ formatDate(order.createdAt) }}</dd><dt v-if="order.submittedAt">提交时间</dt><dd v-if="order.submittedAt">{{ formatDate(order.submittedAt) }}</dd></dl></section>
      <section class="surface detail-section"><h2>附件</h2><AttachmentList :items="attachments" :uploading="uploading" :can-upload="canUpload" :can-remove="canRemove" @upload="upload" @remove="removeAttachment" @open="openAttachment" /></section>
    </template>
  </section>
  <div v-if="order && ((isDraft && (canUpdate || canSubmit)) || (isPending && (canWithdraw || canCancel)))" class="action-bar"><div class="action-bar__inner"><button v-if="isDraft && canUpdate" class="button button--secondary" type="button" @click="router.push({ path: `/orders/${id}/edit`, query: { kind } })">编辑</button><button v-if="isPending && canWithdraw" class="button button--ghost" type="button" :disabled="saving" @click="runAction('withdraw')">撤回</button><button v-if="isDraft && canSubmit" class="button button--primary" type="button" :disabled="saving" @click="runAction('submit')">{{ saving ? '提交中…' : '提交审批' }}</button><button v-if="isPending && canCancel" class="button button--danger" type="button" :disabled="saving" @click="runAction('cancel')">取消单据</button></div></div>
</template>

<style scoped>
.detail-hero { padding: 17px; }
.detail-hero__top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.detail-hero__type { color: var(--mobile-text-secondary); font-size: 12px; }
.detail-hero h2 { margin: 13px 0 6px; font-size: 20px; line-height: 1.35; }
.detail-hero p { margin: 0; color: var(--mobile-text-secondary); font-size: 12px; }
.notice { margin: 12px 0 0; padding: 9px 10px; border-radius: 8px; color: var(--mobile-success); background: var(--mobile-success-soft); font-size: 12px; }
.inline-error { margin: 12px 0 0; padding: 9px 10px; border-radius: 8px; color: var(--mobile-danger); background: var(--mobile-danger-soft); font-size: 12px; }
</style>
