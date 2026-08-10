import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface TenantQuery extends PageQuery {
  isEnabled?: boolean
  status?: TenantStatus
}

export const TenantStatus = {
  Initializing: 0,
  Active: 1,
  Disabled: 2,
  Failed: 3,
  Archived: 4,
} as const

export type TenantStatus = (typeof TenantStatus)[keyof typeof TenantStatus]

export interface TenantItem {
  id: string
  tenantId: string
  code: string
  name: string
  description?: string
  isEnabled: boolean
  status: TenantStatus
  initializationStep?: string
  initializationProgress: number
  initializationAttempts: number
  initializationError?: string
  initializationStartedAt?: string
  initializedAt?: string
  statusChangedAt: string
  createdAt: string
  concurrencyToken: string
}

export interface CreateTenantRequest {
  code: string
  name: string
  description?: string
  administratorUserName: string
  administratorDisplayName: string
  administratorPassword: string
}

export interface UpdateTenantRequest {
  name: string
  description?: string
  concurrencyToken?: string
}

export function getTenants(params: TenantQuery) {
  return request.get<ApiResult<PagedResult<TenantItem>>>('/api/tenants', { params }).then((res) => res.data.data)
}

export function createTenant(data: CreateTenantRequest) {
  return request.post<ApiResult<TenantItem>>('/api/tenants', data).then((res) => res.data.data)
}

export function updateTenant(id: string, data: UpdateTenantRequest) {
  return request.put<ApiResult<TenantItem>>(`/api/tenants/${id}`, data).then((res) => res.data.data)
}

export function setTenantEnabled(id: string, isEnabled: boolean) {
  return request.patch<ApiResult<void>>(`/api/tenants/${id}/enabled`, { isEnabled })
}

export function retryTenantInitialization(id: string) {
  return request.post<ApiResult<void>>(`/api/tenants/${id}/initialization/retry`)
}

export function disableTenant(id: string) {
  return request.post<ApiResult<void>>(`/api/tenants/${id}/disable`)
}

export function restoreTenant(id: string) {
  return request.post<ApiResult<void>>(`/api/tenants/${id}/restore`)
}
