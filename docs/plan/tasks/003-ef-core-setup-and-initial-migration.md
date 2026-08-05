# 003 — EF Core setup + initial migration

**Status:** not started
**Depends on:** 002
**Branch:** `task/003-ef-core-setup-and-initial-migration`

## Goal

The server talks to the Postgres that task 002 brought up: a `DbContext` registered in DI, a
migration pipeline that is proven end to end, and a documented procedure for adding a migration and
applying it. From here on, any task that needs a table adds an entity and generates a migration
rather than inventing the plumbing.

## Scope

In scope:
- Npgsql EF Core provider referenced; `VttDbContext` in `Infrastructure/`, registered scoped, taking
  its connection string from the configuration value task 002 already validates
- Model-wide conventions settled **before** the first migration is generated: table and column
  naming, `DateTimeOffset`/UTC handling, and how JSONB columns will be mapped when they arrive
- An **empty initial migration** — no domain tables. It exists to create `__EFMigrationsHistory`
  and to prove the toolchain, not to guess at a schema
- A wrapper script (`scripts/ef.sh`) that loads `.env` and runs `dotnet ef`, so `migrations add` and
  `database update` work without hand-exporting the connection string
- Migrations applied **explicitly**, never automatically at startup (see Approach)
- Database readiness reported by `GET /health` — the check deferred from task 002
- `docs/dev-setup.md` and `README.md` gain the migration workflow: add, review, apply, roll back
- A test asserting the committed migrations match the model, which needs no live database

Explicitly out of scope (and which task covers it instead):
- **Any entity or table.** `users` is task 010, `campaigns` 020, `characters` 034. This card
  deliberately ships zero domain schema
- An integration test that opens a connection to Postgres — task 005 owns the integration harness
- JSONB document storage in practice — first used by task 034; only the mapping convention is
  decided here
- Connection resiliency, pooling tuning, read replicas, `EnableRetryOnFailure` — nothing yet
  justifies them, and the architecture's "solve it when there is evidence" rule applies
- Running migrations in CI or on deploy — tasks 006 and 101
- Seed data of any kind

## Approach

**The `DbContext` is infrastructure, entities are not.** Per `.claude/rules/backend.md`, module
folders own their entities; `VttDbContext` lives in `Infrastructure/` and picks up entity
configuration by scanning the assembly for `IEntityTypeConfiguration<T>`. That way task 010 adds
`Accounts/User.cs` and `Accounts/UserConfiguration.cs` and touches nothing central — the alternative,
a `DbSet` list and a growing `OnModelCreating`, turns the context into a file every future task must
edit and every future merge must reconcile.

**An empty first migration is the point, not a placeholder.** There is nothing to model yet, and
inventing a table to have something in the file would be exactly the speculative work scope
discipline forbids. What the empty migration buys is real: it creates the history table, and it
proves that `migrations add`, `database update`, and the design-time context construction all work
against the actual container — with a diff small enough to read in full while learning what a
migration file even is.

**Migrations are applied by hand, not by the application.** `Database.Migrate()` at startup is the
tempting shortcut for a solo developer. It is rejected here: it makes schema change a side effect of
a deploy, it races if more than one instance ever starts, and it means a bad migration takes the
app down instead of failing a command the developer is watching. Applying migrations stays a
deliberate step, and task 101 will make it a deliberate step in the deploy too.

**Three conventions must be settled before the first migration exists**, because changing any of
them afterwards rewrites every subsequent migration:

1. **snake_case naming.** Postgres folds unquoted identifiers to lowercase, so EF's default
   `PascalCase` names end up quoted everywhere and every hand-written `psql` query needs
   `"CamelCase"` quoting. Proposal: snake_case throughout, applied as a model convention.
2. **Timestamps are `DateTimeOffset`, stored as `timestamptz`, always UTC.** Sessions get scheduled
   across a group that may not share a timezone; a naive `timestamp` column is a bug waiting for
   task 077.
3. **JSONB is mapped explicitly** with `.HasColumnType("jsonb")` on a POCO or `JsonDocument`, never
   inferred. Only written down here; first exercised at task 034.

**Design-time is a separate execution context, and it will bite.** `dotnet ef migrations add` builds
the `DbContext` without running `Program.cs`'s normal path, and the fail-fast check from task 002
means it cannot fall back to a default. `scripts/ef.sh` supplies the same environment the server
gets, so there is one answer to "where does the connection string come from" rather than two.

