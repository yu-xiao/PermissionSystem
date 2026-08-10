import { request } from '../utils/request'
import { sensitiveVerificationHeaders } from './security'
import type { ApiResult, PagedResult, PageQuery } from './types'
import type { DataScopeType } from './roles'

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
  isBuiltin: boolean
  isSuperAdmin: boolean
  isCurrentUser: boolean
  createdAt: string
  roleIds: string[]
  roleCodes: string[]
}

export interface ImportError {
  rowNumber: number
  columnName: string
  message: string
  rawValue?: string
}

export interface UserImportRow {
  userName: string
  displayName: string
  password: string
  email?: string
  phoneNumber?: string
  isEnabled: boolean
}

export interface ImportResult<T> {
  totalRows: number
  successRows: number
  failedRows: number
  items: T[]
  errors: ImportError[]
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

export interface UserDataScope {
  userId: string
  hasOverride: boolean
  scopeType: DataScopeType
  departmentIds: string[]
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

export function deleteUser(id: string, stepUpTicket?: string) {
  return request.delete<ApiResult<void>>(`/api/users/${id}`, {
    headers: sensitiveVerificationHeaders(stepUpTicket),
  })
}

export function setUserEnabled(id: string, isEnabled: boolean) {
  return request.patch<ApiResult<void>>(`/api/users/${id}/enabled`, { isEnabled })
}

export function resetUserPassword(id: string, newPassword: string, stepUpTicket?: string) {
  return request.post<ApiResult<void>>(
    `/api/users/${id}/reset-password`,
    { newPassword },
    { headers: sensitiveVerificationHeaders(stepUpTicket) },
  )
}

export function assignUserRoles(id: string, roleIds: string[], stepUpTicket?: string) {
  return request.post<ApiResult<void>>(
    `/api/users/${id}/roles`,
    { roleIds },
    { headers: sensitiveVerificationHeaders(stepUpTicket) },
  )
}

export function getUserDataScope(id: string) {
  return request
    .get<ApiResult<UserDataScope>>(`/api/users/${id}/data-scope`)
    .then((res) => res.data.data)
}

export function setUserDataScope(id: string, scopeType: DataScopeType, departmentIds: string[]) {
  return request.put<ApiResult<void>>(`/api/users/${id}/data-scope`, { scopeType, departmentIds })
}

export function clearUserDataScope(id: string) {
  return request.delete<ApiResult<void>>(`/api/users/${id}/data-scope`)
}

export function exportUsers(params: UserQuery) {
  return request.get<Blob>('/api/users/export', {
    params,
    responseType: 'blob',
    timeout: 60000,
  })
}

export function downloadUserImportTemplate() {
  return request.get<Blob>('/api/users/import-template', {
    responseType: 'blob',
    timeout: 60000,
  })
}

export function importUsers(file: File) {
  const form = new FormData()
  form.append('file', file)

  return request
    .post<ApiResult<ImportResult<UserImportRow>>>('/api/users/import', form, {
      headers: { 'Content-Type': 'multipart/form-data' },
      timeout: 60000,
    })
    .then((res) => res.data.data)
}
