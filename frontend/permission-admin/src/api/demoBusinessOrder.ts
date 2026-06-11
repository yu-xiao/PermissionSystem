import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'
import type { FileResourceItem } from './files'
import type { OperationLogItem } from './operation-logs'
import type { PrintTemplateItem } from './printTemplate'

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

export interface DemoBusinessOrderImportResult {
  totalRows: number
  successRows: number
  failedRows: number
  items: Array<{
    title: string
    customerName: string
    amount: number
  }>
  errors: Array<{
    rowNumber: number
    columnName: string
    message: string
    rawValue?: string
  }>
}

export interface DemoBusinessOrderPrintResult {
  templateId: string
  templateName: string
  html: string
}

const baseUrl = '/api/demo-business-orders'

export function getDemoBusinessOrders(params: DemoBusinessOrderQuery) {
  return request
    .get<ApiResult<PagedResult<DemoBusinessOrderItem>>>(baseUrl, { params })
    .then((res) => res.data.data)
}

export function createDemoBusinessOrder(data: SaveDemoBusinessOrderRequest) {
  return request.post<ApiResult<DemoBusinessOrderItem>>(baseUrl, data).then((res) => res.data.data)
}

export function updateDemoBusinessOrder(id: string, data: SaveDemoBusinessOrderRequest) {
  return request.put<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}`, data).then((res) => res.data.data)
}

export function deleteDemoBusinessOrder(id: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/${id}`)
}

export function submitDemoBusinessOrder(id: string, remark?: string) {
  return request
    .post<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}/submit`, { remark })
    .then((res) => res.data.data)
}

export function withdrawDemoBusinessOrder(id: string, comment?: string) {
  return request
    .post<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}/withdraw`, { comment })
    .then((res) => res.data.data)
}

export function cancelDemoBusinessOrder(id: string, comment?: string) {
  return request
    .post<ApiResult<DemoBusinessOrderItem>>(`${baseUrl}/${id}/cancel`, { comment })
    .then((res) => res.data.data)
}

export function exportDemoBusinessOrders(params: DemoBusinessOrderQuery) {
  return request.get<Blob>(`${baseUrl}/export`, {
    params,
    responseType: 'blob',
    timeout: 60000,
  })
}

export function downloadDemoBusinessOrderImportTemplate() {
  return request.get<Blob>(`${baseUrl}/import-template`, {
    responseType: 'blob',
    timeout: 60000,
  })
}

export function importDemoBusinessOrders(file: File) {
  const form = new FormData()
  form.append('file', file)
  return request
    .post<ApiResult<DemoBusinessOrderImportResult>>(`${baseUrl}/import`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    })
    .then((res) => res.data.data)
}

export function getDemoBusinessOrderAttachments(id: string) {
  return request
    .get<ApiResult<FileResourceItem[]>>(`${baseUrl}/${id}/attachments`)
    .then((res) => res.data.data)
}

export function uploadDemoBusinessOrderAttachment(id: string, file: File) {
  const form = new FormData()
  form.append('file', file)
  return request
    .post<ApiResult<FileResourceItem>>(`${baseUrl}/${id}/attachments`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    })
    .then((res) => res.data.data)
}

export function getDemoBusinessOrderPrintTemplates() {
  return request.get<ApiResult<PrintTemplateItem[]>>(`${baseUrl}/print-templates`).then((res) => res.data.data)
}

export function printDemoBusinessOrder(id: string, templateId: string) {
  return request
    .post<ApiResult<DemoBusinessOrderPrintResult>>(`${baseUrl}/${id}/print/${templateId}`)
    .then((res) => res.data.data)
}

export function getDemoBusinessOrderOperationLogs(id: string, params: PageQuery) {
  return request
    .get<ApiResult<PagedResult<OperationLogItem>>>(`${baseUrl}/${id}/operation-logs`, { params })
    .then((res) => res.data.data)
}

export function getDemoBusinessOrderChangeHistories(id: string) {
  return request
    .get<ApiResult<DemoBusinessOrderChangeHistoryItem[]>>(`${baseUrl}/${id}/change-histories`)
    .then((res) => res.data.data)
}

export function notifyDemoBusinessOrderOwner(id: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/${id}/notify`)
}
