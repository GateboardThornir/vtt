# 001 — Repository scaffold

**Status:** not started
**Depends on:** nothing
**Branch:** `task/001-repo-scaffold`

## Goal

A repository that builds and is laid out the way the whole project will stay. Every later task
assumes this structure, so getting it wrong is expensive to undo.

## Scope

In scope:
- Solution file and project layout: `src/Server` (ASP.NET Core web API), `src/Client` placeholder,
  `tests/Server.Tests`
- `.editorconfig` encoding the C# style rules from `.claude/rules/backend.md`
- `.gitignore` covering .NET, Node, Docker, IDE files, `.env`, `CLAUDE.local.md`
- `README.md`: what the project is, how to run it locally on Windows + WSL2
- Nullable reference types enabled, warnings-as-errors on the server project

Out of scope:
- Frontend scaffolding — task 004
- Docker Compose and Postgres — task 002
- Any EF Core or database code — task 003
- CI — task 006

## Approach

Minimal ASP.NET Core web API with a single `/health` endpoint returning 200, plus an empty xUnit
project referencing it. No database, no authentication, no domain code. The point is the skeleton
and the conventions, not functionality.

## Acceptance criteria

- [ ] `dotnet build` succeeds from the repository root with zero warnings
- [ ] `dotnet test` runs and passes (one trivial test proving the harness works)
- [ ] `GET /health` returns 200 when the app is run locally
- [ ] `README.md` gets a fresh WSL2 environment from clone to running app
- [ ] `.gitignore` verified: no build output, secrets, or IDE files are tracked

## Concepts to explain

- How a .NET solution relates to projects, and why this layout rather than a single project
- What the ASP.NET Core minimal hosting model does at startup — what `WebApplication.CreateBuilder`
  actually assembles, and where dependency injection and configuration come from
- How configuration layering works (appsettings, environment variables, user secrets) and why
  secrets never go in `appsettings.json`
- Why nullable reference types and warnings-as-errors are enabled from day one rather than later
- What `.editorconfig` controls and how it is enforced

## Risks and things to watch

- Work inside the WSL2 filesystem (`~/projects/...`), never `/mnt/c/...` — cross-filesystem I/O is
  slow and file watching is unreliable. The README must state this explicitly.
- Resist adding structure for things not yet built. Empty folders "for later" are noise.
