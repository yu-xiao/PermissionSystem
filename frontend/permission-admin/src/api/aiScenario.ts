import { request } from '../utils/request'
import type { ApiResult } from './types'

export interface AiScenarioConfiguration {
  supplementPrompt: string
  toolCodes: string[]
  maxModelRounds: number
  maxToolCalls: number
  maxHistoryMessages: number
  maxRunSeconds: number
  temperature: number
  maxTokens: number
}
export interface AiScenario {
  id: string
  code: string
  name: string
  description: string
  isEnabled: boolean
  currentVersionId?: string
  revision: number
  concurrencyToken: string
  configuration: AiScenarioConfiguration
}
export interface AiScenarioVersion {
  id: string
  versionNumber: number
  contentHash: string
  buildIdentity: string
  createdAt: string
  published: boolean
  stopped: boolean
  compatible: boolean
}
export interface AiScenarioEvaluation {
  id: string
  versionId: string
  reportHash: string
  mode: string
  modelFingerprint: string
  automaticPassed: boolean
  createdAt: string
  reviewJson?: string
}
export interface AiScenarioDetail {
  scenario: AiScenario
  versions: AiScenarioVersion[]
  evaluations: AiScenarioEvaluation[]
  events: Array<{
    id: string
    versionId: string
    previousVersionId?: string
    evaluationId?: string
    type: number
    actorUserId: string
    reason: string
    createdAt: string
  }>
}
export interface AiScenarioOption {
  id: string
  code: string
  name: string
  versionId: string
  versionNumber: number
}
export interface AiEvaluationReport {
  mode: 'Offline' | 'Live'
  snapshotHash?: string
  results: Array<{
    key: string
    caseHash: string
    status: string
    safetyCritical: boolean
    steps: Array<{
      input: string
      output: string
      evidence?: unknown[]
      tools?: unknown[]
      checks: Array<{ code: string; passed: boolean }>
    }>
  }>
}
export interface AiCaseReview {
  key: string
  caseHash: string
  passed: boolean
  notes: string
}
export interface AiScenarioChange {
  concurrencyToken: string
  reason: string
  confirmInitialBaseline?: boolean
}

const base = '/api/ai/scenarios'
export const listAiScenarios = () =>
  request.get<ApiResult<AiScenario[]>>(base).then((r) => r.data.data)
export const getAiScenarioOptions = () =>
  request.get<ApiResult<AiScenarioOption[]>>(`${base}/options`).then((r) => r.data.data)
export const getAiScenarioDetail = (id: string) =>
  request.get<ApiResult<AiScenarioDetail>>(`${base}/${id}`).then((r) => r.data.data)
export const saveAiScenario = (data: {
  code: string
  name: string
  description: string
  configuration: AiScenarioConfiguration
  concurrencyToken?: string
}) => request.put<ApiResult<AiScenario>>(base, data).then((r) => r.data.data)
export const freezeAiScenario = (id: string, data: AiScenarioChange) =>
  request.post<ApiResult<AiScenarioVersion>>(`${base}/${id}/freeze`, data).then((r) => r.data.data)
export const copyAiScenarioVersion = (id: string, data: AiScenarioChange) =>
  request.post<ApiResult<AiScenario>>(`${base}/versions/${id}/copy`, data).then((r) => r.data.data)
export const exportAiScenarioSnapshot = (id: string) =>
  request.get<ApiResult<unknown>>(`${base}/versions/${id}/snapshot`).then((r) => r.data.data)
export const importAiScenarioEvaluation = (id: string, reportJson: string) =>
  request
    .post<ApiResult<AiScenarioEvaluation>>(`${base}/versions/${id}/evaluations`, { reportJson })
    .then((r) => r.data.data)
export const getAiScenarioEvaluationReport = (id: string) =>
  request
    .get<ApiResult<AiEvaluationReport>>(`${base}/evaluations/${id}/report`)
    .then((r) => r.data.data)
export const reviewAiScenarioEvaluation = (
  id: string,
  data: {
    concurrencyToken: string
    reportHash: string
    goldenCasesApproved: boolean
    cases: AiCaseReview[]
    reason: string
  },
) => request.post(`${base}/evaluations/${id}/review`, data)
export const changeAiScenarioVersion = (
  id: string,
  action: 'publish' | 'rollback' | 'stop',
  data: AiScenarioChange,
) => request.post(`${base}/versions/${id}/${action}`, data)
export const revokeAiScenarioEvaluation = (id: string, data: AiScenarioChange) =>
  request.post(`${base}/evaluations/${id}/revoke`, data)
