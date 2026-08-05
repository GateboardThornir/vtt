import { request, type ApiResult } from './client'

export interface CharacterSummary {
  id: string
  name: string
  ownerUserId: string
  updatedAt: string
}

export interface CharacterDetail extends CharacterSummary {
  /** The sheet, as JSON text. Its shape belongs to the campaign's pinned game system. */
  sheet: string
}

export interface SheetError {
  path: string
  message: string
}

export function listCharacters(campaignId: string): Promise<ApiResult<CharacterSummary[]>> {
  return request<CharacterSummary[]>(`/api/campaigns/${campaignId}/characters`)
}

export function getCharacter(campaignId: string, id: string): Promise<ApiResult<CharacterDetail>> {
  return request<CharacterDetail>(`/api/campaigns/${campaignId}/characters/${id}`)
}

export function createCharacter(
  campaignId: string,
  name: string,
  sheet: string,
): Promise<ApiResult<CharacterDetail>> {
  return request<CharacterDetail>(`/api/campaigns/${campaignId}/characters`, {
    method: 'POST',
    body: JSON.stringify({ name, sheet }),
  })
}

export function saveCharacter(
  campaignId: string,
  id: string,
  name: string,
  sheet: string,
): Promise<ApiResult<CharacterDetail>> {
  return request<CharacterDetail>(`/api/campaigns/${campaignId}/characters/${id}`, {
    method: 'PUT',
    body: JSON.stringify({ name, sheet }),
  })
}
