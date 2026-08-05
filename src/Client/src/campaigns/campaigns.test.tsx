import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'
import { initialiseI18n } from '../i18n'

type Reply = { status: number; body?: unknown }

const session = { id: 'u1', username: 'Mattia' }

function stubFetch(routes: (url: string, method: string) => Reply | undefined): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: string, init?: RequestInit) => {
      const url = String(input)
      const method = init?.method ?? 'GET'

      const reply =
        routes(url, method) ??
        (url === '/api/session'
          ? { status: 200, body: session }
          : { status: 200, body: [] })

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

describe('the campaign list', () => {
  it('says so when you are in none', async () => {
    stubFetch(() => undefined)

    renderAt('/campaigns')

    expect(await screen.findByText(/not in any campaign/i)).toBeInTheDocument()
  })

  it('shows what you are to each campaign', async () => {
    stubFetch((url) =>
      url === '/api/campaigns'
        ? {
            status: 200,
            body: [
              {
                id: 'c1',
                name: 'Curse of Strahd',
                systemId: 'dnd5e',
                systemVersion: '1.0',
                createdAt: '2026-08-05',
                role: 'Master',
              },
            ],
          }
        : undefined,
    )

    renderAt('/campaigns')

    expect(await screen.findByRole('link', { name: 'Curse of Strahd' })).toBeInTheDocument()
    expect(screen.getByText(/Master/)).toBeInTheDocument()
    expect(screen.getByText(/dnd5e 1\.0/)).toBeInTheDocument()
  })

  it('offers to accept or decline an invitation', async () => {
    stubFetch((url) =>
      url === '/api/campaigns/invitations'
        ? {
            status: 200,
            body: [
              {
                id: 'c2',
                name: 'Rime',
                systemId: 'dnd5e',
                systemVersion: '1.0',
                createdAt: '2026-08-05',
                role: 'Player',
              },
            ],
          }
        : undefined,
    )

    renderAt('/campaigns')

    expect(await screen.findByRole('button', { name: /accept/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /decline/i })).toBeInTheDocument()
  })

  it('offers the registered systems rather than asking you to type one', async () => {
    // The bug this closes: the form took a system id and a version as free text, so a typo
    // produced a campaign whose pin resolved to nothing — and no character could ever be created
    // in it. The failure surfaced much later than the mistake.
    stubFetch((url) =>
      url === '/api/systems'
        ? { status: 200, body: [{ systemId: 'dnd5e', version: '1.0.0' }] }
        : undefined,
    )

    renderAt('/campaigns')

    const chooser = await screen.findByLabelText(/game system/i)

    expect(chooser.tagName).toBe('SELECT')
    expect(screen.getByRole('option', { name: /dnd5e 1\.0\.0/ })).toBeInTheDocument()
  })
})

describe('a campaign', () => {
  const campaign = {
    id: 'c1',
    name: 'Curse of Strahd',
    systemId: 'dnd5e',
    systemVersion: '1.0',
    createdAt: '2026-08-05',
    role: 'Master',
  }

  const roster = [
    { userId: 'u1', username: 'Mattia', role: 'Master', state: 'Active' },
    { userId: 'u2', username: 'Amico', role: 'Player', state: 'Active' },
  ]

  it('shows the roster, and lets a Master invite', async () => {
    stubFetch((url) =>
      url === '/api/campaigns/c1'
        ? { status: 200, body: campaign }
        : url === '/api/campaigns/c1/roster'
          ? { status: 200, body: roster }
          : undefined,
    )

    renderAt('/campaigns/c1')

    expect(await screen.findByRole('heading', { name: 'Curse of Strahd' })).toBeInTheDocument()
    expect(screen.getByText(/Amico/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /^invite$/i })).toBeInTheDocument()
  })

  it('offers a Player leaving rather than inviting', async () => {
    stubFetch((url) =>
      url === '/api/campaigns/c1'
        ? { status: 200, body: { ...campaign, role: 'Player' } }
        : url === '/api/campaigns/c1/roster'
          ? { status: 200, body: roster }
          : undefined,
    )

    renderAt('/campaigns/c1')

    expect(await screen.findByRole('button', { name: /leave/i })).toBeInTheDocument()
    // Not authorisation — the server refuses a Player who calls the endpoint anyway. This only
    // avoids offering a control that would fail.
    expect(screen.queryByRole('button', { name: /^invite$/i })).not.toBeInTheDocument()
  })

  it('says a campaign is missing rather than showing a blank page', async () => {
    // The server answers 404 for a campaign you may not see, and never confirms one exists.
    stubFetch((url) => (url.startsWith('/api/campaigns/c9') ? { status: 404 } : undefined))

    renderAt('/campaigns/c9')

    expect(await screen.findByRole('alert')).toHaveTextContent(/does not exist, or you are not in it/i)
  })

  it('explains why an invitation was refused', async () => {
    stubFetch((url, method) =>
      url === '/api/campaigns/c1' && method === 'GET'
        ? { status: 200, body: campaign }
        : url === '/api/campaigns/c1/roster' && method === 'GET'
          ? { status: 200, body: roster }
          : url === '/api/campaigns/c1/roster' && method === 'POST'
            ? { status: 400 }
            : undefined,
    )

    renderAt('/campaigns/c1')
    await screen.findByRole('heading', { name: 'Curse of Strahd' })

    await userEvent.type(screen.getByLabelText(/username/i), 'Nobody')
    await userEvent.click(screen.getByRole('button', { name: /^invite$/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/no active account/i)
  })
})

describe('notifications', () => {
  const invitation = {
    id: 'n1',
    kind: 'CampaignInvitation',
    subject: 'Curse of Strahd',
    createdAt: '2026-08-05',
    read: false,
  }

  it('renders a sentence from a kind and its parameter', async () => {
    // The server sends no prose at all, so the client owns the wording in each language.
    stubFetch((url) => (url === '/api/notifications' ? { status: 200, body: [invitation] } : undefined))

    renderAt('/')

    await userEvent.click(await screen.findByRole('button', { name: /notifications/i }))

    expect(screen.getByText(/you have been invited to curse of strahd/i)).toBeInTheDocument()
  })

  it('renders the same notification in Italian', async () => {
    stubFetch((url) => (url === '/api/notifications' ? { status: 200, body: [invitation] } : undefined))

    renderAt('/')
    await screen.findByRole('button', { name: /notifications/i })

    await userEvent.selectOptions(screen.getByLabelText(/language/i), 'it')
    await userEvent.click(screen.getByRole('button', { name: /notifiche/i }))

    await waitFor(() => {
      expect(screen.getByText(/sei stato invitato a curse of strahd/i)).toBeInTheDocument()
    })
  })

  it('counts what is unread', async () => {
    stubFetch((url) =>
      url === '/api/notifications' ? { status: 200, body: [invitation, { ...invitation, id: 'n2' }] } : undefined,
    )

    renderAt('/')

    expect(await screen.findByRole('button', { name: /2 unread/i })).toBeInTheDocument()
  })
})
