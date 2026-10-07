<script setup lang="ts">
import {
  ChatDotRound,
  CircleCheck,
  CircleClose,
  Delete,
  Plus,
  Promotion,
  Refresh,
} from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { computed, nextTick, onBeforeUnmount, ref, watch } from 'vue'
import {
  cancelAiRun,
  createAiConversation,
  deleteAiConversation,
  getAiConversation,
  getAiConversations,
  getAiRun,
  getAiSubmission,
  saveMyAiFeedback,
  sendAiMessage,
  retryAiRun,
  type AiConversationDetail,
  type AiConversationListItem,
  type AiFeedback,
  type AiRunRealtimeMessage,
  type AiToolCitation,
  type AiContextReference,
  type AiStructuredResult,
  type AiRun,
} from '../api/ai'
import { startAiRunConnection, type SignalRLiteConnection } from '../utils/signalr-lite'
import { useAuthStore } from '../stores/auth'
import { getAiScenarioOptions, type AiScenarioOption } from '../api/aiScenario'
import AiDocumentDraftCard from './AiDocumentDraftCard.vue'
import AiPermissionDiagnosticCard from './AiPermissionDiagnosticCard.vue'
import AiStructuredResultCard from './AiStructuredResultCard.vue'
import type { AiDocumentDraft } from '../api/ai'

const authStore = useAuthStore()
const visible = ref(false)
const loading = ref(false)
const sending = ref(false)
const conversations = ref<AiConversationListItem[]>([])
const sceneOptions = ref<AiScenarioOption[]>([])
const newScenarioId = ref('')
const upgradeOption = computed(() =>
  sceneOptions.value.find(
    (s) => s.id === current.value?.scenarioId && s.versionId !== current.value?.scenarioVersionId,
  ),
)
const current = ref<AiConversationDetail>()
const draft = ref('')
const activeRunId = ref('')
const activeRunStatus = ref<number>()
const cancellationPending = ref(false)
let progressVersion = -1
let pollTimer: ReturnType<typeof setTimeout> | undefined
let refreshing = false
let resultRefreshPending = false
let pollFailures = 0
let selectionVersion = 0
interface PendingSubmission {
  key: string
  content?: string
  contextRef?: AiContextReference
  offset?: number
  retryRunId?: string
}
const uncertainSubmissions = ref<Record<string, PendingSubmission>>({})
const citations = ref<AiToolCitation[]>([])
const toolEvents = ref<AiRunRealtimeMessage[]>([])
const feedbackByRun = ref<Record<string, AiFeedback>>({})
const feedbackDialogVisible = ref(false)
const feedbackRunId = ref('')
const feedbackReason = ref('incorrect')
const feedbackComment = ref('')
const messageViewport = ref<HTMLElement>()
const contextRef = ref<AiContextReference>()
const utcOffsetMinutes = ref<number>()
const utcOffsets = Array.from({ length: 27 }, (_, index) => index - 12).map((hours) => ({
  value: hours * 60,
  label: `UTC${hours >= 0 ? '+' : '-'}${String(Math.abs(hours)).padStart(2, '0')}:00`,
}))
const structuredResults = computed(() => current.value?.structuredResults ?? [])
const selectedResult = computed(() =>
  structuredResults.value.find(
    (item) =>
      item.runId === contextRef.value?.runId &&
      item.invocationId === contextRef.value?.invocationId,
  ),
)
const legacyDiagnostics = computed(() =>
  (current.value?.permissionDiagnostics ?? []).filter(
    (item) =>
      !structuredResults.value.some(
        (result) => result.runId === item.runId && result.invocationId === item.invocationId,
      ),
  ),
)
const unattachedResults = computed(() =>
  structuredResults.value.filter(
    (item) => !current.value?.messages.some((message) => message.runId === item.runId),
  ),
)
watch(
  () => current.value?.id,
  () => {
    contextRef.value = undefined
    utcOffsetMinutes.value = undefined
  },
)
watch(structuredResults, () => {
  if (contextRef.value && !selectedResult.value) contextRef.value = undefined
})

function chooseContext(reference: AiContextReference) {
  if (!sending.value) contextRef.value = reference
}
function isSelected(result: AiStructuredResult) {
  return (
    result.runId === contextRef.value?.runId &&
    result.invocationId === contextRef.value?.invocationId
  )
}
let connection: SignalRLiteConnection | undefined

