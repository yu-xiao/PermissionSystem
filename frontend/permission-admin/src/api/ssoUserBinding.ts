import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface SsoUserBindingQuery extends PageQuery {
  providerId?: string
}

export interface SsoUserBindingItem {
  id: string
  tenantId: string
  providerId: string
  providerCode: string
  providerName?: string
  externalUserId: string
  externalUserName?: string
  externalEmail?: string
  externalPhone?: string
  localUserId: string
  localUserName?: string
  localDisplayName?: string
  lastLoginAt?: string
  createdAt: string
}

export interface SsoUserBindingDetail extends SsoUserBindingItem {
  claimsJson?: string
}

export function getSsoUserBindings(params: SsoUserBindingQuery) {
  return request
    .get<ApiResult<PagedResult<SsoUserBindingItem>>>('/api/sso/user-bindings', { params })
    .then((res) => res.data.data)
}

export function getSsoUserBinding(id: string) {
  return request
    .get<ApiResult<SsoUserBindingDetail>>(`/api/sso/user-bindings/${id}`)
    .then((res) => res.data.data)
}

export function unbindSsoUser(id: string) {
  return request.post<ApiResult<void>>(`/api/sso/user-bindings/${id}/unbind`)
}

export function deleteSsoUserBinding(id: string) {
  return request.delete<ApiResult<void>>(`/api/sso/user-bindings/${id}`)
}
