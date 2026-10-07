import { request } from '../utils/request'
import type { ApiResult, PagedResult } from './types'

export interface AiAnomalyRule {
  id: string
  isEnabled: boolean
  lastNotifiedAt?: string
  lastRunAt?: string
  lastRunSucceeded?: boolean
  lastRunMessage?: string
  rowVersion: string
  basis: string
  timeZone: string
  intervalMinutes: number
  cooldownHours: number
}
export interface AiAnomalyEvent {
  id: string
  ruleId: string
  episodeSequence: number
  observedCount?: number | null
  observedAt: string
  closedAt?: string
  closeReason?: string
  deliveryStatus: string
  attemptCount: number
  nextAttemptAt?: string
  errorCode?: string
  rowVersion: string
  evidenceUnavailable: boolean
  transportStatus?: string
  basis: string
}
const base = '/api/ai/anomalies'
export const getAnomalyRules = (pageIndex = 1) =>
  request
    .get<ApiResult<PagedResult<AiAnomalyRule>>>(`${base}/rules`, { params: { pageIndex } })
    .then((r) => r.data.data)
export const createAnomalyRule = () =>
  request.post<ApiResult<AiAnomalyRule>>(`${base}/rules`).then((r) => r.data.data)
export const setAnomalyEnabled = (rule: AiAnomalyRule, isEnabled: boolean) =>
  request
    .put<ApiResult<AiAnomalyRule>>(`${base}/rules/${rule.id}/enabled`, {
      rowVersion: rule.rowVersion,
      isEnabled,
    })
    .then((r) => r.data.data)
export const triggerAnomalyCheck = (id: string) => request.post(`${base}/rules/${id}/check`)
export const getAnomalyEvents = (id: string, pageIndex = 1) =>
  request
    .get<ApiResult<PagedResult<AiAnomalyEvent>>>(`${base}/rules/${id}/events`, {
      params: { pageIndex },
    })
    .then((r) => r.data.data)
export const getAnomalyEvent = (id: string) =>
  request.get<ApiResult<AiAnomalyEvent>>(`${base}/events/${id}`).then((r) => r.data.data)
export const closeAnomalyEvent = (event: AiAnomalyEvent) =>
  request.post(`${base}/events/${event.id}/close`, { rowVersion: event.rowVersion })

export function anomalyEventLink(link?: string | null): string | undefined {
  const match =
    /^\/system\/ai-anomalies\?eventId=([a-f\d]{8}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{4}-[a-f\d]{12})$/i.exec(
      link ?? '',
    )
  return match ? `/system/ai-anomalies?eventId=${match[1]}` : undefined
}
