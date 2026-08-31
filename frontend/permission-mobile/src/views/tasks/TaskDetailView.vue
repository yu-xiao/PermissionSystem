<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute } from 'vue-router'
import { addSignTask, approveTask, getTodoTasks, rejectTask, transferTask, type WorkflowTaskItem } from '../../api/workflowTask'
import { getInstanceDetail, type WorkflowInstanceDetail } from '../../api/workflowInstance'
import ProcessTimeline from '../../components/ProcessTimeline.vue'
import StateView from '../../components/StateView.vue'
import StatusTag from '../../components/StatusTag.vue'
import { usePermissionStore } from '../../stores/permission'

const route = useRoute()
const permission = usePermissionStore()
const loading = ref(true)
const saving = ref(false)
const error = ref('')
const task = ref<WorkflowTaskItem>()
const detail = ref<WorkflowInstanceDetail>()
const action = ref<'approve' | 'reject' | 'transfer' | 'add-sign' | ''>('')
const comment = ref('')
const targetUserId = ref('')

const taskId = computed(() => String(route.params.id || ''))
const instanceId = computed(() => typeof route.query.instanceId === 'string' ? route.query.instanceId : '')
const isPending = computed(() => Boolean(task.value && task.value.status === 0))
const canApprove = computed(() => permission.hasPermission('workflow:task:approve'))
const canReject = computed(() => permission.hasPermission('workflow:task:reject'))
const canTransfer = computed(() => permission.hasPermission('workflow:task:transfer'))
const canAddSign = computed(() => permission.hasPermission('workflow:task:add-sign'))
const hasAction = computed(() => isPending.value && permission.canAny([
  'workflow:task:approve',
  'workflow:task:reject',
  'workflow:task:transfer',
  'workflow:task:add-sign',
]))
const title = computed(() => task.value?.businessTitle || detail.value?.businessTitle || '审批任务')
const records = computed(() => (detail.value?.records || []).map((record) => ({
  id: record.id,
  nodeName: record.nodeName || '流程操作',
  actorName: record.operatorUserName,
  comment: record.comment,
  createdAt: record.operatedAt,
  statusLabel: '已完成',
})))

function statusLabel(status?: number) {
  const labels: Record<number, string> = { 0: '审批中', 1: '已通过', 2: '已驳回', 3: '已撤回', 4: '已取消', 5: '异常' }
  return labels[status ?? 0] || '未知状态'
}
function statusTone(status?: number): 'primary' | 'success' | 'warning' | 'danger' | 'neutral' {
  if (status === 1) return 'success'
  if (status === 2 || status === 5) return 'danger'
  if (status === 3 || status === 4) return 'neutral'
  return 'warning'
}
function formatDate(value?: string) {
  if (!value) return '--'
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('zh-CN', { dateStyle: 'medium', timeStyle: 'short' }).format(date)
}

async function load() {
  loading.value = true
  error.value = ''
  try {
    let resolvedInstanceId = instanceId.value
    if (!resolvedInstanceId) {
      const page = await getTodoTasks({ pageIndex: 1, pageSize: 100 })
      resolvedInstanceId = page?.items?.find((item) => item.id === taskId.value)?.instanceId || ''
    }
    if (!resolvedInstanceId) throw new Error('任务链接缺少流程实例信息，请从待办列表重新打开。')
    detail.value = await getInstanceDetail(resolvedInstanceId)
    task.value = detail.value?.tasks?.find((item) => item.id === taskId.value)
    if (!task.value) throw new Error('当前流程中未找到该任务，任务可能已被处理。')
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '审批详情加载失败。'
  } finally {
    loading.value = false
  }
}

function openAction(next: typeof action.value) {
  action.value = next
  comment.value = ''
  targetUserId.value = ''
}

