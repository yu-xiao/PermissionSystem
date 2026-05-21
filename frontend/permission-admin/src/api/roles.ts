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

export const DataScopeType = {
  All: 0,
  CurrentUser: 1,
  CurrentDepartment: 2,
  CurrentDepartmentAndChildren: 3,
  CustomDepartments: 4,
} as const

export type DataScopeType = (typeof DataScopeType)[keyof typeof DataScopeType]

export interface RoleDataScope {
  roleId: string
  scopeType: DataScopeType
  departmentIds: string[]
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

export function getRoleDataScope(id: string) {
  return request.get<ApiResult<RoleDataScope>>(`/api/roles/${id}/data-scope`).then((res) => res.data.data)
}

export function setRoleDataScope(id: string, scopeType: DataScopeType, departmentIds: string[]) {
  return request.post<ApiResult<void>>(`/api/roles/${id}/data-scope`, { scopeType, departmentIds })
}
