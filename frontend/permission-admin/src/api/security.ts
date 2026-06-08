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
}

export type UpdateSecurityPolicyRequest = Omit<SecurityPolicy, 'id' | 'tenantId'>

export interface SendSensitiveVerificationRequest {
  operationCode: string
}

export interface SendSensitiveVerificationResponse {
  operationCode: string
  verifyCode?: string
  expiresAt: string
  deliveryMessage: string
}

export interface VerifySensitiveOperationRequest {
  operationCode: string
  verifyCode: string
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
}

export interface SaveIpAccessRuleRequest {
  ruleType: string
  ipPattern: string
  description?: string
  isEnabled: boolean
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

export function sensitiveVerificationHeaders(verificationCode?: string) {
  return verificationCode ? { 'X-Sensitive-Verification-Code': verificationCode } : undefined
}

export function getSecurityPolicy() {
  return request.get<ApiResult<SecurityPolicy>>('/api/security/policy').then((res) => res.data.data)
}

export function updateSecurityPolicy(data: UpdateSecurityPolicyRequest, verificationCode?: string) {
  return request
    .put<ApiResult<SecurityPolicy>>('/api/security/policy', data, {
      headers: sensitiveVerificationHeaders(verificationCode),
    })
    .then((res) => res.data.data)
}

export function sendSensitiveVerification(data: SendSensitiveVerificationRequest) {
  return request
    .post<ApiResult<SendSensitiveVerificationResponse>>('/api/security/verification/send', data)
    .then((res) => res.data.data)
}

export function verifySensitiveOperation(data: VerifySensitiveOperationRequest) {
  return request.post<ApiResult<void>>('/api/security/verification/verify', data)
}

export function getIpAccessRules(params: IpAccessRuleQuery) {
  return request
    .get<ApiResult<PagedResult<IpAccessRuleItem>>>('/api/security/ip-rules', { params })
    .then((res) => res.data.data)
}

export function createIpAccessRule(data: SaveIpAccessRuleRequest, verificationCode?: string) {
  return request
    .post<ApiResult<IpAccessRuleItem>>('/api/security/ip-rules', data, {
      headers: sensitiveVerificationHeaders(verificationCode),
    })
    .then((res) => res.data.data)
}

export function updateIpAccessRule(id: string, data: SaveIpAccessRuleRequest, verificationCode?: string) {
  return request
    .put<ApiResult<IpAccessRuleItem>>(`/api/security/ip-rules/${id}`, data, {
      headers: sensitiveVerificationHeaders(verificationCode),
    })
    .then((res) => res.data.data)
}

export function deleteIpAccessRule(id: string, verificationCode?: string) {
  return request.delete<ApiResult<void>>(`/api/security/ip-rules/${id}`, {
    headers: sensitiveVerificationHeaders(verificationCode),
  })
}

export function getLoginFailures(params: LoginFailureQuery) {
  return request
    .get<ApiResult<PagedResult<LoginFailureRecordItem>>>('/api/security/login-failures', { params })
    .then((res) => res.data.data)
}
