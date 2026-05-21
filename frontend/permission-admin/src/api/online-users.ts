import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface OnlineUserQuery extends PageQuery {
  tenantId?: string
  isRevoked?: boolean
}

export interface OnlineUserItem {
  id: string
  tenantId: string
  userId: string
  userName: string
  sessionId: string
  ipAddress?: string
  userAgent?: string
  loginAt: string
  lastActiveAt: string
  expiresAt: string
  isRevoked: boolean
  revokedAt?: string
  revokedReason?: string
}

export function getOnlineUsers(params: OnlineUserQuery) {
  return request
    .get<ApiResult<PagedResult<OnlineUserItem>>>('/api/online-users', { params })
    .then((res) => res.data.data)
}

export function getOnlineUserDetail(id: string) {
  return request.get<ApiResult<OnlineUserItem>>(`/api/online-users/${id}`).then((res) => res.data.data)
}

export function kickoutOnlineUser(id: string, reason?: string) {
  return request.post<ApiResult<void>>(`/api/online-users/${id}/kickout`, { reason })
}