const canSend = computed(() =>
  Boolean(
    current.value &&
    draft.value.trim() &&
    !sending.value &&
    !canCancel.value &&
    !uncertainSubmissions.value[current.value.id] &&
    draft.value.length <= 4000,
  ),
)
const canCancel = computed(() =>
  Boolean(activeRunId.value && (activeRunStatus.value === 1 || activeRunStatus.value === 2)),
)
const canVerifyDocuments = computed(() =>
  ['security:verification:send', 'security:verification:verify'].every((permission) =>
    authStore.hasPermission(permission),
  ),
)
const composerPlaceholder = computed(
  () => '输入权限排障需求，或查询用户、部门、角色、日志及已批准报表',
)

async function open() {
  const existingId = current.value?.id
  visible.value = true
  if (!connection) {
    connection = await startAiRunConnection(handleRunEvent, () => {
      void refreshRun()
    }).catch(() => undefined)
  }
  await Promise.all([loadConversations(), loadSceneOptions()])
  if (existingId) await restoreConversation(existingId)
}

async function loadConversations() {
  loading.value = true
  try {
    const result = await getAiConversations({ pageIndex: 1, pageSize: 50 })
    conversations.value = result.items
    if (!current.value && conversations.value.length > 0) {
      await selectConversation(conversations.value[0].id)
    }
  } finally {
    loading.value = false
  }
}

async function loadSceneOptions() {
  sceneOptions.value = await getAiScenarioOptions()
  if (!sceneOptions.value.some((s) => s.id === newScenarioId.value)) newScenarioId.value = ''
}

async function upgradeConversation() {
  if (!upgradeOption.value || sending.value) return
  await ElMessageBox.confirm(
    '将使用当前发布版本新建会话。旧会话及结果保留，新会话需要重新明确查询条件。',
    '升级会话',
  )
  newScenarioId.value = upgradeOption.value.id
  await newConversation()
}

async function newConversation() {
  if (sending.value) return
  const created = await createAiConversation(undefined, newScenarioId.value || undefined)
  selectionVersion++
  conversations.value.unshift(created)
  current.value = created
  resetRunState()
  await scrollToBottom()
}

async function selectConversation(id: string) {
  if (current.value?.id === id) {
    return
  }
  await restoreConversation(id)
  await loadFeedback()
  await scrollToBottom()
}

async function restoreConversation(id: string) {
  const version = ++selectionVersion
  const detail = await getAiConversation(id)
  if (version !== selectionVersion) return
  current.value = detail
  resetRunState()
  if (detail.latestRun) applyRun(detail.latestRun)
}

function applyRun(run: AiRun) {
  if (run.conversationId && run.conversationId !== current.value?.id) return
  if (activeRunId.value === run.id && (run.progressVersion ?? 0) < progressVersion) return
  if (
    activeRunId.value === run.id &&
    activeRunStatus.value &&
    activeRunStatus.value >= 3 &&
    run.status < 3
  )
    return
  activeRunId.value = run.id
  activeRunStatus.value = run.status
  progressVersion = run.progressVersion ?? 0
  cancellationPending.value = Boolean(run.cancellationRequestedAt && run.status < 3)
  citations.value = run.citations
  toolEvents.value = (run.toolProgress ?? []).map((t) => ({
    runId: run.id,
    conversationId: run.conversationId,
    eventType: 'tool.snapshot',
    status: run.status,
    progressVersion: run.progressVersion,
    invocationId: t.invocationId,
    toolCode: t.toolCode,
    toolStatus: t.status,
    occurredAt: t.completedAt ?? '',
  }))
  connection?.subscribeRun?.(run.id)
  schedulePoll()
}

function schedulePoll() {
  if (pollTimer) clearTimeout(pollTimer)
  pollTimer = undefined
  if (visible.value && (canCancel.value || resultRefreshPending))
    pollTimer = setTimeout(
      () => {
        void refreshRun()
      },
      Math.min(30_000, 3000 * 2 ** pollFailures),
    )
}

async function refreshRun() {
  if (refreshing || !activeRunId.value || !current.value || !visible.value) return
  const runId = activeRunId.value
  const conversationId = current.value.id
  refreshing = true
  try {
    const run = await getAiRun(runId)
    if (current.value?.id !== conversationId || activeRunId.value !== runId) return
    applyRun(run)
    pollFailures = 0
    if (run.status >= 3) {
      resultRefreshPending = true
      const detail = await getAiConversation(conversationId)
      if (current.value?.id === conversationId && activeRunId.value === runId) {
        current.value = detail
        resultRefreshPending = false
        if (detail.latestRun && detail.latestRun.id !== runId) applyRun(detail.latestRun)
        await loadFeedback()
        await scrollToBottom()
      }
    }
  } catch {
    pollFailures = Math.min(4, pollFailures + 1)
  } finally {
    refreshing = false
    schedulePoll()
  }
}

