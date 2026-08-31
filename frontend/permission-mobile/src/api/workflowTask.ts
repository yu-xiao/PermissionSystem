import type { AxiosRequestConfig } from 'axios'
import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'

export const WorkflowTaskStatus = {
  Pending: 0,
  Approved: 1,
  Rejected: 2,
  Transferred: 3,
  Added: 4,
  Canceled: 5,
  Expired: 6,
} as const

export type WorkflowTaskStatus = (typeof WorkflowTaskStatus)[keyof typeof WorkflowTaskStatus]

export const WorkflowInstanceStatus = {
  Running: 0,
  Approved: 1,
  Rejected: 2,
  Withdrawn: 3,
  Canceled: 4,
  Exception: 5,
} as const

export type WorkflowInstanceStatus = (typeof WorkflowInstanceStatus)[keyof typeof WorkflowInstanceStatus]

export interface WorkflowTaskQuery extends PageQuery {
  status?: WorkflowTaskStatus
}

export interface WorkflowTaskItem {
  id: string
  tenantId: string
  instanceId: string
  nodeKey: string
  nodeName: string
  approverUserId: string
  approverUserName: string
  status: WorkflowTaskStatus
  assignedAt: string
  completedAt?: string
  dueAt?: string
  businessType: string
  businessId: string
  businessTitle: string
  definitionName: string
  starterUserName: string
  instanceStatus: WorkflowInstanceStatus
  startedAt?: string
}

export interface WorkflowTaskActionRequest {
  comment?: string
}

export interface TransferWorkflowTaskRequest extends WorkflowTaskActionRequest {
  targetUserId: string
}

export interface AddSignWorkflowTaskRequest extends WorkflowTaskActionRequest {
  targetUserId: string
}

export async function getTodoTasks(
  params: WorkflowTaskQuery = {},
  config?: AxiosRequestConfig,
) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<WorkflowTaskItem>>>('/api/v1/workflow/tasks/todo', {
      ...config,
      params,
    }),
  )
}

export async function getDoneTasks(
  params: WorkflowTaskQuery = {},
  config?: AxiosRequestConfig,
) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<WorkflowTaskItem>>>('/api/v1/workflow/tasks/done', {
      ...config,
      params,
    }),
  )
}

export async function approveTask(taskId: string, payload: WorkflowTaskActionRequest = {}) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/workflow/tasks/${taskId}/approve`, payload))
}

export async function rejectTask(taskId: string, payload: WorkflowTaskActionRequest = {}) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/workflow/tasks/${taskId}/reject`, payload))
}

export async function transferTask(taskId: string, payload: TransferWorkflowTaskRequest) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/workflow/tasks/${taskId}/transfer`, payload))
}

export async function addSignTask(taskId: string, payload: AddSignWorkflowTaskRequest) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/workflow/tasks/${taskId}/add-sign`, payload))
}

// Keep the longer names available to feature modules and tests.
export const approveWorkflowTask = approveTask
export const rejectWorkflowTask = rejectTask
export const transferWorkflowTask = transferTask
export const addSignWorkflowTask = addSignTask
