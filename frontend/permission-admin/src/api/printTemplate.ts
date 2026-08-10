import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface PrintTemplateQuery extends PageQuery {
  businessType?: string
  templateType?: string
  isEnabled?: boolean
}

export interface PrintTemplateItem {
  id: string
  tenantId: string
  templateCode: string
  templateName: string
  businessType: string
  templateType: string
  contentHtml: string
  contentJson?: string
  paperSize: string
  orientation: string
  isDefault: boolean
  isEnabled: boolean
  version: number
  remark?: string
  createdAt: string
  concurrencyToken: string
}

export interface CreatePrintTemplateRequest {
  templateCode: string
  templateName: string
  businessType: string
  templateType: string
  contentHtml: string
  contentJson?: string
  paperSize: string
  orientation: string
  isDefault: boolean
  isEnabled: boolean
  version: number
  remark?: string
}

export type UpdatePrintTemplateRequest = Omit<CreatePrintTemplateRequest, 'templateCode'> & {
  concurrencyToken?: string
}

export interface PrintRenderRequest {
  businessId?: string
  data?: Record<string, unknown>
}

export interface PrintRenderResult {
  templateId: string
  templateCode: string
  templateName: string
  html: string
}

export interface PrintRecordQuery extends PageQuery {
  businessType?: string
  businessId?: string
  templateId?: string
}

export interface PrintRecordItem {
  id: string
  tenantId: string
  templateId: string
  businessType: string
  businessId: string
  printUserId?: string
  printUserName?: string
  printedAt: string
  printCount: number
}

const baseUrl = '/api/system'

export function getPrintTemplates(params: PrintTemplateQuery) {
  return request
    .get<ApiResult<PagedResult<PrintTemplateItem>>>(`${baseUrl}/print-templates`, { params })
    .then((res) => res.data.data)
}

export function getPrintTemplate(id: string) {
  return request.get<ApiResult<PrintTemplateItem>>(`${baseUrl}/print-templates/${id}`).then((res) => res.data.data)
}

export function createPrintTemplate(data: CreatePrintTemplateRequest) {
  return request.post<ApiResult<PrintTemplateItem>>(`${baseUrl}/print-templates`, data).then((res) => res.data.data)
}

export function updatePrintTemplate(id: string, data: UpdatePrintTemplateRequest) {
  return request.put<ApiResult<PrintTemplateItem>>(`${baseUrl}/print-templates/${id}`, data).then((res) => res.data.data)
}

export function deletePrintTemplate(id: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/print-templates/${id}`)
}

export function getPrintTemplatesByBusinessType(businessType: string) {
  return request
    .get<ApiResult<PrintTemplateItem[]>>(`${baseUrl}/print-templates/by-business-type/${businessType}`)
    .then((res) => res.data.data)
}

export function setDefaultPrintTemplate(id: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/print-templates/${id}/set-default`)
}

export function previewPrintTemplate(id: string, data: PrintRenderRequest) {
  return request
    .post<ApiResult<PrintRenderResult>>(`${baseUrl}/print-templates/${id}/preview`, data)
    .then((res) => res.data.data)
}

export function renderPrintTemplate(id: string, data: PrintRenderRequest) {
  return request
    .post<ApiResult<PrintRenderResult>>(`${baseUrl}/print-templates/${id}/render`, data)
    .then((res) => res.data.data)
}

export function getPrintRecords(params: PrintRecordQuery) {
  return request
    .get<ApiResult<PagedResult<PrintRecordItem>>>(`${baseUrl}/print-records`, { params })
    .then((res) => res.data.data)
}
