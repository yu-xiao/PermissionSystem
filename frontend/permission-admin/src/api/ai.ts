import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export const AiProviderType = {
  OpenAiCompatible: 1,
} as const

export type AiProviderType = (typeof AiProviderType)[keyof typeof AiProviderType]

export type AiConversationStatus = 1 | 2 | 3
export type AiMessageRole = 1 | 2 | 3 | 4
export type AiRunStatus = 1 | 2 | 3 | 4 | 5
export type AiInvocationStatus = 1 | 2 | 3 | 4 | 5

export interface AiProviderQuery extends PageQuery {
  enabled?: boolean
}

export interface AiProviderListItem {
  id: string
  tenantId: string
  providerCode: string
  providerName: string
  providerType: AiProviderType
  baseUrl: string
  modelName: string
  isDefault: boolean
  isEnabled: boolean
  dataResidency?: string
  complianceConfirmedAt?: string
  createdAt: string
  concurrencyToken: string
}

export interface AiProviderDetail extends AiProviderListItem {
  chatCompletionsPath: string
  apiKey: string
  hasApiKey: boolean
  timeoutSeconds: number
  temperature?: number
  maxTokens?: number
  allowInsecureHttp: boolean
  allowPrivateNetwork: boolean
  allowedHosts: string[]
  remark?: string
  updatedAt?: string
}

export interface SaveAiProviderRequest {
  tenantId?: string
  providerCode?: string
  providerName: string
  providerType?: AiProviderType
  baseUrl: string
  chatCompletionsPath: string
  apiKey?: string
  modelName: string
  isDefault?: boolean
  isEnabled?: boolean
  timeoutSeconds: number
  temperature?: number
  maxTokens?: number
  allowInsecureHttp: boolean
  allowPrivateNetwork: boolean
  allowedHosts: string[]
  dataResidency?: string
  remark?: string
  concurrencyToken?: string
}

export interface AiConversationListItem {
  id: string
  title: string
  status: AiConversationStatus
  lastMessageAt: string
  lastRunAt?: string
}

export interface AiMessageItem {
  id: string
  role: AiMessageRole
  content: string
  sequence: number
  modelGenerated: boolean
  createdAt: string
}

export interface AiConversationDetail extends AiConversationListItem {
  agentCode: string
  agentVersion: string
  messages: AiMessageItem[]
}

export interface AiToolCitation {
  sourceSystem: string
  toolCode: string
  toolVersion: string
  datasetCode?: string
  datasetVersion?: string
  queryParametersDigest: string
  queriedAt: string
  asOf?: string
  rowCount: number
}

export interface AiRun {
  id: string
  conversationId: string
  requestMessageId: string
  responseMessageId?: string
  status: AiRunStatus
  modelName: string
  traceId: string
  startedAt?: string
  completedAt?: string
  durationMilliseconds?: number
  inputTokens?: number
  outputTokens?: number
  errorCode?: string
  errorSummary?: string
  cancellationRequestedAt?: string
  responseMessage?: AiMessageItem
  citations: AiToolCitation[]
}

export interface AiRunRealtimeMessage {
  runId: string
  conversationId: string
  eventType: string
  status: AiRunStatus
  toolCode?: string
  toolStatus?: AiInvocationStatus
  errorCode?: string
  occurredAt: string
}

export function getAiProviders(params: AiProviderQuery) {
  return request
    .get<ApiResult<PagedResult<AiProviderListItem>>>('/api/ai/providers', { params })
    .then((res) => res.data.data)
}

export function getAiProvider(id: string) {
  return request.get<ApiResult<AiProviderDetail>>(`/api/ai/providers/${id}`).then((res) => res.data.data)
}

export function createAiProvider(data: SaveAiProviderRequest) {
  return request.post<ApiResult<AiProviderDetail>>('/api/ai/providers', data).then((res) => res.data.data)
}

export function updateAiProvider(id: string, data: SaveAiProviderRequest) {
  return request.put<ApiResult<AiProviderDetail>>(`/api/ai/providers/${id}`, data).then((res) => res.data.data)
}

export function deleteAiProvider(id: string) {
  return request.delete<ApiResult<void>>(`/api/ai/providers/${id}`)
}

export function setAiProviderEnabled(id: string, isEnabled: boolean, concurrencyToken: string) {
  return request.put<ApiResult<void>>(`/api/ai/providers/${id}/enabled`, { isEnabled, concurrencyToken })
}

export function setDefaultAiProvider(id: string) {
  return request.post<ApiResult<void>>(`/api/ai/providers/${id}/default`)
}

export function testAiProvider(id: string) {
  return request
    .post<ApiResult<{ succeeded: boolean; message: string; modelName: string }>>(`/api/ai/providers/${id}/test`)
    .then((res) => res.data.data)
}

export function setAiProviderCompliance(id: string, isConfirmed: boolean, concurrencyToken: string) {
  return request.put<ApiResult<void>>(`/api/ai/providers/${id}/compliance`, {
    isConfirmed,
    concurrencyToken,
  })
}

export function getAiConversations(params: PageQuery) {
  return request
    .get<ApiResult<PagedResult<AiConversationListItem>>>('/api/ai/conversations', { params })
    .then((res) => res.data.data)
}

export function getAiConversation(id: string) {
  return request
    .get<ApiResult<AiConversationDetail>>(`/api/ai/conversations/${id}`)
    .then((res) => res.data.data)
}

export function createAiConversation(title?: string) {
  return request
    .post<ApiResult<AiConversationDetail>>('/api/ai/conversations', { title })
    .then((res) => res.data.data)
}

export function deleteAiConversation(id: string) {
  return request.delete<ApiResult<void>>(`/api/ai/conversations/${id}`)
}

export function sendAiMessage(conversationId: string, content: string) {
  return request
    .post<ApiResult<AiRun>>(
      `/api/ai/conversations/${conversationId}/messages`,
      { content },
      { timeout: 95_000 },
    )
    .then((res) => res.data.data)
}

export function getAiRun(runId: string) {
  return request.get<ApiResult<AiRun>>(`/api/ai/runs/${runId}`).then((res) => res.data.data)
}

export function cancelAiRun(runId: string) {
  return request.post<ApiResult<void>>(`/api/ai/runs/${runId}/cancel`)
}
