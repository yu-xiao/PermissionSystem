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
