/**
 * Every call to the Accounts endpoints, in one place.
 *
 * One module owns them so that later screens extend this rather than scattering `fetch` calls
 * through components. Paths are relative: the Vite proxy handles them in development and Caddy
 * will in production, and anything hardcoding a host breaks in exactly one of those.
 */

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

/** Either the server answered, or it did not. Nothing in between is guessed at. */
export type ApiResult<T> =
  | { kind: 'ok'; value: T }
  | { kind: 'error'; status: number; code?: string }

async function request<T>(path: string, init?: RequestInit): Promise<ApiResult<T>> {
  let response: Response

  try {
    response = await fetch(path, {
      ...init,
      headers: { 'Content-Type': 'application/json', ...init?.headers },
    })
  } catch {
    return { kind: 'error', status: 0 }
  }

  if (response.ok) {
    const text = await response.text()

    return { kind: 'ok', value: (text ? JSON.parse(text) : undefined) as T }
  }

  // The server sends stable codes rather than English sentences, precisely so the client can
  // translate them — see task 012. `title` is where ProblemDetails puts ours.
  let code: string | undefined

  try {
    const body = (await response.json()) as { error?: string; title?: string }
    code = body.error ?? body.title
  } catch {
    code = undefined
  }

  return { kind: 'error', status: response.status, code }
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

export function listAccounts(): Promise<ApiResult<AccountSummary[]>> {
  return request<AccountSummary[]>('/api/admin/accounts')
}

export function setAccountState(id: string, state: AccountState): Promise<ApiResult<undefined>> {
  return request<undefined>(`/api/admin/accounts/${id}/state`, {
    method: 'PUT',
    body: JSON.stringify({ state }),
  })
}