watch(visible, () => {
  schedulePoll()
})

async function removeConversation(item: AiConversationListItem) {
  await ElMessageBox.confirm(`确认删除会话“${item.title}”？`, '删除会话')
  await deleteAiConversation(item.id)
  conversations.value = conversations.value.filter((conversation) => conversation.id !== item.id)
  if (current.value?.id === item.id) {
    current.value = undefined
    resetRunState()
    if (conversations.value.length > 0) {
      await selectConversation(conversations.value[0].id)
    }
  }
}

async function submit() {
  const content = draft.value.trim()
  if (!canSend.value || !current.value) {
    return
  }

  const conversationId = current.value.id
  const submission = {
    key: crypto.randomUUID(),
    content,
    contextRef: contextRef.value,
    offset: utcOffsetMinutes.value,
  }
  uncertainSubmissions.value[conversationId] = submission
  current.value.messages.push({
    id: `pending-${Date.now()}`,
    role: 2,
    content,
    sequence: current.value.messages.length + 1,
    modelGenerated: false,
    createdAt: new Date().toISOString(),
  })
  draft.value = ''
  sending.value = true
  activeRunStatus.value = 1
  citations.value = []
  toolEvents.value = []
  await scrollToBottom()

  try {
    const run = await sendAiMessage(
      conversationId,
      content,
      submission.contextRef,
      submission.offset,
      submission.key,
    )
    delete uncertainSubmissions.value[conversationId]
    if (current.value?.id === conversationId) {
      applyRun(run)
      const detail = await getAiConversation(conversationId)
      if (current.value?.id === conversationId) {
        current.value = detail
        await loadFeedback()
      }
    }
    await loadConversations()
    await scrollToBottom()
  } catch (error) {
    if ([400, 401, 403, 422].includes(responseStatus(error) ?? 0))
      delete uncertainSubmissions.value[conversationId]
    if (current.value?.id === conversationId) {
      await restoreConversation(conversationId).catch(() => undefined)
      ElMessage.warning('提交结果未确认，请查看会话中的运行状态后再操作。')
    }
  } finally {
    sending.value = false
  }
}

function responseStatus(error: unknown) {
  return (error as { response?: { status?: number } })?.response?.status
}

async function recoverSubmission() {
  const id = current.value?.id
  const pending = id && uncertainSubmissions.value[id]
  if (!id || !pending || sending.value) return
  sending.value = true
  try {
    let run: AiRun
    try {
      run = await getAiSubmission(id, pending.key)
    } catch (error) {
      if (responseStatus(error) !== 404) throw error
      run = pending.retryRunId
        ? await retryAiRun(pending.retryRunId, pending.key)
        : await sendAiMessage(id, pending.content!, pending.contextRef, pending.offset, pending.key)
    }
    delete uncertainSubmissions.value[id]
    if (current.value?.id === id) {
      applyRun(run)
      const detail = await getAiConversation(id)
      if (current.value?.id === id) current.value = detail
    }
  } catch {
    ElMessage.warning('仍未确认提交结果，请稍后恢复状态。')
  } finally {
    sending.value = false
  }
}

async function loadFeedback() {
  const entries =
    current.value?.messages.flatMap((message) =>
      message.runId && message.feedback ? [[message.runId, message.feedback] as const] : [],
    ) ?? []
  feedbackByRun.value = Object.fromEntries(entries) as Record<string, AiFeedback>
}

async function approveAnswer(runId: string) {
  feedbackByRun.value[runId] = await saveMyAiFeedback(runId, { rating: 1 })
  ElMessage.success('感谢反馈')
}

function openNegativeFeedback(runId: string) {
  const existing = feedbackByRun.value[runId]
  feedbackRunId.value = runId
  feedbackReason.value = existing?.reasonCode ?? 'incorrect'
  feedbackComment.value = existing?.comment ?? ''
  feedbackDialogVisible.value = true
}

