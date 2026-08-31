import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'

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

const baseUrl = '/api/v1/demo-approval-orders'

export async function getDemoApprovalOrders(params: DemoApprovalOrderQuery = {}) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<DemoApprovalOrderItem>>>(baseUrl, { params }),
  )
}

export async function getDemoApprovalOrder(id: string) {
  return unwrapApiResult(await request.get<ApiResult<DemoApprovalOrderItem>>(`${baseUrl}/${id}`))
}

export async function createDemoApprovalOrder(payload: CreateDemoApprovalOrderRequest) {
  return unwrapApiResult(await request.post<ApiResult<DemoApprovalOrderItem>>(baseUrl, payload))
}

export async function updateDemoApprovalOrder(id: string, payload: UpdateDemoApprovalOrderRequest) {
  return unwrapApiResult(await request.put<ApiResult<DemoApprovalOrderItem>>(`${baseUrl}/${id}`, payload))
}

export async function deleteDemoApprovalOrder(id: string) {
  return unwrapApiResult(await request.delete<ApiResult<void>>(`${baseUrl}/${id}`))
}

export async function submitDemoApprovalOrder(id: string, payload: SubmitDemoApprovalOrderRequest = {}) {
  return unwrapApiResult(
    await request.post<ApiResult<DemoApprovalOrderItem>>(`${baseUrl}/${id}/submit`, payload),
  )
}

export async function withdrawDemoApprovalOrder(id: string, comment?: string) {
  return unwrapApiResult(
    await request.post<ApiResult<DemoApprovalOrderItem>>(`${baseUrl}/${id}/withdraw`, { comment }),
  )
}

export async function cancelDemoApprovalOrder(id: string, comment?: string) {
  return unwrapApiResult(
    await request.post<ApiResult<DemoApprovalOrderItem>>(`${baseUrl}/${id}/cancel`, { comment }),
  )
}

