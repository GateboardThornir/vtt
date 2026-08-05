# Progress ledger

Updated at the end of every completed task, after review approval. This file is how work resumes
across sessions — a Claude Code session starts with no memory of the last one, and this is what it
reads to find out where things stand.

Keep entries short. One line per task, plus notes only where something surprising happened.

## Current state

**Phase:** 1 — First playable (Phase 0 complete)
**Next task:** 024 — Session entity
**Blocked on:** nothing

## Completed

<!-- Format: | ID | Task | Date | Notes | -->

| ID | Task | Date | Notes |
|---|---|---|---|
| 001 | Repository scaffold | 2026-07-29 | Layout and build conventions in ADR 001. Server on port 5080, `/health` live |
| 002 | Docker Compose dev environment | 2026-08-05 | Postgres 18 on host port 55432, `.env` + `scripts/dev-server.sh`. Arrangement in ADR 002 |
| 003 | EF Core setup + initial migration | 2026-08-05 | `VttDbContext`, `scripts/ef.sh`, empty `InitialCreate`. Migrations applied explicitly (ADR 003); snake_case / `timestamptz` / explicit `jsonb` (ADR 004) |
| 004 | Frontend scaffold | 2026-08-05 | Vite 8 + React 19 + TS 6 on port 5173, `/api` proxied to 5080. Health endpoint moved to `/api/health`. ESLint kept over the template's new Oxlint default, which pins TS to 6.0.x |
| 005 | Test harness | 2026-08-05 | Testcontainers + `WebApplicationFactory` (ADR 005), Vitest + jsdom. `dotnet test` now needs Docker; `--filter "Category!=Integration"` is the fast subset |
| 006 | CI pipeline | 2026-08-05 | GitHub Actions, two parallel jobs, green on first run. Turning on the format gate exposed six existing violations, all fixed in `.editorconfig` rather than in the code |
| 010 | User entity + password hashing | 2026-08-05 | First domain table and first module folder. Identity declined, its hasher adopted (ADR 006). EF derived the table name `user`, a PostgreSQL reserved word — caught by reading the migration, fixed with an explicit `ToTable("users")` |
| 011 | Invite tokens | 2026-08-05 | Hashed tokens, single-use via one conditional `UPDATE` (ADR 007). `TimeProvider` adopted. The check-then-act version was written and shown to let all 16 concurrent racers win before being replaced |
| 012 | Registration via invite URL | 2026-08-05 | Endpoint plus the `create-account` bootstrap command (ADR 008), closing the gap open since 010. Registration is one transaction: without it, eight parallel racers left eight orphaned accounts |
| 013 | Login / logout | 2026-08-05 | Cookie sessions carrying identity only. Wrong password and unknown username answer identically; state is checked only after the password verifies. First consumer of 010's rehash signal |
| 014 | Admin approval queue | 2026-08-05 | Platform role column moved here from 016 — an approval queue anyone can call is not one. Approve, reject, disable and re-enable are one guarded transition. Roles read per request, never from the cookie |
| 015 | Admin recovery codes | 2026-08-05 | Admin-mediated recovery with no email anywhere. `SecureToken` extracted now that 011 and 015 give two examples — the identical part only. Recovery restores the password and nothing else |
| 016 | Authorization policies | 2026-08-05 | Declared policies replace 014's hand-rolled guard. Closes 013's gap: a disabled account's existing cookie now stops working on the next request rather than at expiry |
| 017 | Auth UI | 2026-08-05 | Sign in, register from an invite link, awaiting-approval, admin queue. i18next infrastructure lands here (IT + EN) per 004's deviation. Enums now cross the wire as names — they were serialising as numbers, which the frontend would have rendered as raw keys |
| 017a | Issue invites over HTTP | 2026-08-05 | Gap found while writing test instructions: `IInviteService` could mint invites from 011 and nothing could reach it, so closing the loop needed SQL by hand. 017's card listed the screen and did not build it |
| 020 | Campaign entity | 2026-08-05 | Creator becomes Master; `(SystemId, SystemVersion)` pinned from the first campaign, stored but not validated until 030. A campaign you cannot see is a 404, never a 403 |
| 021 | Campaign roster | 2026-08-05 | Master moves from a column into the roster; the campaign-role resolver lands here per 016's deviation. The generated migration dropped `master_user_id` before the roster existed — reordered by hand and a backfill added |
| 022 | In-app notifications | 2026-08-05 | The only channel the platform has, since it collects no email. Payload is a kind plus one parameter, never a sentence, so it can be translated. Marking read is scoped by recipient inside the UPDATE |
| 023 | Campaign list and detail UI | 2026-08-05 | Campaign list, detail with roster, invitations, notification bell. The API client split out of `accounts.ts` so each module owns its own calls |

