import { useCallback, useEffect, useState, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import {
  listAccounts,
  setAccountState,
  type AccountState,
  type AccountSummary,
} from '../api/accounts'

/**
 * The administrator's account queue.
 *
 * Reaching this screen proves nothing: the server refuses every one of these calls to a
 * non-administrator regardless of what the client renders. Hiding the link elsewhere is a
 * courtesy so members are not offered something that would fail, not access control.
 */
export function AdminAccountsPage(): JSX.Element {
  const { t } = useTranslation()
  const [accounts, setAccounts] = useState<AccountSummary[] | null>(null)
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    const result = await listAccounts()

    if (result.kind === 'ok') {
      setAccounts(result.value)
      setError(null)
    } else {
      setError(t('common.unexpectedError'))
    }
  }, [t])

  useEffect(() => {
    // Same discipline as SessionProvider: the request outlives the render, so its result is only
    // applied if this component is still mounted when it arrives.
    let cancelled = false

    void (async () => {
      const result = await listAccounts()

      if (cancelled) {
        return
      }

      if (result.kind === 'ok') {
        setAccounts(result.value)
      } else {
        setError(t('common.unexpectedError'))
      }
    })()

    return () => {
      cancelled = true
    }
  }, [t])

  async function change(id: string, state: AccountState): Promise<void> {
    await setAccountState(id, state)
    await load()
  }

  if (error !== null) {
    return <p role="alert">{error}</p>
  }

  if (accounts === null) {
    return <p>{t('common.loading')}</p>
  }

  const pending = accounts.filter((account) => account.state === 'Pending')

  return (
    <section>
      <h1>{t('admin.title')}</h1>

      <h2>{t('admin.pendingTitle')}</h2>
      {pending.length === 0 ? (
        <p>{t('admin.noPending')}</p>
      ) : (
        <ul>
          {pending.map((account) => (
            <li key={account.id}>
              {account.username}
              <button type="button" onClick={() => void change(account.id, 'Active')}>
                {t('admin.approve')}
              </button>
              <button type="button" onClick={() => void change(account.id, 'Disabled')}>
                {t('admin.reject')}
              </button>
            </li>
          ))}
        </ul>
      )}

      <table>
        <thead>
          <tr>
            <th>{t('admin.username')}</th>
            <th>{t('admin.state')}</th>
            <th>{t('admin.role')}</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {accounts.map((account) => (
            <tr key={account.id}>
              <td>{account.username}</td>
              <td>{t(`admin.states.${account.state}`)}</td>
              <td>{t(`admin.roles.${account.role}`)}</td>
              <td>
                {account.state === 'Active' && (
                  <button type="button" onClick={() => void change(account.id, 'Disabled')}>
                    {t('admin.disable')}
                  </button>
                )}
                {account.state === 'Disabled' && (
                  <button type="button" onClick={() => void change(account.id, 'Active')}>
                    {t('admin.enable')}
                  </button>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </section>
  )
}
