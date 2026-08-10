import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export const SsoProviderType = {
  Oidc: 0,
  Saml: 1,
  OAuth2: 2,
} as const

export type SsoProviderType = (typeof SsoProviderType)[keyof typeof SsoProviderType]

export interface SsoProviderQuery extends PageQuery {
  providerType?: SsoProviderType
  enabled?: boolean
}

export interface SsoProviderListItem {
  id: string
  tenantId: string
  providerCode: string
  providerName: string
  providerType: SsoProviderType
  enabled: boolean
  authority?: string
  metadataAddress?: string
  scopes?: string
  callbackPath: string
  usePkce: boolean
  autoCreateUser: boolean
  autoBindUser: boolean
  allowLocalLoginFallback: boolean
  createdAt: string
  concurrencyToken: string
}

export interface SsoProviderDetail extends SsoProviderListItem {
  clientId?: string
  clientSecret: string
  hasClientSecret: boolean
  responseType: string
  getClaimsFromUserInfoEndpoint: boolean
  userIdClaim: string
  userNameClaim: string
  emailClaim: string
  phoneClaim: string
  displayNameClaim: string
  roleClaim: string
  departmentClaim: string
  defaultRoleIds?: string
  logoutRedirectUri?: string
  remark?: string
  updatedAt?: string
}

export interface SaveSsoProviderRequest {
  tenantId?: string
  providerCode?: string
  providerName: string
  providerType: SsoProviderType
  enabled?: boolean
  authority?: string
  metadataAddress?: string
  clientId?: string
  clientSecret?: string
  scopes?: string
  callbackPath?: string
  responseType?: string
  usePkce: boolean
  getClaimsFromUserInfoEndpoint: boolean
  userIdClaim?: string
  userNameClaim?: string
  emailClaim?: string
  phoneClaim?: string
  displayNameClaim?: string
  roleClaim?: string
  departmentClaim?: string
  autoCreateUser: boolean
  autoBindUser: boolean
  defaultRoleIds?: string
  allowLocalLoginFallback: boolean
  logoutRedirectUri?: string
  remark?: string
  concurrencyToken?: string
}

export interface TestSsoProviderRequest {
  authority?: string
  metadataAddress?: string
  clientId?: string
  clientSecret?: string
}

export interface SsoProviderTestResult {
  succeeded: boolean
  message: string
  metadataAddress?: string
}

export function getSsoProviders(params: SsoProviderQuery) {
  return request
    .get<ApiResult<PagedResult<SsoProviderListItem>>>('/api/sso/providers', { params })
    .then((res) => res.data.data)
}

export function getEnabledSsoProviders() {
  return request
    .get<ApiResult<SsoProviderListItem[]>>('/api/sso/providers/enabled', {
      headers: { 'X-Skip-Progress': 'true' },
    })
    .then((res) => res.data.data)
}

export function getSsoProvider(id: string) {
  return request.get<ApiResult<SsoProviderDetail>>(`/api/sso/providers/${id}`).then((res) => res.data.data)
}

export function createSsoProvider(data: SaveSsoProviderRequest) {
  return request.post<ApiResult<SsoProviderDetail>>('/api/sso/providers', data).then((res) => res.data.data)
}

export function updateSsoProvider(id: string, data: SaveSsoProviderRequest) {
  return request.put<ApiResult<SsoProviderDetail>>(`/api/sso/providers/${id}`, data).then((res) => res.data.data)
}

export function deleteSsoProvider(id: string) {
  return request.delete<ApiResult<void>>(`/api/sso/providers/${id}`)
}

export function enableSsoProvider(id: string) {
  return request.post<ApiResult<void>>(`/api/sso/providers/${id}/enable`)
}

export function disableSsoProvider(id: string) {
  return request.post<ApiResult<void>>(`/api/sso/providers/${id}/disable`)
}

export function testSsoProvider(id: string, data?: TestSsoProviderRequest) {
  return request
    .post<ApiResult<SsoProviderTestResult>>(`/api/sso/providers/${id}/test`, data ?? {})
    .then((res) => res.data.data)
}
