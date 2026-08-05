# 002 — Docker Compose dev environment

**Status:** done
**Depends on:** 001
**Branch:** `task/002-docker-compose-dev-environment`

## Goal

One command brings up the local backing services, and the server takes its configuration from the
environment rather than from files in the repository. Task 003 can then point EF Core at a real
database that is already running, and no task after this one ever needs a secret in a tracked file.

## Scope

In scope:
- `docker-compose.yml` at the repository root with a single `postgres` service: pinned major
  version, named volume for data, container healthcheck, port published to the host
- Credentials and database name supplied from a gitignored `.env`, with a committed
  `.env.example` documenting every variable
- Server reads its database connection string from configuration (`ConnectionStrings__Default`)
  and fails fast at startup with a clear message if it is absent
- Startup log line confirming which host and database were configured — never the password
- `docs/dev-setup.md` and `README.md` updated: bring the stack up, connect to it, reset it, tear
  it down

Explicitly out of scope:
- EF Core, `DbContext`, migrations, and any actual query against the database — task 003
- A database readiness probe wired into `/health` — task 003, which is when there is a real
  connection to probe
- An integration test that hits Postgres — task 005
- A `Dockerfile` for the server and a production compose file with Caddy and coturn — task 101
- Any additional service (Redis, pgAdmin, Adminer, coturn). The architecture explicitly rejects
  the first two; the third is a browser and psql away from unnecessary

## Approach

In development, compose runs **only the backing services**; the server and the frontend run on the
host with `dotnet run` and `npm run dev`. That keeps the inner loop fast — hot reload and the
debugger attach to a normal local process — while the thing that genuinely benefits from being
containerised, Postgres, stays reproducible and disposable. Production containerises everything,
and that difference is deliberate; task 101 owns it.

Configuration flows one way: `.env` is read by compose for the container's credentials, and the
same values appear in the server's environment as `ConnectionStrings__Default`. There is one place
to change a password. `appsettings.json` gains no connection string, not even a placeholder with
fake credentials — a tracked file containing something that looks like a secret is how a real one
eventually lands there.

## Acceptance criteria

- [ ] `docker compose up -d` from a clean clone (after copying `.env.example` to `.env`) reaches
      a `healthy` postgres container
- [ ] `psql` from the host connects using the values in `.env`
- [ ] `dotnet run` starts with the connection string coming from the environment; `GET /health`
      returns 200
- [ ] Starting the server with no connection string configured fails immediately with a message
      that names the missing setting — it does not start and fail later on first query
- [ ] `docker compose down` then `up -d` preserves data; `docker compose down -v` discards it,
      and both are documented
- [ ] `git status` is clean after bringing the stack up: `.env` is untracked, `.env.example` is
      committed
- [ ] `dotnet build` and `dotnet test` still pass with zero warnings

## Concepts to explain

- What Docker Compose actually is: a declarative description of a set of containers, the network
  they share, and their storage — and what `up -d`, `down`, and `down -v` each do to that state
- Named volumes versus bind mounts, and where Postgres data physically lives under WSL2
- How the Postgres image initialises itself, and why it does so **only once, on an empty volume**
- What a compose healthcheck means, and what `depends_on: condition: service_healthy` buys later
  when the app is also a container
- How .NET's configuration system layers providers (`appsettings.json` → environment-specific file
  → environment variables → user secrets), how `__` in an environment variable name maps to a
  nested configuration key, and why the environment layer wins
- Why secrets belong in the environment rather than in a tracked file, and what `.env.example`
  is for
- Fail-fast configuration validation: why a missing setting should stop startup rather than
  surface as a confusing error on the first request

## Risks and things to watch

- **Changing `POSTGRES_PASSWORD` after the first run does nothing.** The image only applies
  credentials while initialising an empty data directory. The symptom is an authentication failure
  that looks like a typo. The fix is `docker compose down -v`, which destroys the data. Worth
  writing down in the docs at the moment it is learned, not the second time it happens.
- Port 5432 may already be taken by a Postgres installed on the host or by another project's
  container. Publishing on a non-default host port avoids the collision permanently.
- `.env` must be verified as ignored *before* the first `docker compose up`, not after.
- Pin the Postgres major version. An unpinned `postgres:latest` will one day pull a new major, and
  the old data directory will refuse to start.
- Under WSL2, Docker Desktop must have integration enabled for the Ubuntu distribution or `docker`
  is simply not on PATH; `docs/dev-setup.md` §5 already covers this and should be referenced
  rather than repeated.
- Resist letting this card grow into 003. If something needs a live database connection to verify,
  it belongs in the next task.
