---
paths:
  - "src/Server/**/*.cs"
  - "tests/Server.Tests/**/*.cs"
---

# Backend conventions

## Structure

Modular monolith. One assembly, folders as module boundaries: `Accounts/`, `Campaigns/`,
`Sessions/`, `Table/`, `Systems/`, `Assets/`, `Infrastructure/`.
Modules talk through public interfaces in their own folder — never reach into another module's
internals. If two modules need shared types, they go in `Shared/`.

## Table engine

One actor per active session: a single-threaded processing loop owning that table's in-memory
state. All mutations for a table go through its loop — never mutate table state from a request
thread or a hub method directly. Hub methods enqueue intents and return.

Intent flow, in strict order: **auth → permission → resolution → visibility filter → broadcast.**
Never reorder, never skip the filter. The filter computes a per-recipient view; there is no
"broadcast everything and let the client hide it" path.

Two resolution paths produce the same delta type:
- gameplay intents → the active `IGameSystem` module handler
- Master overrides → direct write, bypassing **rule** validation but **not** schema validation

After any write to a sheet — automated or override — call `RecomputeDerived`. Unconditionally.
This is what stops free-edit overrides from desyncing derived stats.

## Persistence

Event log is append-only during a session. Snapshot on scene change and every 5 minutes.
Once a snapshot is durable, prune preceding event rows — recovery only, no replay history.
Write-behind: never block the intent loop on a database write.

## EF Core

`DbContext` per request, scoped. No lazy loading — load explicitly with `Include` or projections.
JSONB columns are mapped as `JsonDocument`/POCO with `.HasColumnType("jsonb")`; never query
JSONB with string concatenation. Migrations are generated, reviewed, and committed with the
change that needs them.

## Errors and validation

Validate at the boundary: request DTOs and intent envelopes. Domain code assumes valid input.
Return typed results (`Result<T>` style) from domain operations, exceptions only for genuinely
exceptional conditions. Never leak internal exception detail to clients.

## Tests

Unit tests for rules resolution, permission checks, and visibility filtering — these three are
where bugs are expensive. Integration tests against a real Postgres in Docker, not an in-memory
provider (behaviour differs, especially for JSONB). Test names describe the behaviour asserted.

Integration tests live in `tests/Server.Tests/Integration/`, carry `[Trait("Category",
"Integration")]`, and join `[Collection(IntegrationDatabase.Name)]` so they share the one
Testcontainers instance — a fixture per class means a container per class, which is how a suite
becomes too slow to run. Everything else is a plain unit test that must not need Docker, so
`dotnet test --filter "Category!=Integration"` stays fast.

Reach the application through `PostgresFixture.CreateClient()` rather than instantiating types
directly: the point of an integration test is proving the wiring, which is what breaks.
