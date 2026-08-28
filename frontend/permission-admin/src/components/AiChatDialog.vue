<script setup lang="ts">
import {
  ChatDotRound,
  CircleClose,
  Delete,
  Plus,
  Promotion,
  Refresh,
} from '@element-plus/icons-vue'
import { ElMessageBox } from 'element-plus'
import { computed, nextTick, onBeforeUnmount, ref } from 'vue'
import {
  cancelAiRun,
  createAiConversation,
  deleteAiConversation,
  getAiConversation,
  getAiConversations,
  sendAiMessage,
  type AiConversationDetail,
  type AiConversationListItem,
  type AiRunRealtimeMessage,
  type AiToolCitation,
} from '../api/ai'
import { startAiRunConnection, type SignalRLiteConnection } from '../utils/signalr-lite'
import { useAuthStore } from '../stores/auth'
import AiDocumentDraftCard from './AiDocumentDraftCard.vue'
import type { AiDocumentDraft } from '../api/ai'

const authStore = useAuthStore()
const visible = ref(false)
const loading = ref(false)
const sending = ref(false)
const conversations = ref<AiConversationListItem[]>([])
const current = ref<AiConversationDetail>()
const draft = ref('')
const activeRunId = ref('')
const activeRunStatus = ref<number>()
const citations = ref<AiToolCitation[]>([])
const toolEvents = ref<AiRunRealtimeMessage[]>([])
const messageViewport = ref<HTMLElement>()
let connection: SignalRLiteConnection | undefined

const canSend = computed(
  () => Boolean(current.value && draft.value.trim() && !sending.value && draft.value.length <= 4000),
)
const canCancel = computed(
  () => Boolean(activeRunId.value && (activeRunStatus.value === 1 || activeRunStatus.value === 2)),
)
const composerPlaceholder = computed(() =>
  authStore.hasPermission('ai:document:draft') && authStore.hasPermission('demo-business-order:create')
    ? '输入查询需求，或描述需要生成的 Demo 业务单据草稿'
    : '输入需要查询的用户、部门、角色、日志或已批准报表范围',
)

async function open() {
  visible.value = true
  if (!connection) {
    connection = await startAiRunConnection(handleRunEvent)
  }
  await loadConversations()
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

async function newConversation() {
  const created = await createAiConversation()
  conversations.value.unshift(created)
  current.value = created
  resetRunState()
  await scrollToBottom()
}

async function selectConversation(id: string) {
  if (current.value?.id === id) {
    return
  }
  current.value = await getAiConversation(id)
  resetRunState()
  await scrollToBottom()
}

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
    const run = await sendAiMessage(conversationId, content)
    activeRunId.value = run.id
    activeRunStatus.value = run.status
    citations.value = run.citations
    if (current.value?.id === conversationId) {
      current.value = await getAiConversation(conversationId)
    }
    await loadConversations()
    await scrollToBottom()
  } finally {
    sending.value = false
  }
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
  activeRunStatus.value = 5
}

function handleRunEvent(value: unknown) {
  const event = value as AiRunRealtimeMessage
  if (!event?.runId || event.conversationId !== current.value?.id) {
    return
  }

  activeRunId.value = event.runId
  activeRunStatus.value = event.status
  if (event.toolCode) {
    let runningIndex = -1
    for (let index = toolEvents.value.length - 1; index >= 0; index -= 1) {
      const item = toolEvents.value[index]
      if (item.toolCode === event.toolCode && item.toolStatus === 2) {
        runningIndex = index
        break
      }
    }
    if (runningIndex >= 0 && event.toolStatus !== 2) {
      toolEvents.value[runningIndex] = event
    } else {
      toolEvents.value.push(event)
    }
  }
}

function resetRunState() {
  activeRunId.value = ''
  activeRunStatus.value = undefined
  citations.value = []
  toolEvents.value = []
}

function runStatusText() {
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

onBeforeUnmount(() => connection?.stop())

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" class="ai-chat-dialog" width="min(1040px, 96vw)" top="4vh">
    <template #header>
      <div class="ai-dialog-title">
        <ChatDotRound />
        <span>AI 中心</span>
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
              <el-button type="primary" text :icon="Plus" @click="newConversation" />
            </el-tooltip>
          </div>
        </div>
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
          <el-empty v-if="!loading && conversations.length === 0" :image-size="64" description="暂无会话" />
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
                <div v-for="citation in citations" :key="`${citation.toolCode}-${citation.queriedAt}`" class="ai-citation">
                  <strong>{{ citation.toolCode }}</strong>
                  <span>{{ citation.sourceSystem }} · {{ citation.rowCount }} 行 · {{ formatDate(citation.queriedAt) }}</span>
                  <span v-if="citation.datasetCode">数据集：{{ citation.datasetCode }}</span>
                </div>
              </el-collapse-item>
            </el-collapse>

            <section v-if="current.documentDrafts.length" class="ai-document-drafts">
              <AiDocumentDraftCard
                v-for="item in current.documentDrafts"
                :key="item.id"
                :draft="item"
                @updated="updateDocumentDraft"
              />
            </section>
          </template>
        </div>

        <div class="ai-composer">
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
            <el-tooltip content="取消当前任务" placement="top">
              <el-button :icon="CircleClose" :disabled="!canCancel" @click="cancelRun">取消</el-button>
            </el-tooltip>
            <el-tooltip content="发送" placement="top">
              <el-button type="primary" :icon="Promotion" :disabled="!canSend" @click="submit" />
            </el-tooltip>
          </div>
        </div>
      </section>
    </div>
  </el-dialog>
</template>

<style scoped>
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
