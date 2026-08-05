# 035 — Character sheet UI

**Status:** done
**Depends on:** 034
**Branch:** `task/035-character-sheet-ui`

## Goal

A character sheet you can read and edit, with the computed values visibly computed — so it is
obvious which numbers you choose and which the server derives.

## Scope

In scope:
- A character list within a campaign, and a sheet screen
- Editing the raw fields: identity, ability scores, proficiency bonus, hit points, armour class,
  proficiencies
- Derived values shown **read-only**, refreshed from the server's answer after each save
- Schema errors from the server rendered against the field they name
- The Master can open and edit any character; a Player only their own
- Italian and English; component tests

Explicitly out of scope:
- The table, tokens, dice — later
- Character export — 097
- Any client-side rules. The client never computes a modifier

## Approach

**The client computes nothing.** Derived values come from the server's response and are displayed,
never calculated locally — `.claude/rules/frontend.md` forbids deriving rules on the client, and a
modifier computed in two places will eventually disagree in one of them.

**Derived fields are visibly not editable**, so it is clear at a glance what a player controls.
That is most of what "derived fields visibly computed" means in the roadmap.

**Errors land on their field.** The server returns a path like `/abilities/strength`; showing the
message next to that input is the difference between a fixable mistake and a shrug.

## Acceptance criteria

- [ ] A Player creates and edits their own character; the sheet reloads with server-computed values
- [ ] Derived values are visible and not editable
- [ ] Changing a score and saving updates the modifier shown, from the server's answer
- [ ] A schema error is shown against the field it names
- [ ] A Player cannot open the edit controls for another's character; the server refuses anyway
- [ ] Both languages; frontend tests, lint, typecheck, build clean; CI green

## Risks and things to watch

- **Do not compute modifiers in the client "for responsiveness".** Two implementations of one rule
  is the bug the frontend rules exist to prevent.
- The sheet is a document: a field the schema does not know must not be silently dropped on save.
