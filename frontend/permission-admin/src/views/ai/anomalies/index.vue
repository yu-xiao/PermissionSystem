<script setup lang="ts">
defineOptions({ name: 'AiAnomalies' })
import { computed, onActivated, onDeactivated, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import PageContainer from '../../../components/PageContainer/index.vue'
import { useAuthStore } from '../../../stores/auth'
import {
  closeAnomalyEvent,
  createAnomalyRule,
  getAnomalyEvent,
  getAnomalyEvents,
  getAnomalyRules,
  setAnomalyEnabled,
  triggerAnomalyCheck,
  type AiAnomalyEvent,
  type AiAnomalyRule,
} from '../../../api/ai-anomalies'

const auth = useAuthStore()
const route = useRoute()
const canView = computed(
  () =>
    auth.hasPermission('system:scheduled-task:view') &&
    auth.hasPermission('demo-business-order:view'),
)
const canCreate = computed(() => auth.hasPermission('system:scheduled-task:create'))
const canUpdate = computed(() => auth.hasPermission('system:scheduled-task:update'))
const canTrigger = computed(() => auth.hasPermission('system:scheduled-task:trigger'))
const rule = ref<AiAnomalyRule>()
const events = ref<AiAnomalyEvent[]>([])
const detail = ref<AiAnomalyEvent>()
const page = ref(1)
const total = ref(0)
const loading = ref(false)
const saving = ref(false)
const error = ref('')
let generation = 0
let active = true

const statuses: Record<string, string> = {
  Pending: '待投递',
  CoolingDown: '冷却中',
  Disabled: '通知禁用',
  RetryPending: '等待重试',
  Queued: '已入队，待消费',
  Delivered: '已保存站内通知',
  Suppressed: '已抑制',
  Failed: '失败，需人工处理',
}
const reasons: Record<string, string> = {
  manual: '人工关闭',
  recovered: '已恢复正常',
  scope_changed: '授权范围变化',
  result_changed: '结果变化',
  delivery_no_longer_authorized: '投递条件失效',
}
const state = (value: string) => statuses[value] ?? '未知状态'
const date = (value?: string) =>
  value ? new Date(value).toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai' }) : '—'
function clear() {
  generation++
  rule.value = undefined
  events.value = []
  detail.value = undefined
  total.value = 0
  loading.value = false
}
async function load() {
  clear()
  const id = generation
  error.value = ''
  if (!active || !canView.value) return
  loading.value = true
  try {
    const result = await getAnomalyRules()
    if (id !== generation) return
    rule.value = result.items[0]
    if (rule.value) {
      const history = await getAnomalyEvents(rule.value.id, page.value)
      if (id !== generation) return
      events.value = history.items
      total.value = history.totalCount
    }
    const eventId = route.query.eventId
    if (
      typeof eventId === 'string' &&
      /^[a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12}$/i.test(eventId)
    ) {
      const event = await getAnomalyEvent(eventId)
      if (id === generation) detail.value = event
    }
  } catch {
    if (id === generation) {
      events.value = []
      detail.value = undefined
      error.value = '读取失败，请核对当前身份、业务权限和提醒配置后重试。'
    }
  } finally {
    if (id === generation) loading.value = false
  }
}
async function act(action: () => Promise<unknown>, success: string) {
  if (saving.value || !active) return
  const id = generation
  saving.value = true
  try {
    await action()
    if (id === generation) ElMessage.success(success)
  } catch {
    if (id === generation) ElMessage.error('操作失败，可能已撤权或记录已变化，请刷新后重试。')
  } finally {
    saving.value = false
    if (id === generation) await load()
  }
}
async function enable(item: AiAnomalyRule) {
  const id = generation
  if (!item.isEnabled) {
    try {
      await ElMessageBox.confirm(
        '启用后系统将持续以你当前的权限范围检查 Demo 单据，仅向你发送站内提醒。关闭浏览器不会停止，请使用暂停停止检查。',
        '启用个人周期提醒',
        { type: 'warning' },
      )
    } catch {
      return
    }
  }
  if (id === generation && canUpdate.value)
    await act(() => setAnomalyEnabled(item, !item.isEnabled), item.isEnabled ? '已暂停' : '已启用')
}
async function close(item: AiAnomalyEvent) {
  const id = generation
  try {
    await ElMessageBox.confirm(
      '关闭后同一持续异常不会再次提醒；一次成功查询确认数量归零后，才会开始新的异常周期。源单据不会被修改。',
      '关闭当前异常',
      { type: 'warning' },
    )
  } catch {
    return
  }
  if (id === generation && canUpdate.value)
    await act(() => closeAnomalyEvent(item), '已关闭当前异常')
}
async function inspect(item: AiAnomalyEvent) {
  detail.value = undefined
  const id = generation
  try {
    const result = await getAnomalyEvent(item.id)
    if (id === generation) detail.value = result
  } catch {
    if (id === generation) error.value = '证据已不可读，请重新检查当前授权。'
  }
}
watch(
  () => [
    auth.effectiveTenantId,
    auth.currentUser?.userId,
    auth.currentUser?.permissionCodes,
    canView.value,
  ],
  () => {
    page.value = 1
    void load()
  },
  { deep: true },
)
watch(
  () => route.query.eventId,
  () => {
    void load()
  },
)
onMounted(() => {
  void load()
})
let activatedOnce = false
onActivated(() => {
  active = true
  if (activatedOnce) void load()
  activatedOnce = true
})
onDeactivated(() => {
  active = false
  clear()
})
</script>

<template>
  <PageContainer
    title="Demo 异常提醒"
    description="固定规则验证：授权范围内存在待审批 Demo 单据。不是审批超时规则，不调用模型。"
  >
    <template #actions>
      <el-button :disabled="loading || saving" @click="load">刷新</el-button>
      <el-button
        v-if="canView && canCreate && !rule && !loading && !error"
        type="primary"
        :loading="saving"
        @click="act(createAnomalyRule, '规则已创建，默认暂停')"
        >创建个人提醒</el-button
      >
    </template>
    <el-alert
      v-if="!canView"
      title="需要定时任务查看及 Demo 单据查看权限。"
      type="warning"
      :closable="false"
    />
    <template v-else>
      <el-alert
        title="每 5 分钟检查 · 上海时区 · 仅通知本人 · 持续异常单次通知 · 24 小时冷却 · 模型费用 0"
        type="info"
        :closable="false"
        show-icon
      />
      <el-alert v-if="error" :title="error" type="error" :closable="false" role="alert" />
      <section v-loading="loading" class="rule-panel" aria-label="个人提醒规则">
        <el-empty v-if="!rule && !loading && !error" description="尚未创建个人提醒" />
        <template v-if="rule">
          <el-descriptions :column="1" border>
            <el-descriptions-item label="判定口径">{{ rule.basis }}</el-descriptions-item>
            <el-descriptions-item label="运行状态"
              ><el-tag :type="rule.isEnabled ? 'success' : 'info'">{{
                rule.isEnabled ? '已启用' : '已暂停'
              }}</el-tag></el-descriptions-item
            >
            <el-descriptions-item label="最近检查（上海时间）"
              >{{ date(rule.lastRunAt) }} ·
              {{
                rule.lastRunSucceeded == null ? '未执行' : rule.lastRunSucceeded ? '成功' : '失败'
              }}</el-descriptions-item
            >
            <el-descriptions-item label="最近通知（上海时间）">{{
              date(rule.lastNotifiedAt)
            }}</el-descriptions-item>
          </el-descriptions>
          <div class="rule-actions">
            <el-button v-if="canUpdate" :loading="saving" @click="enable(rule)">{{
              rule.isEnabled ? '暂停' : '启用'
            }}</el-button>
            <el-button
              v-if="canTrigger"
              :disabled="!rule.isEnabled || saving"
              @click="act(() => triggerAnomalyCheck(rule!.id), '检查已入队，请稍后刷新查看结果')"
              >立即检查</el-button
            >
          </div>
        </template>
      </section>
      <h2 v-if="rule">异常事件</h2>
      <el-table v-if="rule" :data="events" border aria-label="异常事件列表">
        <el-table-column prop="episodeSequence" label="周期" width="80" />
        <el-table-column label="观察时间（上海）" min-width="180"
          ><template #default="{ row }">{{ date(row.observedAt) }}</template></el-table-column
        >
        <el-table-column label="观察数量（单）" min-width="160"
          ><template #default="{ row }">{{
            row.evidenceUnavailable ? '范围已变化，证据隐藏' : row.observedCount
          }}</template></el-table-column
        >
        <el-table-column label="通知状态" min-width="160"
          ><template #default="{ row }">{{ state(row.deliveryStatus) }}</template></el-table-column
        >
        <el-table-column label="异常状态" min-width="140"
          ><template #default="{ row }">{{
            row.closedAt ? (reasons[row.closeReason ?? ''] ?? '已关闭') : '待处理'
          }}</template></el-table-column
        >
        <el-table-column label="操作" width="160"
          ><template #default="{ row }">
            <el-button link type="primary" :disabled="saving" @click="inspect(row)"
              >重新核验</el-button
            >
            <el-button
              v-if="canUpdate && !row.closedAt"
              link
              type="danger"
              :disabled="saving"
              @click="close(row)"
              >关闭</el-button
            >
          </template></el-table-column
        >
      </el-table>
      <el-pagination
        v-if="rule"
        v-model:current-page="page"
        class="pager"
        layout="total, prev, pager, next"
        :total="total"
        :page-size="20"
        @current-change="load"
      />
      <el-dialog
        :model-value="Boolean(detail)"
        title="当前授权下的事件核验"
        width="min(680px, 95vw)"
        @close="detail = undefined"
      >
        <el-descriptions v-if="detail" :column="1" border>
          <el-descriptions-item label="口径">{{ detail.basis }}</el-descriptions-item>
          <el-descriptions-item label="历史观察数量">{{
            detail.evidenceUnavailable
              ? '当前范围与原范围不同，已隐藏；请重新检查。'
              : `${detail.observedCount} 单`
          }}</el-descriptions-item>
          <el-descriptions-item label="观察时间（上海）">{{
            date(detail.observedAt)
          }}</el-descriptions-item>
          <el-descriptions-item label="投递">{{
            state(detail.deliveryStatus)
          }}</el-descriptions-item>
          <el-descriptions-item label="发送尝试"
            >{{ detail.attemptCount }} / 3</el-descriptions-item
          >
          <el-descriptions-item label="下次尝试（上海）">{{
            date(detail.nextAttemptAt)
          }}</el-descriptions-item>
          <el-descriptions-item label="错误码">{{ detail.errorCode || '—' }}</el-descriptions-item>
          <el-descriptions-item label="运输状态">{{
            detail.transportStatus || '—'
          }}</el-descriptions-item>
        </el-descriptions>
      </el-dialog>
    </template>
  </PageContainer>
</template>

<style scoped>
.rule-panel {
  margin-block: 20px;
}
.rule-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
  margin-top: 16px;
}
h2 {
  font-size: 18px;
  color: var(--el-text-color-primary);
}
.pager {
  margin-top: 16px;
}
</style>
