import { useEffect, useState, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { useParams } from 'react-router'
import { getCharacter, saveCharacter, type SheetError } from '../api/characters'
import { abilities, forSaving, parseSheet, signed, type Ability, type Sheet } from './sheet'

export function CharacterSheetPage(): JSX.Element {
  const { t } = useTranslation()
  const { id = '', characterId = '' } = useParams()

  const [sheet, setSheet] = useState<Sheet | null>(null)
  const [name, setName] = useState('')
  const [missing, setMissing] = useState(false)
  const [errors, setErrors] = useState<SheetError[]>([])
  const [saved, setSaved] = useState(false)

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const result = await getCharacter(id, characterId)

      if (cancelled) {
        return
      }

      if (result.kind === 'ok') {
        setSheet(parseSheet(result.value.sheet))
        setName(result.value.name)
      } else {
        setMissing(true)
      }
    })()

    return () => {
      cancelled = true
    }
  }, [id, characterId])

  async function save(): Promise<void> {
    if (sheet === null) {
      return
    }

    setErrors([])
    setSaved(false)

    const result = await saveCharacter(id, characterId, name, forSaving(sheet))

    if (result.kind === 'ok') {
      // Reloaded from the server's answer rather than kept locally: the derived values are the
      // module's arithmetic, and the client never computes a rule.
      setSheet(parseSheet(result.value.sheet))
      setSaved(true)
      return
    }

    if (result.status === 403) {
      setErrors([{ path: '/', message: t('characters.notYours') }])
      return
    }

    setErrors(result.errors ?? [{ path: '/', message: t('common.unexpectedError') }])
  }

  if (missing) {
    return <p role="alert">{t('characters.notFound')}</p>
  }

  if (sheet === null) {
    return <p>{t('common.loading')}</p>
  }

  const derived = sheet.derived ?? {}

  function setAbility(ability: Ability, value: number): void {
    setSheet({ ...sheet!, abilities: { ...sheet!.abilities, [ability]: value } })
  }

  /** The message for one ability input, when the server blamed that path. */
  function errorFor(ability: Ability): string | undefined {
    return errors.find((error) => error.path.endsWith(`/${ability}`))?.message
  }

  /**
   * Anything this screen cannot pin to a field, shown at the bottom rather than swallowed.
   *
   * Stated as "not one of the ability paths" rather than "path is /", because the server may name
   * a path for a field this screen does not render and that message still has to reach somebody.
   */
  function generalError(): string | undefined {
    return errors.find(
      (error) => !abilities.some((ability) => error.path.endsWith(`/${ability}`)),
    )?.message
  }

  return (
    <section>
      <h1>{name}</h1>

      <label>
        {t('characters.name')}
        <input value={name} onChange={(event) => setName(event.target.value)} />
      </label>

      <h2>{t('characters.abilities')}</h2>
      {abilities.map((ability) => (
        <div key={ability}>
          <label>
            {t(`characters.${ability}`)}
            <input
              type="number"
              value={sheet.abilities[ability]}
              onChange={(event) => setAbility(ability, Number(event.target.value))}
            />
          </label>
          {/* Read-only, and visibly so: the point of the screen is that you can see which numbers
              you choose and which the server derives from them. */}
          <output>{signed(derived.abilityModifiers?.[ability] ?? 0)}</output>
          {errorFor(ability) !== undefined && <span role="alert">{errorFor(ability)}</span>}
        </div>
      ))}

      <label>
        {t('characters.proficiencyBonus')}
        <input
          type="number"
          value={sheet.proficiencyBonus}
          onChange={(event) => setSheet({ ...sheet, proficiencyBonus: Number(event.target.value) })}
        />
      </label>

      <label>
        {t('characters.armourClass')}
        <input
          type="number"
          value={sheet.armourClass}
          onChange={(event) => setSheet({ ...sheet, armourClass: Number(event.target.value) })}
        />
      </label>

      <h2>{t('characters.derived')}</h2>
      <p>{t('characters.readOnly')}</p>
      <p>
        {t('characters.passivePerception')}: <output>{derived.passivePerception ?? 0}</output>
      </p>
      <ul>
        {Object.entries(derived.skills ?? {}).map(([skill, total]) => (
          <li key={skill}>
            {skill}: <output>{signed(total)}</output>
          </li>
        ))}
      </ul>

      <button type="button" onClick={() => void save()}>
        {t('characters.save')}
      </button>

      {saved && <p>{t('characters.saved')}</p>}
      {generalError() !== undefined && <p role="alert">{generalError()}</p>}
    </section>
  )
}
