import { computed, ref } from 'vue'
import { defineStore } from 'pinia'
import {
  addSignTask,
  approveTask,
  getDoneTasks,
  getTodoTasks,
  rejectTask,
  transferTask,
  type AddSignWorkflowTaskRequest,
  type TransferWorkflowTaskRequest,
  type WorkflowTaskActionRequest,
  type WorkflowTaskItem,
  type WorkflowTaskQuery,
} from '../api/workflowTask'
import { getInstanceDetail, type WorkflowInstanceDetail } from '../api/workflowInstance'
import type { PagedResult } from '../api/types'

const emptyPage = <T>(): PagedResult<T> => ({
  items: [],
  pageIndex: 1,
  pageSize: 20,
  totalCount: 0,
  totalPages: 0,
  hasPreviousPage: false,
  hasNextPage: false,
})

export const useTaskStore = defineStore('tasks', () => {
  const todo = ref<PagedResult<WorkflowTaskItem>>(emptyPage())
  const done = ref<PagedResult<WorkflowTaskItem>>(emptyPage())
  const detail = ref<WorkflowInstanceDetail>()
  const loading = ref(false)
  const actionLoading = ref(false)
  const error = ref<string>()
  const todoCount = computed(() => todo.value.totalCount)

  async function loadTodo(params: WorkflowTaskQuery = {}) {
    loading.value = true
    error.value = undefined
    try {
      todo.value = await getTodoTasks(params)
      return todo.value
    } catch (reason) {
      error.value = reason instanceof Error ? reason.message : '待办任务加载失败。'
      throw reason
    } finally {
      loading.value = false
    }
  }

  async function loadDone(params: WorkflowTaskQuery = {}) {
    loading.value = true
    error.value = undefined
    try {
      done.value = await getDoneTasks(params)
      return done.value
    } catch (reason) {
      error.value = reason instanceof Error ? reason.message : '已办任务加载失败。'
      throw reason
    } finally {
      loading.value = false
    }
  }

  async function loadDetail(instanceId: string) {
    loading.value = true
    try {
      detail.value = await getInstanceDetail(instanceId)
      return detail.value
    } finally {
      loading.value = false
    }
  }

  async function runAction(action: () => Promise<unknown>, instanceId?: string) {
    actionLoading.value = true
    try {
      await action()
      // The server is the source of truth for status transitions. Refresh the
      // affected detail/list after every successful write.
      if (instanceId) {
        await loadDetail(instanceId)
      }
      await loadTodo({ pageIndex: todo.value.pageIndex, pageSize: todo.value.pageSize })
    } finally {
      actionLoading.value = false
    }
  }

  async function approve(task: WorkflowTaskItem | string, payload: WorkflowTaskActionRequest = {}) {
    const taskId = typeof task === 'string' ? task : task.id
    const instanceId = typeof task === 'string' ? undefined : task.instanceId
    return runAction(() => approveTask(taskId, payload), instanceId)
  }

  async function reject(task: WorkflowTaskItem | string, payload: WorkflowTaskActionRequest = {}) {
    const taskId = typeof task === 'string' ? task : task.id
    const instanceId = typeof task === 'string' ? undefined : task.instanceId
    return runAction(() => rejectTask(taskId, payload), instanceId)
  }

  async function transfer(task: WorkflowTaskItem | string, payload: TransferWorkflowTaskRequest) {
    const taskId = typeof task === 'string' ? task : task.id
    const instanceId = typeof task === 'string' ? undefined : task.instanceId
    return runAction(() => transferTask(taskId, payload), instanceId)
  }

  async function addSign(task: WorkflowTaskItem | string, payload: AddSignWorkflowTaskRequest) {
    const taskId = typeof task === 'string' ? task : task.id
    const instanceId = typeof task === 'string' ? undefined : task.instanceId
    return runAction(() => addSignTask(taskId, payload), instanceId)
  }

  function reset() {
    todo.value = emptyPage()
    done.value = emptyPage()
    detail.value = undefined
    loading.value = false
    actionLoading.value = false
    error.value = undefined
  }

  return {
    todo,
    done,
    detail,
    loading,
    actionLoading,
    error,
    todoCount,
    loadTodo,
    loadDone,
    loadDetail,
    approve,
    reject,
    transfer,
    addSign,
    reset,
  }
})

export const useWorkflowTaskStore = useTaskStore

