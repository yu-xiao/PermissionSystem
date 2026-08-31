<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getMyNotifications, getMyUnreadNotificationCount, type NotificationItem } from '../../api/notifications'
import { getTodoTasks, type WorkflowTaskItem } from '../../api/workflowTask'
import { useAuthStore } from '../../stores/auth'
import { useTenantStore } from '../../stores/tenant'
import { usePermissionStore } from '../../stores/permission'
import StateView from '../../components/StateView.vue'
import StatusTag from '../../components/StatusTag.vue'

const router = useRouter()
const authStore = useAuthStore()
const tenantStore = useTenantStore()
const permission = usePermissionStore()
const loading = ref(true)
const loadError = ref('')
const todoItems = ref<WorkflowTaskItem[]>([])
const latestNotifications = ref<NotificationItem[]>([])
const unreadCount = ref(0)
const canViewTasks = computed(() => permission.hasPermission('workflow:task:todo'))
const canViewNotifications = computed(() => permission.hasPermission('system:notification:view'))
const canViewOrders = computed(() => permission.canAny(['demo-business-order:view', 'demo-approval-order:view']))

const user = computed(() => {
  const store = authStore as unknown as Record<string, any>
  return (store.currentProfile || store.profile || store.currentUser || {}) as Record<string, any>
})
const tenantName = computed(() => {
  const tenant = tenantStore as unknown as Record<string, any>
  return tenant.targetTenantName || tenant.currentTenant?.name || user.value.tenantName || '当前租户'
})
const greetingName = computed(() => user.value.nickName || user.value.realName || user.value.username || user.value.userName || '同事')

