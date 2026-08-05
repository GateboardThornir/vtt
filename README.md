# VTT

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
./scripts/dev-server.sh
```

The server listens on <http://localhost:5080>. Confirm it is alive:

```bash
curl http://localhost:5080/health     # -> Healthy
```

HTTP only in development — no local HTTPS certificate to install. TLS is terminated by Caddy in
production.

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
psql "host=localhost port=55432 dbname=vtt user=vtt"
```

**Changing `POSTGRES_PASSWORD` in `.env` after the first start does nothing.** The Postgres image
applies credentials only while initialising an empty data directory; afterwards the old password
stays in force and authentication fails as if you had mistyped it. The only fix is
`docker compose down -v`, which discards the local database.

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
| `dotnet build` | Builds the solution. Warnings are errors — a warning fails the build |
| `dotnet test` | Runs the backend test suite |
| `dotnet format` | Applies the `.editorconfig` conventions |

## Layout

```
src/Server/          ASP.NET Core backend. Modular monolith: one assembly,
                     folders as module boundaries
src/Client/          React + TypeScript frontend (arrives in task 004)
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
