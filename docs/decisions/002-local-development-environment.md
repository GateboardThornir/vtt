# 002 — Local development environment

**Date:** 2026-08-05
**Status:** accepted

## Context

The project needs a database from task 003 onward, and a place to put credentials that is not a
tracked file. Production is settled: Docker Compose on a single VPS running Caddy, the application,
PostgreSQL and coturn (`docs/architecture.md`). What was not settled is whether development mirrors
that arrangement, and where a password lives on the developer's machine.

Constraints in play: a single developer on Windows with WSL2 and Docker Desktop, an inner loop that
gets exercised dozens of times a day, and a security rule that no secret enters the repository.

## Decision

**Compose runs backing services only.** `docker-compose.yml` describes PostgreSQL and nothing else.
The server runs on the host with `dotnet run`, the frontend with `npm run dev`. Production
containerises the application too, and that difference is deliberate; task 101 owns the production
compose file.

**One `.env`, two consumers.** The gitignored `.env` supplies the container's credentials to
compose and, through `scripts/dev-server.sh`, the connection string to the server. A committed
`.env.example` documents the variables. `appsettings.json` gets no connection string, not even a
placeholder with invented credentials.

**Configuration is validated at startup.** The server resolves `ConnectionStrings__Default` before
the host is built and refuses to start without it. The configured host and database are logged;
the password is redacted first.

**PostgreSQL 18, major pinned, on a non-default host port** (55432 by default), with `PGDATA` set
explicitly inside the mounted volume.

## Consequences

The inner loop stays fast: a code change is `dotnet run` away, hot reload works, and the debugger
attaches to an ordinary local process with no container indirection. The cost is that development
and production no longer run the same way, so a defect in the application's own container image
cannot appear until task 101 builds one. That is an accepted trade — it moves a class of problem to
a task that exists specifically to deal with it, rather than paying container-rebuild latency on
every edit for months.

The single `.env` means a password is changed in one place. It also means the developer must launch
the server through `scripts/dev-server.sh` rather than a bare `dotnet run`, because .NET does not
read `.env` files. Forgetting is not silent: startup fails with a message naming the missing
setting and the script to use. Debugging from an IDE, which does not go through the script, will
need its own arrangement when that first matters — `appsettings.Development.local.json` is already
gitignored for exactly that.

The non-default host port avoids a collision with any PostgreSQL already installed on the machine.
The price is that every connection string and `psql` invocation must carry `port=55432`, and a
copy-pasted command from the internet will silently target the wrong server if one is running on
5432.

Pinning the major version means a new PostgreSQL release cannot break the local stack by surprise;
it also means upgrades are a deliberate task, with a dump and restore, rather than something that
happens on `docker compose pull`.

## Alternatives rejected

**Containerise the application in development too**, so that development and production are
identical. This is the textbook answer and its central argument is real: the closer the two
environments, the fewer surprises at deploy time. Rejected because the inner loop pays for it
continuously — a rebuild or a bind-mounted watcher on every change, a debugger reaching into a
container — while the class of bug it prevents (image and container-wiring defects) shows up
exactly once, when the production image is first built. Worth revisiting if deployment turns out to
break repeatedly in ways local development could have caught.

**Install PostgreSQL natively in WSL2.** Fastest of all, no Docker dependency. Rejected because the
version and configuration then live in the developer's head instead of in a file in the repository,
and they drift from the VPS. The reproducibility of a pinned image is the entire point.

**A `.env`-reading NuGet package** (DotNetEnv and similar) so a bare `dotnet run` works. Genuinely
convenient, and it would remove the wrapper script. Rejected because it adds a third-party
dependency to the server for something a three-line shell script does, and it blurs where
configuration comes from — the environment layer is the mechanism the framework already provides
and the one production will use.

**Keeping the development connection string in `appsettings.Development.json`,** tracked, with
throwaway credentials. Simplest possible setup and no script at all. Rejected on the grounds in
`.claude/rules/security.md`: a tracked file containing a working connection string is how a real
credential eventually gets committed, and the habit is worse than the individual file.
