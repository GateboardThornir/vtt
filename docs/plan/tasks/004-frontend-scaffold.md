# 004 — Frontend scaffold

**Status:** done
**Depends on:** 001 (003 supplies the payload the page renders)
**Branch:** `task/004-frontend-scaffold`

## Goal

`npm run dev` serves a React application that reaches the ASP.NET server through the Vite dev
proxy, so both halves of the stack run side by side on one developer machine. Every UI task from
017 onward assumes this scaffold, and the proxy arrangement is what makes cookie sessions work at
task 013 without CORS.

## Scope

In scope:
- Vite + React + TypeScript in `src/Client`, replacing the placeholder README from task 001
- TypeScript `strict`, plus the flags that catch what `strict` alone does not
  (`noUncheckedIndexedAccess`, `noFallthroughCasesInSwitch`, `noImplicitOverride`)
- ESLint + the TypeScript plugin, configured to enforce the rules in `.claude/rules/frontend.md`
  that a linter can actually enforce: no `any`, explicit return types on exported functions,
  no class components
- Dev proxy in `vite.config.ts` forwarding `/api` to `http://localhost:5080`, `changeOrigin` off so
  cookies stay first-party
- One rendered page that fetches the server's health endpoint through the proxy and displays the
  result — the smallest thing that proves the whole path rather than just that React mounts
- `.nvmrc` pinning the Node major, matching the nvm workflow in `docs/dev-setup.md` §4
- `README.md` and `docs/dev-setup.md`: run the frontend, run both halves at once, what the proxy does

Explicitly out of scope (and which task covers it instead):
- **Vitest and any test file** — task 005 owns the test harness on both sides. This card ships a
  scaffold whose tests do not exist yet, exactly as task 001 did for the backend
- Routing, a component library, a design system, global state management — nothing yet has two
  pages or shared state to justify any of them. Routing arrives with task 017's auth screens
- Any call to a real API endpoint. `/health` is the only endpoint that exists
- SignalR client wiring — task 040
- PixiJS — task 052. It must not appear in `package.json` here
- Serving the built assets from ASP.NET, and the production build pipeline — task 101
- A lint gate in CI — task 006, mirroring how task 001 shipped `.editorconfig` and left
  `dotnet format --verify-no-changes` to CI

## Conflict resolved before implementation

**Settled 2026-08-05: option 1 below was chosen.** i18next stays out of 004, and the
infrastructure moves from task 098 to task 017. `roadmap.md` is amended accordingly and
`PROGRESS.md` records the deviation. The original analysis is kept below because the reasoning is
what makes the amendment defensible later.

---


**i18n has three sources saying different things, and this card is where they first collide.**

- `.claude/rules/frontend.md` (binding): *"All user-facing strings go through the translation layer
  from the first line of UI code — retrofitting i18n is far more expensive than doing it inline."*
- `roadmap.md` task 098 (Phase 3): *"i18next **infrastructure**, full IT + EN string coverage,
  language switcher"* — the infrastructure is owned by a task two phases away.
- `functional-spec.md` §10: *"internationalization infrastructure in place from the first release."*

This card is the first line of UI code. Taken literally, the rule requires i18next here; the
roadmap assigns that same infrastructure to 098.

**Recommendation: keep i18next out of 004, and move the infrastructure to task 017 rather than
leaving it at 098.** The scaffold's page is a diagnostic that gets deleted the moment real screens
exist, so translating it buys nothing. But 017 (auth UI) is the first screen a user actually reads,
and every UI task after it compounds the retrofit. Leaving the infrastructure at 098 means Phase 1
and Phase 2 ship hundreds of hardcoded strings and 098 becomes an archaeology exercise — which is
the outcome the rule was written to prevent, just delayed.

That is a change to `roadmap.md`, so it needs an explicit decision and a row in the Deviations
table of `PROGRESS.md`.

## Approach

**Proxy, not CORS.** The Vite dev server serves the app on one port and the API on another; the
proxy makes the browser see a single origin. This is not a convenience — task 013 uses `HttpOnly`,
`SameSite=Lax` session cookies, and cross-origin XHR does not carry those without `credentials`,
matching CORS headers, and `SameSite=None`, which in turn requires HTTPS in development. Proxying
sidesteps all of it and makes development resemble production, where Caddy serves both from one
origin anyway.

