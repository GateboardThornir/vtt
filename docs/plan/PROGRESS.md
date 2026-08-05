# Progress ledger

Updated at the end of every completed task, after review approval. This file is how work resumes
across sessions — a Claude Code session starts with no memory of the last one, and this is what it
reads to find out where things stand.

Keep entries short. One line per task, plus notes only where something surprising happened.

## Current state

**Phase:** 0 — Foundations
**Next task:** 004 — Frontend scaffold
**Blocked on:** nothing

## Completed

<!-- Format: | ID | Task | Date | Notes | -->

| ID | Task | Date | Notes |
|---|---|---|---|
| 001 | Repository scaffold | 2026-07-29 | Layout and build conventions in ADR 001. Server on port 5080, `/health` live |
| 002 | Docker Compose dev environment | 2026-08-05 | Postgres 18 on host port 55432, `.env` + `scripts/dev-server.sh`. Arrangement in ADR 002 |
| 003 | EF Core setup + initial migration | 2026-08-05 | `VttDbContext`, `scripts/ef.sh`, empty `InitialCreate`. Migrations applied explicitly (ADR 003); snake_case / `timestamptz` / explicit `jsonb` (ADR 004) |

## Deviations from the plan

Record every departure from `roadmap.md` and why. A plan that quietly diverges from reality is
worse than no plan.

| Task | What changed | Why |
|---|---|---|
| — | — | — |

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
| Bounding the `/health` database-check timeout | 101 | Measured in 003: when the server starts while Postgres is unreachable, every probe takes ~16s — Npgsql's default `Timeout=15`. The other paths are fast (200 in ~0.4s; 503 in ~0.06s when the database dies under a running server). Harmless in development, but Caddy will front `/health` with its own timeout, so 101 is where a hanging probe actually costs something |