async function submitNegativeFeedback() {
  feedbackByRun.value[feedbackRunId.value] = await saveMyAiFeedback(feedbackRunId.value, {
    rating: 2,
    reasonCode: feedbackReason.value,
    comment: feedbackComment.value.trim() || undefined,
  })
  feedbackDialogVisible.value = false
  ElMessage.success('反馈已提交')
}

function updateDocumentDraft(value: AiDocumentDraft) {
  if (!current.value) return
  const index = current.value.documentDrafts.findIndex((item) => item.id === value.id)
  if (index >= 0) {
    current.value.documentDrafts[index] = value
  } else {
    current.value.documentDrafts.push(value)
  }
}

async function cancelRun() {
  if (!activeRunId.value) {
    return
  }
  await cancelAiRun(activeRunId.value)
  cancellationPending.value = true
  await refreshRun()
}

async function retryRun() {
  if (!activeRunId.value || !current.value || sending.value) return
  sending.value = true
  const conversationId = current.value.id
  const submission = { key: crypto.randomUUID(), retryRunId: activeRunId.value }
  uncertainSubmissions.value[conversationId] = submission
  try {
    const run = await retryAiRun(submission.retryRunId, submission.key)
    delete uncertainSubmissions.value[conversationId]
    if (current.value?.id !== conversationId) return
    applyRun(run)
    const detail = await getAiConversation(conversationId)
    if (current.value?.id !== conversationId) return
    current.value = detail
    await loadFeedback()
    await loadConversations()
  } catch (error) {
    if ([400, 401, 403, 422].includes(responseStatus(error) ?? 0))
      delete uncertainSubmissions.value[conversationId]
    ElMessage.warning('重试提交结果未确认，可恢复提交状态。')
  } finally {
    sending.value = false
  }
}

function handleRunEvent(value: unknown) {
  const event = value as AiRunRealtimeMessage
  if (!event?.runId || event.conversationId !== current.value?.id) {
    return
  }
  if (event.runId !== activeRunId.value || (event.progressVersion ?? 0) < progressVersion) return
  if (activeRunStatus.value && activeRunStatus.value >= 3 && event.status < 3) return
  progressVersion = event.progressVersion ?? progressVersion
  activeRunStatus.value = event.status
  if (event.toolCode) {
    let runningIndex = -1
    for (let index = toolEvents.value.length - 1; index >= 0; index -= 1) {
      const item = toolEvents.value[index]
      if (item.invocationId === event.invocationId) {
        runningIndex = index
        break
      }
    }
    if (runningIndex >= 0) {
      toolEvents.value[runningIndex] = event
    } else {
      toolEvents.value.push(event)
    }
  }
  if (event.status >= 3) {
    resultRefreshPending = true
    void refreshRun()
  }
}

function resetRunState() {
  if (pollTimer) clearTimeout(pollTimer)
  pollTimer = undefined
  progressVersion = -1
  resultRefreshPending = false
  cancellationPending.value = false
  contextRef.value = undefined
  activeRunId.value = ''
  activeRunStatus.value = undefined
  citations.value = []
  toolEvents.value = []
}

function runStatusText() {
  if (cancellationPending.value) return '取消请求已提交'
  switch (activeRunStatus.value) {
    case 1:
      return '等待执行'
    case 2:
      return '正在查询'
    case 3:
      return '已完成'
    case 4:
      return '执行失败'
    case 5:
      return '已取消'
    default:
      return ''
  }
}

function toolStatusType(status?: number) {
  if (status === 3) return 'success'
  if (status === 4) return 'danger'
  if (status === 5) return 'info'
  return 'primary'
}

function toolStatusText(status?: number) {
  if (status === 3) return '完成'
  if (status === 4) return '失败'
  if (status === 5) return '取消'
  return '查询中'
}

function formatDate(value?: string) {
  return value ? new Date(value).toLocaleString() : '-'
}

async function scrollToBottom() {
  await nextTick()
  if (messageViewport.value) {
    messageViewport.value.scrollTop = messageViewport.value.scrollHeight
  }
}

