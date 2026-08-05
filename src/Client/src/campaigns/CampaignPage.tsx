import { useEffect, useState, type FormEvent, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate, useParams } from 'react-router'
import {
  getCampaign,
  getRoster,
  inviteMember,
  leaveCampaign,
  removeMember,
  type CampaignSummary,
  type RosterEntry,
} from '../api/campaigns'

export function CampaignPage(): JSX.Element {
  const { t } = useTranslation()
  const { id = '' } = useParams()
  const navigate = useNavigate()

  const [campaign, setCampaign] = useState<CampaignSummary | null>(null)
  const [roster, setRoster] = useState<RosterEntry[]>([])
  const [missing, setMissing] = useState(false)
  const [username, setUsername] = useState('')
  const [error, setError] = useState<string | null>(null)

  async function load(): Promise<void> {
    const [detail, entries] = await Promise.all([getCampaign(id), getRoster(id)])

    if (detail.kind === 'ok') {
      setCampaign(detail.value)
      setRoster(entries.kind === 'ok' ? entries.value : [])
    } else {
      // A campaign you may not see is a 404 from the server, deliberately — it never confirms one
      // exists. The screen says so rather than showing an empty page.
      setMissing(true)
    }
  }

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const [detail, entries] = await Promise.all([getCampaign(id), getRoster(id)])

      if (cancelled) {
        return
      }

      if (detail.kind === 'ok') {
        setCampaign(detail.value)
        setRoster(entries.kind === 'ok' ? entries.value : [])
      } else {
        setMissing(true)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [id])

  async function invite(event: FormEvent): Promise<void> {
    event.preventDefault()
    setError(null)

    const result = await inviteMember(id, username)

    if (result.kind === 'ok') {
      setUsername('')
      await load()
      return
    }

    setError(
      result.status === 409 ? t('campaigns.alreadyOnRoster') : t('campaigns.noSuchAccount'),
    )
  }

  if (missing) {
    return <p role="alert">{t('campaigns.notFound')}</p>
  }

  if (campaign === null) {
    return <p>{t('common.loading')}</p>
  }

  // Rendered from what the server said this caller is. It is not authorisation: the server refuses
  // a Player who calls the endpoint anyway, and this only avoids offering a control that would fail.
  const isMaster = campaign.role === 'Master'

  return (
    <section>
      <h1>{campaign.name}</h1>
      <p>
        {campaign.systemId} {campaign.systemVersion}
      </p>

      <h2>{t('campaigns.roster')}</h2>
      <ul>
        {roster.map((entry) => (
          <li key={entry.userId}>
            {entry.username} — {t(`campaigns.roles.${entry.role}`)} (
            {t(`campaigns.states.${entry.state}`)})
            {isMaster && entry.role !== 'Master' && (
              <button
                type="button"
                onClick={() => void removeMember(id, entry.userId).then(load)}
              >
                {t('campaigns.remove')}
              </button>
            )}
          </li>
        ))}
      </ul>

      {isMaster ? (
        <form onSubmit={(event) => void invite(event)}>
          <label>
            {t('campaigns.inviteUsername')}
            <input value={username} onChange={(event) => setUsername(event.target.value)} />
          </label>
          <button type="submit">{t('campaigns.inviteSubmit')}</button>
        </form>
      ) : (
        <button type="button" onClick={() => void leaveCampaign(id).then(() => navigate('/campaigns'))}>
          {t('campaigns.leave')}
        </button>
      )}

      {error !== null && <p role="alert">{error}</p>}
    </section>
  )
}
