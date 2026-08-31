import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'

export const TenantStatus = {
  Initializing: 0,
  Active: 1,
  Disabled: 2,
  Failed: 3,
  Archived: 4,
} as const

export type TenantStatus = (typeof TenantStatus)[keyof typeof TenantStatus]

export interface TenantQuery extends PageQuery {
  isEnabled?: boolean
  status?: TenantStatus
}

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
  concurrencyToken?: string
}

export interface CreateTenantRequest {
  code: string
  name: string
  description?: string
  administratorUserName?: string
  administratorDisplayName?: string
  administratorPassword?: string
}

export interface UpdateTenantRequest {
  name: string
  description?: string
  concurrencyToken?: string
}

export async function getTenants(params: TenantQuery = {}) {
  return unwrapApiResult(await request.get<ApiResult<PagedResult<TenantItem>>>('/api/v1/tenants', { params }))
}

export async function createTenant(payload: CreateTenantRequest) {
  return unwrapApiResult(await request.post<ApiResult<TenantItem>>('/api/v1/tenants', payload))
}

export async function updateTenant(id: string, payload: UpdateTenantRequest) {
  return unwrapApiResult(await request.put<ApiResult<TenantItem>>(`/api/v1/tenants/${id}`, payload))
}

export async function setTenantEnabled(id: string, isEnabled: boolean) {
  return unwrapApiResult(await request.patch<ApiResult<void>>(`/api/v1/tenants/${id}/enabled`, { isEnabled }))
}

export async function retryTenantInitialization(id: string) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/tenants/${id}/initialization/retry`))
}

export async function disableTenant(id: string) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/tenants/${id}/disable`))
}

export async function restoreTenant(id: string) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/tenants/${id}/restore`))
}

