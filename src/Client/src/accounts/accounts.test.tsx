import { cleanup, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import App from '../App'
import { initialiseI18n } from '../i18n'

/**
 * The network is stubbed at `fetch`, not at the API module: that proves these screens handle what
 * the server actually sends, including the status codes that mean different things.
 */
type Reply = { status: number; body?: unknown }

function stubFetch(routes: (url: string, method: string) => Reply): void {
  vi.stubGlobal(
    'fetch',
    vi.fn((input: string, init?: RequestInit) => {
      const url = String(input)

      // The shell asks for these on every signed-in render. Answering them here keeps each test's
      // routes about the thing it is testing.
      const reply =
        url === '/api/notifications' || url === '/api/campaigns' || url === '/api/campaigns/invitations'
          ? { status: 200, body: [] }
          : routes(url, init?.method ?? 'GET')

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

beforeEach(() => {
  initialiseI18n('en')
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('signing in', () => {
  it('shows the sign-in form when nobody is signed in', async () => {
    stubFetch(() => ({ status: 401 }))

    renderAt('/')

    expect(await screen.findByRole('heading', { name: /sign in/i })).toBeInTheDocument()
  })

  it('shows the signed-in view once the server reports a session', async () => {
    stubFetch(() => ({ status: 200, body: { id: 'a', username: 'Mattia' } }))

    renderAt('/')

    expect(await screen.findByText(/signed in as mattia/i)).toBeInTheDocument()
  })

  it('reports wrong credentials without guessing which half was wrong', async () => {
    stubFetch((url, method) =>
      url === '/api/session' && method === 'POST' ? { status: 401 } : { status: 401 },
    )

    renderAt('/')
    await screen.findByRole('heading', { name: /sign in/i })

    await userEvent.type(screen.getByLabelText(/username/i), 'Mattia')
    await userEvent.type(screen.getByLabelText(/password/i), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/do not match/i)

    // Still on the sign-in screen: a refusal must not leave the app looking signed in.
    expect(screen.getByRole('heading', { name: /sign in/i })).toBeInTheDocument()
  })

  it('sends a pending account to the awaiting-approval screen, not an error', async () => {
    // The distinction 013 built: a pending applicant has proved the account is theirs, so they get
    // told to wait rather than told their password is wrong.
    stubFetch((url, method) =>
      url === '/api/session' && method === 'POST'
        ? { status: 403, body: { title: 'awaiting_approval' } }
        : { status: 401 },
    )

    renderAt('/')
    await screen.findByRole('heading', { name: /sign in/i })

    await userEvent.type(screen.getByLabelText(/username/i), 'Newcomer')
    await userEvent.type(screen.getByLabelText(/password/i), 'a perfectly ordinary passphrase')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('heading', { name: /waiting for approval/i })).toBeInTheDocument()
  })

  it('says the server is unreachable rather than offering a sign-in form', async () => {
    // A dead backend is not "wrong password". Offering the form would invite typing a password at
    // nothing and concluding the credentials were bad.
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('connection refused'))))

    renderAt('/')

    expect(await screen.findByRole('alert')).toHaveTextContent(/cannot reach the server/i)
    expect(screen.queryByRole('heading', { name: /sign in/i })).not.toBeInTheDocument()
  })
})

describe('registering', () => {
  it('explains a link with no invitation on it', async () => {
    stubFetch(() => ({ status: 401 }))

    renderAt('/register')

    expect(await screen.findByRole('alert')).toHaveTextContent(/missing its invitation/i)
  })

  it('confirms what happens next on success', async () => {
    stubFetch((url) => (url === '/api/registration' ? { status: 201 } : { status: 401 }))

    renderAt('/register?token=abc')

    await userEvent.type(screen.getByLabelText(/username/i), 'Newcomer')
    await userEvent.type(screen.getByLabelText(/password/i), 'a perfectly ordinary passphrase')
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    expect(await screen.findByText(/waiting for an administrator/i)).toBeInTheDocument()
  })

  it('translates a server error code into a sentence', async () => {
    stubFetch((url) =>
      url === '/api/registration'
        ? { status: 400, body: { error: 'invite_expired' } }
        : { status: 401 },
    )

    renderAt('/register?token=abc')

    await userEvent.type(screen.getByLabelText(/username/i), 'Newcomer')
    await userEvent.type(screen.getByLabelText(/password/i), 'a perfectly ordinary passphrase')
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const alert = await screen.findByRole('alert')

    expect(alert).toHaveTextContent(/expired/i)
    // The raw code is for the client to translate, never for the user to read.
    expect(alert).not.toHaveTextContent('invite_expired')
  })

  it('falls back to a generic message for a code it has never heard of', async () => {
    stubFetch((url) =>
      url === '/api/registration'
        ? { status: 400, body: { error: 'something_new_from_the_server' } }
        : { status: 401 },
    )

    renderAt('/register?token=abc')

    await userEvent.type(screen.getByLabelText(/username/i), 'Newcomer')
    await userEvent.type(screen.getByLabelText(/password/i), 'a perfectly ordinary passphrase')
    await userEvent.click(screen.getByRole('button', { name: /create account/i }))

    const alert = await screen.findByRole('alert')

    expect(alert).toHaveTextContent(/something went wrong/i)
    expect(alert).not.toHaveTextContent('something_new_from_the_server')
  })
})

describe('the account queue', () => {
  const accounts = [
    { id: '1', username: 'Newcomer', state: 'Pending', role: 'Member', createdAt: '2026-08-05' },
    { id: '2', username: 'Mattia', state: 'Active', role: 'Admin', createdAt: '2026-08-01' },
  ]

  it('lists people waiting and offers a decision', async () => {
    stubFetch((url) =>
      url === '/api/admin/accounts'
        ? { status: 200, body: accounts }
        : { status: 200, body: { id: '2', username: 'Mattia' } },
    )

    renderAt('/admin/accounts')

    expect(await screen.findByRole('heading', { name: /^accounts$/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /approve/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /reject/i })).toBeInTheDocument()
  })

  it('reports a refusal rather than rendering an empty table', async () => {
    // A member reaching this route is refused by the server. The client renders what it was told,
    // and never decides for itself whether the call would have been allowed.
    stubFetch((url) =>
      url === '/api/admin/accounts'
        ? { status: 403 }
        : { status: 200, body: { id: '3', username: 'Player' } },
    )

    renderAt('/admin/accounts')

    expect(await screen.findByRole('alert')).toBeInTheDocument()
  })
})

describe('language', () => {
  it('switches every string without a reload', async () => {
    stubFetch(() => ({ status: 401 }))

    renderAt('/')
    await screen.findByRole('heading', { name: /sign in/i })

    await userEvent.selectOptions(screen.getByLabelText(/language/i), 'it')

    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /accedi/i })).toBeInTheDocument()
    })
  })
})
