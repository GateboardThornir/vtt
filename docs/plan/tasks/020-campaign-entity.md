# 020 — Campaign entity

**Status:** not started
**Depends on:** 017 (a signed-in member to create one)
**Branch:** `task/020-campaign-entity`

## Goal

A member can create a campaign and becomes its Master. The campaign pins the game system and version
it was created with — a foundational rule of the project, and one that is far cheaper to establish
now than to retrofit.

## Scope

In scope:
- A `Campaign` entity in a new `Campaigns/` module: name, Master, pinned `(SystemId, SystemVersion)`,
  creation timestamp
- Creating a campaign, which makes the creator its Master
- Listing the campaigns the caller can see, and fetching one
- **Non-membership means total invisibility**, per `.claude/rules/security.md`: a campaign the caller
  has nothing to do with must not appear, and fetching it directly must not confirm it exists
- Integration tests, including that a stranger gets a 404 rather than a 403

Explicitly out of scope (and which task covers it instead):
- **The roster** — inviting players, accepting, leaving — 021. Until then a campaign has exactly one
  member, its Master
- The campaign-role resolver, moved here from 016 by that task's deviation → lands at **021**, with
  the roster it resolves against
- Validating the pinned system against a real registry — 030, which is where `IGameSystem` and its
  versions first exist
- Sessions within a campaign — 024
- Campaign screens — 023
- Renaming, archiving or deleting a campaign. Nothing yet needs them, and deletion in particular
  wants a policy decision rather than an endpoint

## Approach

**Pinning is recorded now and enforced later.** The spec is unambiguous that a campaign pins its
system version and never migrates implicitly. Nothing can validate `(SystemId, SystemVersion)` until
030 builds the registry, so 020 stores what it is given and 030 adds the check. Storing it from the
first campaign means no campaign ever exists without a pin, which is the part that would be
expensive to fix later.

**A 404, not a 403, for a campaign you cannot see.** A 403 confirms the campaign exists, which is
itself information — the roster of a private group's campaigns is not public. Everything a caller is
not entitled to see simply is not there.

**Master is a column, not a role table, until 021.** With one member per campaign there is nothing
to join to. 021 introduces membership properly and the Master becomes a row in it like anyone else,
or stays denormalised — that is 021's decision to make with the roster in front of it.

## Acceptance criteria

- [ ] A member can create a campaign and is its Master
- [ ] The pinned system id and version are stored exactly as supplied, and are required
- [ ] Listing returns only campaigns the caller masters; a stranger's campaign never appears
- [ ] Fetching another member's campaign by id returns **404**, not 403
- [ ] An anonymous caller gets 401 from every endpoint
- [ ] A disabled account cannot create or read campaigns — the policy from 016 applies
- [ ] Name is validated at the boundary: not blank, bounded length
- [ ] Migration read before committing; table named explicitly
- [ ] Suite green, format clean, CI green

## Concepts to explain

- **Why version pinning is foundational**, and what breaks in a long-running campaign without it
- **404 versus 403 as a disclosure decision**, and when each is right
- **Denormalising the Master** now and what 021 will have to reconcile
- **Validating at the boundary** versus enforcing in the domain, applied to a name

## Risks and things to watch

- **Do not validate the system id against a hardcoded list.** That list would be a second source of
  truth that 030 then has to remove. Store what is given; 030 owns the check.
- **The 404 has to be a real 404 all the way down** — the same response shape as a campaign that
  genuinely does not exist, or the difference leaks through timing or body.
- Platform admin grants no campaign access. An administrator listing campaigns sees their own only.
- Resist adding a members table "since it is coming anyway". 021 owns it and will want to decide its
  shape with the invite flow in view.
