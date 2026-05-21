import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface JobInfoQuery extends PageQuery {
  status?: string
}

export interface JobInfoItem {
  jobName: string
  jobId?: string
  jobType: string
  source: string
  queue: string
  cronExpression?: string
  isEnabled: boolean
  status: string
  lastRunAt?: string
  lastRunStatus?: string
  lastJobId?: string
  lastErrorMessage?: string
}

export interface JobExecutionLogQuery extends PageQuery {
  jobName?: string
  status?: string
}

export interface JobExecutionLogItem {
  id: string
  tenantId: string
  jobName: string
  jobId?: string
  status: string
  startedAt: string
  finishedAt?: string
  elapsedMilliseconds: number
  errorMessage?: string
  traceId?: string
}

export function getJobs(params: JobInfoQuery) {
  return request
    .get<ApiResult<PagedResult<JobInfoItem>>>('/api/jobs', { params })
    .then((res) => res.data.data)
}

export function getJobLogs(params: JobExecutionLogQuery) {
  return request
    .get<ApiResult<PagedResult<JobExecutionLogItem>>>('/api/jobs/logs', { params })
    .then((res) => res.data.data)
}

export function triggerJob(jobName: string) {
  return request.post<ApiResult<void>>(`/api/jobs/${encodeURIComponent(jobName)}/trigger`)
}

export function enableJob(jobName: string) {
  return request.post<ApiResult<void>>(`/api/jobs/${encodeURIComponent(jobName)}/enable`)
}

export function disableJob(jobName: string) {
  return request.post<ApiResult<void>>(`/api/jobs/${encodeURIComponent(jobName)}/disable`)
}

export function getHangfireDashboardUrl() {
  const baseUrl = request.defaults.baseURL ?? ''
  return `${baseUrl}/hangfire`
}