**The page proves the path, not the framework.** Rendering "Hello world" would prove Vite works.
Fetching `/api/health` and showing whether the server and its database are reachable proves Vite,
the proxy, the server and the database are all wired together — the thing that will actually be
broken on someone's machine. It needs a server-side route change: `/health` is currently at the
root, and the proxy rule covers `/api`. Decide in plan mode whether to move the endpoint under
`/api` or to proxy `/health` explicitly; moving it is tidier and costs one line in `Program.cs` plus
a docs update, but it does change a URL that `README.md`, `docs/dev-setup.md` and ADR 002 all cite.

**Two processes, deliberately.** `scripts/dev-server.sh` in one terminal, `npm run dev` in another.
No orchestration script combining them — interleaved output from two watchers is harder to read than
two windows, and the first thing anyone does when a stack script misbehaves is run the halves
separately anyway.

## Acceptance criteria

- [ ] `npm install && npm run dev` in `src/Client` serves the app, and the URL is printed
- [ ] With `scripts/dev-server.sh` running, the page shows the server as reachable and reports the
      database status it received; with the server stopped, it shows a failure rather than a blank
      screen or an unhandled rejection
- [ ] The browser makes a **same-origin** request — the network tab shows the Vite origin, not
      `localhost:5080`
- [ ] `npx tsc --noEmit` passes; introducing an `any` or an untyped export fails lint
- [ ] `npm run build` produces a production bundle into `dist/`
- [ ] Editing a component hot-reloads without a full page refresh
- [ ] `git status` clean: `node_modules/` and `dist/` are ignored, `package-lock.json` is committed
- [ ] `dotnet build` and `dotnet test` still pass with zero warnings

## Concepts to explain

- **What Vite actually does**, and why it is not webpack: native ES modules served unbundled in
  development so startup does not scale with project size, esbuild for dependency pre-bundling,
  Rollup for the production build — and why development and production therefore run different
  machinery, which is where a class of "works in dev, breaks in build" bugs comes from
- **Hot Module Replacement**: what gets swapped, what forces a full reload, and why component state
  sometimes survives an edit and sometimes does not
- **What a dev proxy is** — the dev server acting as reverse proxy — and the same-origin policy it
  exists to sidestep. Why this matters specifically for cookie authentication at task 013
- **TypeScript `strict` is a bundle of flags**, not one switch. What `strictNullChecks` changes
  about how you write code, and what `noUncheckedIndexedAccess` catches that `strict` does not
- **React function components and hooks**: what a re-render is, why it is not a DOM rebuild, and
  the mental model of state as a snapshot per render
- **`StrictMode` deliberately double-invokes** effects and renders in development to surface impure
  code. It is not a bug, and it will look like one at tasks 040 and 052
- **Where Vite's environment variables come from** and why only `VITE_`-prefixed ones are exposed —
  they are inlined into the bundle at build time and are therefore public by construction

## Risks and things to watch

- **`VITE_`-prefixed variables are baked into the shipped JavaScript.** They are not configuration,
  they are published content. No secret, connection string or token may ever be one, and the habit
  needs to form here rather than at task 101.
- **`StrictMode`'s double effect invocation** will look like a defect the first time a Pixi canvas
  is mounted twice (052) or a SignalR connection is opened twice (040). Both tasks need explicit
  cleanup in the effect. Worth writing down now, because the symptom is bizarre and the cause is
  invisible.
- The proxy exists **only in the Vite dev server**. The production build has no proxy — Caddy plays
  that role (task 101). Any code that assumes a relative `/api` path keeps working; any code that
  hardcodes `localhost:5080` breaks in production and nowhere else.
- **Node version drift.** `.nvmrc` records the intended major, but nothing enforces it. A different
  Node on another machine, or after `nvm install --lts` pulls a newer one, changes lockfile
  resolution. The failure is confusing and remote — CI (006) is where it should be caught.
- Do not let the scaffold acquire dependencies "since we'll need them anyway". A router, a state
  library and a component kit chosen now are three decisions made with no requirements in hand, and
  each one is harder to remove than to add.
- `npm run dev` runs from `src/Client`, not the repository root. `CLAUDE.md` already says so; the
  README should not contradict it.
