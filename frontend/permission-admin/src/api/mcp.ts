import { request } from '../utils/request'
import { sensitiveVerificationHeaders } from './security'
import type { ApiResult, PagedResult, PageQuery } from './types'

export const mcpToolScopes = [
  { label: '数据集列表', value: 'mcp:dataset:list' },
  { label: '数据集描述', value: 'mcp:dataset:describe' },
  { label: '数据集查询', value: 'mcp:dataset:query' },
] as const

export interface McpDatasetField {
  fieldCode: string
  displayName: string
  dataType: string
  dataClassification: string
  isFilterable: boolean
  isDefault: boolean
}

export interface McpDataset {
  id: string
  datasetCode: string
  datasetName: string
  version: string
  description?: string
  dataClassification: string
  maxRows: number
  fields: McpDatasetField[]
}

export interface McpDatasetGrant {
  datasetId: string
  datasetCode: string
  datasetName: string
  allowedFields: string[]
}

export interface McpClient {
  id: string
  apiClientId: string
  oauthClientId: string
  clientCode: string
  clientName: string
  description?: string
  isEnabled: boolean
  allowedScopes: string[]
  allowedIpList: string
  rateLimitPerMinute: number
  datasetGrants: McpDatasetGrant[]
  concurrencyToken: string
  createdAt: string
}

export interface McpClientQuery extends PageQuery {
  keyword?: string
  isEnabled?: boolean
}

export interface McpDatasetGrantRequest {
  datasetId: string
  allowedFields: string[]
}

export interface CreateMcpClientRequest {
  clientCode: string
  clientName: string
  description?: string
  allowedScopes: string[]
  allowedIpList: string
  rateLimitPerMinute: number
  datasetGrants: McpDatasetGrantRequest[]
}

export interface UpdateMcpClientRequest extends Omit<CreateMcpClientRequest, 'clientCode'> {
  concurrencyToken: string
}

export interface McpClientCredential {
  client: McpClient
  clientSecret: string
}

export interface McpInvocationLog {
  id: string
  callerType: number
  clientBindingId?: string
  oauthClientId?: string
  toolName: string
  datasetCode?: string
  traceId: string
  ipAddress?: string
  status: number
  rowCount: number
  isTruncated: boolean
  durationMilliseconds: number
  errorCode?: string
  errorSummary?: string
  createdAt: string
}

export interface McpInvocationQuery extends PageQuery {
  clientBindingId?: string
  datasetCode?: string
  status?: number
}

const baseUrl = '/api/ai/mcp'

export function getMcpClients(params: McpClientQuery) {
  return request.get<ApiResult<PagedResult<McpClient>>>(`${baseUrl}/clients`, { params }).then((res) => res.data.data)
}

export function getMcpDatasets() {
  return request.get<ApiResult<McpDataset[]>>(`${baseUrl}/datasets`).then((res) => res.data.data)
}

export function createMcpClient(data: CreateMcpClientRequest, stepUpTicket: string) {
  return request.post<ApiResult<McpClientCredential>>(`${baseUrl}/clients`, data, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  }).then((res) => res.data.data)
}

export function updateMcpClient(id: string, data: UpdateMcpClientRequest, stepUpTicket: string) {
  return request.put<ApiResult<McpClient>>(`${baseUrl}/clients/${id}`, data, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  }).then((res) => res.data.data)
}

export function setMcpClientEnabled(id: string, isEnabled: boolean, concurrencyToken: string, stepUpTicket: string) {
  return request.put<ApiResult<McpClient>>(`${baseUrl}/clients/${id}/enabled`, {
    isEnabled,
    concurrencyToken,
  }, { headers: sensitiveVerificationHeaders(stepUpTicket) }).then((res) => res.data.data)
}

export function rotateMcpClientSecret(id: string, concurrencyToken: string, stepUpTicket: string) {
  return request.post<ApiResult<McpClientCredential>>(`${baseUrl}/clients/${id}/rotate-secret`, { concurrencyToken }, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  }).then((res) => res.data.data)
}

export function getMcpInvocationLogs(params: McpInvocationQuery) {
  return request.get<ApiResult<PagedResult<McpInvocationLog>>>(`${baseUrl}/invocations`, { params }).then((res) => res.data.data)
}
