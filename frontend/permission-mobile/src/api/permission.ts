import type { AxiosRequestConfig } from 'axios'
import { getCurrentUserMenus, getCurrentUserPermissionCodes, type MenuTreeResponse } from './me'

export interface PermissionSnapshot {
  menus: MenuTreeResponse[]
  permissionCodes: string[]
}

export async function getMyPermissions(config?: AxiosRequestConfig): Promise<string[]> {
  return getCurrentUserPermissionCodes(config)
}

export async function getMyMenus(config?: AxiosRequestConfig) {
  return getCurrentUserMenus(config)
}

export async function getPermissionSnapshot(config?: AxiosRequestConfig): Promise<PermissionSnapshot> {
  const [menus, permissionCodes] = await Promise.all([
    getMyMenus(config),
    getMyPermissions(config),
  ])
  return { menus, permissionCodes }
}

export const getCurrentUserPermissions = getMyPermissions

