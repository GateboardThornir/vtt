# VTT

[![CI](https://github.com/GateboardThornir/vtt/actions/workflows/ci.yml/badge.svg)](https://github.com/GateboardThornir/vtt/actions/workflows/ci.yml)

A private, invitation-only virtual tabletop for playing tabletop RPGs live over the internet:
shared battlemaps, tokens, fog of war, dice, character sheets, chat and voice, with the server
understanding and automating the rules of the game system in play.

Multi-system by design. D&D 5e (SRD content only) is the first system module.

Not a public product — it serves a closed group of fewer than fifty people, and every design
decision is sized to that.

## Documentation

| Document | What it covers |
|---|---|
| [`docs/functional-spec.md`](docs/functional-spec.md) | What the platform is and must do. Also available in Italian: [`functional-spec.it.md`](docs/functional-spec.it.md) |
| [`docs/architecture.md`](docs/architecture.md) | How it is built, and what is deliberately excluded |
| [`docs/plan/roadmap.md`](docs/plan/roadmap.md) | Every task from empty repository to release |
| [`docs/plan/PROGRESS.md`](docs/plan/PROGRESS.md) | Where the work currently stands |
| [`docs/decisions/`](docs/decisions/) | Architecture decision records |
| [`docs/dev-setup.md`](docs/dev-setup.md) | Setting up a Windows machine from nothing |

## Requirements

- .NET SDK 10 (LTS) — the exact band is pinned in `global.json`
- Docker, with Compose
- Node.js LTS — needed from task 004 onward
- Linux, or Windows with WSL2

## Running it locally

```bash
git clone git@github.com:<your-username>/vtt.git ~/projects/vtt
cd ~/projects/vtt

cp .env.example .env          # then set POSTGRES_PASSWORD to anything you like
docker compose up -d          # PostgreSQL, on host port 55432

dotnet build
./scripts/ef.sh database update   # brings the schema up to date
./scripts/dev-server.sh
```

Then, in a second terminal, the frontend:

```bash
cd src/Client
npm install
npm run dev                   # http://localhost:5173
```

The server listens on <http://localhost:5080>. Confirm it is alive:

```bash
curl http://localhost:5080/api/health
# -> {"status":"Healthy","checks":{"database":"Healthy"}}
```

A 200 means the server can also reach the database. If the database is unreachable the server still
starts and serves, and `/api/health` returns 503 naming the check that failed.

The frontend at <http://localhost:5173> shows the same thing, fetched through the Vite dev proxy —
which makes it the quickest check that both halves of the stack are talking to each other.

HTTP only in development — no local HTTPS certificate to install. TLS is terminated by Caddy in
production.

### Two terminals, on purpose

The server and the frontend are separate processes, and there is no script that starts both.
Interleaved output from two watchers is harder to read than two windows, and the first thing anyone
does when a combined script misbehaves is run the halves separately anyway.

`vite.config.ts` proxies `/api` to the server, so the browser only ever sees one origin. That is
what lets task 013's `HttpOnly`, `SameSite=Lax` session cookies work without CORS or a development
HTTPS certificate. Details in [`src/Client/README.md`](src/Client/README.md).

### Why the script instead of `dotnet run`

.NET does not read `.env` files. `scripts/dev-server.sh` loads `.env`, builds the connection string
from it, and hands it to the server as an environment variable — so the database password exists in
exactly one file, which is gitignored. A bare `dotnet run` refuses to start and tells you as much.

Compose runs only the database; the server and the frontend run on the host, which keeps hot reload
and the debugger working. Production containerises everything — see
[ADR 002](docs/decisions/002-local-development-environment.md).

### Managing the database

```bash
docker compose ps             # postgres should read "healthy"
docker compose down           # stop; the data survives
docker compose down -v        # stop and delete the data volume

docker compose exec postgres psql -U vtt -d vtt    # a psql shell, nothing to install
```

The container carries its own `psql`, so there is no client to install on the host. If you would
rather use a local client or a GUI, connect to `localhost:55432` with the credentials from `.env`.

**Changing `POSTGRES_PASSWORD` in `.env` after the first start does nothing.** The Postgres image
applies credentials only while initialising an empty data directory; afterwards the old password
stays in force and authentication fails as if you had mistyped it. The only fix is
`docker compose down -v`, which discards the local database.

### Database migrations

The schema is versioned as EF Core migrations under `src/Server/Infrastructure/Migrations/`. They
are **never applied automatically at startup** — see
[ADR 003](docs/decisions/003-migrations-applied-explicitly.md).

```bash
./scripts/ef.sh migrations add AddSomething --output-dir Infrastructure/Migrations
./scripts/ef.sh database update      # apply everything outstanding
./scripts/ef.sh migrations list      # what exists, and what is applied
```

Read the generated migration before committing it. EF infers it from the difference between your
model and the last snapshot, and it will happily generate a destructive column drop when you
expected a rename.

Getting one wrong is cheap **before** it is merged and expensive after:

```bash
./scripts/ef.sh database update 0    # revert to an empty database
./scripts/ef.sh migrations remove    # delete the migration and roll back the snapshot
```

`migrations remove` refuses while the migration is still applied, which is the tool telling you to
revert first. Once a migration is on `main` it is append-only: correct it with a *new* migration,
never by editing the old one, because anyone who has already applied it will never re-run it.

### Tests

```bash
dotnet test                                      # backend, needs Docker
dotnet test --filter "Category!=Integration"     # the fast subset, no Docker
cd src/Client && npm run test                    # frontend
```

Backend integration tests start their **own throwaway PostgreSQL container** and destroy it
afterwards. They never touch the database `docker compose` runs, so running them mid-session cannot
harm your data — verified, not assumed. The cost is that `dotnet test` needs Docker running;
the `Category` filter above is the escape hatch when it is not.

Frontend tests run in jsdom with React Testing Library. Reasoning in
[ADR 005](docs/decisions/005-integration-tests-against-a-real-database.md).

### What CI enforces

Every push runs `.github/workflows/ci.yml`: two parallel jobs covering the backend (build, tests
including the container-backed integration suite, `dotnet format --verify-no-changes`) and the
frontend (`lint`, `typecheck`, `test`, `build`).

Every one of those is a command you can run locally in the same form. If CI is red, reproduce it on
your machine rather than guessing — that is the whole reason no step does anything bespoke.

Style is checked here and never in the build. A formatting violation should not fail a build in the
middle of a task; see [ADR 001](docs/decisions/001-repository-layout-and-build-conventions.md).

### On Windows: keep the project inside WSL2

Clone to a Linux path such as `~/projects/vtt`. **Never** work under `/mnt/c/...`. WSL2 can reach
the Windows drives there, but every file operation crosses a translation layer: builds crawl, and
file watching (hot reload, test watchers) silently stops noticing changes. The symptom is
mystifying, the cause is invisible, and the fix is always "move the project".

## Commands

| Command | What it does |
|---|---|
| `docker compose up -d` | Starts PostgreSQL on host port 55432 |
| `docker compose down` | Stops it, keeping the data |
| `docker compose down -v` | Stops it and deletes the data volume |
| `./scripts/dev-server.sh` | Starts the server on port 5080, with `.env` loaded |
| `./scripts/ef.sh database update` | Applies outstanding migrations |
| `./scripts/ef.sh migrations add X --output-dir Infrastructure/Migrations` | Generates a migration |
| `dotnet build` | Builds the solution. Warnings are errors — a warning fails the build |
| `dotnet test` | Runs the backend test suite. Needs Docker — see below |
| `dotnet test --filter "Category!=Integration"` | The fast subset, no Docker required |
| `npm run test` | Frontend tests (Vitest), from `src/Client` |
| `dotnet format` | Applies the `.editorconfig` conventions |
| `npm run dev` | Frontend dev server on port 5173, from `src/Client` |
| `npm run lint` / `npm run typecheck` | ESLint / `tsc`, from `src/Client` |

## Layout

```
src/Server/          ASP.NET Core backend. Modular monolith: one assembly,
                     folders as module boundaries
src/Client/          React + TypeScript frontend (Vite). Proxies /api to the server
tests/Server.Tests/  Backend tests
scripts/             Development helpers
docs/                Specification, architecture, roadmap, decisions
.claude/rules/       Conventions that bind both humans and Claude Code
```

## Contributing

Single-maintainer project, built one task card at a time. The process is described in
[`CLAUDE.md`](CLAUDE.md): one card, one branch, one review. Nothing is committed to `main`
directly.

## Licensing

Bundled D&D 5e content comes exclusively from the System Reference Document, released by Wizards
of the Coast under Creative Commons. No copyrighted rulebook material ships in this repository.
