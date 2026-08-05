import { useEffect, useRef, useState, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import { getCampaign } from '../api/campaigns'
import { TableConnection } from './TableConnection'
import { renderRoll } from './renderRoll'
import type { ChatLine, ChatVoice, Participant, RollLine, RollVisibility } from './types'

type Entry = { kind: 'chat'; line: ChatLine } | { kind: 'roll'; roll: RollLine }

export function TablePage(): JSX.Element {
  const { t } = useTranslation()
  const { id = '', sessionId = '' } = useParams()

  const connection = useRef<TableConnection | null>(null)
  const [participants, setParticipants] = useState<Participant[]>([])
  const [entries, setEntries] = useState<Entry[]>([])
  const [joined, setJoined] = useState<boolean | null>(null)
  const [body, setBody] = useState('')
  const [voice, setVoice] = useState<ChatVoice>('InCharacter')
  const [expression, setExpression] = useState('d20')
  const [visibility, setVisibility] = useState<RollVisibility>('Public')
  const [rejected, setRejected] = useState(false)
  const [isMaster, setIsMaster] = useState(false)

  useEffect(() => {
    // One connection, owned here. A component that opens its own would break task 065's
    // reconnection before it is written.
    const table = new TableConnection()
    connection.current = table

    let cancelled = false

    void (async () => {
      const ok = await table.start(sessionId, {
        onParticipants: (list) => setParticipants(list),
        onParticipantJoined: (who) => setParticipants((current) => [...current, who]),
        onParticipantLeft: (who) =>
          setParticipants((current) => current.filter((entry) => entry.userId !== who.userId)),
        onChatHistory: (lines) =>
          setEntries((current) => [...lines.map((line) => ({ kind: 'chat' as const, line })), ...current]),
        onChatSaid: (line) => setEntries((current) => [...current, { kind: 'chat', line }]),
        onRollHistory: (rolls) =>
          setEntries((current) => [...rolls.map((roll) => ({ kind: 'roll' as const, roll })), ...current]),
        onRolled: (roll) => setEntries((current) => [...current, { kind: 'roll', roll }]),
      })

      if (!cancelled) {
        setJoined(ok)
      }
    })()

    void (async () => {
      // Read from the server's answer about this campaign, not guessed from who is connected.
      const campaign = await getCampaign(id)

      if (!cancelled && campaign.kind === 'ok') {
        setIsMaster(campaign.value.role === 'Master')
      }
    })()

    return () => {
      cancelled = true
      void table.stop()
    }
  }, [id, sessionId])

  async function send(): Promise<void> {
    const ok = (await connection.current?.say(sessionId, body, voice)) ?? false

    setRejected(!ok)

    if (ok) {
      setBody('')
    }
  }

  async function rollDice(): Promise<void> {
    setRejected(!((await connection.current?.roll(sessionId, expression, visibility)) ?? false))
  }

  if (joined === null) {
    return <p>{t('table.connecting')}</p>
  }

  if (!joined) {
    return <p role="alert">{t('table.joinFailed')}</p>
  }

  return (
    <section>
      <h1>{t('table.title')}</h1>

      <h2>{t('table.participants')}</h2>
      <ul>
        {participants.map((participant) => (
          <li key={participant.userId}>{participant.username}</li>
        ))}
      </ul>

      <ul aria-label={t('table.title')}>
        {entries.map((entry) =>
          entry.kind === 'chat' ? (
            <li key={entry.line.id} data-voice={entry.line.voice}>
              <strong>{entry.line.authorUsername}</strong>{' '}
              <em>{t(`table.${entry.line.voice === 'InCharacter' ? 'inCharacter' : 'outOfCharacter'}`)}</em>{' '}
              {entry.line.body}
            </li>
          ) : (
            <li key={entry.roll.id}>
              <strong>{entry.roll.rollerUsername}</strong> {renderRoll(entry.roll)}
            </li>
          ),
        )}
      </ul>

      <label>
        {t('table.message')}
        <input value={body} onChange={(event) => setBody(event.target.value)} />
      </label>
      <label>
        {t('table.inCharacter')}
        <input
          type="checkbox"
          checked={voice === 'InCharacter'}
          onChange={(event) => setVoice(event.target.checked ? 'InCharacter' : 'OutOfCharacter')}
        />
      </label>
      <button type="button" onClick={() => void send()}>
        {t('table.say')}
      </button>

      <label>
        {t('table.expression')}
        <input value={expression} onChange={(event) => setExpression(event.target.value)} />
      </label>
      <label>
        {t('table.visibility')}
        <select value={visibility} onChange={(event) => setVisibility(event.target.value as RollVisibility)}>
          <option value="Public">{t('table.visibilities.Public')}</option>
          <option value="Private">{t('table.visibilities.Private')}</option>
          {/* Offered only to a Master. Not enforcement — the server refuses a player who asks
              anyway, which task 043 tests — but there is no point offering what will be refused. */}
          {isMaster && <option value="MasterOnly">{t('table.visibilities.MasterOnly')}</option>}
        </select>
      </label>
      <button type="button" onClick={() => void rollDice()}>
        {t('table.roll')}
      </button>

      {rejected && <p role="alert">{t('table.rejected')}</p>}
    </section>
  )
}
