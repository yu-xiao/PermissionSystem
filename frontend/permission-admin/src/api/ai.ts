import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'
import type { AiKnowledgeHit, AiKnowledgeReference } from './aiKnowledge'

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
  scenarioId?: string
  scenarioVersionId?: string
  historicalConfigurationIncomplete?: boolean
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
  latestRun?: AiRun
  structuredResults?: AiStructuredResult[]
  structuredResultsUnavailable?: boolean
  structuredResultsWindowLimited?: boolean
  permissionDiagnostics?: AiPermissionDiagnosticResult[]
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
  canEdit?: boolean
  canCancel?: boolean
  canConfirm?: boolean
  canExecute?: boolean
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

export type PermissionDiagnosticKind = 'Menu' | 'Permission' | 'DataScope'
export type PermissionDiagnosticConclusion =
  'Allowed' | 'Denied' | 'Limited' | 'InsufficientEvidence'

export interface PermissionDiagnosticRequest {
  kind: PermissionDiagnosticKind
  targetUserId?: string
  menuId?: string
  permissionCode?: string
}

export interface PermissionDiagnosticResponse {
  version: number
  target: {
    userId: string
    kind: PermissionDiagnosticKind
    menuId?: string
    permissionCode?: string
  }
  evaluationBasis: 'CurrentServerIdentity' | 'CurrentConfiguration'
  evaluatedAt: string
  conclusion: PermissionDiagnosticConclusion
  summary: string
  checks: {
    code: string
    status: 'Passed' | 'Failed' | 'NotEvaluated'
    description: string
    source: string
  }[]
  limitations: string[]
  suggestedEntries: { code: string; label: string }[]
  isTruncated: boolean
}

export interface AiPermissionDiagnosticResult {
  runId: string
  invocationId: string
  data: PermissionDiagnosticResponse
}

export interface AiContextReference {
  runId: string
  invocationId: string
}

export interface AiUserTableData {
  totalCount: number
  displayedRowCount: number
  items: {
    id: string
    userName: string
    displayName: string
    departmentId?: string
    isEnabled: boolean
    createdAt: string
  }[]
}

export interface AiStatisticsData {
  totalCount: number
  groups: {
    code: string
    totalGroupCount: number
    displayedGroupCount: number
    isTruncated: boolean
    items: { key: string; count: number }[]
  }[]
}

export interface AiStructuredResult extends AiContextReference {
  knowledgeReferences?: AiKnowledgeReference[]
  knowledgeHits?: AiKnowledgeHit[]
  type: string
  version: number
  toolCode: string
  toolVersion: string
  queriedAt: string
  evaluationBasis: string
  context: { version: number; parameters: Record<string, unknown> }
  citation: AiToolCitation
  isTruncated: boolean
  limitations: string[]
  diagnostic?: PermissionDiagnosticResponse
  table?: AiUserTableData
  statistics?: AiStatisticsData
  demoOrders?: AiDemoBusinessOrderTableData
  metrics?: AiControlledUserMetrics
  report?: {
    reportDefinitionId: string
    datasetKey: string
    datasetVersion: string
    definitionFingerprint: string
  }
}

export interface AiDemoBusinessOrderTableData {
  totalCount: number
  displayedRowCount: number
  items: {
    id: string
    orderNo: string
    title: string
    approvalStatus: 'Draft' | 'Pending' | 'Approved' | 'Rejected' | 'Withdrawn' | 'Cancelled'
    departmentId?: string | null
    createdAt: string
  }[]
}

export interface AiUserMetricValues {
  userCount: number
  enabledUserCount: number
  disabledUserCount: number
}

export interface AiControlledUserMetrics {
  dimension: 'None' | 'IsEnabled' | 'DepartmentId'
  definitions: { code: string; name: string; definition: string; unit: string }[]
  totals: AiUserMetricValues
  totalGroupCount: number
  displayedGroupCount: number
  isTruncated: boolean
  groups: { key: string | null; values: AiUserMetricValues }[]
}

