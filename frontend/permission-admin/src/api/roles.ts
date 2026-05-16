import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface RoleQuery extends PageQuery {
  isEnabled?: boolean
}

export interface RoleItem {
  id: string
  tenantId: string
  code: string
  name: string
  description?: string
  isEnabled: boolean
  sort: number
  createdAt: string
}

export interface CreateRoleRequest {
  tenantId: string
  code: string
  name: string
  description?: string
  isEnabled: boolean
  sort: number
}

export interface UpdateRoleRequest {
  name: string
  description?: string
  isEnabled: boolean
  sort: number
}

export function getRoles(params: RoleQuery) {
  return request.get<ApiResult<PagedResult<RoleItem>>>('/api/roles', { params }).then((res) => res.data.data)
}

export function createRole(data: CreateRoleRequest) {
  return request.post<ApiResult<RoleItem>>('/api/roles', data).then((res) => res.data.data)
}

export function updateRole(id: string, data: UpdateRoleRequest) {
  return request.put<ApiResult<RoleItem>>(`/api/roles/${id}`, data).then((res) => res.data.data)
}

export function deleteRole(id: string) {
  return request.delete<ApiResult<void>>(`/api/roles/${id}`)
}

export function assignRoleMenus(id: string, menuIds: string[]) {
  return request.post<ApiResult<void>>(`/api/roles/${id}/menus`, { menuIds })
}

export function assignRolePermissions(id: string, permissionIds: string[]) {
  return request.post<ApiResult<void>>(`/api/roles/${id}/permissions`, { permissionIds })
}
