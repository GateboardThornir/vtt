/** Every call to the Accounts endpoints. */
import { request, type ApiResult } from './client'


export type AccountState = 'Pending' | 'Active' | 'Disabled'

export type PlatformRole = 'Member' | 'Admin'

export interface Session {
  id: string
  username: string
}

export interface AccountSummary {
  id: string
  username: string
  state: AccountState
  role: PlatformRole
  createdAt: string
}

export function getSession(): Promise<ApiResult<Session>> {
  return request<Session>('/api/session')
}

export function signIn(username: string, password: string): Promise<ApiResult<Session>> {
  return request<Session>('/api/session', {
    method: 'POST',
    body: JSON.stringify({ username, password }),
  })
}

export function signOut(): Promise<ApiResult<undefined>> {
  return request<undefined>('/api/session', { method: 'DELETE' })
}

export function register(
  token: string,
  username: string,
  password: string,
): Promise<ApiResult<undefined>> {
  return request<undefined>('/api/registration', {
    method: 'POST',
    body: JSON.stringify({ token, username, password }),
  })
}

export interface IssuedInvite {
  id: string
  token: string
  expiresAt: string
}

/** Mints an invite. The token comes back once and is never recoverable afterwards. */
export function issueInvite(): Promise<ApiResult<IssuedInvite>> {
  return request<IssuedInvite>('/api/admin/invites', { method: 'POST' })
}

export function listAccounts(): Promise<ApiResult<AccountSummary[]>> {
  return request<AccountSummary[]>('/api/admin/accounts')
}

export function setAccountState(id: string, state: AccountState): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/admin/accounts/${id}/state`, {
    method: 'PUT',
    body: JSON.stringify({ state }),
  })
}
