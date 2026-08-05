# 005 — Integration tests run against a real, throwaway database

**Date:** 2026-08-05
**Status:** accepted

## Context

Task 005 needed to decide what "integration test" means here before any were written, because the
answer shapes every test from task 010 onward — and tasks 062 and 070 are where tests stop being a
safety net and become the specification.

Three constraints are in play. `.claude/rules/backend.md` already bans the EF in-memory provider.
The data these tests exercise is eventually a multi-year campaign, so a test suite that can damage
the development database is a suite that will not be run. And there is one developer, who will
abandon anything slow enough to be annoying.

## Decision

**Testcontainers starts a throwaway `postgres:18` container per test run.** The fixture applies
migrations to it and disposes it afterwards. Tests never touch the container `docker-compose.yml`
runs.

**The real application runs in-process** via `WebApplicationFactory<Program>`: real dependency
injection, real middleware, real EF Core, with the socket replaced by an in-memory transport.
`Program.cs` gains `public partial class Program;` so the factory can name the entry point.

**The connection string reaches the application as an environment variable**, set by the fixture
before the factory is constructed. `Program.cs` resolves it before `builder.Build()`, and
`WebApplicationFactory`'s configuration hooks are applied when the factory intercepts the build —
too late for code that has already run.

**Integration tests are marked** with `[Trait("Category", "Integration")]` and share one collection
fixture, so `dotnet test --filter "Category!=Integration"` is a fast, Docker-free subset.

## Consequences

Tests exercise the database that ships. JSONB semantics, `timestamptz` behaviour, migration
correctness and provider-specific translation are all covered by construction rather than by hope —
which matters most for the JSONB documents at task 034, where the in-memory provider differs from
Postgres in ways that would not surface until production.

**`dotnet test` now requires Docker.** This is the real cost. The project already requires Docker to
run at all, so it is a smaller step than it sounds, but the test suite acquires a moving part that
can fail for reasons unrelated to the code, and a first-time contributor with Docker Desktop not
running gets a failure that is not about their change. The `Category` trait is the mitigation: the
unit subset runs in about a fifth of a second with nothing external.

Container startup dominates the run — roughly four seconds against a quarter of a second for the
unit tests. That is why the container lives in a collection fixture and not in a class fixture. If
integration tests are ever split across collections, the cost multiplies silently.

The environment variable is process-wide. Harmless with one container per run, and the fixture
restores the previous value on dispose, but it is a shared mutable global and should be treated as
one if tests ever run in parallel across collections.

Making `Program` public is production code changed for a test's benefit. It is one line and the
entry point is not a meaningful attack surface, but the direction of the dependency is worth
noticing rather than forgetting.

## Alternatives rejected

**The EF Core in-memory provider.** No Docker, near-instant, and the default suggestion everywhere.
Rejected — and already banned by `.claude/rules/backend.md` — because it is not Postgres: it has no
real SQL translation, different transaction and concurrency semantics, and no JSONB at all. A green
suite against it would say nothing about the queries that actually ship, which is worse than having
no integration tests, because it feels like coverage.

**SQLite in memory.** Closer to a real database than the in-memory provider, still fast, no Docker.
Rejected for the same reason one step removed: it is a different SQL dialect with different types,
and this project leans specifically on Postgres features — JSONB, `timestamptz`, snake_case
identifier folding — that SQLite either lacks or handles differently.

**A second, permanent database in `docker-compose.yml`,** e.g. `vtt_test`, that tests migrate and
truncate. No per-run container startup, so faster, and no new dependency. Rejected because it is
shared mutable state with a lifetime longer than the test run: it drifts as migrations land, it
needs manual resetting when a test leaves it dirty, and pointing at the wrong one — a mistyped
database name in a connection string — silently destroys development data. Testcontainers makes
that mistake impossible by construction rather than by discipline.

**Pointing the tests at the development database directly.** Simplest possible thing. Rejected
outright: the first destructive test deletes the maintainer's own campaign, and a suite that has
done that once will never be trusted again.
