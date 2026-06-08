import { request } from '../utils/request'
import type { ApiResult } from './types'

export interface SsoDepartmentMappingItem {
  id?: string
  tenantId?: string
  providerId?: string
  externalDepartment: string
  localDepartmentId: string
  localDepartmentCode?: string
  localDepartmentName?: string
}

export interface SaveSsoDepartmentMappingRequest {
  externalDepartment: string
  localDepartmentId: string
}

export function getSsoDepartmentMappings(providerId: string) {
  return request
    .get<ApiResult<SsoDepartmentMappingItem[]>>(`/api/sso/providers/${providerId}/department-mappings`)
    .then((res) => res.data.data)
}

export function saveSsoDepartmentMappings(providerId: string, data: SaveSsoDepartmentMappingRequest[]) {
  return request
    .put<ApiResult<SsoDepartmentMappingItem[]>>(`/api/sso/providers/${providerId}/department-mappings`, data)
    .then((res) => res.data.data)
}
