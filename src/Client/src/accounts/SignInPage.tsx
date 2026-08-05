import { useState, type FormEvent, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { signIn } from '../api/accounts'
import { useSession } from './sessionContext'

/**
 * Sign in, or find out why not.
 *
 * The three refusals are genuinely different and the screen says so: wrong credentials, an account
 * still waiting for approval, and a disabled one. 013 made the server distinguish them only after
 * the password verifies, so showing them here discloses nothing.
 */
export function SignInPage(): JSX.Element {
  const { t } = useTranslation()
  const { refresh } = useSession()
  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [pending, setPending] = useState(false)
  const [busy, setBusy] = useState(false)

  async function submit(event: FormEvent): Promise<void> {
    event.preventDefault()
    setBusy(true)
    setError(null)
    setPending(false)

    const result = await signIn(username, password)

    setBusy(false)

    if (result.kind === 'ok') {
      await refresh()
      return
    }

    if (result.code === 'awaiting_approval') {
      setPending(true)
      return
    }

    setError(
      result.code === 'account_disabled'
        ? t('signIn.accountDisabled')
        : result.status === 401
          ? t('signIn.invalidCredentials')
          : t('common.unexpectedError'),
    )
  }

  if (pending) {
    return (
      <section>
        <h1>{t('pending.title')}</h1>
        <p>{t('pending.body')}</p>
      </section>
    )
  }

  return (
    <section>
      <h1>{t('signIn.title')}</h1>
      <form onSubmit={(event) => void submit(event)}>
        <label>
          {t('signIn.username')}
          <input
            name="username"
            autoComplete="username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
          />
        </label>
        <label>
          {t('signIn.password')}
          <input
            name="password"
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <button type="submit" disabled={busy}>
          {t('signIn.submit')}
        </button>
      </form>
      {error !== null && <p role="alert">{error}</p>}
    </section>
  )
}
