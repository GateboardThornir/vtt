import { useCallback, useEffect, useState, type JSX, type ReactNode } from 'react'
import { getSession, signOut as signOutRequest } from '../api/accounts'
import type { Session } from '../api/accounts'
import { SessionContext } from './sessionContext'

/**
 * Holds who is signed in, for the whole tree.
 *
 * The value comes from `GET /api/session` rather than from whatever the sign-in call returned:
 * the client keeps a projection of what the server said, never a guess about what it would say.
 * That is the same rule the table engine will follow, and it is cheaper to learn here.
 */
export function SessionProvider({ children }: { children: ReactNode }): JSX.Element {
  const [session, setSession] = useState<Session | null>(null)
  const [loading, setLoading] = useState(true)

  const refresh = useCallback(async () => {
    const result = await getSession()

    // A 401 here is the ordinary answer for "not signed in", not an error to report.
    setSession(result.kind === 'ok' ? result.value : null)
    setLoading(false)
  }, [])

  const signOut = useCallback(async () => {
    await signOutRequest()
    await refresh()
  }, [refresh])

  useEffect(() => {
    // The cancelled flag matters twice over: StrictMode runs this effect twice in development, and
    // a component unmounted mid-request would otherwise set state on a tree that has gone.
    let cancelled = false

    void (async () => {
      const result = await getSession()

      if (!cancelled) {
        setSession(result.kind === 'ok' ? result.value : null)
        setLoading(false)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  return (
    <SessionContext value={{ session, loading, refresh, signOut }}>{children}</SessionContext>
  )
}