export interface AiRun {
  progressVersion?: number
  toolProgress?: {
    invocationId: string
    toolCode: string
    status: AiInvocationStatus
    completedAt?: string
  }[]
  scenarioVersionId?: string
  scenarioContentHash?: string
  buildIdentity?: string
  executionConfigurationHash?: string
  historicalConfigurationIncomplete?: boolean
  structuredResults?: AiStructuredResult[]
  structuredResultsUnavailable?: boolean
  structuredResultsWindowLimited?: boolean
  permissionDiagnostics?: AiPermissionDiagnosticResult[]
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
  progressVersion?: number
  invocationId?: string
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

export interface AiScenarioOperationsItem {
  scenarioId: string | null
  scenarioName: string
  scenarioCode: string | null
  scenarioAvailable: boolean
  runCount: number
  pendingRunCount: number
  runningRunCount: number
  completedRunCount: number
  failedRunCount: number
  cancelledRunCount: number
  unknownStatusRunCount: number
  terminalRunCount: number
  timeoutFailureCount: number
  technicalCompletionRate: number | null
  feedbackEligibleRunCount: number
  positiveFeedbackCount: number
  negativeFeedbackCount: number
  feedbackCoverageRate: number | null
  positiveFeedbackRate: number | null
  durationSampleCount: number
  p95DurationMilliseconds: number | null
  inputTokens: number
  outputTokens: number
  unknownUsageInvocationCount: number
  unknownCostInvocationCount: number
  unsettledInvocationCount: number
  unknownStatusInvocationCount: number
  estimatedCosts: AiCurrencyCost[]
}

export interface AiScenarioOperations {
  metricsVersion: number
  from: string
  to: string
  observedFrom: string
  observedTo: string
  limits: { maxRuns: number; maxUsages: number; maxFeedback: number }
  scenarios: PagedResult<AiScenarioOperationsItem>
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

export function createAiConversation(title?: string, scenarioId?: string) {
  return request
    .post<ApiResult<AiConversationDetail>>('/api/ai/conversations', { title, scenarioId })
    .then((res) => res.data.data)
}

export function deleteAiConversation(id: string) {
  return request.delete<ApiResult<void>>(`/api/ai/conversations/${id}`)
}

export function sendAiMessage(
  conversationId: string,
  content: string,
  contextRef?: AiContextReference,
  utcOffsetMinutes?: number,
  submissionKey?: string,
) {
  return request
    .post<ApiResult<AiRun>>(
      `/api/ai/conversations/${conversationId}/runs`,
      { content, contextRef, utcOffsetMinutes },
      { headers: submissionKey ? { 'X-Idempotency-Key': submissionKey } : undefined },
    )
    .then((res) => res.data.data)
}

export function getAiRun(runId: string) {
  return request.get<ApiResult<AiRun>>(`/api/ai/runs/${runId}`).then((res) => res.data.data)
}

export function getAiSubmission(conversationId: string, submissionKey: string) {
  return request
    .get<ApiResult<AiRun>>(`/api/ai/conversations/${conversationId}/submission`, {
      headers: { 'X-Idempotency-Key': submissionKey },
    })
    .then((res) => res.data.data)
}

export function diagnoseAiPermission(data: PermissionDiagnosticRequest) {
  return request
    .post<ApiResult<PermissionDiagnosticResponse>>('/api/ai/permission-diagnostics', data)
    .then((res) => res.data.data)
}

export function cancelAiRun(runId: string) {
  return request.post<ApiResult<void>>(`/api/ai/runs/${runId}/cancel`)
}

export function retryAiRun(runId: string, submissionKey?: string) {
  return request
    .post<ApiResult<AiRun>>(`/api/ai/runs/${runId}/retry-async`, undefined, {
      headers: submissionKey ? { 'X-Idempotency-Key': submissionKey } : undefined,
    })
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

export function getAiScenarioOperations(params: {
  from?: string
  to?: string
  pageIndex: number
  pageSize: number
}) {
  return request
    .get<ApiResult<AiScenarioOperations>>('/api/ai/operations/scenarios', { params })
    .then((res) => res.data.data)
}

export interface AiTechnicalExportReceiptSummary {
  exportId: string
  firstRecordedAt: string
  lastRecordedAt: string
  requestedCount: number
  preparedCount: number
  failedCount: number
  canVerify: boolean
  verificationReason: string
}

export interface AiTechnicalExportReceipt {
  receiptId: string
  recordedAt: string
  schemaVersion: number
  outcome: 'Requested' | 'Prepared' | 'Failed'
  from: string
  to: string
  observedFrom: string
  observedTo: string | null
  runCount: number | null
  usageCount: number | null
  bytes: number | null
  fileSha256: string | null
  failureCode: string | null
}

interface AiTechnicalExportReceiptWindow {
  tenantId: string
  scope: 'CurrentCaller'
  receiptFrom: string
  receiptTo: string
  observedFrom: string
  observedTo: string
  matchedRecordCount: number
  unreadableRecordCount: number
  windowInterpretable: boolean
}

export interface AiTechnicalExportReceiptPage extends AiTechnicalExportReceiptWindow {
  exports: PagedResult<AiTechnicalExportReceiptSummary>
}

export interface AiTechnicalExportReceiptDetail extends AiTechnicalExportReceiptWindow {
  export: AiTechnicalExportReceiptSummary
  receipts: AiTechnicalExportReceipt[]
}

export function getAiTechnicalExportReceipts(
  params: { from?: string; to?: string; pageIndex: number; pageSize: number },
  signal: AbortSignal,
) {
  return request
    .get<ApiResult<AiTechnicalExportReceiptPage>>('/api/ai/operations/technical-export-receipts', {
      params,
      signal,
    })
    .then((res) => res.data.data)
}

export function getAiTechnicalExportReceipt(
  exportId: string,
  params: { from: string; to: string },
  signal: AbortSignal,
) {
  return request
    .get<ApiResult<AiTechnicalExportReceiptDetail>>(
      `/api/ai/operations/technical-export-receipts/${encodeURIComponent(exportId)}`,
      { params, signal },
    )
    .then((res) => res.data.data)
}

export interface AiCostQualityBasis {
  recordedBothCount: number
  mixedFallbackCount: number
  estimatedBothCount: number
  unusableTokenPairCount: number
}
export interface AiCostQualityPopulation {
  invocationCount: number
  terminalCount: number
  unsettledCount: number
  unknownStatusCount: number
  comparableCostCount: number
  consistentCostCount: number
  differentCostCount: number
  uncomparableCostCount: number
}
export interface AiCostQualityTokenComparison {
  sampleCount: number
  recordedTokens: number
  estimatedTokens: number
  differenceTokens: number
  absoluteDifferenceTokens: number
  weightedRatioPercentage: number | null
  zeroEstimatePairCount: number
  aboveEstimateCount: number
}
export interface AiCostQualityCurrencySummary {
  currency: string
  terminalCount: number
  basis: AiCostQualityBasis
  comparableCostCount: number
  consistentCostCount: number
  differentCostCount: number
  uncomparableCostCount: number
  storedCost: string | null
  recomputedCost: string | null
  differenceCost: string | null
  absoluteDifferenceCost: string | null
}
export interface AiCostQualityResponse {
  metricsVersion: number
  scope: 'CurrentTenantReadableRunInvocations'
  costBasis: 'HistoricalSnapshotEstimateNotSupplierInvoice'
  tenantId: string
  from: string
  to: string
  observedFrom: string
  observedTo: string
  limits: { maxUsages: number; readSeconds: number }
  population: AiCostQualityPopulation
  basis: AiCostQualityBasis
  issues: { code: string; count: number }[]
  inputComparison: AiCostQualityTokenComparison
  outputLimitComparison: AiCostQualityTokenComparison
  totalTokens: { comparableCount: number; differentCount: number }
  currencies: PagedResult<AiCostQualityCurrencySummary>
}
export function getAiCostQuality(
  params: { from?: string; to?: string; pageIndex?: number; pageSize?: number },
  signal: AbortSignal,
) {
  return request
    .get<ApiResult<AiCostQualityResponse>>('/api/ai/operations/cost-quality', { params, signal })
    .then((res) => res.data.data)
}

export interface AiCostQualityDay {
  date: string
  bucketFrom: string
  bucketTo: string
  isPartialDay: boolean
  population: AiCostQualityPopulation
  basis: AiCostQualityBasis
  issues: { code: string; count: number }[]
  inputComparison: AiCostQualityTokenComparison
  outputLimitComparison: AiCostQualityTokenComparison
  totalTokens: { comparableCount: number; differentCount: number }
}
export interface AiCostQualityCurrencyDistribution {
  summary: AiCostQualityCurrencySummary
  storedAboveRecomputedCount: number
  storedBelowRecomputedCount: number
}
export interface AiCostQualityTrendResponse extends Omit<AiCostQualityResponse, 'currencies'> {
  grouping: 'UsageCreatedAtUtcDay'
  bucketTimezone: 'UTC'
  daily: AiCostQualityDay[]
  currencies: PagedResult<AiCostQualityCurrencyDistribution>
}
export function getAiCostQualityTrends(
  params: { from?: string; to?: string; pageIndex?: number; pageSize?: number },
  signal: AbortSignal,
) {
  return request
    .get<ApiResult<AiCostQualityTrendResponse>>('/api/ai/operations/cost-quality/trends', {
      params,
      signal,
    })
    .then((res) => res.data.data)
}

export async function exportAiTechnicalMetadata(
  data: { from: string; to: string },
  signal: AbortSignal,
) {
  const response = await request.post<Blob>('/api/ai/operations/technical-export', data, {
    responseType: 'blob',
    signal,
  })
  const disposition = String(response.headers['content-disposition'] ?? '')
  const fileName = disposition.match(/filename="?(ai-technical-[a-f0-9]{32}\.json)"?(?:;|$)/i)?.[1]
  if (
    response.status !== 200 ||
    !String(response.headers['content-type'] ?? '')
      .toLowerCase()
      .startsWith('application/json') ||
    !disposition.toLowerCase().startsWith('attachment;') ||
    !fileName ||
    !(response.data instanceof Blob) ||
    response.data.size > 16 * 1024 * 1024 ||
    response.data.size === 0
  ) {
    throw new Error('技术元数据导出响应无效，请重试。')
  }
  return { content: response.data, fileName }
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
