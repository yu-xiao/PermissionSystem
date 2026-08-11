import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'

export interface DeadLetterMessageQuery extends PageQuery {
  consumer?: string
  sourceQueue?: string
  status?: string
  startTime?: string
  endTime?: string
}

export interface DeadLetterMessageItem {
  id: string
  tenantId: string
  messageId: string
  consumer: string
  sourceQueue: string
  exchange: string
  routingKey: string
  messageType: string
  retryCount: number
  failureReason: string
  status: string
  replayCount: number
  lastReplayedAt?: string
  dispositionRemark?: string
  createdAt: string
}

export interface DeadLetterMessageDetail extends DeadLetterMessageItem {
  payload: string
  headers?: string
}

export function getDeadLetterMessages(params: DeadLetterMessageQuery) {
  return request
    .get<ApiResult<PagedResult<DeadLetterMessageItem>>>('/api/dead-letter-messages', { params })
    .then((res) => res.data.data)
}

export function getDeadLetterMessageDetail(id: string) {
  return request
    .get<ApiResult<DeadLetterMessageDetail>>(`/api/dead-letter-messages/${id}`)
    .then((res) => res.data.data)
}

export function replayDeadLetterMessage(id: string) {
  return request.post<ApiResult<void>>(`/api/dead-letter-messages/${id}/replay`)
}

export function discardDeadLetterMessage(id: string, remark: string) {
  return request.post<ApiResult<void>>(`/api/dead-letter-messages/${id}/discard`, { remark })
}
