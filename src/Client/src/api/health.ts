/** Mirrors the payload written by `HealthCheckResponse.Write` on the server. */
export interface HealthReport {
  status: string
  checks: Record<string, string>
}

/**
 * The outcome of asking the server how it is. `unreachable` and `reported` are deliberately
 * different: a 503 is the server answering, which tells us the proxy and the server both work and
 * only the database is down. A network failure tells us nothing past the proxy.
 */
export type HealthResult =
  | { kind: 'reported'; ok: boolean; report: HealthReport }
  | { kind: 'unreachable'; reason: string }

export async function fetchHealth(signal: AbortSignal): Promise<HealthResult> {
  let response: Response

  try {
    response = await fetch('/api/health', { signal })
  } catch (error) {
    return { kind: 'unreachable', reason: error instanceof Error ? error.message : 'network error' }
  }

  try {
    // fetch does not throw on a non-2xx, so a 503 lands here and is a real answer: the server is
    // up and telling us the database is not.
    const report = (await response.json()) as HealthReport

    return { kind: 'reported', ok: response.ok, report }
  } catch {
    // With the server stopped, the Vite proxy answers 502 with an empty body. Anything that is
    // not a health report means the request never reached the endpoint.
    return {
      kind: 'unreachable',
      reason: `the dev proxy returned ${String(response.status)} with no health report`,
    }
  }
}
