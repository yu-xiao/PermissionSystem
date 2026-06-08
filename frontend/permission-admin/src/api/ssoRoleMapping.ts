import { request } from '../utils/request'
import type { ApiResult } from './types'

export interface SsoRoleMappingItem {
  id?: string
  tenantId?: string
  providerId?: string
  externalRole: string
  localRoleId: string
  localRoleCode?: string
  localRoleName?: string
}

export interface SaveSsoRoleMappingRequest {
  externalRole: string
  localRoleId: string
}

export function getSsoRoleMappings(providerId: string) {
  return request
    .get<ApiResult<SsoRoleMappingItem[]>>(`/api/sso/providers/${providerId}/role-mappings`)
    .then((res) => res.data.data)
}

export function saveSsoRoleMappings(providerId: string, data: SaveSsoRoleMappingRequest[]) {
  return request
    .put<ApiResult<SsoRoleMappingItem[]>>(`/api/sso/providers/${providerId}/role-mappings`, data)
    .then((res) => res.data.data)
}
