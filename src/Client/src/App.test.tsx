import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import App from './App'

/**
 * The network is stubbed at `fetch`, not at the `fetchHealth` wrapper. Stubbing the wrapper would
 * only prove the component calls a function; stubbing `fetch` proves it handles what the server
 * actually sends — including the 503, which is a real answer rather than an error.
 */
function stubFetch(status: number, body: unknown): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(() =>
      Promise.resolve({
        ok: status >= 200 && status < 300,
        status,
        json: () => Promise.resolve(body),
      }),
    ),
  )
}

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

describe('App', () => {
  it('reports the database healthy when the server says so', async () => {
    stubFetch(200, { status: 'Healthy', checks: { database: 'Healthy' } })

    render(<App />)

    expect(await screen.findByRole('heading', { name: /server responded: healthy/i }))
      .toBeInTheDocument()
    expect(screen.getByText('database')).toBeInTheDocument()
  })

  it('distinguishes a reachable server reporting itself unhealthy', async () => {
    stubFetch(503, { status: 'Unhealthy', checks: { database: 'Unhealthy' } })

    render(<App />)

    // The server answered, so this is not an "unreachable" state — the distinction is the whole
    // point of the three-way result type.
    expect(await screen.findByRole('heading', { name: /server responded: unhealthy/i }))
      .toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /server unreachable/i })).not.toBeInTheDocument()
  })

  it('reports the server unreachable when the request fails', async () => {
    vi.stubGlobal('fetch', vi.fn(() => Promise.reject(new Error('connection refused'))))

    render(<App />)

    expect(await screen.findByRole('heading', { name: /server unreachable/i })).toBeInTheDocument()
    expect(screen.getByText(/connection refused/)).toBeInTheDocument()
  })
})