async function submitAction() {
  if (!task.value || !action.value) return
  if ((action.value === 'transfer' || action.value === 'add-sign') && !targetUserId.value.trim()) return
  saving.value = true
  try {
    const payload = { comment: comment.value.trim() || undefined }
    if (action.value === 'approve') await approveTask(task.value.id, payload)
    if (action.value === 'reject') await rejectTask(task.value.id, payload)
    if (action.value === 'transfer') await transferTask(task.value.id, { ...payload, targetUserId: targetUserId.value.trim() })
    if (action.value === 'add-sign') await addSignTask(task.value.id, { ...payload, targetUserId: targetUserId.value.trim() })
    action.value = ''
    await Promise.all([load(), getTodoTasks({ pageIndex: 1, pageSize: 20 })])
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '操作未完成，请稍后重试。'
  } finally {
    saving.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="task-detail-view">
    <StateView v-if="loading" kind="loading" title="正在加载审批详情" />
    <StateView v-else-if="error && !detail" kind="error" :hint="error" action-label="重新加载" @action="load" />
    <template v-else>
      <div class="detail-hero surface">
        <div class="detail-hero__top"><span class="detail-hero__type">{{ detail?.definitionName || '审批流程' }}</span><StatusTag :label="statusLabel(detail?.status)" :tone="statusTone(detail?.status)" /></div>
        <h2>{{ title }}</h2>
        <p>{{ detail?.starterUserName || task?.starterUserName || '发起人未知' }} · {{ formatDate(detail?.startedAt || task?.assignedAt) }}</p>
      </div>

      <section class="surface detail-section">
        <h2>关键信息</h2>
        <dl class="detail-grid">
          <dt>业务类型</dt><dd>{{ detail?.businessType || task?.businessType || '--' }}</dd>
          <dt>业务编号</dt><dd>{{ detail?.businessId || task?.businessId || '--' }}</dd>
          <dt>当前节点</dt><dd>{{ task?.nodeName || detail?.currentNodeKey || '--' }}</dd>
          <dt>发起时间</dt><dd>{{ formatDate(detail?.startedAt || task?.startedAt) }}</dd>
        </dl>
      </section>

      <section class="surface detail-section"><h2>流程轨迹</h2><ProcessTimeline :items="records" /></section>

      <section v-if="detail?.formDataJson" class="surface detail-section"><h2>表单信息</h2><pre class="form-json">{{ detail.formDataJson }}</pre></section>
      <p v-if="error" class="inline-error" role="alert">{{ error }}</p>
    </template>

    <div v-if="action" class="action-modal" role="dialog" aria-modal="true" aria-label="审批操作">
      <button class="action-modal__backdrop" type="button" aria-label="关闭" @click="action = ''" />
      <section class="action-modal__sheet">
        <div class="action-modal__handle" aria-hidden="true" />
        <h2>{{ action === 'approve' ? '同意审批' : action === 'reject' ? '驳回审批' : action === 'transfer' ? '转交任务' : '加签任务' }}</h2>
        <div v-if="action === 'transfer' || action === 'add-sign'" class="form-field"><label for="target-user">目标用户 ID</label><input id="target-user" v-model="targetUserId" type="text" placeholder="请输入目标用户 ID" /></div>
        <div class="form-field"><label for="action-comment">{{ action === 'reject' ? '驳回原因' : '审批意见（可选）' }}</label><textarea id="action-comment" v-model="comment" :placeholder="action === 'reject' ? '请填写驳回原因' : '填写处理意见'" /></div>
        <div class="action-modal__buttons"><button class="button button--ghost" type="button" @click="action = ''">取消</button><button class="button" :class="action === 'reject' ? 'button--danger' : 'button--primary'" type="button" :disabled="saving" @click="submitAction">{{ saving ? '提交中…' : '确认' }}</button></div>
      </section>
    </div>
  </section>
  <div v-if="!loading && task && hasAction" class="action-bar"><div class="action-bar__inner"><button v-if="canTransfer" class="button button--secondary" type="button" @click="openAction('transfer')">转交</button><button v-if="canAddSign" class="button button--secondary" type="button" @click="openAction('add-sign')">加签</button><button v-if="canReject" class="button button--ghost" type="button" @click="openAction('reject')">驳回</button><button v-if="canApprove" class="button button--primary" type="button" @click="openAction('approve')">同意</button></div></div>
</template>

<style scoped>
.detail-hero { padding: 17px; }
.detail-hero__top { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
.detail-hero__type { color: var(--mobile-text-secondary); font-size: 12px; }
.detail-hero h2 { margin: 13px 0 6px; font-size: 20px; line-height: 1.35; }
.detail-hero p { margin: 0; color: var(--mobile-text-secondary); font-size: 12px; }
.form-json { max-height: 220px; margin: 0; overflow: auto; padding: 11px; border-radius: 8px; color: var(--mobile-text-secondary); background: var(--mobile-surface-muted); font-family: ui-monospace, monospace; font-size: 11px; line-height: 1.5; white-space: pre-wrap; overflow-wrap: anywhere; }
.inline-error { margin: 13px 0; padding: 10px; border-radius: 8px; color: var(--mobile-danger); background: var(--mobile-danger-soft); font-size: 12px; }
.action-modal { position: fixed; z-index: 60; inset: 0; display: grid; align-items: end; }
.action-modal__backdrop { position: absolute; inset: 0; border: 0; background: rgba(12, 22, 38, .45); }
.action-modal__sheet { position: relative; width: min(100%, 768px); margin: 0 auto; padding: 10px 16px calc(18px + env(safe-area-inset-bottom)); border-radius: 17px 17px 0 0; background: var(--mobile-surface); box-shadow: 0 -12px 35px rgba(12,22,38,.2); }
.action-modal__handle { width: 38px; height: 4px; margin: 0 auto 14px; border-radius: 3px; background: var(--mobile-border); }
.action-modal__sheet h2 { margin: 0 0 16px; font-size: 17px; }
.action-modal__buttons { display: flex; gap: 8px; margin-top: 18px; }
.action-modal__buttons .button { flex: 1; }
</style>
