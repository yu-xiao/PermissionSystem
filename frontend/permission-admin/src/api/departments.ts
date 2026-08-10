import { request } from '../utils/request'
import type { ApiResult } from './types'

export interface DepartmentItem {
  id: string
  tenantId: string
  parentId?: string
  code: string
  name: string
  treePath: string
  sort: number
  status: string
  isEnabled: boolean
  concurrencyToken: string
  children: DepartmentItem[]
}

export interface SaveDepartmentRequest {
  tenantId: string
  parentId?: string
  code: string
  name: string
  sort: number
  status: string
  concurrencyToken?: string
}

export function getDepartmentTree(tenantId?: string) {
  return request
    .get<ApiResult<DepartmentItem[]>>('/api/departments/tree', { params: { tenantId } })
    .then((res) => res.data.data)
}

export function createDepartment(data: SaveDepartmentRequest) {
  return request.post<ApiResult<DepartmentItem>>('/api/departments', data).then((res) => res.data.data)
}

export function updateDepartment(id: string, data: Omit<SaveDepartmentRequest, 'tenantId' | 'code'>) {
  return request.put<ApiResult<DepartmentItem>>(`/api/departments/${id}`, data).then((res) => res.data.data)
}

export function deleteDepartment(id: string) {
  return request.delete<ApiResult<void>>(`/api/departments/${id}`)
}

export function setDepartmentEnabled(id: string, isEnabled: boolean) {
  return request.patch<ApiResult<void>>(`/api/departments/${id}/enabled`, { isEnabled })
}
