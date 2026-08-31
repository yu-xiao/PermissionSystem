import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  cancelDemoApprovalOrder,
  createDemoApprovalOrder,
  deleteDemoApprovalOrder,
  getDemoApprovalOrder,
  getDemoApprovalOrders,
  submitDemoApprovalOrder,
  updateDemoApprovalOrder,
  withdrawDemoApprovalOrder,
  type CreateDemoApprovalOrderRequest,
  type DemoApprovalOrderItem,
  type DemoApprovalOrderQuery,
  type UpdateDemoApprovalOrderRequest,
} from '../api/demoApprovalOrder'
import {
  cancelDemoBusinessOrder,
  createDemoBusinessOrder,
  deleteDemoBusinessOrder,
  getDemoBusinessOrder,
  getDemoBusinessOrders,
  submitDemoBusinessOrder,
  updateDemoBusinessOrder,
  withdrawDemoBusinessOrder,
  type DemoBusinessOrderItem,
  type DemoBusinessOrderQuery,
  type SaveDemoBusinessOrderRequest,
} from '../api/demoBusinessOrder'
import type { PagedResult } from '../api/types'

type OrderKind = 'business' | 'approval'
type OrderItem = DemoBusinessOrderItem | DemoApprovalOrderItem

const emptyPage = <T>(): PagedResult<T> => ({
  items: [],
  pageIndex: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
})

export const useOrderStore = defineStore('orders', () => {
  const kind = ref<OrderKind>('business')
  const businessOrders = ref<PagedResult<DemoBusinessOrderItem>>(emptyPage())
  const approvalOrders = ref<PagedResult<DemoApprovalOrderItem>>(emptyPage())
  const current = ref<OrderItem>()
  const loading = ref(false)
  const actionLoading = ref(false)
  const error = ref<string>()
  const orders = computed(() => kind.value === 'business' ? businessOrders.value.items : approvalOrders.value.items)

  async function loadOrders(orderKind: OrderKind = kind.value, params: DemoBusinessOrderQuery & DemoApprovalOrderQuery = {}) {
    kind.value = orderKind
    loading.value = true
    error.value = undefined
    try {
      if (orderKind === 'business') {
        businessOrders.value = await getDemoBusinessOrders(params)
        return businessOrders.value
      }
      approvalOrders.value = await getDemoApprovalOrders(params)
      return approvalOrders.value
    } catch (reason) {
      error.value = reason instanceof Error ? reason.message : '单据加载失败。'
      throw reason
    } finally {
      loading.value = false
    }
  }

  async function loadBusinessOrders(params: DemoBusinessOrderQuery = {}) {
    return loadOrders('business', params)
  }

  async function loadApprovalOrders(params: DemoApprovalOrderQuery = {}) {
    return loadOrders('approval', params)
  }

  async function loadOrder(id: string, orderKind: OrderKind = kind.value) {
    loading.value = true
    try {
      current.value = orderKind === 'business'
        ? await getDemoBusinessOrder(id)
        : await getDemoApprovalOrder(id)
      return current.value
    } finally {
      loading.value = false
    }
  }

  async function runAction(action: () => Promise<unknown>, orderKind = kind.value) {
    actionLoading.value = true
    try {
      await action()
      await loadOrders(orderKind)
    } finally {
      actionLoading.value = false
    }
  }

  async function createBusiness(payload: SaveDemoBusinessOrderRequest) {
    return createDemoBusinessOrder(payload)
  }

  async function updateBusiness(id: string, payload: SaveDemoBusinessOrderRequest) {
    return updateDemoBusinessOrder(id, payload)
  }

  async function createApproval(payload: CreateDemoApprovalOrderRequest) {
    return createDemoApprovalOrder(payload)
  }

  async function updateApproval(id: string, payload: UpdateDemoApprovalOrderRequest) {
    return updateDemoApprovalOrder(id, payload)
  }

  async function submit(id: string, orderKind = kind.value, remark?: string) {
    return runAction(
      () => orderKind === 'business'
        ? submitDemoBusinessOrder(id, remark)
        : submitDemoApprovalOrder(id, { remark }),
      orderKind,
    )
  }

  async function withdraw(id: string, orderKind = kind.value, comment?: string) {
    return runAction(
      () => orderKind === 'business'
        ? withdrawDemoBusinessOrder(id, comment)
        : withdrawDemoApprovalOrder(id, comment),
      orderKind,
    )
  }

  async function cancel(id: string, orderKind = kind.value, comment?: string) {
    return runAction(
      () => orderKind === 'business'
        ? cancelDemoBusinessOrder(id, comment)
        : cancelDemoApprovalOrder(id, comment),
      orderKind,
    )
  }

  async function remove(id: string, orderKind = kind.value) {
    return runAction(
      () => orderKind === 'business' ? deleteDemoBusinessOrder(id) : deleteDemoApprovalOrder(id),
      orderKind,
    )
  }

  function reset() {
    businessOrders.value = emptyPage()
    approvalOrders.value = emptyPage()
    current.value = undefined
    loading.value = false
    actionLoading.value = false
    error.value = undefined
  }

  return {
    kind,
    businessOrders,
    approvalOrders,
    orders,
    current,
    loading,
    actionLoading,
    error,
    loadOrders,
    loadBusinessOrders,
    loadApprovalOrders,
    loadOrder,
    createBusiness,
    updateBusiness,
    createApproval,
    updateApproval,
    submit,
    withdraw,
    cancel,
    remove,
    reset,
  }
})

export const useOrdersStore = useOrderStore
