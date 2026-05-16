import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface UserQuery extends PageQuery {
  isEnabled?: boolean
}

export interface UserItem {
  id: string
  tenantId: string
  departmentId?: string
  userName: string
  displayName: string
  email?: string
  phoneNumber?: string
  isEnabled: boolean
  createdAt: string
  roleIds: string[]
}

export interface CreateUserRequest {
  tenantId: string
  departmentId?: string
  userName: string
  password: string
  displayName: string
  email?: string
  phoneNumber?: string
  isEnabled: boolean
}

export interface UpdateUserRequest {
  departmentId?: string
  displayName: string
  email?: string
  phoneNumber?: string
  isEnabled: boolean
}

export function getUsers(params: UserQuery) {
  return request.get<ApiResult<PagedResult<UserItem>>>('/api/users', { params }).then((res) => res.data.data)
}

export function createUser(data: CreateUserRequest) {
  return request.post<ApiResult<UserItem>>('/api/users', data).then((res) => res.data.data)
}

export function updateUser(id: string, data: UpdateUserRequest) {
  return request.put<ApiResult<UserItem>>(`/api/users/${id}`, data).then((res) => res.data.data)
}

export function deleteUser(id: string) {
  return request.delete<ApiResult<void>>(`/api/users/${id}`)
}

export function setUserEnabled(id: string, isEnabled: boolean) {
  return request.patch<ApiResult<void>>(`/api/users/${id}/enabled`, { isEnabled })
}

export function resetUserPassword(id: string, newPassword: string) {
  return request.post<ApiResult<void>>(`/api/users/${id}/reset-password`, { newPassword })
}

export function assignUserRoles(id: string, roleIds: string[]) {
  return request.post<ApiResult<void>>(`/api/users/${id}/roles`, { roleIds })
}
