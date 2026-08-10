import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface PermissionQuery extends PageQuery {
  group?: string
}

export interface PermissionItem {
  id: string
  tenantId: string
  code: string
  name: string
  group: string
  description?: string
  resource?: string
  action?: string
  createdAt: string
  concurrencyToken: string
}

export interface CreatePermissionRequest {
  tenantId: string
  code: string
  name: string
  group: string
  description?: string
  resource?: string
  action?: string
}

export interface UpdatePermissionRequest {
  name: string
  group: string
  description?: string
  resource?: string
  action?: string
  concurrencyToken?: string
}

export function getPermissions(params: PermissionQuery) {
  return request
    .get<ApiResult<PagedResult<PermissionItem>>>('/api/permissions', { params })
    .then((res) => res.data.data)
}

export function createPermission(data: CreatePermissionRequest) {
  return request.post<ApiResult<PermissionItem>>('/api/permissions', data).then((res) => res.data.data)
}

export function updatePermission(id: string, data: UpdatePermissionRequest) {
  return request.put<ApiResult<PermissionItem>>(`/api/permissions/${id}`, data).then((res) => res.data.data)
}

export function deletePermission(id: string) {
  return request.delete<ApiResult<void>>(`/api/permissions/${id}`)
}
