import { request } from '../utils/request'
import type { ApiResult } from './types'

export interface HealthSummaryResponse {
  status: string
  totalDurationMilliseconds: number
  checkedAt: string
}

export interface HealthEntryResponse {
  name: string
  status: string
  durationMilliseconds: number
  description?: string
  error?: string
  tags: string[]
  data: Record<string, string | undefined>
}

export interface HealthDetailResponse extends HealthSummaryResponse {
  entries: HealthEntryResponse[]
}

export function getHealthDetail() {
  return request.get<ApiResult<HealthDetailResponse>>('/health/detail').then((res) => res.data.data)
}
