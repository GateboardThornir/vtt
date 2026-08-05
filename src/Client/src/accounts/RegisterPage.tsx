import { useState, type FormEvent, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { register } from '../api/accounts'

const errorKeys = [
  'invite_invalid',
  'invite_expired',
  'invite_already_used',
  'username_taken',
  'username_invalid',
  'password_too_short',
] as const

/**
 * Turn an invitation into an account.
 *
 * The token arrives in the query string and goes straight into the request body. It is a
 * credential: it is never rendered, never put in a form field the user can see, and never logged.
 */
export function RegisterPage(): JSX.Element {
  const { t } = useTranslation()
  const [params] = useSearchParams()
  const token = params.get('token')

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)
  const [busy, setBusy] = useState(false)

  if (token === null || token === '') {
    return (
      <section>
        <h1>{t('register.title')}</h1>
        <p role="alert">{t('register.missingToken')}</p>
      </section>
    )
  }

  async function submit(event: FormEvent): Promise<void> {
    event.preventDefault()
    setBusy(true)
    setError(null)

    const result = await register(token!, username, password)

    setBusy(false)

    if (result.kind === 'ok') {
      setDone(true)
      return
    }

    // A code the server invented and this build has never heard of falls back to something
    // generic, rather than showing the user a bare identifier.
    const known = errorKeys.find((key) => key === result.code)

    setError(known === undefined ? t('common.unexpectedError') : t(`register.errors.${known}`))
  }

  if (done) {
    return (
      <section>
        <h1>{t('register.title')}</h1>
        <p>{t('register.success')}</p>
      </section>
    )
  }

  return (
    <section>
      <h1>{t('register.title')}</h1>
      <p>{t('register.intro')}</p>
      <form onSubmit={(event) => void submit(event)}>
        <label>
          {t('register.username')}
          <input
            name="username"
            autoComplete="username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
          />
        </label>
        <label>
          {t('register.password')}
          <input
            name="password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
          />
        </label>
        <button type="submit" disabled={busy}>
          {t('register.submit')}
        </button>
      </form>
      {error !== null && <p role="alert">{error}</p>}
    </section>
  )
}
