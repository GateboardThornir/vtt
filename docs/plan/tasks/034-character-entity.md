# 034 — Character entity

**Status:** done
**Depends on:** 033
**Branch:** `task/034-character-entity`

## Goal

Characters exist in a campaign, owned by a player, with their sheet stored as a JSONB document
validated against the campaign's pinned system.

## Scope

In scope:
- A `Character` entity: campaign, owner, name, sheet document, timestamps
- Creating, reading and updating a character within a campaign
- **Validation and `RecomputeDerived` on every write, unconditionally** — the rule the binding
  contract states and the reason 033 exists
- The Master may edit any character in their campaign; a Player only their own
- The first JSONB column, using the mapping ADR 004 wrote down and nothing has exercised
- Integration tests, including that a Player cannot touch somebody else's character

Explicitly out of scope (and which task covers it instead):
- The sheet UI — 035
- Tokens representing a character on a map — 053
- The Master override *path* as a distinct operation — 072. Here the Master simply has wider write
  access; the schema floor applies to both
- Character export — 097

## Approach

**Every write validates, then recomputes, in that order.** Validation rejects a malformed document
before anything is stored; `RecomputeDerived` then runs unconditionally, so the derived values in
the database are always the module's and never the client's. A client may send `derived` and it will
simply be overwritten.

**The pinned system decides, not the current one.** A character is validated against the module its
*campaign* pinned. That is what version pinning is for, and reaching for "the latest dnd5e" here
would quietly undo it.

**Ownership is a column, permissions come from the roster.** Whether you may edit a character is
"are you its owner, or this campaign's Master" — answered by 021's resolver.

## Acceptance criteria

- [ ] A Player creates a character in a campaign they are on, and is its owner
- [ ] A sheet failing the schema is refused with the offending path, and nothing is stored
- [ ] `derived` in the stored document is always the module's output, even if the client sent its own
- [ ] The Master can edit any character in their campaign; a Player cannot edit another's
- [ ] A stranger gets 404 for everything, matching the campaign
- [ ] The sheet round-trips through JSONB unchanged apart from `derived`
- [ ] Migration read before committing; the column is `jsonb`
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **Forgetting `RecomputeDerived` on one path** is how derived values silently rot. It belongs in
  the service, on every write, not at each call site.
- **Validating against the wrong module version** defeats pinning entirely.
- JSONB comparison and update semantics differ from text; ADR 004 chose the explicit mapping and
  this is the first time it matters.
