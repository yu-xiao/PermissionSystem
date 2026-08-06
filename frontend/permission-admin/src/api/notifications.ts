import { request } from '../utils/request'
import type { ApiResult, PagedResult, PageQuery } from './types'

export type NotificationType = 'System' | 'Security' | 'Task' | 'Approval'

export interface NotificationQuery extends PageQuery {
  type?: string
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

export interface SendSystemNotificationRequest {
  tenantId?: string
  recipientUserIds?: string[]
  type: NotificationType
  title: string
  content: string
  linkUrl?: string
  payload?: string
}

export type NotificationDeliveryMode = 'Direct' | 'OutboxRabbitMQ' | 'Disabled'

export type NotificationDeliveryStatus = 'Delivered' | 'Queued' | 'Disabled'

export interface NotificationDeliveryResult {
  mode: NotificationDeliveryMode
  status: NotificationDeliveryStatus
  notificationId?: string
  messageId?: string
}

export interface NotificationDeliveryStatusResponse {
  mode: NotificationDeliveryMode
  isEnabled: boolean
  description: string
}

export interface NotificationTemplateQuery extends PageQuery {
  type?: string
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
}

export type SaveNotificationTemplateRequest = Omit<NotificationTemplateItem, 'id' | 'tenantId' | 'createdAt'>

export function getMyNotifications(params: NotificationQuery) {
  return request
    .get<ApiResult<PagedResult<NotificationItem>>>('/api/notifications/my', { params })
    .then((res) => res.data.data)
}

export function getMyUnreadNotificationCount() {
  return request.get<ApiResult<number>>('/api/notifications/my/unread-count').then((res) => res.data.data)
}

export function markNotificationRead(id: string) {
  return request.post<ApiResult<void>>(`/api/notifications/my/${id}/read`)
}

export function markAllNotificationsRead() {
  return request.post<ApiResult<void>>('/api/notifications/my/read-all')
}

export function deleteMyNotification(id: string) {
  return request.delete<ApiResult<void>>(`/api/notifications/my/${id}`)
}

export function sendSystemNotification(data: SendSystemNotificationRequest) {
  return request
    .post<ApiResult<NotificationDeliveryResult>>('/api/notifications/admin/send', data)
    .then((res) => res.data.data)
}

export function getNotificationDeliveryStatus() {
  return request
    .get<ApiResult<NotificationDeliveryStatusResponse>>('/api/notifications/admin/delivery-status')
    .then((res) => res.data.data)
}

export function getNotificationTemplates(params: NotificationTemplateQuery) {
  return request
    .get<ApiResult<PagedResult<NotificationTemplateItem>>>('/api/notifications/templates', { params })
    .then((res) => res.data.data)
}

export function createNotificationTemplate(data: SaveNotificationTemplateRequest) {
  return request
    .post<ApiResult<NotificationTemplateItem>>('/api/notifications/templates', data)
    .then((res) => res.data.data)
}

export function updateNotificationTemplate(id: string, data: SaveNotificationTemplateRequest) {
  return request
    .put<ApiResult<NotificationTemplateItem>>(`/api/notifications/templates/${id}`, data)
    .then((res) => res.data.data)
}

export function deleteNotificationTemplate(id: string) {
  return request.delete<ApiResult<void>>(`/api/notifications/templates/${id}`)
}
