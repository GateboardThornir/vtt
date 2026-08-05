# 032 — 5e module skeleton + sheet schema v1

**Status:** done
**Depends on:** 031
**Branch:** `task/032-5e-module-skeleton`

## Goal

The first real game system: a D&D 5e module with a character sheet schema narrow enough to be
correct and broad enough to play with. Abilities, proficiency, hit points, armour class, skills.

## Scope

In scope:
- A `Systems/Dnd5e/` module implementing `IGameSystem`, registered as `dnd5e` version `1.0.0`
- The character sheet schema v1: identity, six abilities, proficiency bonus, hit points, armour
  class, skill proficiencies
- A compendium entry schema, deliberately minimal — nothing consumes it until 078
- `MigrateSheet` refusing any version it does not know, rather than silently passing a document
  through
- Tests: a realistic sheet validates, each required field is enforced, out-of-range values fail

Explicitly out of scope (and which task covers it instead):
- `RecomputeDerived` — 033, which is the next card and the reason the schema separates raw from
  derived
- Any SRD content: spells, monsters, items — 078's ingestion pipeline
- Anything the rules *do*: attacks, damage, conditions — Phase 2

## Approach

**Deliberately narrow, and versioned from the first line.** The roadmap says narrow; the binding
rule says a schema change means a version bump and a migration. A small v1 that is right beats a
large one that needs a breaking change in a fortnight.

**Raw and derived are separated in the schema.** Ability scores are input; modifiers, saving throws,
skill totals and passive perception are computed. Keeping them in distinct objects makes 033's job
obvious and makes it visible when a derived value has been written by hand.

**Only SRD material.** No spell, monster or item text enters the repository. The schema describes
shapes, not content.

**The registered version is `1.0.0`, not `1.0`.** Existing dev campaigns pin `dnd5e 1.0`, which will
stop resolving. That is a deliberate choice: semantic versions are the contract, and a database with
three test rows in it is the cheapest moment this will ever be to get right.

## Acceptance criteria

- [ ] The module registers and resolves as `dnd5e 1.0.0`
- [ ] A realistic level-3 character validates
- [ ] Missing abilities, a non-integer score, a score outside 1–30, and an unknown skill each fail
- [ ] `MigrateSheet` refuses an unknown source version rather than returning the document unchanged
- [ ] No SRD content is committed
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **A schema change after this ships needs a version bump and a migration.** That is the one rule
  whose violation destroys years of campaign data.
- **Existing campaigns pinning `1.0` will not resolve.** Handle it deliberately: with three test
  rows, deleting them is honest and a migration would be theatre.
- Do not add fields "while we are here". Every field is a promise that needs migrating later.