## Deviations from the plan

Record every departure from `roadmap.md` and why. A plan that quietly diverges from reality is
worse than no plan.

| Task | What changed | Why |
|---|---|---|
| 004 | i18next infrastructure moved from 098 to 017; 098 keeps full IT + EN coverage and the switcher | `.claude/rules/frontend.md` requires a translation layer from the first line of UI code, and the spec wants the infrastructure from the first release — but the roadmap parked it in Phase 3. Leaving it at 098 would ship every Phase 1 and 2 screen with hardcoded strings and turn 098 into archaeology. 017 is the first screen a user actually reads, so it lands there. 004 itself ships no i18next: its page is a diagnostic that 017 deletes |
| 016 | The campaign-role resolver moved from 016 to 021 | The roadmap paired it with the platform-role policy, but campaigns arrive at 020 and membership at 021, so a resolver written at 016 would have no table to read, no consumer and no test that exercises it. 016 ships the platform half; 021 builds the campaign half where it is first needed |
| 014 | The platform-role column moved from 016 to 014 | 010 deferred it as speculative because nothing read it. 014's approval queue must enforce against it, and a task cannot own a column an earlier task already depends on. 016 keeps what it was for: the policy infrastructure and the campaign-role resolver |
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
| Table naming for every later entity | each entity's own configuration | 010 set `ToTable("users")` explicitly because EF derived `user`, a reserved word. EF does not pluralise, so **every** entity configuration must name its table — `architecture.md` specifies plural names throughout |
| Invite revocation as a state rather than a row delete | 017, if at all | Deleting the row revokes an invite completely and needs no schema. A `Revoked` state only earns its place if the admin screen wants revoked invites visible as history — a display requirement, not a security one (ADR 007) |
| Coverage measurement and thresholds | when there is domain logic worth covering | A coverage number over a codebase with no rules engine is theatre |
| End-to-end browser testing | when a flow exists that is worth driving | The frontend is one diagnostic page; 017's auth screens are the first candidate |
| Confirming the no-Docker failure message against a genuinely stopped Docker | next time Docker Desktop is restarted | 005 could not simulate it: `DOCKER_HOST` overrides are ignored by Testcontainers' socket discovery, and Docker Desktop runs Windows-side. An explicit guard in `PostgresFixture` makes the message deterministic, verified by forcing a container start failure — but the real stopped-Docker path is untested |
| Invalidating live sessions when a password is reset | with the session work, or 102 | 015 changes the password but cannot evict anyone already holding the cookie. Fixing it needs a value in the cookie that a reset can invalidate — real, and not something to bolt onto the recovery flow |
| Login rate limiting and lockout after repeated failures | when the platform is deployed, or 102 | 013 ships no lockout, so passwords can be tried indefinitely. Acceptable for an undeployed private tool; not acceptable once it is reachable |
| Persisting data protection keys | 101 | 013's session cookie is signed with keys that default to the local filesystem. A container without a persistent volume regenerates them on restart and silently signs everybody out |
| Connection string for IDE-launched debugging | when it first bites | `scripts/dev-server.sh` covers the terminal; `appsettings.Development.local.json` is already gitignored for the other case |
| **Enabling branch protection on `main`** | **outstanding — manual** | CI is advisory until GitHub is told to require it. Branch protection is a repository setting, not a file, so it cannot be committed: Settings → Branches → require the `Backend` and `Frontend` checks. Until then a red build blocks nothing |
| TypeScript 7 upgrade | when `typescript-eslint` supports it | 004 holds TS at 6.0.x because `typescript-eslint` declares `>=4.8.4 <6.1.0`. TS 7's native compiler is stable and much faster; the alternative unlock is switching to Oxlint, which `create-vite` now scaffolds by default but which cannot enforce all of `.claude/rules/frontend.md` |
| Bounding the `/api/health` database-check timeout | 101 | Measured in 003: when the server starts while Postgres is unreachable, every probe takes ~16s — Npgsql's default `Timeout=15`. The other paths are fast (200 in ~0.4s; 503 in ~0.06s when the database dies under a running server). Harmless in development, but Caddy will front the endpoint with its own timeout, so 101 is where a hanging probe actually costs something. This is also why 005 has no integration test for the unhealthy path |
