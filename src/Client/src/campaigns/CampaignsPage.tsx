import { useEffect, useState, type FormEvent, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import {
  createCampaign,
  listCampaigns,
  listGameSystems,
  listInvitations,
  respondToInvitation,
  type CampaignSummary,
  type GameSystemSummary,
} from '../api/campaigns'

function key(system: GameSystemSummary): string {
  return `${system.systemId}@${system.version}`
}

export function CampaignsPage(): JSX.Element {
  const { t } = useTranslation()
  const [campaigns, setCampaigns] = useState<CampaignSummary[] | null>(null)
  const [invitations, setInvitations] = useState<CampaignSummary[]>([])
  const [name, setName] = useState('')
  const [systems, setSystems] = useState<GameSystemSummary[]>([])
  const [chosen, setChosen] = useState('')

  async function load(): Promise<void> {
    const [mine, invited] = await Promise.all([listCampaigns(), listInvitations()])

    setCampaigns(mine.kind === 'ok' ? mine.value : [])
    setInvitations(invited.kind === 'ok' ? invited.value : [])
  }

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const [mine, invited, available] = await Promise.all([
        listCampaigns(),
        listInvitations(),
        listGameSystems(),
      ])

      if (cancelled) {
        return
      }

      setCampaigns(mine.kind === 'ok' ? mine.value : [])
      setInvitations(invited.kind === 'ok' ? invited.value : [])

      if (available.kind === 'ok') {
        setSystems(available.value)
        setChosen(available.value.length > 0 ? key(available.value[0]!) : '')
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  async function submit(event: FormEvent): Promise<void> {
    event.preventDefault()

    const system = systems.find((candidate) => key(candidate) === chosen)

    if (system === undefined) {
      return
    }

    await createCampaign(name, system.systemId, system.version)
    setName('')
    await load()
  }

  async function respond(id: string, accept: boolean): Promise<void> {
    await respondToInvitation(id, accept)
    await load()
  }

  if (campaigns === null) {
    return <p>{t('common.loading')}</p>
  }

  return (
    <section>
      <h1>{t('campaigns.title')}</h1>

      {invitations.length > 0 && (
        <>
          <h2>{t('campaigns.invitations')}</h2>
          <ul>
            {invitations.map((campaign) => (
              <li key={campaign.id}>
                {campaign.name}
                <button type="button" onClick={() => void respond(campaign.id, true)}>
                  {t('campaigns.accept')}
                </button>
                <button type="button" onClick={() => void respond(campaign.id, false)}>
                  {t('campaigns.decline')}
                </button>
              </li>
            ))}
          </ul>
        </>
      )}

      {campaigns.length === 0 ? (
        <p>{t('campaigns.none')}</p>
      ) : (
        <ul>
          {campaigns.map((campaign) => (
            <li key={campaign.id}>
              <Link to={`/campaigns/${campaign.id}`}>{campaign.name}</Link>
              {' — '}
              {t(`campaigns.roles.${campaign.role}`)}
              {` (${campaign.systemId} ${campaign.systemVersion})`}
            </li>
          ))}
        </ul>
      )}

      <h2>{t('campaigns.create')}</h2>
      <form onSubmit={(event) => void submit(event)}>
        <label>
          {t('campaigns.name')}
          <input value={name} onChange={(event) => setName(event.target.value)} />
        </label>
        {/* Chosen, not typed. A pin that does not resolve makes a campaign in which no character
            can ever be created, and the mistake surfaces long after it is made. */}
        <label>
          {t('campaigns.system')}
          <select value={chosen} onChange={(event) => setChosen(event.target.value)}>
            {systems.map((system) => (
              <option key={key(system)} value={key(system)}>
                {system.systemId} {system.version}
              </option>
            ))}
          </select>
        </label>
        <button type="submit">{t('campaigns.submit')}</button>
      </form>
    </section>
  )
}
