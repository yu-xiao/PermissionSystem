import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'
import type {
  WorkflowInstanceStatus,
  WorkflowTaskItem,
} from './workflowTask'

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

export interface WorkflowInstanceDetail extends WorkflowInstanceItem {
  tasks: WorkflowTaskItem[]
  ccs: WorkflowCcItem[]
  records: WorkflowRecordItem[]
}

export interface StartWorkflowInstanceRequest {
  businessType: string
  businessId: string
  businessTitle: string
  formData?: unknown
  formDataJson?: string
  remark?: string
}

export async function getMyStartedInstances(params: WorkflowInstanceQuery = {}) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<WorkflowInstanceItem>>>('/api/v1/workflow/instances/my-started', { params }),
  )
}

export async function getInstanceDetail(instanceId: string) {
  return unwrapApiResult(
    await request.get<ApiResult<WorkflowInstanceDetail>>(`/api/v1/workflow/instances/${instanceId}`),
  )
}

export async function getInstanceRecords(instanceId: string) {
  return unwrapApiResult(
    await request.get<ApiResult<WorkflowRecordItem[]>>(`/api/v1/workflow/instances/${instanceId}/records`),
  )
}

export async function startWorkflowInstance(payload: StartWorkflowInstanceRequest) {
  return unwrapApiResult(
    await request.post<ApiResult<WorkflowInstanceDetail>>('/api/v1/workflow/instances/start', payload),
  )
}

export async function withdrawInstance(instanceId: string, comment?: string) {
  return unwrapApiResult(
    await request.post<ApiResult<void>>(`/api/v1/workflow/instances/${instanceId}/withdraw`, { comment }),
  )
}

