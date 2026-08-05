import { request, type ApiResult } from './client'

export type SessionState = 'Planned' | 'Open' | 'Closed'

export interface PlaySessionView {
  id: string
  title: string
  state: SessionState
  createdAt: string
  openedAt: string | null
  closedAt: string | null
}

export function listSessions(campaignId: string): Promise<ApiResult<PlaySessionView[]>> {
  return request<PlaySessionView[]>(`/api/campaigns/${campaignId}/sessions`)
}

export function createSession(campaignId: string, title: string): Promise<ApiResult<PlaySessionView>> {
  return request<PlaySessionView>(`/api/campaigns/${campaignId}/sessions`, {
    method: 'POST',
    body: JSON.stringify({ title }),
  })
}

export function setSessionState(
  campaignId: string,
  sessionId: string,
  state: SessionState,
): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/campaigns/${campaignId}/sessions/${sessionId}/state`, {
    method: 'PUT',
    body: JSON.stringify({ state }),
  })
}
