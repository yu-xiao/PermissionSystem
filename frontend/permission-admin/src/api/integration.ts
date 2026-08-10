import { request } from '../utils/request'
import { sensitiveVerificationHeaders } from './security'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface ApiClientQuery extends PageQuery {
  keyword?: string
  isEnabled?: boolean
}

export interface ApiClientItem {
  id: string
  tenantId: string
  clientCode: string
  clientName: string
  description?: string
  isEnabled: boolean
  allowedScopes?: string
  allowedIpList?: string
  rateLimitPerMinute: number
  createdAt: string
  concurrencyToken: string
}

export interface CreateApiClientRequest {
  clientCode: string
  clientName: string
  description?: string
  isEnabled: boolean
  allowedScopes?: string
  allowedIpList?: string
  rateLimitPerMinute: number
}

export interface UpdateApiClientRequest {
  clientName: string
  description?: string
  allowedScopes?: string
  allowedIpList?: string
  rateLimitPerMinute: number
  concurrencyToken?: string
}

export interface GeneratedApiSecret {
  clientId: string
  apiKey: string
  apiSecret: string
  expiresAt?: string
}

export interface WebhookQuery extends PageQuery {
  eventType?: string
  isEnabled?: boolean
}

export interface WebhookItem {
  id: string
  tenantId: string
  eventType: string
  targetUrl: string
  secret: string
  isEnabled: boolean
  retryCount: number
  createdAt: string
  concurrencyToken: string
}

export interface SaveWebhookRequest {
  eventType: string
  targetUrl: string
  secret?: string
  isEnabled: boolean
  retryCount: number
  concurrencyToken?: string
}

export interface WebhookLogQuery extends PageQuery {
  eventType?: string
  status?: string
}

export interface WebhookLogItem {
  id: string
  tenantId: string
  subscriptionId: string
  eventType: string
  payload: string
  status: string
  responseStatusCode?: number
  responseBody?: string
  retryCount: number
  createdAt: string
}

export interface ApiCallLogQuery extends PageQuery {
  clientId?: string
  path?: string
}

export interface ApiCallLogItem {
  id: string
  tenantId: string
  clientId?: string
  clientCode?: string
  path: string
  method: string
  ipAddress?: string
  statusCode: number
  elapsedMilliseconds: number
  createdAt: string
}

const baseUrl = '/api/integration'

export function getApiClients(params: ApiClientQuery) {
  return request.get<ApiResult<PagedResult<ApiClientItem>>>(`${baseUrl}/clients`, { params }).then((res) => res.data.data)
}

export function createApiClient(data: CreateApiClientRequest, stepUpTicket?: string) {
  return request
    .post<ApiResult<ApiClientItem>>(`${baseUrl}/clients`, data, {
      headers: sensitiveVerificationHeaders(stepUpTicket),
    })
    .then((res) => res.data.data)
}

export function updateApiClient(id: string, data: UpdateApiClientRequest, stepUpTicket?: string) {
  return request
    .put<ApiResult<ApiClientItem>>(`${baseUrl}/clients/${id}`, data, {
      headers: sensitiveVerificationHeaders(stepUpTicket),
    })
    .then((res) => res.data.data)
}

export function deleteApiClient(id: string, stepUpTicket?: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/clients/${id}`, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}

export function generateApiClientSecret(id: string, stepUpTicket?: string) {
  return request
    .post<ApiResult<GeneratedApiSecret>>(`${baseUrl}/clients/${id}/generate-secret`, undefined, {
      headers: sensitiveVerificationHeaders(stepUpTicket),
    })
    .then((res) => res.data.data)
}

export function enableApiClient(id: string, stepUpTicket?: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/clients/${id}/enable`, undefined, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}

export function disableApiClient(id: string, stepUpTicket?: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/clients/${id}/disable`, undefined, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}

export function getWebhooks(params: WebhookQuery) {
  return request.get<ApiResult<PagedResult<WebhookItem>>>(`${baseUrl}/webhooks`, { params }).then((res) => res.data.data)
}

export function createWebhook(data: SaveWebhookRequest) {
  return request.post<ApiResult<WebhookItem>>(`${baseUrl}/webhooks`, data).then((res) => res.data.data)
}

export function updateWebhook(id: string, data: SaveWebhookRequest) {
  return request.put<ApiResult<WebhookItem>>(`${baseUrl}/webhooks/${id}`, data).then((res) => res.data.data)
}

export function deleteWebhook(id: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/webhooks/${id}`)
}

export function testWebhook(id: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/webhooks/${id}/test`)
}

export function getWebhookLogs(params: WebhookLogQuery) {
  return request.get<ApiResult<PagedResult<WebhookLogItem>>>(`${baseUrl}/webhook-logs`, { params }).then((res) => res.data.data)
}

export function getApiCallLogs(params: ApiCallLogQuery) {
  return request.get<ApiResult<PagedResult<ApiCallLogItem>>>(`${baseUrl}/api-call-logs`, { params }).then((res) => res.data.data)
}
