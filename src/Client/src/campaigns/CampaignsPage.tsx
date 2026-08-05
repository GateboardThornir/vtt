import { useEffect, useState, type FormEvent, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import {
  createCampaign,
  listCampaigns,
  listInvitations,
  respondToInvitation,
  type CampaignSummary,
} from '../api/campaigns'

export function CampaignsPage(): JSX.Element {
  const { t } = useTranslation()
  const [campaigns, setCampaigns] = useState<CampaignSummary[] | null>(null)
  const [invitations, setInvitations] = useState<CampaignSummary[]>([])
  const [name, setName] = useState('')
  const [systemId, setSystemId] = useState('dnd5e')
  const [version, setVersion] = useState('1.0')

  async function load(): Promise<void> {
    const [mine, invited] = await Promise.all([listCampaigns(), listInvitations()])

    setCampaigns(mine.kind === 'ok' ? mine.value : [])
    setInvitations(invited.kind === 'ok' ? invited.value : [])
  }

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const [mine, invited] = await Promise.all([listCampaigns(), listInvitations()])

      if (!cancelled) {
        setCampaigns(mine.kind === 'ok' ? mine.value : [])
        setInvitations(invited.kind === 'ok' ? invited.value : [])
      }
    })()

    return () => {
      cancelled = true
    }
  }, [])

  async function submit(event: FormEvent): Promise<void> {
    event.preventDefault()
    await createCampaign(name, systemId, version)
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
        <label>
          {t('campaigns.system')}
          <input value={systemId} onChange={(event) => setSystemId(event.target.value)} />
        </label>
        <label>
          {t('campaigns.version')}
          <input value={version} onChange={(event) => setVersion(event.target.value)} />
        </label>
        <button type="submit">{t('campaigns.submit')}</button>
      </form>
    </section>
  )
}
