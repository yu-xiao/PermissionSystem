import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export const WorkflowDefinitionStatus = {
  Draft: 0,
  Published: 1,
  Disabled: 2,
  Archived: 3,
} as const

export type WorkflowDefinitionStatus =
  (typeof WorkflowDefinitionStatus)[keyof typeof WorkflowDefinitionStatus]

export interface WorkflowDefinitionQuery extends PageQuery {
  tenantId?: string
  status?: WorkflowDefinitionStatus
  isPublished?: boolean
}

export interface WorkflowDefinitionItem {
  id: string
  tenantId: string
  code: string
  name: string
  description?: string
  businessType?: string
  version: number
  status: WorkflowDefinitionStatus
  isPublished: boolean
  publishedAt?: string
  createdAt: string
  updatedAt?: string
  concurrencyToken: string
}

export interface WorkflowDefinitionDetail extends WorkflowDefinitionItem {
  designer: WorkflowDesigner
}

export interface CreateWorkflowDefinitionRequest {
  tenantId?: string
  code: string
  name: string
  description?: string
  businessType?: string
}

export interface UpdateWorkflowDefinitionRequest {
  name: string
  description?: string
  businessType?: string
  concurrencyToken?: string
}

export interface WorkflowDesignerNode {
  id?: string
  nodeKey: string
  nodeName: string
  nodeType: number
  approverType?: number
  approverIds?: string
  approvalMode?: number
  configJson?: string
  positionX: number
  positionY: number
  sort: number
}

export interface WorkflowDesignerEdge {
  id?: string
  fromNodeKey: string
  toNodeKey: string
  conditionId?: string
  isDefault: boolean
  sort: number
}

export interface WorkflowDesignerCondition {
  id?: string
  nodeKey: string
  conditionName: string
  expressionJson: string
  sort: number
}

export interface WorkflowDesigner {
  concurrencyToken?: string
  nodes: WorkflowDesignerNode[]
  edges: WorkflowDesignerEdge[]
  conditions: WorkflowDesignerCondition[]
}

export interface PublishWorkflowDefinitionRequest {
  remark?: string
}

export function getWorkflowDefinitions(params: WorkflowDefinitionQuery) {
  return request
    .get<ApiResult<PagedResult<WorkflowDefinitionItem>>>('/api/workflow/definitions', { params })
    .then((res) => res.data.data)
}

export function getWorkflowDefinition(id: string) {
  return request
    .get<ApiResult<WorkflowDefinitionDetail>>(`/api/workflow/definitions/${id}`)
    .then((res) => res.data.data)
}

export function createWorkflowDefinition(data: CreateWorkflowDefinitionRequest) {
  return request
    .post<ApiResult<WorkflowDefinitionItem>>('/api/workflow/definitions', data)
    .then((res) => res.data.data)
}

export function updateWorkflowDefinition(id: string, data: UpdateWorkflowDefinitionRequest) {
  return request
    .put<ApiResult<WorkflowDefinitionItem>>(`/api/workflow/definitions/${id}`, data)
    .then((res) => res.data.data)
}

export function deleteWorkflowDefinition(id: string) {
  return request.delete<ApiResult<void>>(`/api/workflow/definitions/${id}`)
}

export function publishWorkflowDefinition(id: string, data: PublishWorkflowDefinitionRequest = {}) {
  return request
    .post<ApiResult<WorkflowDefinitionItem>>(`/api/workflow/definitions/${id}/publish`, data)
    .then((res) => res.data.data)
}

export function disableWorkflowDefinition(id: string) {
  return request
    .post<ApiResult<WorkflowDefinitionItem>>(`/api/workflow/definitions/${id}/disable`)
    .then((res) => res.data.data)
}

export function copyWorkflowDefinition(id: string) {
  return request
    .post<ApiResult<WorkflowDefinitionDetail>>(`/api/workflow/definitions/${id}/copy`)
    .then((res) => res.data.data)
}

export function getWorkflowDesigner(id: string) {
  return request
    .get<ApiResult<WorkflowDesigner>>(`/api/workflow/definitions/${id}/designer`)
    .then((res) => res.data.data)
}

export function saveWorkflowDesigner(id: string, data: WorkflowDesigner) {
  return request
    .put<ApiResult<WorkflowDesigner>>(`/api/workflow/definitions/${id}/designer`, data)
    .then((res) => res.data.data)
}
