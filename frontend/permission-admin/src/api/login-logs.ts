import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface LoginLogQuery extends PageQuery {
  userName?: string
  loginType?: string
  loginResult?: string
  traceId?: string
  startTime?: string
  endTime?: string
}

export interface LoginLogItem {
  id: string
  tenantId: string
  userId?: string
  userName: string
  loginType: string
  ipAddress?: string
  userAgent?: string
  loginResult: string
  failureReason?: string
  traceId?: string
  createdAt: string
}

export function getLoginLogs(params: LoginLogQuery) {
  return request
    .get<ApiResult<PagedResult<LoginLogItem>>>('/api/login-logs', { params })
    .then((res) => res.data.data)
}

export function getLoginLogDetail(id: string) {
  return request.get<ApiResult<LoginLogItem>>(`/api/login-logs/${id}`).then((res) => res.data.data)
}
