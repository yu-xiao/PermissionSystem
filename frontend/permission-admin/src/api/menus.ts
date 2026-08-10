import { request } from '../utils/request'
import type { ApiResult } from './types'

export interface MenuItem {
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
  concurrencyToken: string
  children: MenuItem[]
}

export interface SaveMenuRequest {
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
}

export function getMenuTree(tenantId?: string) {
  return request.get<ApiResult<MenuItem[]>>('/api/menus/tree', { params: { tenantId } }).then((res) => res.data.data)
}

export function createMenu(data: SaveMenuRequest) {
  return request.post<ApiResult<MenuItem>>('/api/menus', data).then((res) => res.data.data)
}

export function updateMenu(id: string, data: Omit<SaveMenuRequest, 'tenantId'>) {
  return request.put<ApiResult<MenuItem>>(`/api/menus/${id}`, data).then((res) => res.data.data)
}

export function deleteMenu(id: string) {
  return request.delete<ApiResult<void>>(`/api/menus/${id}`)
}
