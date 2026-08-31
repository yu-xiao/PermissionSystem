import type { AxiosRequestConfig } from 'axios'
import { request } from '../utils/request'
import type { ApiResult, PageQuery, PagedResult } from './types'
import { unwrapApiResult } from './types'

export type NotificationType = 'System' | 'Security' | 'Task' | 'Approval' | string

export interface NotificationQuery extends PageQuery {
  type?: NotificationType
  isRead?: boolean
}

export interface NotificationItem {
  id: string
  notificationId: string
  type: NotificationType
  title: string
  content: string
  senderName?: string
  linkUrl?: string
  payload?: string
  isRead: boolean
  readAt?: string
  createdAt: string
}

export interface NotificationRealtimeMessage {
  id: string
  notificationId: string
  type: NotificationType
  title: string
  content: string
  linkUrl?: string
  createdAt: string
}

export interface NotificationSummary {
  unreadCount: number
  latest: NotificationItem[]
}

export async function getMyNotifications(
  params: NotificationQuery = {},
  config?: AxiosRequestConfig,
) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<NotificationItem>>>('/api/v1/notifications/my', {
      ...config,
      params,
    }),
  )
}

export async function getMyUnreadNotificationCount(config?: AxiosRequestConfig) {
  return unwrapApiResult(
    await request.get<ApiResult<number>>('/api/v1/notifications/my/unread-count', config),
  )
}

export async function markNotificationRead(id: string) {
  return unwrapApiResult(await request.post<ApiResult<void>>(`/api/v1/notifications/my/${id}/read`))
}

export async function markAllNotificationsRead() {
  return unwrapApiResult(await request.post<ApiResult<void>>('/api/v1/notifications/my/read-all'))
}

export async function deleteMyNotification(id: string) {
  return unwrapApiResult(await request.delete<ApiResult<void>>(`/api/v1/notifications/my/${id}`))
}

export interface NotificationDeliveryStatusResponse {
  mode: string
  isEnabled: boolean
  description: string
}

export interface NotificationDeliveryResult {
  mode: string
  status: string
  notificationId?: string
  messageId?: string
}

export interface SendSystemNotificationRequest {
  tenantId?: string
  recipientUserIds?: string[]
  type: NotificationType
  title: string
  content: string
  linkUrl?: string
  payload?: string
}

export async function getNotificationDeliveryStatus() {
  return unwrapApiResult(
    await request.get<ApiResult<NotificationDeliveryStatusResponse>>('/api/v1/notifications/admin/delivery-status'),
  )
}

export async function sendSystemNotification(payload: SendSystemNotificationRequest) {
  return unwrapApiResult(
    await request.post<ApiResult<NotificationDeliveryResult>>('/api/v1/notifications/admin/send', payload),
  )
}

export interface NotificationTemplateQuery extends PageQuery {
  type?: NotificationType
  status?: string
}

export interface NotificationTemplateItem {
  id: string
  tenantId: string
  code: string
  name: string
  type: NotificationType
  titleTemplate: string
  contentTemplate: string
  status: string
  sort: number
  remark?: string
  createdAt: string
  concurrencyToken?: string
}

export interface SaveNotificationTemplateRequest {
  concurrencyToken?: string
  tenantId?: string
  code: string
  name: string
  type: NotificationType
  titleTemplate: string
  contentTemplate: string
  status: string
  sort: number
  remark?: string
}

export async function getNotificationTemplates(params: NotificationTemplateQuery = {}) {
  return unwrapApiResult(
    await request.get<ApiResult<PagedResult<NotificationTemplateItem>>>('/api/v1/notifications/templates', { params }),
  )
}

export async function createNotificationTemplate(payload: SaveNotificationTemplateRequest) {
  return unwrapApiResult(
    await request.post<ApiResult<NotificationTemplateItem>>('/api/v1/notifications/templates', payload),
  )
}

export async function updateNotificationTemplate(id: string, payload: SaveNotificationTemplateRequest) {
  return unwrapApiResult(
    await request.put<ApiResult<NotificationTemplateItem>>(`/api/v1/notifications/templates/${id}`, payload),
  )
}

export async function deleteNotificationTemplate(id: string) {
  return unwrapApiResult(await request.delete<ApiResult<void>>(`/api/v1/notifications/templates/${id}`))
}

