import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface OutboxMessageQuery extends PageQuery {
  status?: string
  messageType?: string
  routingKey?: string
  startTime?: string
  endTime?: string
}

export interface OutboxMessageItem {
  id: string
  tenantId: string
  messageId: string
  exchange: string
  routingKey: string
  messageType: string
  headers?: string
  status: string
  retryCount: number
  nextRetryAt?: string
  errorMessage?: string
  createdAt: string
  processedAt?: string
}

export interface OutboxMessageDetail extends OutboxMessageItem {
  payload: string
}

export function getOutboxMessages(params: OutboxMessageQuery) {
  return request
    .get<ApiResult<PagedResult<OutboxMessageItem>>>('/api/outbox-messages', { params })
    .then((res) => res.data.data)
}

export function getOutboxMessageDetail(id: string) {
  return request
    .get<ApiResult<OutboxMessageDetail>>(`/api/outbox-messages/${id}`)
    .then((res) => res.data.data)
}
