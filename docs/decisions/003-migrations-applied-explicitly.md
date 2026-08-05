# 003 — Migrations are applied explicitly, never at startup

**Date:** 2026-08-05
**Status:** accepted

## Context

EF Core offers `Database.Migrate()`, one line in `Program.cs` that brings the schema up to date every
time the application starts. For a single developer deploying to a single VPS with
`compose pull && compose up -d`, this is the obvious way to never think about schema deployment
again.

The constraints that argue against it are equally real: the platform holds campaigns accumulating
years of play, its stated data requirement is that they must not be lost, and it is operated by one
person who is also the only one who would notice a schema change going wrong.

## Decision

Migrations are applied by an explicit command — `scripts/ef.sh database update` in development, and
a deliberate step in the deployment procedure that task 101 will define. `Program.cs` does not call
`Database.Migrate()`, `EnsureCreated()`, or anything else that mutates schema.

`scripts/ef.sh` is the only supported entry point for the EF tools. It exports the same
`ConnectionStrings__Default` that `dev-server.sh` does, because `dotnet ef` executes `Program.cs`
up to `builder.Build()` and therefore runs the fail-fast configuration check at design time as well.

`dotnet-ef` is pinned in `.config/dotnet-tools.json` rather than installed globally.

## Consequences

Schema change becomes a thing you do and watch, rather than a side effect of a process restart. A
migration that fails fails a command in front of you, with its error on your terminal, and the
application that is currently running is untouched. Under `Database.Migrate()` the same failure
takes the app down at boot, during a deploy, quite possibly minutes before a scheduled session.

It also removes a race that is invisible until it isn't: two instances starting together both run
migrations, and EF's migration lock is what stands between that and a corrupted schema. The single
VPS makes this unlikely rather than impossible — a `compose up` during an overlapping restart is
enough.

The cost is a step that can be forgotten. Deploying new code against an un-migrated database gives
runtime errors on the first query that touches a missing column, which is a worse failure signal
than a refusal to start. Two things mitigate it: `dotnet test` fails when the model and the
committed migrations disagree, and task 101 owns making the deploy procedure include the step
explicitly. Neither is in place yet for a production that does not exist.

Requiring `scripts/ef.sh` means a bare `dotnet ef` fails with a connection-string error that looks
like a project fault until you know why. The script's header comment explains it, and `dev-setup.md`
says to always use the script.

## Alternatives rejected

**`Database.Migrate()` at startup.** One line, impossible to forget, and genuinely the right answer
for a small app whose data is reconstructible. Rejected because this data is not reconstructible and
the failure mode is badly timed: it converts "a migration is broken" into "the platform is down",
and it does so during a deploy rather than at a moment of the maintainer's choosing. Worth
reconsidering only if forgetting the step turns out to bite more often than a bad migration does.

**`Database.Migrate()` guarded by an environment flag,** on in development, off in production. Keeps
the inner loop frictionless. Rejected because it makes development and production differ in *when
schema changes happen*, which is precisely the mechanism you want to have rehearsed before it runs
against real data.

**`EnsureCreated()` for local development,** skipping migrations entirely until the schema settles.
Faster while iterating. Rejected outright: it creates a database with no migration history, so the
first real `database update` finds tables it did not create and fails. It also means the migrations
would go untested exactly during the period they are changing most.

**A global `dotnet-ef` install.** One less file. Rejected because the tool version then lives in
whoever's machine rather than in the repository, and a version mismatch against the SDK pinned in
`global.json` produces errors that read like project faults.
