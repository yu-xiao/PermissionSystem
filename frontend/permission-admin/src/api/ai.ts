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
export type AiDocumentDraftStatus = 1 | 2 | 3 | 4 | 5 | 6
export type AiBudgetScopeType = 1 | 2
export type AiFeedbackRating = 1 | 2

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
  supportsTools: boolean
  supportsJsonSchema: boolean
  inputTokenPricePerMillion?: number
  outputTokenPricePerMillion?: number
  pricingCurrency?: string
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
  supportsTools: boolean
  supportsJsonSchema: boolean
  inputTokenPricePerMillion?: number
  outputTokenPricePerMillion?: number
  pricingCurrency?: string
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
  runId?: string
  feedback?: AiFeedback
}

export interface AiConversationDetail extends AiConversationListItem {
  agentCode: string
  agentVersion: string
  messages: AiMessageItem[]
  documentDrafts: AiDocumentDraft[]
}

export interface AiDraftAssociationCandidate {
  id: string
  code: string
  name: string
}

export interface AiDraftValidationError {
  field: string
  code: string
  message: string
  candidates: AiDraftAssociationCandidate[]
}

export interface DemoBusinessOrderDraftPayload {
  title?: string
  customerName?: string
  amount?: number
  departmentId?: string
  departmentCode?: string
  departmentName?: string
  departmentReference?: string
}

export interface AiDocumentDraft {
  id: string
  conversationId: string
  runId: string
  businessType: string
  handlerVersion: string
  status: AiDocumentDraftStatus
  draftVersion: number
  payload: DemoBusinessOrderDraftPayload
  payloadHash: string
  validationErrors: AiDraftValidationError[]
  expiresAt: string
  lastValidatedAt?: string
  concurrencyToken: string
  execution?: AiDocumentExecutionResult
}

export interface UpdateAiDocumentDraftRequest extends DemoBusinessOrderDraftPayload {
  concurrencyToken: string
}

export interface AiDocumentConfirmation {
  id: string
  draftId: string
  draftVersion: number
  confirmationVersion: number
  payloadHash: string
  handlerVersion: string
  confirmedAt: string
  expiresAt: string
  concurrencyToken: string
}

