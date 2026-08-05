/**
 * The one place an HTTP call is made.
 *
 * Paths are relative: the Vite proxy handles them in development and Caddy will in production, and
 * anything hardcoding a host breaks in exactly one of those.
 */
/** Either the server answered, or it did not. Nothing in between is guessed at. */
export type ApiResult<T> =
  | { kind: 'ok'; value: T }
  | { kind: 'error'; status: number; code?: string }

export async function request<T>(path: string, init?: RequestInit): Promise<ApiResult<T>> {
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

