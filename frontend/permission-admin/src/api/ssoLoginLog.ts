import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'
import type { SsoProviderType } from './ssoProvider'

export const SsoLoginResult = {
  Success: 0,
  Failed: 1,
  UserDisabled: 2,
  TenantDisabled: 3,
  BindingFailed: 4,
  AutoCreateFailed: 5,
} as const

export type SsoLoginResult = (typeof SsoLoginResult)[keyof typeof SsoLoginResult]

export interface SsoLoginLogQuery extends PageQuery {
  providerCode?: string
  providerType?: SsoProviderType
  loginResult?: SsoLoginResult
  startAt?: string
  endAt?: string
}

export interface SsoLoginLogItem {
  id: string
  tenantId: string
  providerCode: string
  providerName: string
  providerType: SsoProviderType
  externalUserId?: string
  externalUserName?: string
  localUserId?: string
  localUserName?: string
  loginResult: SsoLoginResult
  failureReason?: string
  ipAddress?: string
  userAgent?: string
  traceId?: string
  createdAt: string
}

export function getSsoLoginLogs(params: SsoLoginLogQuery) {
  return request
    .get<ApiResult<PagedResult<SsoLoginLogItem>>>('/api/sso/login-logs', { params })
    .then((res) => res.data.data)
}

export function getSsoLoginLog(id: string) {
  return request.get<ApiResult<SsoLoginLogItem>>(`/api/sso/login-logs/${id}`).then((res) => res.data.data)
}
