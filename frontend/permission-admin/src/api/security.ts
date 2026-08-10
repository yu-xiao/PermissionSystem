import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface SecurityPolicy {
  id: string
  tenantId: string
  passwordMinLength: number
  requireDigit: boolean
  requireUppercase: boolean
  requireLowercase: boolean
  requireSpecialChar: boolean
  passwordExpireDays: number
  loginFailureLockThreshold: number
  loginFailureLockMinutes: number
  enableMfa: boolean
  enableSensitiveOperationVerify: boolean
  enableIpWhitelist: boolean
  enableIpBlacklist: boolean
  concurrencyToken: string
}

export type UpdateSecurityPolicyRequest = Omit<SecurityPolicy, 'id' | 'tenantId' | 'concurrencyToken'> & {
  concurrencyToken?: string
}

export interface SendSensitiveVerificationRequest {
  operationCode: string
}

export interface SendSensitiveVerificationResponse {
  challengeId: string
  operationCode: string
  verificationMethod: string
  expiresAt: string
}

export interface VerifySensitiveOperationRequest {
  challengeId: string
  password: string
}

export interface VerifySensitiveOperationResponse {
  stepUpTicket: string
  expiresAt: string
}

export interface IpAccessRuleQuery extends PageQuery {
  ruleType?: string
  keyword?: string
  isEnabled?: boolean
}

export interface IpAccessRuleItem {
  id: string
  tenantId: string
  ruleType: string
  ipPattern: string
  description?: string
  isEnabled: boolean
  createdAt: string
  concurrencyToken: string
}

export interface SaveIpAccessRuleRequest {
  ruleType: string
  ipPattern: string
  description?: string
  isEnabled: boolean
  concurrencyToken?: string
}

export interface LoginFailureQuery extends PageQuery {
  keyword?: string
}

export interface LoginFailureRecordItem {
  id: string
  tenantId: string
  userName: string
  ipAddress?: string
  failureCount: number
  lockedUntil?: string
  lastFailureAt: string
}

export function sensitiveVerificationHeaders(stepUpTicket?: string) {
  return stepUpTicket ? { 'X-Step-Up-Ticket': stepUpTicket } : undefined
}

export function getSecurityPolicy() {
  return request.get<ApiResult<SecurityPolicy>>('/api/security/policy').then((res) => res.data.data)
}

export function updateSecurityPolicy(data: UpdateSecurityPolicyRequest, stepUpTicket?: string) {
  return request
    .put<ApiResult<SecurityPolicy>>('/api/security/policy', data, {
      headers: sensitiveVerificationHeaders(stepUpTicket),
    })
    .then((res) => res.data.data)
}

export function sendSensitiveVerification(data: SendSensitiveVerificationRequest) {
  return request
    .post<ApiResult<SendSensitiveVerificationResponse>>('/api/security/verification/send', data)
    .then((res) => res.data.data)
}

export function verifySensitiveOperation(data: VerifySensitiveOperationRequest) {
  return request
    .post<ApiResult<VerifySensitiveOperationResponse>>('/api/security/verification/verify', data)
    .then((res) => res.data.data)
}

export function getIpAccessRules(params: IpAccessRuleQuery) {
  return request
    .get<ApiResult<PagedResult<IpAccessRuleItem>>>('/api/security/ip-rules', { params })
    .then((res) => res.data.data)
}

export function createIpAccessRule(data: SaveIpAccessRuleRequest, stepUpTicket?: string) {
  return request
    .post<ApiResult<IpAccessRuleItem>>('/api/security/ip-rules', data, {
      headers: sensitiveVerificationHeaders(stepUpTicket),
    })
    .then((res) => res.data.data)
}

export function updateIpAccessRule(id: string, data: SaveIpAccessRuleRequest, stepUpTicket?: string) {
  return request
    .put<ApiResult<IpAccessRuleItem>>(`/api/security/ip-rules/${id}`, data, {
      headers: sensitiveVerificationHeaders(stepUpTicket),
    })
    .then((res) => res.data.data)
}

export function deleteIpAccessRule(id: string, stepUpTicket?: string) {
  return request.delete<ApiResult<void>>(`/api/security/ip-rules/${id}`, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}

export function getLoginFailures(params: LoginFailureQuery) {
  return request
    .get<ApiResult<PagedResult<LoginFailureRecordItem>>>('/api/security/login-failures', { params })
    .then((res) => res.data.data)
}
