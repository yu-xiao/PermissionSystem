import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

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

export type WorkflowInstanceStatus =
  (typeof WorkflowInstanceStatus)[keyof typeof WorkflowInstanceStatus]

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

export interface TransferWorkflowTaskRequest {
  targetUserId: string
  comment?: string
}

export interface AddSignWorkflowTaskRequest {
  targetUserId: string
  comment?: string
}

export function getTodoTasks(params: WorkflowTaskQuery) {
  return request
    .get<ApiResult<PagedResult<WorkflowTaskItem>>>('/api/workflow/tasks/todo', { params })
    .then((res) => res.data.data)
}

export function getDoneTasks(params: WorkflowTaskQuery) {
  return request
    .get<ApiResult<PagedResult<WorkflowTaskItem>>>('/api/workflow/tasks/done', { params })
    .then((res) => res.data.data)
}

export function approveTask(taskId: string, data: WorkflowTaskActionRequest) {
  return request.post<ApiResult<void>>(`/api/workflow/tasks/${taskId}/approve`, data)
}

export function rejectTask(taskId: string, data: WorkflowTaskActionRequest) {
  return request.post<ApiResult<void>>(`/api/workflow/tasks/${taskId}/reject`, data)
}

export function transferTask(taskId: string, data: TransferWorkflowTaskRequest) {
  return request.post<ApiResult<void>>(`/api/workflow/tasks/${taskId}/transfer`, data)
}

export function addSignTask(taskId: string, data: AddSignWorkflowTaskRequest) {
  return request.post<ApiResult<void>>(`/api/workflow/tasks/${taskId}/add-sign`, data)
}