onBeforeUnmount(() => {
  connection?.stop()
  if (pollTimer) clearTimeout(pollTimer)
})

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" class="ai-chat-dialog" width="min(1040px, 96vw)" top="4vh">
    <template #header>
      <div class="ai-dialog-title">
        <ChatDotRound />
        <span>AI 中心</span>
        <el-tag v-if="current?.scenarioVersionId" size="small"
          >权限助手 v{{ current.agentVersion }}</el-tag
        >
        <el-tag v-else size="small" type="info">内置平台助手</el-tag>
        <el-tag v-if="runStatusText()" size="small" effect="plain">{{ runStatusText() }}</el-tag>
      </div>
    </template>

    <div class="ai-workspace">
      <aside class="ai-sidebar">
        <div class="ai-sidebar__toolbar">
          <strong>会话</strong>
          <div>
            <el-tooltip content="刷新会话" placement="bottom">
              <el-button text :icon="Refresh" @click="loadConversations" />
            </el-tooltip>
            <el-tooltip content="新建会话" placement="bottom">
              <el-button
                type="primary"
                text
                :icon="Plus"
                aria-label="新建会话"
                @click="newConversation"
              />
            </el-tooltip>
          </div>
        </div>
        <el-select
          v-model="newScenarioId"
          aria-label="新会话场景"
          :disabled="sending"
          placeholder="选择新会话场景"
        >
          <el-option label="内置平台助手" value="" />
          <el-option
            v-for="scene in sceneOptions"
            :key="scene.id"
            :label="`${scene.name} v${scene.versionNumber}`"
            :value="scene.id"
          />
        </el-select>
        <el-button v-if="upgradeOption" :disabled="sending" @click="upgradeConversation"
          >以当前发布版本新建会话</el-button
        >
        <div v-loading="loading" class="ai-conversation-list">
          <button
            v-for="item in conversations"
            :key="item.id"
            class="ai-conversation-item"
            :class="{ 'is-active': current?.id === item.id }"
            type="button"
            @click="selectConversation(item.id)"
          >
            <span class="ai-conversation-item__title">{{ item.title }}</span>
            <span class="ai-conversation-item__time">{{ formatDate(item.lastMessageAt) }}</span>
            <el-button
              class="ai-conversation-item__delete"
              text
              :icon="Delete"
              aria-label="删除会话"
              @click.stop="removeConversation(item)"
            />
          </button>
          <el-empty
            v-if="!loading && conversations.length === 0"
            :image-size="64"
            description="暂无会话"
          />
        </div>
      </aside>

      <section class="ai-chat-pane">
        <div ref="messageViewport" class="ai-messages">
          <el-empty v-if="!current" :image-size="88" description="新建会话后开始提问" />
          <template v-else>
            <div
              v-for="message in current.messages"
              :key="message.id"
              class="ai-message"
              :class="message.role === 2 ? 'is-user' : 'is-assistant'"
            >
              <div class="ai-message__meta">{{ message.role === 2 ? '我' : 'AI' }}</div>
              <div class="ai-message__content">{{ message.content }}</div>
              <AiStructuredResultCard
                v-for="result in structuredResults.filter((item) => item.runId === message.runId)"
                :key="`${result.runId}-${result.invocationId}`"
                :result="result"
                :selected="isSelected(result)"
                :busy="sending"
                @select="chooseContext"
              />
              <div v-if="message.runId" class="ai-message__feedback">
                <el-tooltip content="回答有帮助">
                  <el-button
                    circle
                    text
                    :type="feedbackByRun[message.runId]?.rating === 1 ? 'success' : 'default'"
                    :icon="CircleCheck"
                    @click="approveAnswer(message.runId)"
                  />
                </el-tooltip>
                <el-tooltip content="回答需要改进">
                  <el-button
                    circle
                    text
                    :type="feedbackByRun[message.runId]?.rating === 2 ? 'danger' : 'default'"
                    :icon="CircleClose"
                    @click="openNegativeFeedback(message.runId)"
                  />
                </el-tooltip>
              </div>
            </div>

            <div v-if="sending" class="ai-progress">
              <span>{{ runStatusText() || '正在处理' }}</span>
              <el-tag
                v-for="(event, index) in toolEvents"
                :key="`${event.toolCode}-${index}`"
                size="small"
                :type="toolStatusType(event.toolStatus)"
                effect="plain"
              >
                {{ event.toolCode }} · {{ toolStatusText(event.toolStatus) }}
              </el-tag>
            </div>

            <el-collapse v-if="citations.length" class="ai-citations">
              <el-collapse-item :title="`引用来源（${citations.length}）`" name="citations">
                <div
                  v-for="citation in citations"
                  :key="`${citation.toolCode}-${citation.queriedAt}`"
                  class="ai-citation"
                >
                  <strong>{{ citation.toolCode }}</strong>
                  <span
                    >{{ citation.sourceSystem }} · {{ citation.rowCount }} 条（引用口径） ·
                    {{ formatDate(citation.queriedAt) }}</span
                  >
                  <span v-if="citation.datasetCode">数据集：{{ citation.datasetCode }}</span>
                </div>
              </el-collapse-item>
            </el-collapse>

            <section v-if="legacyDiagnostics.length" aria-label="权限诊断结果">
              <AiPermissionDiagnosticCard
                v-for="item in legacyDiagnostics"
                :key="`${item.runId}-${item.invocationId}`"
                :diagnostic="item"
              />
            </section>

            <AiStructuredResultCard
              v-for="result in unattachedResults"
              :key="`${result.runId}-${result.invocationId}`"
              :result="result"
              :selected="isSelected(result)"
              :busy="sending"
              @select="chooseContext"
            />
            <p v-if="current.structuredResultsUnavailable" class="ai-result-notice">
              部分历史结果已过期或当前不可读取，请重新明确条件并查询。
            </p>
            <p v-if="current.structuredResultsWindowLimited" class="ai-result-notice">
              仅展示近期且大小允许的结构化结果；请明确选择查询对象。
            </p>

            <section v-if="current.documentDrafts.length" class="ai-document-drafts">
              <AiDocumentDraftCard
                v-for="item in current.documentDrafts"
                :key="item.id"
                :draft="item"
                :can-verify="canVerifyDocuments"
                @updated="updateDocumentDraft"
              />
            </section>
          </template>
        </div>

        <div class="ai-composer">
          <el-select
            v-model="utcOffsetMinutes"
            clearable
            placeholder="自然月查询时区（需明确选择）"
            :disabled="sending"
            aria-label="自然月查询时区"
            class="ai-timezone-select"
          >
            <el-option
              v-for="offset in utcOffsets"
              :key="offset.value"
              :label="offset.label"
              :value="offset.value"
            />
          </el-select>
          <div v-if="selectedResult" class="ai-follow-up-selection" aria-label="当前追问对象">
            <span
              >追问：{{
                selectedResult.type === 'table'
                  ? '用户查询'
                  : selectedResult.type === 'statistics-summary'
                    ? '日志统计'
                    : '权限证据'
              }}
              · {{ formatDate(selectedResult.queriedAt) }}</span
            >
            <el-button text size="small" :disabled="sending" @click="contextRef = undefined"
              >清除选择</el-button
            >
          </div>
          <el-input
            v-model="draft"
            type="textarea"
            resize="none"
            :rows="3"
            :maxlength="4000"
            show-word-limit
            :placeholder="composerPlaceholder"
            :disabled="!current || sending"
            @keydown.ctrl.enter.prevent="submit"
          />
          <div class="ai-composer__actions">
            <el-button
              v-if="current && uncertainSubmissions[current.id]"
              :disabled="sending"
              @click="recoverSubmission"
              >恢复提交状态</el-button
            >
            <el-tooltip content="取消当前任务" placement="top">
              <el-button :icon="CircleClose" :disabled="!canCancel" @click="cancelRun"
                >取消</el-button
              >
            </el-tooltip>
            <el-button
              v-if="activeRunId && (activeRunStatus === 4 || activeRunStatus === 5)"
              :disabled="sending"
              @click="retryRun"
              >重试</el-button
            >
            <el-tooltip content="发送" placement="top">
              <el-button type="primary" :icon="Promotion" :disabled="!canSend" @click="submit" />
            </el-tooltip>
          </div>
        </div>
      </section>
    </div>

    <el-dialog v-model="feedbackDialogVisible" append-to-body title="回答反馈" width="480px">
      <el-form label-position="top">
        <el-form-item label="原因">
          <el-select v-model="feedbackReason">
            <el-option label="结论不正确" value="incorrect" />
            <el-option label="没有回答问题" value="not_relevant" />
            <el-option label="来源或口径不清楚" value="unclear_source" />
            <el-option label="回答不完整" value="incomplete" />
          </el-select>
        </el-form-item>
        <el-form-item label="补充说明">
          <el-input
            v-model="feedbackComment"
            type="textarea"
            :rows="4"
            maxlength="500"
            show-word-limit
          />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="feedbackDialogVisible = false">取消</el-button>
        <el-button type="primary" @click="submitNegativeFeedback">提交</el-button>
      </template>
    </el-dialog>
  </el-dialog>
