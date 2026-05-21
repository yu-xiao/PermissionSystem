import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export type SystemConfigStatus = 'Enabled' | 'Disabled'
export type SystemConfigType = 'String' | 'Number' | 'Boolean' | 'Json'

export interface SystemConfigQuery extends PageQuery {
  groupCode?: string
  configType?: string
  status?: SystemConfigStatus
  isEncrypted?: boolean
  isSystem?: boolean
}

export interface SystemConfigItem {
  id: string
  tenantId: string
  configKey: string
  configValue: string
  configType: string
  groupCode: string
  name: string
  description?: string
  isEncrypted: boolean
  isSystem: boolean
  status: SystemConfigStatus
  sort: number
  createdAt: string
}

export interface CreateSystemConfigRequest {
  tenantId: string
  configKey: string
  configValue: string
  configType: string
  groupCode: string
  name: string
  description?: string
  isEncrypted: boolean
  isSystem: boolean
  status: SystemConfigStatus
  sort: number
}

export interface UpdateSystemConfigRequest {
  configValue?: string
  configType: string
  groupCode: string
  name: string
  description?: string
  isEncrypted: boolean
  isSystem: boolean
  status: SystemConfigStatus
  sort: number
}

export interface SystemConfigValue {
  configKey: string
  configValue: string
  configType: string
  isEncrypted: boolean
}

export function getSystemConfigs(params: SystemConfigQuery) {
  return request
    .get<ApiResult<PagedResult<SystemConfigItem>>>('/api/system-configs', { params })
    .then((res) => res.data.data)
}

export function createSystemConfig(data: CreateSystemConfigRequest) {
  return request.post<ApiResult<SystemConfigItem>>('/api/system-configs', data).then((res) => res.data.data)
}

export function updateSystemConfig(id: string, data: UpdateSystemConfigRequest) {
  return request.put<ApiResult<SystemConfigItem>>(`/api/system-configs/${id}`, data).then((res) => res.data.data)
}

export function deleteSystemConfig(id: string) {
  return request.delete<ApiResult<void>>(`/api/system-configs/${id}`)
}

export function getSystemConfigValue(configKey: string, revealSensitive = false) {
  return request
    .get<ApiResult<SystemConfigValue>>(`/api/system-configs/values/${configKey}`, {
      params: { revealSensitive },
    })
    .then((res) => res.data.data)
}

export function getSystemConfigsByGroup(groupCode: string) {
  return request
    .get<ApiResult<SystemConfigItem[]>>(`/api/system-configs/groups/${groupCode}`)
    .then((res) => res.data.data)
}