export interface AiDocumentExecutionResult {
  executionId: string
  draftId: string
  runId: string
  businessEntityId: string
  businessNo: string
  businessStatus: string
  linkUrl: string
  traceId: string
  completedAt: string
  draftStatus: AiDocumentDraftStatus
  draftConcurrencyToken: string
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
  estimatedCost?: number
  fallbackCount: number
  errorCode?: string
  errorSummary?: string
  cancellationRequestedAt?: string
  responseMessage?: AiMessageItem
  citations: AiToolCitation[]
  documentDrafts: AiDocumentDraft[]
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

export interface AiModelRoutePolicy {
  id: string
  tenantId: string
  agentCode: string
  primaryProviderConfigId: string
  canaryProviderConfigId?: string
  canaryPercentage: number
  fallbackProviderConfigId?: string
  isEnabled: boolean
  concurrencyToken: string
}

export interface SaveAiModelRoutePolicyRequest {
  tenantId?: string
  agentCode: string
  primaryProviderConfigId: string
  canaryProviderConfigId?: string
  canaryPercentage: number
  fallbackProviderConfigId?: string
  isEnabled: boolean
  concurrencyToken?: string
}

export interface AiModelRouteProviderOption {
  id: string
  providerName: string
  modelName: string
  isEnabled: boolean
  isComplianceConfirmed: boolean
  supportsTools: boolean
  dataResidency?: string
  pricingCurrency?: string
}

export interface AiBudgetPolicy {
  id: string
  tenantId: string
  policyCode: string
  policyName: string
  scopeType: AiBudgetScopeType
  userId?: string
  monthlyLimit: number
  currency: string
  isHardLimit: boolean
  alertThresholdPercentage: number
  isEnabled: boolean
  currentAmount: number
  isAlertThresholdExceeded: boolean
  isLimitExceeded: boolean
  concurrencyToken: string
}

export interface SaveAiBudgetPolicyRequest extends Omit<
  AiBudgetPolicy,
  | 'id'
  | 'tenantId'
  | 'concurrencyToken'
  | 'currentAmount'
  | 'isAlertThresholdExceeded'
  | 'isLimitExceeded'
> {
  tenantId?: string
  concurrencyToken?: string
}

export interface AiFeedback {
  runId: string
  rating: AiFeedbackRating
  reasonCode?: string
  comment?: string
  updatedAt: string
}

export interface AiCurrencyCost {
  currency: string
  amount: number
}

export interface AiProviderOperations {
  providerConfigId: string
  providerName: string
  invocationCount: number
  failedInvocationCount: number
  inputTokens: number
  outputTokens: number
}

export interface AiDailyOperations {
  date: string
  runCount: number
  successfulRunCount: number
  positiveFeedbackCount: number
  negativeFeedbackCount: number
}

export interface AiOperationsSummary {
  from: string
  to: string
  runCount: number
  successfulRunCount: number
  failedRunCount: number
  fallbackRunCount: number
  inputTokens: number
  outputTokens: number
  unknownCostInvocationCount: number
  positiveFeedbackCount: number
  negativeFeedbackCount: number
  p95DurationMilliseconds?: number
  costs: AiCurrencyCost[]
  providers: AiProviderOperations[]
  daily: AiDailyOperations[]
}

export function getAiProviders(params: AiProviderQuery) {
  return request
    .get<ApiResult<PagedResult<AiProviderListItem>>>('/api/ai/providers', { params })
    .then((res) => res.data.data)
}

export function getAiProvider(id: string) {
  return request
    .get<ApiResult<AiProviderDetail>>(`/api/ai/providers/${id}`)
    .then((res) => res.data.data)
}

export function createAiProvider(data: SaveAiProviderRequest) {
  return request
    .post<ApiResult<AiProviderDetail>>('/api/ai/providers', data)
    .then((res) => res.data.data)
}

export function updateAiProvider(id: string, data: SaveAiProviderRequest) {
  return request
    .put<ApiResult<AiProviderDetail>>(`/api/ai/providers/${id}`, data)
    .then((res) => res.data.data)
}

export function deleteAiProvider(id: string) {
  return request.delete<ApiResult<void>>(`/api/ai/providers/${id}`)
}

export function setAiProviderEnabled(id: string, isEnabled: boolean, concurrencyToken: string) {
  return request.put<ApiResult<void>>(`/api/ai/providers/${id}/enabled`, {
    isEnabled,
    concurrencyToken,
  })
}

export function setDefaultAiProvider(id: string) {
  return request.post<ApiResult<void>>(`/api/ai/providers/${id}/default`)
}

export function testAiProvider(id: string) {
  return request
    .post<ApiResult<{ succeeded: boolean; message: string; modelName: string }>>(
      `/api/ai/providers/${id}/test`,
    )
    .then((res) => res.data.data)
}

export function setAiProviderCompliance(
  id: string,
  isConfirmed: boolean,
  concurrencyToken: string,
) {
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

export function retryAiRun(runId: string) {
  return request
    .post<ApiResult<AiRun>>(`/api/ai/runs/${runId}/retry`)
    .then((res) => res.data.data)
}

export function getAiModelRoutes() {
  return request
    .get<ApiResult<AiModelRoutePolicy[]>>('/api/ai/governance/routes')
    .then((res) => res.data.data)
}

export function saveAiModelRoute(data: SaveAiModelRoutePolicyRequest) {
  return request
    .put<ApiResult<AiModelRoutePolicy>>('/api/ai/governance/routes', data)
    .then((res) => res.data.data)
}

export function getAiModelRouteProviders() {
  return request
    .get<ApiResult<AiModelRouteProviderOption[]>>('/api/ai/governance/providers')
    .then((res) => res.data.data)
}

export function getAiBudgetPolicies() {
  return request
    .get<ApiResult<AiBudgetPolicy[]>>('/api/ai/governance/budgets')
    .then((res) => res.data.data)
}

export function saveAiBudgetPolicy(data: SaveAiBudgetPolicyRequest) {
  return request
    .put<ApiResult<AiBudgetPolicy>>('/api/ai/governance/budgets', data)
    .then((res) => res.data.data)
}

export function getMyAiFeedback(runId: string) {
  return request
    .get<ApiResult<AiFeedback | null>>(`/api/ai/runs/${runId}/feedback`)
    .then((res) => res.data.data)
}

export function saveMyAiFeedback(
  runId: string,
  data: { rating: AiFeedbackRating; reasonCode?: string; comment?: string },
) {
  return request
    .put<ApiResult<AiFeedback>>(`/api/ai/runs/${runId}/feedback`, data)
    .then((res) => res.data.data)
}

export function getAiOperationsSummary(params: { from?: string; to?: string }) {
  return request
    .get<ApiResult<AiOperationsSummary>>('/api/ai/operations/summary', { params })
    .then((res) => res.data.data)
}

export function updateAiDocumentDraft(id: string, data: UpdateAiDocumentDraftRequest) {
  return request
    .put<ApiResult<AiDocumentDraft>>(`/api/ai/document-drafts/${id}`, data)
    .then((res) => res.data.data)
}

export function cancelAiDocumentDraft(id: string, concurrencyToken: string) {
  return request
    .post<ApiResult<AiDocumentDraft>>(`/api/ai/document-drafts/${id}/cancel`, { concurrencyToken })
    .then((res) => res.data.data)
}

export function confirmAiDocumentDraft(
  id: string,
  draftConcurrencyToken: string,
  stepUpTicket: string,
) {
  return request
    .post<ApiResult<AiDocumentConfirmation>>(
      `/api/ai/document-drafts/${id}/confirmation`,
      { draftConcurrencyToken },
      { headers: { 'X-Step-Up-Ticket': stepUpTicket } },
    )
    .then((res) => res.data.data)
}

export function executeAiDocumentDraft(
  id: string,
  draftConcurrencyToken: string,
  confirmation: AiDocumentConfirmation,
) {
  return request
    .post<ApiResult<AiDocumentExecutionResult>>(`/api/ai/document-drafts/${id}/execute`, {
      confirmationId: confirmation.id,
      confirmationVersion: confirmation.confirmationVersion,
      confirmationConcurrencyToken: confirmation.concurrencyToken,
      draftConcurrencyToken,
    })
    .then((res) => res.data.data)
}