**Health becomes a readiness statement.** `/health` currently proves the process is up. Adding the
database check makes a 200 mean "this instance can serve a request that touches the database",
which is what the compose healthcheck and later Caddy actually want to know. The cost is that a
`/health` failure no longer distinguishes "app dead" from "database unreachable"; the response body
names the failing check, which is enough at this scale.

## Acceptance criteria

- [ ] `scripts/ef.sh migrations add InitialCreate -p src/Server` produces a migration, and the
      generated `Up` is empty — no tables invented
- [ ] `scripts/ef.sh database update` against a running compose stack creates
      `__EFMigrationsHistory` and records the migration; running it twice is a no-op
- [ ] `psql` confirms the history table exists and holds exactly one row
- [ ] `GET /health` returns 200 with the stack up, and a non-200 naming the database check when
      `docker compose stop postgres` has been run
- [ ] Starting the server with the database down does **not** prevent startup — the connection is
      opened lazily, and readiness is reported through `/health`, not through a crash
- [ ] A test asserts there are no pending model changes (the model and the committed migrations
      agree) and passes without a database
- [ ] `docs/dev-setup.md` documents: add a migration, read it before committing, apply it, roll one
      back, and what to do when a migration is wrong but not yet merged
- [ ] `dotnet build` and `dotnet test` pass with zero warnings
- [ ] `git status` clean: no `.env`, no generated artefacts outside the migrations folder

## Concepts to explain

- **What an ORM is doing underneath.** How EF Core turns LINQ into SQL, what the change tracker is,
  and what "unit of work" means in the context of `SaveChangesAsync`
- **The model, and where it comes from.** How EF builds an internal model from conventions plus
  configuration, and why a snapshot of that model is committed alongside the migrations
- **What a migration actually is:** a generated C# class with `Up`/`Down`, a model snapshot, and a
  history table in the database — and why that trio is what makes migrations replayable and why
  hand-editing a merged one corrupts it
- **Why the migration is code and not SQL**, and when to drop to raw SQL inside one anyway
- **`DbContext` lifetime.** Why it is scoped per request, why it is not thread-safe, and what goes
  wrong when one is captured in a singleton — this becomes sharp at task 060, where the table actor
  is long-lived and must not hold a context
- **Design-time versus runtime.** Why the EF tools need to construct a context outside the running
  app, and the options for telling them how
- **Lazy loading, and why it is off.** What N+1 looks like, and why `Include`/projection is the
  house rule
- **The health-check abstraction:** what registering a check does, how the aggregated status is
  computed, and the liveness-versus-readiness distinction

## Risks and things to watch

- **Naming convention is a one-way door in practice.** Switching after tables exist means either a
  rename migration touching everything or throwing the history away. Decide it in plan mode, not
  while generating the migration.
- **Migrations are append-only once merged** (`CLAUDE.md`). Before merge, a wrong migration is
  removed with `migrations remove`; after merge, it is corrected by a *new* migration. Worth doing
  the remove once in this task, on purpose, so the distinction is learned cheaply.
- **`migrations remove` after `database update` needs the database reachable**, because it reverts
  the applied state. Removing while the container is down leaves the model snapshot and the database
  disagreeing.
- **An empty `Up` may read as a mistake later.** A comment in the migration saying why it is empty
  costs one line and prevents someone "fixing" it.
- **The EF tools version and the runtime version must match.** `dotnet-ef` is a separate global (or
  local) tool with its own version; a mismatch against the pinned SDK in `global.json` produces
  errors that read like project faults. Pin it as a local tool manifest so the version is in the
  repository rather than in the machine.
- **Do not let the health check hold a connection open or run on a timer.** It should be a cheap
  liveness query executed per request to `/health`, or the idle cost shows up as connections the
  container is holding for nothing.
- Nothing in this task may introduce an email field, a `User` type, or any other Phase 1 entity by
  anticipation. If a decision here seems to require knowing what `users` looks like, that is a
  signal the decision belongs to task 010.

## Decisions expected to produce an ADR

- Applying migrations explicitly rather than at startup, and what that implies for deployment
- The naming, timestamp and JSONB mapping conventions, since every later migration inherits them
