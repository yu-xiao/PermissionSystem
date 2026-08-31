<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import StateView from '../../components/StateView.vue'
import TaskCard from '../../components/TaskCard.vue'
import type { TaskCardItem } from '../../components/TaskCard.vue'
import { getDoneTasks, getTodoTasks, type WorkflowTaskItem, WorkflowTaskStatus } from '../../api/workflowTask'

const props = withDefaults(defineProps<{ done?: boolean }>(), { done: false })
const route = useRoute()
const router = useRouter()
const loading = ref(true)
const loadingMore = ref(false)
const error = ref('')
const keyword = ref('')
const activeFilter = ref<'all' | 'pending' | 'urgent'>('all')
const items = ref<WorkflowTaskItem[]>([])
const pageIndex = ref(1)
const hasNext = ref(false)

const filters = computed(() => props.done ? [{ key: 'all', label: '全部' }] : [{ key: 'all', label: '全部' }, { key: 'pending', label: '待处理' }, { key: 'urgent', label: '临近截止' }])

function statusLabel(item: WorkflowTaskItem) {
  if (!props.done) return '待处理'
  const labels: Record<number, string> = { [WorkflowTaskStatus.Approved]: '已同意', [WorkflowTaskStatus.Rejected]: '已驳回', [WorkflowTaskStatus.Transferred]: '已转交', [WorkflowTaskStatus.Added]: '已加签', [WorkflowTaskStatus.Canceled]: '已取消', [WorkflowTaskStatus.Expired]: '已超时' }
  return labels[item.status] || '已处理'
}

function statusTone(item: WorkflowTaskItem): 'neutral' | 'success' | 'warning' | 'danger' {
  if (!props.done) return 'warning'
  if (item.status === WorkflowTaskStatus.Rejected || item.status === WorkflowTaskStatus.Expired) return 'danger'
  if (item.status === WorkflowTaskStatus.Approved) return 'success'
  return 'neutral'
}

const cardItems = computed<TaskCardItem[]>(() => items.value.map((item) => ({ ...item, statusLabel: statusLabel(item), statusTone: statusTone(item) })))
const visibleItems = computed(() => {
  const normalized = keyword.value.trim().toLowerCase()
  return cardItems.value.filter((item) => {
    const text = `${item.businessTitle || ''} ${item.title || ''} ${item.nodeName || ''} ${item.starterUserName || ''}`.toLowerCase()
    if (normalized && !text.includes(normalized)) return false
    if (activeFilter.value === 'urgent' && item.dueAt) {
      const due = new Date(item.dueAt).getTime()
      return due > Date.now() && due - Date.now() < 48 * 3600 * 1000
    }
    return activeFilter.value !== 'pending' || !props.done
  })
})

async function load(reset = true) {
  if (reset) {
    pageIndex.value = 1
    loading.value = true
  } else {
    loadingMore.value = true
  }
  error.value = ''
  try {
    const query = { pageIndex: pageIndex.value, pageSize: 20, keyword: keyword.value.trim() || undefined }
    const page = props.done ? await getDoneTasks(query) : await getTodoTasks(query)
    if (reset) items.value = page?.items || []
    else items.value = [...items.value, ...(page?.items || [])]
    hasNext.value = Boolean(page?.hasNextPage)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : '任务列表加载失败。'
  } finally {
    loading.value = false
    loadingMore.value = false
  }
}

async function loadMore() {
  if (!hasNext.value || loadingMore.value) return
  pageIndex.value += 1
  await load(false)
}

function openTask(item: TaskCardItem) {
  void router.push({ path: `/tasks/${item.id}`, query: item.instanceId ? { instanceId: item.instanceId } : undefined })
}

function switchMode(done: boolean) {
  const path = done ? '/tasks/done' : '/tasks/todo'
  if (route.path !== path) void router.replace(path)
}

watch(() => props.done, () => { activeFilter.value = 'all'; void load() })
onMounted(() => void load())
</script>

<template>
  <section class="task-list-view">
    <div class="list-switch" role="tablist" aria-label="任务类型">
      <button type="button" :class="{ 'list-switch__item--active': !done }" role="tab" :aria-selected="!done" @click="switchMode(false)">待办</button>
      <button type="button" :class="{ 'list-switch__item--active': done }" role="tab" :aria-selected="done" @click="switchMode(true)">已办</button>
    </div>
    <div class="search-box task-search"><span class="search-box__icon" aria-hidden="true">⌕</span><input v-model="keyword" type="search" placeholder="搜索标题、流程或发起人" @keyup.enter="load()" /><button v-if="keyword" class="icon-button" type="button" aria-label="清除搜索" @click="keyword = ''; load()">×</button></div>
    <div class="filter-row task-filters" role="list">
      <button v-for="filter in filters" :key="filter.key" class="filter-chip" :class="{ 'filter-chip--active': activeFilter === filter.key }" type="button" @click="activeFilter = filter.key as typeof activeFilter">{{ filter.label }}</button>
    </div>

    <StateView v-if="loading" kind="loading" title="正在加载任务" />
    <StateView v-else-if="error" kind="error" :hint="error" action-label="重新加载" @action="load()" />
    <StateView v-else-if="visibleItems.length === 0" kind="empty" :title="done ? '暂无已办记录' : '暂无待办任务'" :hint="keyword ? '试试更换关键词。' : '新的任务会显示在这里。'" />
    <div v-else class="card-list task-cards">
      <TaskCard v-for="item in visibleItems" :key="item.id" :item="item" :done="done" @open="openTask" />
      <button v-if="hasNext" class="button button--ghost button--block" type="button" :disabled="loadingMore" @click="loadMore">{{ loadingMore ? '正在加载…' : '加载更多' }}</button>
    </div>
  </section>
</template>

<style scoped>
.list-switch { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); margin-bottom: 13px; padding: 3px; border-radius: 10px; background: var(--mobile-surface-muted); }
.list-switch__item { min-height: 36px; border: 0; border-radius: 8px; color: var(--mobile-text-secondary); background: transparent; font-size: 13px; font-weight: 600; }
.list-switch__item--active { color: var(--mobile-primary); background: var(--mobile-surface); box-shadow: 0 2px 7px rgba(20,37,63,.08); }
.task-search { margin-bottom: 11px; }
.task-filters { margin-bottom: 14px; }
.task-cards { gap: 9px; }
</style>
