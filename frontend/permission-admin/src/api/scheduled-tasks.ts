import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export interface ScheduledTaskQuery extends PageQuery {
  jobType?: string
  isEnabled?: boolean
}

export interface ScheduledTaskItem {
  id: string
  tenantId: string
  code: string
  name: string
  jobType: string
  cronExpression: string
  queue: string
  description?: string
  parametersJson?: string
  isEnabled: boolean
  lastRunAt?: string
  lastRunSucceeded?: boolean
  lastRunMessage?: string
  lastJobId?: string
  createdAt: string
}

export interface ScheduledTaskLogItem {
  id: string
  scheduledTaskId: string
  jobId?: string
  jobType: string
  startedAt: string
  finishedAt?: string
  succeeded: boolean
  message?: string
}

export interface CreateScheduledTaskRequest {
  tenantId: string
  code: string
  name: string
  jobType: string
  cronExpression: string
  queue: string
  description?: string
  parametersJson?: string
  isEnabled: boolean
}

export type UpdateScheduledTaskRequest = Omit<CreateScheduledTaskRequest, 'tenantId' | 'code'>

export function getScheduledTasks(params: ScheduledTaskQuery) {
  return request
    .get<ApiResult<PagedResult<ScheduledTaskItem>>>('/api/scheduled-tasks', { params })
    .then((res) => res.data.data)
}

export function getScheduledTaskLogs(taskId: string, params: PageQuery) {
  return request
    .get<ApiResult<PagedResult<ScheduledTaskLogItem>>>(`/api/scheduled-tasks/${taskId}/logs`, {
      params,
    })
    .then((res) => res.data.data)
}

export function createScheduledTask(data: CreateScheduledTaskRequest) {
  return request
    .post<ApiResult<ScheduledTaskItem>>('/api/scheduled-tasks', data)
    .then((res) => res.data.data)
}

export function updateScheduledTask(id: string, data: UpdateScheduledTaskRequest) {
  return request
    .put<ApiResult<ScheduledTaskItem>>(`/api/scheduled-tasks/${id}`, data)
    .then((res) => res.data.data)
}

export function deleteScheduledTask(id: string) {
  return request.delete<ApiResult<void>>(`/api/scheduled-tasks/${id}`)
}

export function enableScheduledTask(id: string) {
  return request.post<ApiResult<void>>(`/api/scheduled-tasks/${id}/enable`)
}

export function disableScheduledTask(id: string) {
  return request.post<ApiResult<void>>(`/api/scheduled-tasks/${id}/disable`)
}

export function triggerScheduledTask(id: string) {
  return request.post<ApiResult<void>>(`/api/scheduled-tasks/${id}/trigger`)
}

export function syncScheduledTasks() {
  return request.post<ApiResult<void>>('/api/scheduled-tasks/sync')
}
