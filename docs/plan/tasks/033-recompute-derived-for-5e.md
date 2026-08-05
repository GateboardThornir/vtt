# 033 — `RecomputeDerived` for 5e

**Status:** not started
**Depends on:** 032
**Branch:** `task/033-recompute-derived-for-5e`

## Goal

The first rules the platform actually computes: ability modifiers, saving throws, skill totals and
passive perception, derived from what a player chose.

## Scope

In scope:
- `RecomputeDerived` on the 5e module, filling the `derived` object from the raw values
- Ability modifiers, saving throws with proficiency, all eighteen skills with their governing
  ability, passive perception
- Tests against hand-checked values, including the negative-modifier boundary

Explicitly out of scope:
- Anything that *resolves* an action — attacks, damage, conditions are Phase 2
- Calling it from a write path — 034 persists sheets and is where the unconditional call lives
- Class features, resistances, encumbrance. The schema does not have them

## Approach

**Pure, and total.** It takes a sheet and returns a new one; it never mutates its input and never
partially fills `derived`. Half-computed derived values are worse than none, because they look
authoritative.

**It overwrites `derived` entirely**, rather than merging. Anything already there was either
computed by a previous call or written by hand, and neither is a reason to keep it — this is what
makes the Master override path safe.

**The rules are ordinary 5e arithmetic**, stated once each: a modifier is `floor((score - 10) / 2)`,
a proficient save or skill adds the proficiency bonus, passive perception is `10 + perception`.

## Acceptance criteria

- [ ] Modifiers match the standard table across the whole 1–30 range, including odd scores
- [ ] A score below 10 gives a negative modifier — `floor`, not truncation toward zero
- [ ] Proficient saves and skills include the bonus; others do not
- [ ] All eighteen skills appear, each against the right ability
- [ ] Passive perception accounts for proficiency
- [ ] The input document is not mutated
- [ ] The output still validates against the schema
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **`floor`, not truncation.** In C#, integer division truncates toward zero, so a score of 7 gives
  −1 by truncation and −2 by the rules. This is the classic bug in this function.
- **Overwriting `derived` is deliberate.** Merging would preserve a hand-edited value and make the
  sheet permanently inconsistent.
- Every rule here is 5e's, and none of it may leak toward the core.
