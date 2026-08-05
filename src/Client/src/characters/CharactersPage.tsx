import { useEffect, useState, type JSX } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useParams } from 'react-router'
import { createCharacter, listCharacters, type CharacterSummary } from '../api/characters'
import { emptySheet, forSaving } from './sheet'

export function CharactersPage(): JSX.Element {
  const { t } = useTranslation()
  const { id = '' } = useParams()

  const [characters, setCharacters] = useState<CharacterSummary[] | null>(null)
  const [name, setName] = useState('')

  async function load(): Promise<void> {
    const result = await listCharacters(id)

    setCharacters(result.kind === 'ok' ? result.value : [])
  }

  useEffect(() => {
    let cancelled = false

    void (async () => {
      const result = await listCharacters(id)

      if (!cancelled) {
        setCharacters(result.kind === 'ok' ? result.value : [])
      }
    })()

    return () => {
      cancelled = true
    }
  }, [id])

  async function create(): Promise<void> {
    await createCharacter(id, name, forSaving(emptySheet(name)))
    setName('')
    await load()
  }

  if (characters === null) {
    return <p>{t('common.loading')}</p>
  }

  return (
    <section>
      <h1>{t('characters.title')}</h1>

      {characters.length === 0 ? (
        <p>{t('characters.none')}</p>
      ) : (
        <ul>
          {characters.map((character) => (
            <li key={character.id}>
              <Link to={`/campaigns/${id}/characters/${character.id}`}>{character.name}</Link>
            </li>
          ))}
        </ul>
      )}

      <h2>{t('characters.create')}</h2>
      <label>
        {t('characters.name')}
        <input value={name} onChange={(event) => setName(event.target.value)} />
      </label>
      <button type="button" onClick={() => void create()}>
        {t('characters.create')}
      </button>
    </section>
  )
}
