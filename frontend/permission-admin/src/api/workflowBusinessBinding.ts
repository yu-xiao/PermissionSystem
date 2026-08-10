import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'
import type { WorkflowDefinitionStatus } from './workflowDefinition'

export interface WorkflowBusinessBindingQuery extends PageQuery {
  businessType?: string
  isEnabled?: boolean
}

export interface WorkflowBusinessBindingItem {
  id: string
  tenantId: string
  businessType: string
  businessName: string
  definitionId: string
  definitionCode: string
  definitionName: string
  definitionVersion: number
  definitionStatus: WorkflowDefinitionStatus
  isEnabled: boolean
  remark?: string
  createdAt: string
  updatedAt?: string
  concurrencyToken: string
}

export interface CreateWorkflowBusinessBindingRequest {
  tenantId?: string
  businessType: string
  businessName: string
  definitionId: string
  isEnabled: boolean
  remark?: string
}

export interface UpdateWorkflowBusinessBindingRequest {
  businessType: string
  businessName: string
  definitionId: string
  concurrencyToken?: string
  remark?: string
}

export function getWorkflowBusinessBindings(params: WorkflowBusinessBindingQuery) {
  return request
    .get<ApiResult<PagedResult<WorkflowBusinessBindingItem>>>('/api/workflow/business-bindings', { params })
    .then((res) => res.data.data)
}

export function createWorkflowBusinessBinding(data: CreateWorkflowBusinessBindingRequest) {
  return request
    .post<ApiResult<WorkflowBusinessBindingItem>>('/api/workflow/business-bindings', data)
    .then((res) => res.data.data)
}

export function updateWorkflowBusinessBinding(id: string, data: UpdateWorkflowBusinessBindingRequest) {
  return request
    .put<ApiResult<WorkflowBusinessBindingItem>>(`/api/workflow/business-bindings/${id}`, data)
    .then((res) => res.data.data)
}

export function deleteWorkflowBusinessBinding(id: string) {
  return request.delete<ApiResult<void>>(`/api/workflow/business-bindings/${id}`)
}

export function enableWorkflowBusinessBinding(id: string) {
  return request
    .post<ApiResult<WorkflowBusinessBindingItem>>(`/api/workflow/business-bindings/${id}/enable`)
    .then((res) => res.data.data)
}

export function disableWorkflowBusinessBinding(id: string) {
  return request
    .post<ApiResult<WorkflowBusinessBindingItem>>(`/api/workflow/business-bindings/${id}/disable`)
    .then((res) => res.data.data)
}

export function getWorkflowBusinessBindingByBusinessType(businessType: string) {
  return request
    .get<ApiResult<WorkflowBusinessBindingItem>>(`/api/workflow/business-bindings/by-business-type/${businessType}`)
    .then((res) => res.data.data)
}