function taskTitle(item: WorkflowTaskItem) { return item.businessTitle || '待处理任务' }
function formatDate(value?: string) {
  if (!value) return ''
  const date = new Date(value)
  return Number.isNaN(date.getTime()) ? value : new Intl.DateTimeFormat('zh-CN', { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' }).format(date)
}

async function load() {
  loading.value = true
  loadError.value = ''
  try {
    const [tasks, notifications, unread] = await Promise.all([
      canViewTasks.value ? getTodoTasks({ pageIndex: 1, pageSize: 4 }) : Promise.resolve(undefined),
      canViewNotifications.value ? getMyNotifications({ pageIndex: 1, pageSize: 3 }) : Promise.resolve(undefined),
      canViewNotifications.value ? getMyUnreadNotificationCount() : Promise.resolve(0),
    ])
    todoItems.value = tasks?.items || []
    latestNotifications.value = notifications?.items || []
    unreadCount.value = unread || 0
  } catch (reason) {
    loadError.value = reason instanceof Error ? reason.message : '工作台暂时无法加载。'
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<template>
  <section class="home-view">
    <div class="hero-panel">
      <p class="hero-panel__eyebrow">{{ tenantName }}</p>
      <h2 class="hero-panel__title">早上好，{{ greetingName }}</h2>
      <p class="hero-panel__sub">今天也高效处理重要事项。</p>
    </div>

    <div class="section-heading"><h2>今日概览</h2><button class="button button--text" type="button" @click="load">刷新</button></div>
    <div class="metric-grid">
      <button v-if="canViewTasks" class="metric" type="button" @click="router.push('/tasks/todo')"><span class="metric__label">待办任务</span><strong class="metric__value">{{ loading ? '—' : todoItems.length }}</strong><span class="metric__hint">查看需要处理的事项</span></button>
      <button v-if="canViewNotifications" class="metric" type="button" @click="router.push('/notifications')"><span class="metric__label">未读通知</span><strong class="metric__value">{{ loading ? '—' : unreadCount }}</strong><span class="metric__hint">及时了解业务动态</span></button>
    </div>

    <div class="section-heading"><h2>快捷入口</h2></div>
    <div class="quick-grid">
      <button v-if="canViewTasks" class="quick-action" type="button" @click="router.push('/tasks/todo')"><span class="quick-action__icon" aria-hidden="true">✓</span><span>我的待办</span></button>
      <button v-if="canViewTasks" class="quick-action" type="button" @click="router.push('/tasks/done')"><span class="quick-action__icon" aria-hidden="true">▣</span><span>已办记录</span></button>
      <button v-if="canViewOrders" class="quick-action" type="button" @click="router.push('/orders')"><span class="quick-action__icon" aria-hidden="true">▤</span><span>业务单据</span></button>
      <button class="quick-action" type="button" @click="router.push('/profile')"><span class="quick-action__icon" aria-hidden="true">○</span><span>我的资料</span></button>
    </div>

    <div v-if="canViewTasks" class="section-heading"><h2>待办任务</h2><button class="button button--text" type="button" @click="router.push('/tasks/todo')">全部</button></div>
    <StateView v-if="canViewTasks && loadError" kind="error" :hint="loadError" action-label="重新加载" @action="load" />
    <StateView v-else-if="canViewTasks && loading" kind="loading" title="正在加载工作台" />
    <StateView v-else-if="canViewTasks && todoItems.length === 0" kind="empty" title="暂无待办任务" hint="新的审批事项会显示在这里。" />
    <div v-else-if="canViewTasks" class="card-list">
      <button v-for="item in todoItems" :key="item.id" class="surface home-task" type="button" @click="router.push({ path: `/tasks/${item.id}`, query: { instanceId: item.instanceId } })">
        <div class="home-task__top"><strong>{{ taskTitle(item) }}</strong><StatusTag label="待处理" tone="warning" /></div>
        <div class="home-task__meta">{{ item.nodeName || '审批节点' }} · {{ item.starterUserName || '发起人未知' }}</div>
        <div class="home-task__time">{{ formatDate(item.assignedAt) }} <span aria-hidden="true">›</span></div>
      </button>
    </div>

    <div v-if="canViewNotifications" class="section-heading"><h2>最近通知</h2><button class="button button--text" type="button" @click="router.push('/notifications')">全部</button></div>
    <StateView v-if="canViewNotifications && !loading && !loadError && latestNotifications.length === 0" kind="empty" title="暂无新通知" />
    <div v-else-if="canViewNotifications && !loading && !loadError" class="card-list">
      <button v-for="item in latestNotifications" :key="item.id" class="surface home-notification" type="button" @click="router.push('/notifications')">
        <span class="home-notification__dot" :class="{ 'home-notification__dot--read': item.isRead }" />
        <span class="home-notification__body"><strong>{{ item.title }}</strong><small>{{ item.content }}</small><time>{{ formatDate(item.createdAt) }}</time></span>
      </button>
    </div>
  </section>
</template>

<style scoped>
.home-view { padding-top: 4px; }
.metric { min-width: 0; padding: 14px; border: 1px solid var(--mobile-border); border-radius: 11px; color: inherit; background: var(--mobile-surface); text-align: left; }
.metric:hover { border-color: color-mix(in srgb, var(--mobile-primary) 36%, var(--mobile-border)); }
.home-task { display: block; width: 100%; padding: 13px 14px; color: inherit; text-align: left; }
.home-task:hover { border-color: color-mix(in srgb, var(--mobile-primary) 36%, var(--mobile-border)); }
.home-task__top { display: flex; align-items: flex-start; justify-content: space-between; gap: 8px; }
.home-task__top strong { min-width: 0; color: var(--mobile-text); font-size: 14px; line-height: 1.4; }
.home-task__meta { margin-top: 8px; color: var(--mobile-text-secondary); font-size: 12px; }
.home-task__time { display: flex; justify-content: space-between; margin-top: 10px; padding-top: 9px; border-top: 1px solid var(--mobile-border); color: var(--mobile-text-muted); font-size: 11px; }
.home-notification { display: flex; width: 100%; align-items: flex-start; gap: 10px; padding: 13px 14px; color: inherit; text-align: left; }
.home-notification__dot { flex: 0 0 auto; width: 8px; height: 8px; margin-top: 5px; border-radius: 50%; background: var(--mobile-danger); }
.home-notification__dot--read { background: var(--mobile-border); }
.home-notification__body { display: grid; min-width: 0; gap: 4px; }
.home-notification__body strong { overflow: hidden; color: var(--mobile-text); font-size: 13px; text-overflow: ellipsis; white-space: nowrap; }
.home-notification__body small { overflow: hidden; color: var(--mobile-text-secondary); font-size: 12px; text-overflow: ellipsis; white-space: nowrap; }
.home-notification__body time { color: var(--mobile-text-muted); font-size: 11px; }
</style>
