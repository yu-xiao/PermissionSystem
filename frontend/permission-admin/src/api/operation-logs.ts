import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface OperationLogQuery extends PageQuery {
  userName?: string
  module?: string
  action?: string
  requestMethod?: string
  statusCode?: number
  traceId?: string
  startTime?: string
  endTime?: string
}

export interface OperationLogItem {
  id: string
  tenantId: string
  userId?: string
  userName?: string
  module: string
  action: string
  method: string
  requestPath?: string
  requestMethod: string
  ipAddress?: string
  userAgent?: string
  statusCode: number
  elapsedMilliseconds: number
  traceId?: string
  createdAt: string
}

export interface OperationLogDetail extends OperationLogItem {
  requestBody?: string
  responseBody?: string
}

export function getOperationLogs(params: OperationLogQuery) {
  return request
    .get<ApiResult<PagedResult<OperationLogItem>>>('/api/operation-logs', { params })
    .then((res) => res.data.data)
}

export function getOperationLogDetail(id: string) {
  return request
    .get<ApiResult<OperationLogDetail>>(`/api/operation-logs/${id}`)
    .then((res) => res.data.data)
}
