import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface TenantQuery extends PageQuery {
  isEnabled?: boolean
}

export interface TenantItem {
  id: string
  tenantId: string
  code: string
  name: string
  description?: string
  isEnabled: boolean
  createdAt: string
}

export interface CreateTenantRequest {
  code: string
  name: string
  description?: string
  isEnabled: boolean
}

export interface UpdateTenantRequest {
  name: string
  description?: string
  isEnabled: boolean
}

export function getTenants(params: TenantQuery) {
  return request.get<ApiResult<PagedResult<TenantItem>>>('/api/tenants', { params }).then((res) => res.data.data)
}

export function createTenant(data: CreateTenantRequest) {
  return request.post<ApiResult<TenantItem>>('/api/tenants', data).then((res) => res.data.data)
}

export function updateTenant(id: string, data: UpdateTenantRequest) {
  return request.put<ApiResult<TenantItem>>(`/api/tenants/${id}`, data).then((res) => res.data.data)
}

export function setTenantEnabled(id: string, isEnabled: boolean) {
  return request.patch<ApiResult<void>>(`/api/tenants/${id}/enabled`, { isEnabled })
}
