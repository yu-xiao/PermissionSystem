import { request } from '../utils/request'
import { sensitiveVerificationHeaders } from './security'
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
  isBuiltin: boolean
  isSuperAdminRole: boolean
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

export interface RoleUsersQuery extends PageQuery {}

export interface RoleUserItem {
  userId: string
  userName: string
  nickName: string
  realName: string
  phoneNumber?: string
  email?: string
  departmentName?: string
  status: string
  checked: boolean
}

export interface RoleUsersResult {
  selectedUserIds: string[]
  users: PagedResult<RoleUserItem>
}

export interface SaveRoleUsersRequest {
  userIds: string[]
}

export interface PermissionItem {
  permissionId: string
  permissionName: string
  permissionCode: string
  permissionType: string
  sort: number
  checked: boolean
}

export interface PermissionMenuRow {
  menuId: string
  parentId?: string
  menuName: string
  menuPath?: string
  menuCode?: string
  icon?: string
  sort: number
  checked: boolean
  indeterminate: boolean
  permissions: PermissionItem[]
  dataScopeEnabled: boolean
  fieldPermissionEnabled: boolean
  dataScopeSummary?: string
  fieldPermissionSummary?: string
}

export interface PermissionModule {
  moduleId: string
  moduleName: string
  moduleCode?: string
  sort: number
  checked: boolean
  indeterminate: boolean
  expanded: boolean
  menus: PermissionMenuRow[]
}

export interface RolePermissionMatrix {
  roleId: string
  roleName: string
  modules: PermissionModule[]
}

export interface RoleMenuDataScopeRequest {
  menuId: string
  scopeType: DataScopeType
  departmentIds: string[]
}

export interface RoleFieldPermissionRequest {
  menuId: string
  fieldCode: string
  visible: boolean
  editable: boolean
  masked: boolean
}

export interface SaveRolePermissionMatrixRequest {
  menuIds: string[]
  permissionIds: string[]
  dataScopes?: RoleMenuDataScopeRequest[]
  fieldPermissions?: RoleFieldPermissionRequest[]
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

export function assignRolePermissions(id: string, permissionIds: string[], stepUpTicket?: string) {
  return request.post<ApiResult<void>>(
    `/api/roles/${id}/permissions`,
    { permissionIds },
    { headers: sensitiveVerificationHeaders(stepUpTicket) },
  )
}

export function getRoleUsers(roleId: string, params: RoleUsersQuery) {
  return request
    .get<ApiResult<RoleUsersResult>>(`/api/roles/${roleId}/users`, { params })
    .then((res) => res.data.data)
}

export function saveRoleUsers(roleId: string, data: SaveRoleUsersRequest, stepUpTicket?: string) {
  return request.put<ApiResult<void>>(`/api/roles/${roleId}/users`, data, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}

export function getRoleDataScope(id: string) {
  return request.get<ApiResult<RoleDataScope>>(`/api/roles/${id}/data-scope`).then((res) => res.data.data)
}

export function setRoleDataScope(id: string, scopeType: DataScopeType, departmentIds: string[]) {
  return request.post<ApiResult<void>>(`/api/roles/${id}/data-scope`, { scopeType, departmentIds })
}

export function getRolePermissionMatrix(roleId: string) {
  return request
    .get<ApiResult<RolePermissionMatrix>>(`/api/roles/${roleId}/permission-matrix`)
    .then((res) => res.data.data)
}

export function saveRolePermissionMatrix(
  roleId: string,
  data: SaveRolePermissionMatrixRequest,
  stepUpTicket?: string,
) {
  return request.put<ApiResult<void>>(`/api/roles/${roleId}/permission-matrix`, data, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}