</template>

<style scoped>
.ai-follow-up-selection {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 12px;
  margin-bottom: 8px;
  font-size: 12px;
}
.ai-result-notice {
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.ai-timezone-select {
  width: 260px;
  margin-bottom: 8px;
}
.ai-dialog-title,
.ai-sidebar__toolbar,
.ai-composer__actions {
  display: flex;
  align-items: center;
}

.ai-dialog-title {
  gap: 8px;
  font-weight: 600;
}

.ai-dialog-title svg {
  width: 20px;
}

.ai-workspace {
  display: grid;
  grid-template-columns: minmax(210px, 240px) minmax(0, 1fr);
  height: min(720px, 78vh);
  overflow: hidden;
  border: 1px solid var(--el-border-color);
}

.ai-sidebar {
  min-width: 0;
  border-right: 1px solid var(--el-border-color);
  background: var(--el-fill-color-extra-light);
}

.ai-sidebar__toolbar {
  justify-content: space-between;
  height: 48px;
  padding: 0 12px;
  border-bottom: 1px solid var(--el-border-color);
}

.ai-conversation-list {
  height: calc(100% - 49px);
  overflow-y: auto;
}

.ai-conversation-item {
  position: relative;
  display: grid;
  gap: 5px;
  width: 100%;
  min-height: 62px;
  padding: 10px 38px 10px 12px;
  border: 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: transparent;
  color: var(--el-text-color-primary);
  text-align: left;
  cursor: pointer;
}

.ai-conversation-item:hover,
.ai-conversation-item.is-active {
  background: var(--el-color-primary-light-9);
}

.ai-conversation-item__title {
  overflow: hidden;
  font-size: 14px;
  font-weight: 500;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-conversation-item__time {
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.ai-conversation-item__delete {
  position: absolute;
  top: 14px;
  right: 5px;
}

.ai-chat-pane {
  display: grid;
  grid-template-rows: minmax(0, 1fr) auto;
  min-width: 0;
  background: var(--el-bg-color);
}

.ai-messages {
  overflow-y: auto;
  padding: 18px;
}

.ai-message {
  width: min(78%, 680px);
  margin-bottom: 16px;
}

.ai-message.is-user {
  margin-left: auto;
}

.ai-message__meta {
  margin-bottom: 5px;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.ai-message.is-user .ai-message__meta {
  text-align: right;
}

.ai-message__content {
  padding: 11px 13px;
  border: 1px solid var(--el-border-color-light);
  border-radius: 6px;
  background: var(--el-fill-color-light);
  line-height: 1.65;
  overflow-wrap: anywhere;
  white-space: pre-wrap;
}

.ai-message.is-user .ai-message__content {
  border-color: var(--el-color-primary-light-7);
  background: var(--el-color-primary-light-9);
}

.ai-message__feedback {
  display: flex;
  gap: 2px;
  justify-content: flex-end;
  min-height: 32px;
  padding-top: 3px;
}

.ai-message__feedback .el-button + .el-button {
  margin-left: 0;
}

.ai-progress {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin: 6px 0 16px;
  color: var(--el-text-color-secondary);
  font-size: 13px;
}

.ai-citations {
  margin-top: 8px;
}

.ai-citation {
  display: grid;
  gap: 3px;
  padding: 8px 0;
  color: var(--el-text-color-secondary);
  font-size: 12px;
}

.ai-citation strong {
  color: var(--el-text-color-primary);
}

.ai-document-drafts {
  margin-top: 16px;
}

.ai-composer {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 10px;
  padding: 12px;
  border-top: 1px solid var(--el-border-color);
}

.ai-composer__actions {
  align-self: end;
  gap: 8px;
}

@media (max-width: 720px) {
  .ai-workspace {
    grid-template-columns: 1fr;
    grid-template-rows: 150px minmax(0, 1fr);
    height: 82vh;
  }

  .ai-sidebar {
    border-right: 0;
    border-bottom: 1px solid var(--el-border-color);
  }

  .ai-conversation-list {
    display: flex;
    height: 101px;
    overflow-x: auto;
    overflow-y: hidden;
  }

  .ai-conversation-item {
    flex: 0 0 190px;
    border-right: 1px solid var(--el-border-color-lighter);
  }

  .ai-message {
    width: 92%;
  }

  .ai-composer {
    grid-template-columns: 1fr;
  }

  .ai-composer__actions {
    justify-content: flex-end;
  }
}
</style>
