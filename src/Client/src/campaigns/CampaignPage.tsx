import { useEffect, useState, type FormEvent, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import {
  createSession,
  listSessions,
  setSessionState,
  type PlaySessionView,
} from '../api/sessions'
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
  const [sessions, setSessions] = useState<PlaySessionView[]>([])
  const [sessionTitle, setSessionTitle] = useState('')

  async function load(): Promise<void> {
    const [detail, entries, plays] = await Promise.all([getCampaign(id), getRoster(id), listSessions(id)])

    setSessions(plays.kind === 'ok' ? plays.value : [])

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
      const [detail, entries, plays] = await Promise.all([
        getCampaign(id),
        getRoster(id),
        listSessions(id),
      ])

      if (cancelled) {
        return
      }

      setSessions(plays.kind === 'ok' ? plays.value : [])

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

  async function planSession(): Promise<void> {
    await createSession(id, sessionTitle)
    setSessionTitle('')
    await load()
  }

  async function changeSession(sessionId: string, state: 'Open' | 'Closed'): Promise<void> {
    const result = await setSessionState(id, sessionId, state)

    // 409 is the partial unique index refusing a second open session — a real answer, not a fault.
    setError(result.kind === 'error' && result.status === 409 ? t('sessions.alreadyOpen') : null)

    await load()
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

      <p>
        <Link to={`/campaigns/${id}/characters`}>{t('characters.title')}</Link>
      </p>

      <h2>{t('sessions.title')}</h2>
      {sessions.length === 0 ? (
        <p>{t('sessions.none')}</p>
      ) : (
        <ul>
          {sessions.map((session) => (
            <li key={session.id}>
              {session.title} — {t(`sessions.states.${session.state}`)}
              {/* The table only exists while a session is open, which is what the hub enforces
                  too: a planned or closed session has no live audience. */}
              {session.state === 'Open' && (
                <Link to={`/campaigns/${id}/sessions/${session.id}`}>{t('sessions.enter')}</Link>
              )}
              {isMaster && session.state === 'Planned' && (
                <button type="button" onClick={() => void changeSession(session.id, 'Open')}>
                  {t('sessions.open')}
                </button>
              )}
              {isMaster && session.state === 'Open' && (
                <button type="button" onClick={() => void changeSession(session.id, 'Closed')}>
                  {t('sessions.close')}
                </button>
              )}
            </li>
          ))}
        </ul>
      )}

      {isMaster && (
        <>
          <label>
            {t('sessions.titleField')}
            <input value={sessionTitle} onChange={(event) => setSessionTitle(event.target.value)} />
          </label>
          <button type="button" onClick={() => void planSession()}>
            {t('sessions.create')}
          </button>
        </>
      )}

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
