<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import OrderCard from '../../components/OrderCard.vue'
import type { OrderCardItem } from '../../components/OrderCard.vue'
import StateView from '../../components/StateView.vue'
import { getDemoApprovalOrders, type DemoApprovalOrderItem } from '../../api/demoApprovalOrder'
import { getDemoBusinessOrders, type DemoBusinessOrderItem } from '../../api/demoBusinessOrder'
import { usePermissionStore } from '../../stores/permission'

type Kind = 'business' | 'approval'
const router = useRouter()
const permission = usePermissionStore()
const canViewBusiness = computed(() => permission.hasPermission('demo-business-order:view'))
const canViewApproval = computed(() => permission.hasPermission('demo-approval-order:view'))
const kind = ref<Kind>(canViewBusiness.value ? 'business' : 'approval')
const loading = ref(true)
const loadingMore = ref(false)
const error = ref('')
const keyword = ref('')
const items = ref<Array<DemoBusinessOrderItem | DemoApprovalOrderItem>>([])
const pageIndex = ref(1)
const hasNext = ref(false)
const statusFilter = ref<'all' | 'draft' | 'pending' | 'done'>('all')
const canCreate = computed(() => permission.hasPermission(kind.value === 'business' ? 'demo-business-order:create' : 'demo-approval-order:create'))

const statuses = [{ key: 'all', label: '全部' }, { key: 'draft', label: '草稿' }, { key: 'pending', label: '审批中' }, { key: 'done', label: '已完成' }] as const
function isBusiness(item: DemoBusinessOrderItem | DemoApprovalOrderItem): item is DemoBusinessOrderItem { return 'customerName' in item }
function labelStatus(status: number) { return ({ 0: '草稿', 1: '审批中', 2: '已通过', 3: '已驳回', 4: '已撤回', 5: '已取消' } as Record<number, string>)[status] || '未知' }
function toneStatus(status: number): 'neutral' | 'primary' | 'success' | 'warning' | 'danger' { if (status === 2) return 'success'; if (status === 3) return 'danger'; if (status === 1) return 'warning'; if (status === 0) return 'neutral'; return 'neutral' }
const cardItems = computed<OrderCardItem[]>(() => items.value.map((item) => ({ id: item.id, orderNo: item.orderNo, title: item.title, customerName: isBusiness(item) ? item.customerName : undefined, amount: item.amount, approvalStatus: item.approvalStatus, statusLabel: labelStatus(item.approvalStatus), statusTone: toneStatus(item.approvalStatus), createdAt: item.createdAt })))
const visibleItems = computed(() => {
  const query = keyword.value.trim().toLowerCase()
  return cardItems.value.filter((item) => {
    if (query && !`${item.title || ''} ${item.orderNo || ''} ${item.customerName || ''}`.toLowerCase().includes(query)) return false
    if (statusFilter.value === 'draft') return item.approvalStatus === 0
    if (statusFilter.value === 'pending') return item.approvalStatus === 1
    if (statusFilter.value === 'done') return [2, 3, 4, 5].includes(Number(item.approvalStatus))
    return true
  })
})

async function load(reset = true) {
  if (reset) { pageIndex.value = 1; loading.value = true } else loadingMore.value = true
  error.value = ''
  try {
    const page = kind.value === 'business' ? await getDemoBusinessOrders({ pageIndex: pageIndex.value, pageSize: 20, keyword: keyword.value.trim() || undefined }) : await getDemoApprovalOrders({ pageIndex: pageIndex.value, pageSize: 20, keyword: keyword.value.trim() || undefined })
    if (reset) items.value = page?.items || []
    else items.value = [...items.value, ...(page?.items || [])]
    hasNext.value = Boolean(page?.hasNextPage)
  } catch (reason) { error.value = reason instanceof Error ? reason.message : '单据列表加载失败。' } finally { loading.value = false; loadingMore.value = false }
}
async function loadMore() { if (!hasNext.value || loadingMore.value) return; pageIndex.value += 1; await load(false) }
function open(id: string) { void router.push({ path: `/orders/${id}`, query: { kind: kind.value } }) }
watch(kind, () => { statusFilter.value = 'all'; void load() })
onMounted(() => void load())
</script>

<template>
  <section class="order-list-view">
    <div class="list-head"><div class="list-switch" :class="{ 'list-switch--single': !canViewBusiness || !canViewApproval }" role="tablist"><button v-if="canViewBusiness" type="button" :class="{ 'list-switch__item--active': kind === 'business' }" role="tab" :aria-selected="kind === 'business'" @click="kind = 'business'">业务订单</button><button v-if="canViewApproval" type="button" :class="{ 'list-switch__item--active': kind === 'approval' }" role="tab" :aria-selected="kind === 'approval'" @click="kind = 'approval'">审批单</button></div><button v-if="canCreate" class="button button--primary new-order" type="button" @click="router.push({ path: '/orders/new', query: { kind } })"><span aria-hidden="true">＋</span>新建</button></div>
    <div class="search-box"><span class="search-box__icon" aria-hidden="true">⌕</span><input v-model="keyword" type="search" placeholder="搜索单号或标题" @keyup.enter="load()" /><button v-if="keyword" class="icon-button" type="button" aria-label="清除搜索" @click="keyword = ''; load()">×</button></div>
    <div class="filter-row order-filters"><button v-for="status in statuses" :key="status.key" class="filter-chip" :class="{ 'filter-chip--active': statusFilter === status.key }" type="button" @click="statusFilter = status.key">{{ status.label }}</button></div>
    <StateView v-if="loading" kind="loading" title="正在加载单据" /><StateView v-else-if="error" kind="error" :hint="error" action-label="重新加载" @action="load()" /><StateView v-else-if="!visibleItems.length" kind="empty" title="暂无业务单据" :hint="keyword ? '试试更换关键词。' : '点击右上角新建第一张单据。'" /><div v-else class="card-list"><OrderCard v-for="item in visibleItems" :key="item.id" :item="item" @open="open" /><button v-if="hasNext" class="button button--ghost button--block" type="button" :disabled="loadingMore" @click="loadMore">{{ loadingMore ? '正在加载…' : '加载更多' }}</button></div>
  </section>
</template>

<style scoped>
.list-head { display: flex; align-items: center; gap: 9px; margin-bottom: 12px; }
.list-switch { display: grid; min-width: 0; flex: 1; grid-template-columns: repeat(2, minmax(0, 1fr)); padding: 3px; border-radius: 10px; background: var(--mobile-surface-muted); }
.list-switch--single { grid-template-columns: minmax(0, 1fr); }
.list-switch__item { min-height: 36px; border: 0; border-radius: 8px; color: var(--mobile-text-secondary); background: transparent; font-size: 12px; font-weight: 600; }
.list-switch__item--active { color: var(--mobile-primary); background: var(--mobile-surface); box-shadow: 0 2px 7px rgba(20,37,63,.08); }
.new-order { flex: 0 0 auto; min-height: 42px; padding: 0 12px; }
.order-filters { margin: 11px 0 14px; }
</style>
