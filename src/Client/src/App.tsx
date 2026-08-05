import { useEffect, useState, type JSX } from 'react'
import { fetchHealth, type HealthResult } from './api/health'

export default function App(): JSX.Element {
  const [result, setResult] = useState<HealthResult | null>(null)

  useEffect(() => {
    // StrictMode runs this effect twice in development to surface missing cleanup. Aborting on
    // teardown is what makes that harmless: the first request is cancelled instead of racing the
    // second one to setState. Tasks 040 and 052 need the same discipline for a SignalR connection
    // and a Pixi canvas, where a leaked second instance is far more visible.
    const controller = new AbortController()

    void fetchHealth(controller.signal).then((next) => {
      if (!controller.signal.aborted) {
        setResult(next)
      }
    })

    return () => {
      controller.abort()
    }
  }, [])

  return (
    <main>
      <h1>VTT</h1>
      <p className="subtitle">
        Frontend scaffold. This page exists to prove the dev proxy reaches the server — it is
        replaced by real screens at task 017.
      </p>
      <Status result={result} />
    </main>
  )
}

function Status({ result }: { result: HealthResult | null }): JSX.Element {
  if (result === null) {
    return <p>Contacting the server…</p>
  }

  if (result.kind === 'unreachable') {
    return (
      <section className="status bad">
        <h2>Server unreachable</h2>
        <p>
          <code>GET /api/health</code> did not complete: {result.reason}
        </p>
        <p>Is <code>scripts/dev-server.sh</code> running?</p>
      </section>
    )
  }

  return (
    <section className={result.ok ? 'status good' : 'status bad'}>
      <h2>Server responded: {result.report.status}</h2>
      <dl>
        {Object.entries(result.report.checks).map(([name, status]) => (
          <div key={name}>
            <dt>{name}</dt>
            <dd>{status}</dd>
          </div>
        ))}
      </dl>
    </section>
  )
}
