import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'
import type { WorkflowInstanceStatus, WorkflowTaskItem } from './workflowTask'

export const WorkflowActionType = {
  Start: 0,
  Approve: 1,
  Reject: 2,
  Withdraw: 3,
  Transfer: 4,
  AddSign: 5,
  Cc: 6,
  Complete: 7,
  System: 8,
} as const

export type WorkflowActionType = (typeof WorkflowActionType)[keyof typeof WorkflowActionType]

export interface WorkflowInstanceQuery extends PageQuery {
  status?: WorkflowInstanceStatus
}

export interface WorkflowCcQuery extends PageQuery {
  isRead?: boolean
}

export interface WorkflowInstanceItem {
  id: string
  tenantId: string
  definitionId: string
  definitionCode: string
  definitionName: string
  businessType: string
  businessId: string
  businessTitle: string
  starterUserId: string
  starterUserName: string
  status: WorkflowInstanceStatus
  currentNodeKey?: string
  formDataJson?: string
  startedAt: string
  completedAt?: string
  createdAt: string
}

export interface WorkflowRecordItem {
  id: string
  instanceId: string
  taskId?: string
  nodeKey?: string
  nodeName?: string
  operatorUserId?: string
  operatorUserName?: string
  action: WorkflowActionType
  comment?: string
  operatedAt: string
}

export interface WorkflowCcItem {
  id: string
  tenantId: string
  instanceId: string
  nodeKey: string
  ccUserId: string
  ccUserName: string
  isRead: boolean
  readAt?: string
  businessType: string
  businessId: string
  businessTitle: string
  definitionName: string
  starterUserName: string
  instanceStatus: WorkflowInstanceStatus
  createdAt: string
}

export interface WorkflowInstanceDetail extends WorkflowInstanceItem {
  tasks: WorkflowTaskItem[]
  ccs: WorkflowCcItem[]
  records: WorkflowRecordItem[]
}

export function getMyStartedInstances(params: WorkflowInstanceQuery) {
  return request
    .get<ApiResult<PagedResult<WorkflowInstanceItem>>>('/api/workflow/instances/my-started', { params })
    .then((res) => res.data.data)
}

export function withdrawInstance(instanceId: string, comment?: string) {
  return request.post<ApiResult<void>>(`/api/workflow/instances/${instanceId}/withdraw`, { comment })
}

export function getMyCc(params: WorkflowCcQuery) {
  return request
    .get<ApiResult<PagedResult<WorkflowCcItem>>>('/api/workflow/cc/my', { params })
    .then((res) => res.data.data)
}

export function markWorkflowCcRead(ccId: string) {
  return request.post<ApiResult<void>>(`/api/workflow/cc/${ccId}/read`)
}

export function getInstanceDetail(instanceId: string) {
  return request
    .get<ApiResult<WorkflowInstanceDetail>>(`/api/workflow/instances/${instanceId}`)
    .then((res) => res.data.data)
}

export function getInstanceRecords(instanceId: string) {
  return request
    .get<ApiResult<WorkflowRecordItem[]>>(`/api/workflow/instances/${instanceId}/records`)
    .then((res) => res.data.data)
}
