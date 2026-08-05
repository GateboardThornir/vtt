import { createContext, useContext } from 'react'
import type { Session } from '../api/accounts'

export interface SessionState {
  session: Session | null
  loading: boolean

  /**
   * The server could not be reached at all.
   *
   * Distinct from "not signed in": showing a sign-in form to somebody whose backend is down invites
   * them to type a password at nothing and conclude their credentials are wrong.
   */
  unreachable: boolean
  refresh: () => Promise<void>
  signOut: () => Promise<void>
}

/**
 * Separated from the provider component so that file exports only components — Fast Refresh
 * silently stops working for a module that mixes the two.
 */
export const SessionContext = createContext<SessionState | null>(null)

export function useSession(): SessionState {
  const state = useContext(SessionContext)

  if (state === null) {
    throw new Error('useSession must be used inside a SessionProvider.')
  }

  return state
}
