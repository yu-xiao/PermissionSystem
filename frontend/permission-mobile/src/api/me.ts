import type { AxiosRequestConfig } from 'axios'
import { request } from '../utils/request'
import type { ApiResult, PagedResult } from './types'
import { unwrapApiResult } from './types'

export interface CurrentUserResponse {
  userId?: string
  tenantId?: string
  username?: string
  isSuperAdmin: boolean
  roles: string[]
  permissionCodes: string[]
}

export interface MyProfileResponse {
  id: string
  userName: string
  nickName: string
  realName: string
  avatar?: string
  email?: string
  phoneNumber?: string
  departmentId?: string
  departmentName?: string
  roles: string[]
  permissions: string[]
  tenantId: string
  tenantName?: string
  lastLoginTime?: string
  createdAt: string
}

export interface UpdateMyProfileRequest {
  nickName?: string
  realName?: string
  avatar?: string
  email?: string
  phoneNumber?: string
}

export interface ChangeMyPasswordRequest {
  oldPassword: string
  newPassword: string
  confirmPassword: string
}

export interface LogoutMySessionRequest {
  refreshToken?: string | null
}

export interface MenuTreeResponse {
  id: string
  tenantId: string
  parentId?: string
  name: string
  path?: string
  component?: string
  redirect?: string
  icon?: string
  sort: number
  visible: boolean
  keepAlive: boolean
  menuType: string
  permissionCode?: string
  concurrencyToken?: string
  children: MenuTreeResponse[]
}

export async function getCurrentUser(config?: AxiosRequestConfig) {
  return unwrapApiResult(await request.get<ApiResult<CurrentUserResponse>>('/api/v1/me', config))
}

export async function getCurrentUserMenus(config?: AxiosRequestConfig) {
  return unwrapApiResult(await request.get<ApiResult<MenuTreeResponse[]>>('/api/v1/me/menus', config))
}

export async function getCurrentUserPermissionCodes(config?: AxiosRequestConfig) {
  return unwrapApiResult(await request.get<ApiResult<string[]>>('/api/v1/me/permissions', config))
}

export async function getMyProfile() {
  return unwrapApiResult(await request.get<ApiResult<MyProfileResponse>>('/api/v1/me/profile'))
}

export async function updateMyProfile(payload: UpdateMyProfileRequest) {
  return unwrapApiResult(await request.put<ApiResult<MyProfileResponse>>('/api/v1/me/profile', payload))
}

export async function changeMyPassword(payload: ChangeMyPasswordRequest) {
  return unwrapApiResult(await request.put<ApiResult<void>>('/api/v1/me/password', payload))
}

export async function logoutSession(refreshToken?: string | null) {
  return unwrapApiResult(await request.post<ApiResult<void>>('/api/v1/me/logout', { refreshToken }))
}

export const logout = logoutSession

export async function logoutAllSessions() {
  return unwrapApiResult(await request.post<ApiResult<void>>('/api/v1/me/logout-all'))
}

export const logoutAll = logoutAllSessions

// This type alias is useful when a page wants to express a paged local cache
// without importing another module solely for the generic.
export type MePagedResult<T> = PagedResult<T>
