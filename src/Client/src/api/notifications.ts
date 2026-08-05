import { request, type ApiResult } from './client'

export type NotificationKind = 'CampaignInvitation' | 'AccountApproved' | 'AccountRejected'

export interface NotificationView {
  id: string
  kind: NotificationKind
  subject: string | null
  createdAt: string
  read: boolean
}

export function listNotifications(): Promise<ApiResult<NotificationView[]>> {
  return request<NotificationView[]>('/api/notifications')
}

export function markAllRead(): Promise<ApiResult<undefined>> {
  return request<undefined>('/api/notifications/read', { method: 'POST' })
}
