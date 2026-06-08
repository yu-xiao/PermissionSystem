import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export const ApprovalStatus = {
  Draft: 0,
  Pending: 1,
  Approved: 2,
  Rejected: 3,
  Withdrawn: 4,
  Cancelled: 5,
} as const

export type ApprovalStatus = (typeof ApprovalStatus)[keyof typeof ApprovalStatus]

export interface DemoApprovalOrderQuery extends PageQuery {
  approvalStatus?: ApprovalStatus
}

export interface DemoApprovalOrderItem {
  id: string
  tenantId: string
  orderNo: string
  title: string
  amount: number
  departmentId?: string
  applicantUserId: string
  applicantUserName: string
  approvalStatus: ApprovalStatus
  workflowInstanceId?: string
  submittedAt?: string
  submittedBy?: string
  approvedAt?: string
  rejectedAt?: string
  withdrawnAt?: string
  createdAt: string
  updatedAt?: string
}

export interface CreateDemoApprovalOrderRequest {
  tenantId?: string
  title: string
  amount: number
  departmentId?: string
}

export interface UpdateDemoApprovalOrderRequest {
  title: string
  amount: number
  departmentId?: string
}

export interface SubmitDemoApprovalOrderRequest {
  remark?: string
}

export function getDemoApprovalOrders(params: DemoApprovalOrderQuery) {
  return request
    .get<ApiResult<PagedResult<DemoApprovalOrderItem>>>('/api/demo-approval-orders', { params })
    .then((res) => res.data.data)
}

export function getDemoApprovalOrder(id: string) {
  return request
    .get<ApiResult<DemoApprovalOrderItem>>(`/api/demo-approval-orders/${id}`)
    .then((res) => res.data.data)
}

export function createDemoApprovalOrder(data: CreateDemoApprovalOrderRequest) {
  return request
    .post<ApiResult<DemoApprovalOrderItem>>('/api/demo-approval-orders', data)
    .then((res) => res.data.data)
}

export function updateDemoApprovalOrder(id: string, data: UpdateDemoApprovalOrderRequest) {
  return request
    .put<ApiResult<DemoApprovalOrderItem>>(`/api/demo-approval-orders/${id}`, data)
    .then((res) => res.data.data)
}

export function deleteDemoApprovalOrder(id: string) {
  return request.delete<ApiResult<void>>(`/api/demo-approval-orders/${id}`)
}

export function submitDemoApprovalOrder(id: string, data: SubmitDemoApprovalOrderRequest = {}) {
  return request
    .post<ApiResult<DemoApprovalOrderItem>>(`/api/demo-approval-orders/${id}/submit`, data)
    .then((res) => res.data.data)
}

export function withdrawDemoApprovalOrder(id: string, comment?: string) {
  return request
    .post<ApiResult<DemoApprovalOrderItem>>(`/api/demo-approval-orders/${id}/withdraw`, { comment })
    .then((res) => res.data.data)
}

export function cancelDemoApprovalOrder(id: string, comment?: string) {
  return request
    .post<ApiResult<DemoApprovalOrderItem>>(`/api/demo-approval-orders/${id}/cancel`, { comment })
    .then((res) => res.data.data)
}
