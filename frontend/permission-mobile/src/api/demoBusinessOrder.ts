import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'
import type { FileResourceItem } from './files'

export const ApprovalStatus = {
  Draft: 0,
  Pending: 1,
  Approved: 2,
  Rejected: 3,
  Withdrawn: 4,
  Cancelled: 5,
} as const

export type ApprovalStatus = (typeof ApprovalStatus)[keyof typeof ApprovalStatus]

export interface DemoBusinessOrderQuery extends PageQuery {
  approvalStatus?: ApprovalStatus
  departmentId?: string
}

export interface DemoBusinessOrderItem {
  id: string
  tenantId: string
  orderNo: string
  title: string
  customerName: string
  amount: number
  departmentId?: string
  ownerUserId: string
  ownerUserName: string
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

export interface SaveDemoBusinessOrderRequest {
  tenantId?: string
  title: string
  customerName: string
  amount: number
  departmentId?: string
}

export interface DemoBusinessOrderChangeHistoryItem {
  changedAt: string
  changedBy?: string
  changedByName?: string
  action: string
  description: string
}

export interface DemoBusinessOrderPrintResult {
  templateId: string
  templateName: string
  html: string
}

const baseUrl = '/api/v1/demo-business-orders'

export async function getDemoBusinessOrders(params: DemoBusinessOrderQuery = {}) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<DemoBusinessOrderItem>>>(baseUrl, { params }),
  )
}

export async function getDemoBusinessOrder(id: string) {
  return unwrapApiResult(await request.get<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}`))
}

export async function createDemoBusinessOrder(payload: SaveDemoBusinessOrderRequest) {
  return unwrapApiResult(await request.post<ApiResult<DemoBusinessOrderItem>>(baseUrl, payload))
}

export async function updateDemoBusinessOrder(id: string, payload: SaveDemoBusinessOrderRequest) {
  return unwrapApiResult(await request.put<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}`, payload))
}

export async function deleteDemoBusinessOrder(id: string) {
  return unwrapApiResult(await request.delete<ApiResult<void>>(`${baseUrl}/${id}`))
}

export async function submitDemoBusinessOrder(id: string, remark?: string) {
  return unwrapApiResult(
    await request.post<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}/submit`, { remark }),
  )
}

export async function withdrawDemoBusinessOrder(id: string, comment?: string) {
  return unwrapApiResult(
    await request.post<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}/withdraw`, { comment }),
  )
}

export async function cancelDemoBusinessOrder(id: string, comment?: string) {
  return unwrapApiResult(
    await request.post<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}/cancel`, { comment }),
  )
}

export async function getDemoBusinessOrderAttachments(id: string) {
  return unwrapApiResult(
    await request.get<ApiResult<FileResourceItem[]>>(`${baseUrl}/${id}/attachments`),
  )
}

export async function uploadDemoBusinessOrderAttachment(id: string, file: File) {
  const form = new FormData()
  form.append('file', file)
  return unwrapApiResult(
    await request.post<ApiResult<FileResourceItem>>(`${baseUrl}/${id}/attachments`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    }),
  )
}

export async function getDemoBusinessOrderChangeHistories(id: string) {
  return unwrapApiResult(
    await request.get<ApiResult<DemoBusinessOrderChangeHistoryItem[]>>(`${baseUrl}/${id}/change-histories`),
  )
}

export async function notifyDemoBusinessOrderOwner(id: string) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`${baseUrl}/${id}/notify`))
}

export async function exportDemoBusinessOrders(params: DemoBusinessOrderQuery = {}) {
  return request.get<Blob>(`${baseUrl}/export`, {
    params,
    responseType: 'blob',
    timeout: 60000,
  })
}

