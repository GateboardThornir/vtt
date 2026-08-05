# 004 — Database model conventions

**Date:** 2026-08-05
**Status:** accepted

## Context

Task 003 generates the first migration. Three model-wide conventions had to be settled before it
existed, because each one is inherited by every migration after it and changing any of them later
means either a rename migration touching the whole schema or discarding the migration history.

The schema will eventually hold both ordinary relational tables and system-defined JSONB documents
(`docs/architecture.md`), and it will be queried by hand — during SRD ingestion (task 078), during
backup and restore drills (task 100), and every time something is being debugged against a live
database.

## Decision

**snake_case for every generated identifier**, applied by `EFCore.NamingConventions` through
`UseSnakeCaseNamingConvention()` in `DatabaseServices.Configure`. Tables, columns, keys, foreign
keys and indexes.

**Timestamps are `DateTimeOffset`, stored as `timestamptz`, always UTC.** No naive `timestamp`
columns, and no `DateTime` in entities.

**JSONB columns are mapped explicitly** with `.HasColumnType("jsonb")` on a POCO or `JsonDocument`,
never inferred and never assembled by string concatenation. Written down here; first exercised at
task 034.

## Consequences

Hand-written SQL stops needing quotes. Postgres folds unquoted identifiers to lowercase, so EF's
default `PascalCase` names only work when double-quoted — `SELECT "Id" FROM "CampaignMembers"` —
forever, in every query, dump and psql session. snake_case makes the database readable in the shell
it will actually be read in.

The cost is a third-party dependency in the data layer. It is a small and well-maintained one,
authored by the Npgsql lead, but the .NET 11 upgrade cannot happen until it ships a matching release.
That is a real constraint accepted with open eyes; the alternative was worse (below).

`DateTimeOffset`/`timestamptz` means an instant is unambiguous. The group scheduling sessions
(task 077) will not necessarily share a timezone, and a naive `timestamp` silently records a wall
clock with no way to recover what moment it meant. The cost is that `DateTimeOffset` is slightly more
awkward in C# than `DateTime`, and Npgsql will reject a `DateTime` with the wrong `Kind` at write
time rather than coercing it — an error at the point of the mistake, which is the behaviour worth
having.

Explicit JSONB mapping keeps the relational/document split in `docs/architecture.md` visible in the
code: a column is a document because someone said so, not because EF guessed from a property type.

There is no test asserting snake_case directly, because there are no tables yet. The migration-parity
test covers it from task 010 onward: removing the convention renames every table and column away
from the committed snapshot, and the test fails.

## Alternatives rejected

**A hand-rolled convention** — roughly thirty lines in `OnModelCreating` iterating
`Model.GetEntityTypes()` and rewriting names. No dependency, and the mechanism stays visible, which
has real teaching value on a project whose point is partly learning. Rejected because it runs as a
post-pass over an already-built model rather than as an EF convention: it silently overwrites any
explicit `.ToTable("x")`, and owned types and TPH discriminators need special handling that is easy
to get subtly wrong. Character sheets bring owned types at task 034, which is exactly where a subtle
error would surface and exactly where it would be most expensive.

**EF's default PascalCase.** No package, no code, no risk of any kind at this point. Rejected
because the cost is permanent and paid by hand every time anyone touches SQL directly, and this
project expects that to happen regularly. The one-time cost of a dependency beats an unbounded
recurring cost in ergonomics.

**`DateTime` with a UTC convention enforced by discipline.** Simpler types. Rejected because the
enforcement is a habit rather than a mechanism, and the failure is silent: a `DateTime` with the
wrong `Kind` round-trips without complaint and is wrong by hours.
