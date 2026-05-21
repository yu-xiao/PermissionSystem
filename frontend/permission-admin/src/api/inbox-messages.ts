import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface InboxMessageQuery extends PageQuery {
  consumer?: string
  status?: string
  messageType?: string
  startTime?: string
  endTime?: string
}

export interface InboxMessageItem {
  id: string
  tenantId: string
  messageId: string
  consumer: string
  messageType: string
  payloadHash: string
  status: string
  createdAt: string
  processedAt?: string
}

export type InboxMessageDetail = InboxMessageItem

export function getInboxMessages(params: InboxMessageQuery) {
  return request
    .get<ApiResult<PagedResult<InboxMessageItem>>>('/api/inbox-messages', { params })
    .then((res) => res.data.data)
}

export function getInboxMessageDetail(id: string) {
  return request
    .get<ApiResult<InboxMessageDetail>>(`/api/inbox-messages/${id}`)
    .then((res) => res.data.data)
}
