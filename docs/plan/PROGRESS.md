# Progress ledger

Updated at the end of every completed task, after review approval. This file is how work resumes
across sessions — a Claude Code session starts with no memory of the last one, and this is what it
reads to find out where things stand.

Keep entries short. One line per task, plus notes only where something surprising happened.

## Current state

**Phase:** 0 — Foundations
**Next task:** 005 — Test harness
**Blocked on:** nothing

## Completed

<!-- Format: | ID | Task | Date | Notes | -->

| ID | Task | Date | Notes |
|---|---|---|---|
| 001 | Repository scaffold | 2026-07-29 | Layout and build conventions in ADR 001. Server on port 5080, `/health` live |
| 002 | Docker Compose dev environment | 2026-08-05 | Postgres 18 on host port 55432, `.env` + `scripts/dev-server.sh`. Arrangement in ADR 002 |
| 003 | EF Core setup + initial migration | 2026-08-05 | `VttDbContext`, `scripts/ef.sh`, empty `InitialCreate`. Migrations applied explicitly (ADR 003); snake_case / `timestamptz` / explicit `jsonb` (ADR 004) |
| 004 | Frontend scaffold | 2026-08-05 | Vite 8 + React 19 + TS 6 on port 5173, `/api` proxied to 5080. Health endpoint moved to `/api/health`. ESLint kept over the template's new Oxlint default, which pins TS to 6.0.x |

## Deviations from the plan

Record every departure from `roadmap.md` and why. A plan that quietly diverges from reality is
worse than no plan.

| Task | What changed | Why |
|---|---|---|
| 004 | i18next infrastructure moved from 098 to 017; 098 keeps full IT + EN coverage and the switcher | `.claude/rules/frontend.md` requires a translation layer from the first line of UI code, and the spec wants the infrastructure from the first release — but the roadmap parked it in Phase 3. Leaving it at 098 would ship every Phase 1 and 2 screen with hardcoded strings and turn 098 into archaeology. 017 is the first screen a user actually reads, so it lands there. 004 itself ships no i18next: its page is a diagnostic that 017 deletes |
| 004 | `GET /health` became `GET /api/health` | One proxy prefix in development and one Caddy rule at 101. A root-level path would also be swallowed by the SPA's index.html fallback for unknown routes unless special-cased |

## Open questions

Things surfaced during implementation that need a decision before they block something.

| Question | Raised in | Status |
|---|---|---|
| — | — | — |

## Deferred

Work consciously postponed, with the task it should attach to.

| Item | Deferred to | Reason |
|---|---|---|
| Automated test for `GET /health` | 005 | Endpoint testing is 005's subject; 001 ships a placeholder test and verifies `/health` by hand |
| Connection string for IDE-launched debugging | when it first bites | `scripts/dev-server.sh` covers the terminal; `appsettings.Development.local.json` is already gitignored for the other case |
| `dotnet format --verify-no-changes` gate | 006 | Style is not enforced by the build, so CI is where drift gets caught |
| Frontend lint gate (`npm run lint`, `npm run typecheck`) | 006 | Same reasoning as the backend format gate — CI is where drift gets caught |
| TypeScript 7 upgrade | when `typescript-eslint` supports it | 004 holds TS at 6.0.x because `typescript-eslint` declares `>=4.8.4 <6.1.0`. TS 7's native compiler is stable and much faster; the alternative unlock is switching to Oxlint, which `create-vite` now scaffolds by default but which cannot enforce all of `.claude/rules/frontend.md` |
| Bounding the `/health` database-check timeout | 101 | Measured in 003: when the server starts while Postgres is unreachable, every probe takes ~16s — Npgsql's default `Timeout=15`. The other paths are fast (200 in ~0.4s; 503 in ~0.06s when the database dies under a running server). Harmless in development, but Caddy will front `/health` with its own timeout, so 101 is where a hanging probe actually costs something |
