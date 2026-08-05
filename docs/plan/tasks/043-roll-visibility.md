# 043 — Roll visibility

**Status:** not started
**Depends on:** 042
**Branch:** `task/043-roll-visibility`

## Goal

Public, private and Master-only rolls, enforced where it counts. The first real per-recipient
filtering in the platform, and a rehearsal for fog of war.

## Scope

In scope:
- Three visibilities: `Public` (everyone at the table), `Private` (the roller and the Master),
  `MasterOnly` (the Master alone, for secret checks they make)
- A `Roll` entity persisted with its visibility, faces and total
- **Per-recipient filtering at the broadcast boundary** — a hidden roll is never sent to a client
  that may not see it, not even as an opaque event
- History filtered the same way
- Tests asserting the *absence* of hidden data in each recipient's payload

Explicitly out of scope:
- The UI — 044
- Rolling from a sheet, or system-supplied semantics — Phase 2
- Fog of war and handout secrets — 062 and 075, which use this shape

## Approach

**Filter by computing per recipient, not by sending and hiding.** `.claude/rules/security.md` is
unambiguous: a payload that reaches a client it should not is a defect, and "the UI will hide it"
is not a mitigation. Each recipient gets a message computed for them or no message at all.

**A hidden roll produces no event for the excluded.** Not a redacted one, not a placeholder. An
opaque "somebody rolled something" is still a disclosure — it tells a player the Master is checking
something, which is exactly the information a secret roll exists to withhold.

**The Master sees private rolls.** A player rolling privately is hiding from other players, not from
the person running the game; that is what "private" means at a table.

**Tests assert absence.** The binding rule requires it for every feature with hidden data. Asserting
that the right people see a roll is the easy half; the half that matters is that the others' payload
does not contain it.

## Acceptance criteria

- [ ] A public roll reaches everyone at the table
- [ ] A private roll reaches its roller and the Master, and **nobody else receives anything**
- [ ] A Master-only roll reaches the Master alone
- [ ] History is filtered identically — a reconnecting player never learns of hidden rolls
- [ ] A player cannot make a Master-only roll
- [ ] The faces of a hidden roll appear in no excluded recipient's payload, asserted directly
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **Broadcasting to a group and filtering client-side is the failure mode.** SignalR makes group
  broadcast the easy path, and it is the wrong one here.
- **An opaque placeholder is still a leak.** Send nothing.
- This is the shape 062 will reuse for fog of war; getting the boundary right here is worth more
  than the feature itself.
