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
- Node.js LTS — needed from task 004 onward
- Docker — needed from task 002 onward
- Linux, or Windows with WSL2

## Running it locally

```bash
git clone git@github.com:<your-username>/vtt.git ~/projects/vtt
cd ~/projects/vtt
dotnet build
dotnet run --project src/Server
```

The server listens on <http://localhost:5080>. Confirm it is alive:

```bash
curl http://localhost:5080/health     # -> Healthy
```

HTTP only in development — no local HTTPS certificate to install. TLS is terminated by Caddy in
production.

### On Windows: keep the project inside WSL2

Clone to a Linux path such as `~/projects/vtt`. **Never** work under `/mnt/c/...`. WSL2 can reach
the Windows drives there, but every file operation crosses a translation layer: builds crawl, and
file watching (hot reload, test watchers) silently stops noticing changes. The symptom is
mystifying, the cause is invisible, and the fix is always "move the project".

## Commands

| Command | What it does |
|---|---|
| `dotnet build` | Builds the solution. Warnings are errors — a warning fails the build |
| `dotnet test` | Runs the backend test suite |
| `dotnet format` | Applies the `.editorconfig` conventions |
| `dotnet run --project src/Server` | Starts the server on port 5080 |

## Layout

```
src/Server/          ASP.NET Core backend. Modular monolith: one assembly,
                     folders as module boundaries
src/Client/          React + TypeScript frontend (arrives in task 004)
tests/Server.Tests/  Backend tests
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
