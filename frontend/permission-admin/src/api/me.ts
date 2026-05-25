import { request } from '../utils/request'

export interface ApiResult<T> {
  succeeded: boolean
  code: number
  message: string
  data: T
  traceId?: string
}

export interface CurrentUserResponse {
  userId?: string
  tenantId?: string
  username?: string
  isSuperAdmin: boolean
  roles: string[]
  permissionCodes: string[]
}

export interface MenuTreeResponse {
  id: string
  parentId?: string
  name: string
  path?: string
  component?: string
  icon?: string
  sort: number
  visible: boolean
  permissionCode?: string
  children: MenuTreeResponse[]
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

export async function getCurrentUser() {
  const { data } = await request.get<ApiResult<CurrentUserResponse>>('/api/me')
  return data.data
}

export async function getCurrentUserMenus() {
  const { data } = await request.get<ApiResult<MenuTreeResponse[]>>('/api/me/menus')
  return data.data
}

export async function getCurrentUserPermissionCodes() {
  const { data } = await request.get<ApiResult<string[]>>('/api/me/permissions')
  return data.data
}

export async function getMyProfile() {
  const { data } = await request.get<ApiResult<MyProfileResponse>>('/api/me/profile')
  return data.data
}

export async function updateMyProfile(payload: UpdateMyProfileRequest) {
  const { data } = await request.put<ApiResult<MyProfileResponse>>('/api/me/profile', payload)
  return data.data
}

export async function changeMyPassword(payload: ChangeMyPasswordRequest) {
  const { data } = await request.put<ApiResult<void>>('/api/me/password', payload)
  return data
}

export async function logout(refreshToken?: string | null) {
  const { data } = await request.post<ApiResult<void>>('/api/me/logout', { refreshToken })
  return data
}

export async function logoutAll() {
  const { data } = await request.post<ApiResult<void>>('/api/me/logout-all')
  return data
}
