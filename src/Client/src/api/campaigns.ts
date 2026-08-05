import { request, type ApiResult } from './client'

export type CampaignRole = 'Master' | 'Player'

export type MembershipState = 'Invited' | 'Active' | 'Declined' | 'Left'

export interface CampaignSummary {
  id: string
  name: string
  systemId: string
  systemVersion: string
  createdAt: string
  role: CampaignRole
}

export interface RosterEntry {
  userId: string
  username: string
  role: CampaignRole
  state: MembershipState
}

export interface GameSystemSummary {
  systemId: string
  version: string
}

/** What a campaign may pin. The form offers these rather than asking people to guess. */
export function listGameSystems(): Promise<ApiResult<GameSystemSummary[]>> {
  return request<GameSystemSummary[]>('/api/systems')
}

export function listCampaigns(): Promise<ApiResult<CampaignSummary[]>> {
  return request<CampaignSummary[]>('/api/campaigns')
}

export function createCampaign(
  name: string,
  systemId: string,
  systemVersion: string,
): Promise<ApiResult<CampaignSummary>> {
  return request<CampaignSummary>('/api/campaigns', {
    method: 'POST',
    body: JSON.stringify({ name, systemId, systemVersion }),
  })
}

export function getCampaign(id: string): Promise<ApiResult<CampaignSummary>> {
  return request<CampaignSummary>(`/api/campaigns/${id}`)
}

export function getRoster(id: string): Promise<ApiResult<RosterEntry[]>> {
  return request<RosterEntry[]>(`/api/campaigns/${id}/roster`)
}

export function inviteMember(id: string, username: string): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/campaigns/${id}/roster`, {
    method: 'POST',
    body: JSON.stringify({ username }),
  })
}

export function removeMember(id: string, userId: string): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/campaigns/${id}/roster/${userId}`, { method: 'DELETE' })
}

export function leaveCampaign(id: string): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/campaigns/${id}/roster/me`, { method: 'DELETE' })
}

export function listInvitations(): Promise<ApiResult<CampaignSummary[]>> {
  return request<CampaignSummary[]>('/api/campaigns/invitations')
}

export function respondToInvitation(id: string, accept: boolean): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/campaigns/${id}/roster/response`, {
    method: 'POST',
    body: JSON.stringify({ accept }),
  })
}
