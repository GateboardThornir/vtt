# Project operating instructions

Private virtual tabletop (VTT) for live tabletop RPG play. Full spec: @docs/functional-spec.md
Architecture: @docs/architecture.md — Plan: @docs/plan/roadmap.md — State: @docs/plan/PROGRESS.md

## Who you are working with

A solo developer, strong in C#, TypeScript, SQL and Python, who is **learning this domain**
(VTT architecture, real-time sync, WebRTC, PixiJS, EF Core patterns) and reviews every change.
Working code alone is not a finished task. See "Teaching duty" below — it is not optional.

## Workflow — follow this for every task

1. Read the task card in `docs/plan/tasks/NNN-*.md`. If it is a stub, expand it first and get approval.
2. **Enter plan mode.** Produce a file-level plan: files created/modified, key types, test approach.
   Do not write code until the plan is explicitly approved.
3. Create a branch: `task/NNN-short-name`. Never commit to `main`.
4. Implement. Small, coherent commits with clear messages.
5. Run build + tests. Do not present work that does not compile or has failing tests.
6. Stop and report. Summarise the diff, then deliver the explanation (see below).
7. Only after explicit approval: update `docs/plan/PROGRESS.md` and merge to `main`.

Never combine two task cards in one branch. If a card turns out to need splitting, say so and stop.

## Teaching duty

Every task ends with a written explanation, in plain language, covering:

- **What was built** and how the pieces fit together.
- **How the technology works** — the framework/library mechanism being used, at concept level.
  Assume fluency in C#/TS syntax; assume no prior exposure to SignalR, PixiJS, WebRTC, EF Core
  internals, or VTT patterns. Explain the mental model, not the API signature list.
- **Why this choice** and what the alternatives were, including the argument against the choice made.
- **What to watch out for** — failure modes, footguns, things that will bite later.

For any decision with lasting consequences, also write an ADR in `docs/decisions/NNN-title.md`
(context / decision / consequences / alternatives rejected). Keep ADRs short and honest.

## Non-negotiables

- **Server-authoritative.** Clients send intents; the server validates and decides. Never trust
  client-supplied state.
- **Visibility is enforced server-side.** Fog of war, hidden prep, per-player secrets: data a user
  is not entitled to see must never leave the server. Filtering in the UI is a security bug.
- **Campaigns pin a game system version.** Never migrate campaign data implicitly.
- **No email anywhere.** No email fields, no email flows, no email libraries. Notifications are in-app.
- **Only SRD content ships.** Never add copyrighted rulebook material to the repo.
- **No secrets in the repo.** Config via environment variables and `.env` files that are gitignored.

## Scope discipline

Build what the task card asks for. No speculative abstraction, no "while I was in here" refactors,
no extra features. If you believe the card is wrong or incomplete, say so and stop — do not
route around it silently. Flagging a conflict between a requirement and sound architecture is
expected behaviour, not friction.

## Stack

- Backend: ASP.NET Core (latest .NET LTS), C#, modular monolith, SignalR for real-time.
- Frontend: React + TypeScript (Vite), PixiJS for the table canvas.
- Data: PostgreSQL — relational core + JSONB for system-defined documents. EF Core, migrations.
- Tests: xUnit (backend), Vitest (frontend).
- Local dev: Docker Compose. Host is Windows + WSL2 — all paths are WSL-side.
- Tooling: Python for offline pipelines (SRD ingestion) only.

## Commands

```
docker compose up -d          # Postgres and supporting services
dotnet build                  # from repo root
dotnet test                   # all backend tests
dotnet ef migrations add X -p src/Server   # new migration
npm run dev                   # frontend, from src/Client
npm run test                  # frontend tests
```

## Style

- C#: nullable enabled, `async`/`await` all the way down, no `.Result`/`.Wait()`.
  Records for DTOs and messages. One type per file.
- TypeScript: strict mode, no `any`. Explicit return types on exported functions.
- SQL/EF: migrations are append-only once merged; never edit a merged migration.
- Comments explain *why*, never *what*. No decorative comment banners.

Detailed conventions live in `.claude/rules/` and load automatically when relevant files are touched.
