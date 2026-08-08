import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface ReportDefinitionQuery extends PageQuery {
  category?: string
  dataSourceType?: string
  isEnabled?: boolean
}

export interface ReportQueryParam {
  id?: string
  reportId?: string
  paramCode: string
  paramName: string
  paramType: string
  defaultValue?: string
  required: boolean
  sort: number
}

export interface ReportDefinitionItem {
  id: string
  tenantId: string
  reportCode: string
  reportName: string
  category: string
  dataSourceType: string
  datasetKey?: string
  apiUrl?: string
  columnsJson?: string
  paramsJson?: string
  isEnabled: boolean
  remark?: string
  createdAt: string
  queryParams: ReportQueryParam[]
}

export interface CreateReportDefinitionRequest {
  reportCode: string
  reportName: string
  category: string
  dataSourceType: string
  datasetKey?: string
  apiUrl?: string
  columnsJson?: string
  paramsJson?: string
  isEnabled: boolean
  remark?: string
  queryParams: ReportQueryParam[]
}

export type UpdateReportDefinitionRequest = Omit<CreateReportDefinitionRequest, 'reportCode'>

export interface ReportDatasetItem {
  key: string
  name: string
}

export interface ReportQueryRequest {
  params: Record<string, unknown>
}

export interface ReportColumn {
  key: string
  title: string
  width?: string
  type?: string
}

export interface ReportQueryResult {
  columns: ReportColumn[]
  rows: Record<string, unknown>[]
  elapsedMilliseconds: number
  rowCount: number
}

export interface ReportExecutionLogQuery extends PageQuery {
  reportCode?: string
  executeUserName?: string
}

export interface ReportExecutionLogItem {
  id: string
  tenantId: string
  reportId: string
  reportCode: string
  executeUserId?: string
  executeUserName?: string
  paramsJson?: string
  elapsedMilliseconds: number
  rowCount: number
  isSuccess: boolean
  failureReason?: string
  createdAt: string
}

const baseUrl = '/api/reports'

export function getReports(params: ReportDefinitionQuery) {
  return request.get<ApiResult<PagedResult<ReportDefinitionItem>>>(baseUrl, { params }).then((res) => res.data.data)
}

export function getReport(id: string) {
  return request.get<ApiResult<ReportDefinitionItem>>(`${baseUrl}/${id}`).then((res) => res.data.data)
}

export function getReportDatasets() {
  return request.get<ApiResult<ReportDatasetItem[]>>(`${baseUrl}/datasets`).then((res) => res.data.data)
}

export function createReport(data: CreateReportDefinitionRequest) {
  return request.post<ApiResult<ReportDefinitionItem>>(baseUrl, data).then((res) => res.data.data)
}

export function updateReport(id: string, data: UpdateReportDefinitionRequest) {
  return request.put<ApiResult<ReportDefinitionItem>>(`${baseUrl}/${id}`, data).then((res) => res.data.data)
}

export function deleteReport(id: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/${id}`)
}

export function queryReport(id: string, data: ReportQueryRequest) {
  return request.post<ApiResult<ReportQueryResult>>(`${baseUrl}/${id}/query`, data).then((res) => res.data.data)
}

export function exportReport(id: string, data: ReportQueryRequest) {
  return request.post(`${baseUrl}/${id}/export`, data, { responseType: 'blob' }).then((res) => res.data as Blob)
}

export function getReportExecutionLogs(params: ReportExecutionLogQuery) {
  return request
    .get<ApiResult<PagedResult<ReportExecutionLogItem>>>(`${baseUrl}/execution-logs`, { params })
    .then((res) => res.data.data)
}
