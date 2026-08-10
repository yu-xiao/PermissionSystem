import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export type NumberRuleResetCycle = 'None' | 'Daily' | 'Monthly' | 'Yearly'

export interface NumberRuleQuery extends PageQuery {
  businessType?: string
  isEnabled?: boolean
}

export interface NumberRuleItem {
  id: string
  tenantId: string
  ruleCode: string
  ruleName: string
  businessType: string
  prefix: string
  dateFormat: string
  sequenceLength: number
  resetCycle: NumberRuleResetCycle
  separator: string
  isEnabled: boolean
  remark?: string
  createdAt: string
  concurrencyToken: string
}

export interface CreateOrUpdateNumberRuleRequest {
  ruleCode: string
  ruleName: string
  businessType: string
  prefix: string
  dateFormat: string
  sequenceLength: number
  resetCycle: NumberRuleResetCycle
  separator: string
  isEnabled: boolean
  remark?: string
  concurrencyToken?: string
}

export interface NumberRulePreview {
  number: string
  pattern: string
}

export interface NumberGenerateResult {
  ruleCode: string
  number: string
}

const baseUrl = '/api/system/number-rules'

export function getNumberRules(params: NumberRuleQuery) {
  return request
    .get<ApiResult<PagedResult<NumberRuleItem>>>(baseUrl, { params })
    .then((res) => res.data.data)
}

export function getNumberRule(id: string) {
  return request.get<ApiResult<NumberRuleItem>>(`${baseUrl}/${id}`).then((res) => res.data.data)
}

export function createNumberRule(data: CreateOrUpdateNumberRuleRequest) {
  return request.post<ApiResult<NumberRuleItem>>(baseUrl, data).then((res) => res.data.data)
}

export function updateNumberRule(id: string, data: CreateOrUpdateNumberRuleRequest) {
  return request.put<ApiResult<NumberRuleItem>>(`${baseUrl}/${id}`, data).then((res) => res.data.data)
}

export function deleteNumberRule(id: string) {
  return request.delete<ApiResult<void>>(`${baseUrl}/${id}`)
}

export function enableNumberRule(id: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/${id}/enable`)
}

export function disableNumberRule(id: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/${id}/disable`)
}

export function previewNumberRule(data: CreateOrUpdateNumberRuleRequest) {
  return request.post<ApiResult<NumberRulePreview>>(`${baseUrl}/preview`, data).then((res) => res.data.data)
}

export function generateNumber(ruleCode: string) {
  return request.post<ApiResult<NumberGenerateResult>>(`${baseUrl}/${ruleCode}/generate`).then((res) => res.data.data)
}

export function resetNumberSequence(ruleCode: string) {
  return request.post<ApiResult<void>>(`${baseUrl}/${ruleCode}/reset-sequence`)
}
