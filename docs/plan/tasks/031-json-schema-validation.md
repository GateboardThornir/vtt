# 031 — JSON schema validation infrastructure

**Status:** done
**Depends on:** 030
**Branch:** `task/031-json-schema-validation`

## Goal

A document can be checked against the schema its pinned module declares, before it reaches the
database. This is the floor under every sheet write, including a Master's overrides.

## Scope

In scope:
- A validator that takes a document and a module's schema and reports conformance with usable errors
- Caching compiled schemas per module — a schema is recompiled per validation otherwise
- Tests: a conforming document passes, each kind of violation fails with a locatable message

Explicitly out of scope:
- Any actual schema content — 032
- Persisting sheets — 034
- Rule validation. This checks *shape*, never whether a value is legal in the game

## Approach

**Shape, not rules — and that distinction is the point.** `.claude/rules/game-systems.md` says a
Master override bypasses rule validation but never schema validation: types, required fields and
enum membership are always enforced. A Master may set a hit point total the rules forbid; they may
not set it to `"banana"`. Without that floor one bad override corrupts a sheet in a way that
surfaces much later, mid-session.

**Errors have to say where.** "Invalid" is useless against a nested sheet; the path to the offending
value is what makes a failure actionable.

**Compile once.** A module's schema never changes at runtime — it is a property of a released
version — so it is compiled on first use and kept.

## Acceptance criteria

- [ ] A conforming document validates
- [ ] A wrong type, a missing required field and a value outside an enum each fail
- [ ] Failures carry the path to the offending value
- [ ] Validating the same schema repeatedly does not recompile it
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **Do not let rule checks leak in here.** The moment this knows what a legal hit point total is,
  the override floor has become a ceiling.
- A permissive schema is worse than none: it implies a guarantee it does not give.
