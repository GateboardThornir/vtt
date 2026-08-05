# Client

React + TypeScript frontend, built with Vite. Landed in task 004.

```bash
npm install
npm run dev        # http://localhost:5173
```

The server must be running separately — `./scripts/dev-server.sh` from the repository root, in its
own terminal. There is deliberately no script that starts both: interleaved output from two watchers
is harder to read than two windows, and the first thing anyone does when a combined script
misbehaves is run the halves separately anyway.

| Script | What it does |
|---|---|
| `npm run dev` | Vite dev server with HMR |
| `npm run build` | Type-checks, then builds to `dist/` |
| `npm run preview` | Serves the built output — no proxy, so `/api` calls will fail |
| `npm run lint` | ESLint |
| `npm run typecheck` | `tsc` with no emit |

## The dev proxy

`vite.config.ts` forwards `/api` to `http://localhost:5080`, so the browser only ever sees one
origin. This exists for cookies: task 013 issues `HttpOnly`, `SameSite=Lax` session cookies, which a
cross-origin request would not carry without `SameSite=None` and therefore HTTPS in development.

Consequences worth remembering:

- **Always use relative paths** (`/api/...`). Anything hardcoding `localhost:5080` works in
  development and breaks in production, where Caddy plays the proxy's role (task 101).
- **`npm run preview` has no proxy.** It serves the production build, so API calls fail there. That
  is expected, not a bug.
- **Only `VITE_`-prefixed environment variables reach the client**, and they are inlined into the
  bundle at build time. They are published content, not configuration — never a secret.

## Linting

ESLint with `typescript-eslint`, rather than the Oxlint config `create-vite` now scaffolds by
default. Oxlint is faster, but ESLint is what enforces the three rules in
`.claude/rules/frontend.md` that a linter can enforce at all — no `any`, explicit return types on
exported functions, no class components — and it is the toolchain with the documentation behind it.

The cost: `typescript-eslint` declares `typescript >=4.8.4 <6.1.0`, so TypeScript stays on 6.0.x
until it supports 7.

## What is deliberately not here

No router, no state management library, no component kit, no test runner, no PixiJS. Each is a
decision that wants requirements behind it: routing and i18n arrive with the auth screens (017),
Vitest with the test harness (005), PixiJS with the canvas (052).
