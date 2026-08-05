import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'
import { initialiseI18n } from '../i18n'

type Reply = { status: number; body?: unknown }

const sheet = {
  identity: { name: 'Ireena', level: 3 },
  abilities: { strength: 16, dexterity: 14, constitution: 15, intelligence: 10, wisdom: 12, charisma: 13 },
  proficiencyBonus: 2,
  hitPoints: { current: 28, maximum: 28, temporary: 0 },
  armourClass: 16,
  derived: {
    abilityModifiers: { strength: 3, dexterity: 2, constitution: 2, intelligence: 0, wisdom: 1, charisma: 1 },
    savingThrows: {},
    skills: { athletics: 5 },
    passivePerception: 13,
  },
}

const character = {
  id: 'ch1',
  name: 'Ireena',
  ownerUserId: 'u1',
  updatedAt: '2026-08-05',
  sheet: JSON.stringify(sheet),
}

function stubFetch(routes: (url: string, method: string) => Reply | undefined): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: string, init?: RequestInit) => {
      const url = String(input)
      const reply =
        routes(url, init?.method ?? 'GET') ??
        (url === '/api/session' ? { status: 200, body: { id: 'u1', username: 'Mattia' } } : { status: 200, body: [] })

      return Promise.resolve({
        ok: reply.status >= 200 && reply.status < 300,
        status: reply.status,
        text: () => Promise.resolve(reply.body === undefined ? '' : JSON.stringify(reply.body)),
        json: () => Promise.resolve(reply.body ?? {}),
      })
    }),
  )
}

function renderAt(path: string): void {
  render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

beforeEach(() => initialiseI18n('en'))

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('the character sheet', () => {
  it('shows the modifiers the server computed', async () => {
    stubFetch((url) => (url.endsWith('/characters/ch1') ? { status: 200, body: character } : undefined))

    renderAt('/campaigns/c1/characters/ch1')

    // Displayed, never calculated here: the client computes no rule, and a modifier implemented in
    // two places will eventually disagree in one of them.
    expect(await screen.findByLabelText(/strength/i)).toHaveValue(16)
    expect(screen.getByText('+3')).toBeInTheDocument()
    expect(screen.getByText('13')).toBeInTheDocument()
  })

  it('presents derived values as output rather than as inputs', async () => {
    stubFetch((url) => (url.endsWith('/characters/ch1') ? { status: 200, body: character } : undefined))

    renderAt('/campaigns/c1/characters/ch1')
    await screen.findByLabelText(/strength/i)

    expect(screen.getByText(/computed from the ones above/i)).toBeInTheDocument()
  })

  it('takes the new modifier from the server after saving, not from its own arithmetic', async () => {
    const saved = {
      ...character,
      sheet: JSON.stringify({
        ...sheet,
        abilities: { ...sheet.abilities, strength: 20 },
        derived: { ...sheet.derived, abilityModifiers: { ...sheet.derived.abilityModifiers, strength: 5 } },
      }),
    }

    stubFetch((url, method) =>
      url.endsWith('/characters/ch1') && method === 'GET'
        ? { status: 200, body: character }
        : url.endsWith('/characters/ch1') && method === 'PUT'
          ? { status: 200, body: saved }
          : undefined,
    )

    renderAt('/campaigns/c1/characters/ch1')
    await screen.findByLabelText(/strength/i)

    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    // Queried against the strength row specifically: the athletics total is also +5, and a bare
    // text match would pass whichever one it found first.
    const strength = await screen.findByLabelText(/strength/i)

    expect(strength.parentElement?.parentElement).toHaveTextContent('+5')
    expect(strength).toHaveValue(20)
  })

  it('shows a schema error against the field it names', async () => {
    stubFetch((url, method) =>
      url.endsWith('/characters/ch1') && method === 'GET'
        ? { status: 200, body: character }
        : url.endsWith('/characters/ch1') && method === 'PUT'
          ? {
              status: 400,
              body: {
                error: 'sheet_invalid',
                errors: [{ path: '/abilities/strength', message: 'Value is not an integer.' }],
              },
            }
          : undefined,
    )

    renderAt('/campaigns/c1/characters/ch1')
    await screen.findByLabelText(/strength/i)

    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    // The path is what makes a schema failure fixable rather than a shrug.
    expect(await screen.findByRole('alert')).toHaveTextContent(/not an integer/i)
  })

  it('says so when the character belongs to somebody else', async () => {
    stubFetch((url, method) =>
      url.endsWith('/characters/ch1') && method === 'GET'
        ? { status: 200, body: character }
        : url.endsWith('/characters/ch1') && method === 'PUT'
          ? { status: 403 }
          : undefined,
    )

    renderAt('/campaigns/c1/characters/ch1')
    await screen.findByLabelText(/strength/i)

    await userEvent.click(screen.getByRole('button', { name: /save/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/belongs to somebody else/i)
  })

  it('reports a character it cannot see rather than showing an empty sheet', async () => {
    stubFetch((url) => (url.endsWith('/characters/ch9') ? { status: 404 } : undefined))

    renderAt('/campaigns/c1/characters/ch9')

    expect(await screen.findByRole('alert')).toHaveTextContent(/does not exist, or you cannot see it/i)
  })
})

describe('the character list', () => {
  it('says so when a campaign has none', async () => {
    stubFetch(() => undefined)

    renderAt('/campaigns/c1/characters')

    expect(await screen.findByText(/no characters yet/i)).toBeInTheDocument()
  })

  it('links to each character', async () => {
    stubFetch((url) =>
      url === '/api/campaigns/c1/characters' ? { status: 200, body: [character] } : undefined,
    )

    renderAt('/campaigns/c1/characters')

    expect(await screen.findByRole('link', { name: 'Ireena' })).toBeInTheDocument()
  })
})
