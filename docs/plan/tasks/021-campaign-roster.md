# 021 — Campaign roster

**Status:** not started
**Depends on:** 020 (a campaign to have a roster)
**Branch:** `task/021-campaign-roster`

## Goal

A Master can invite members into a campaign, and they can accept, decline or leave. This is where
"Master" and "Player" stop being a column and become the campaign roles that every later permission
check reads.

## Scope

In scope:
- A `CampaignMember` entity: campaign, account, role (`Master` | `Player`), state
  (`Invited` | `Active` | `Left`), timestamps
- **The Master becomes a row in it**, resolving what 020 deferred — one place answers "what is this
  account to this campaign", rather than a column plus a table that must agree
- Inviting a member, accepting, declining, leaving; the Master removing someone
- **The campaign-role resolver**, moved here from 016 by that task's deviation: one helper that
  every campaign-scoped endpoint asks
- Campaign visibility widens from "you master it" to "you are on the roster" — the single predicate
  020 built for exactly this
- Integration tests, including that a departed member loses visibility immediately

Explicitly out of scope (and which task covers it instead):
- In-app notification of an invitation — 022. Until then an invitation is visible only by looking
- Campaign screens — 023
- Anything the roles gate other than the roster itself. Scenes, tokens and handouts arrive later and
  will use the resolver; this card gives them something to use
- Transferring the Master role, and campaigns with more than one Master. The spec says one Master
  per campaign; changing that is a design conversation

## Approach

**One table answers membership, including the Master's.** 020 stored `MasterUserId` on the campaign
because with one member there was nothing to join to. Now there is, and keeping both would mean two
sources of truth that can disagree — the classic way an authorisation bug appears. The column goes,
and the migration backfills a `Master` row for every existing campaign.

**Membership has a state, not just a role.** An invitation that has not been accepted is not
membership: an invited account must not see the campaign's content, and declining must be
distinguishable from never being asked. `Left` is kept rather than deleted so that history — who was
in the campaign when something happened — survives.

**The resolver is the single question.** `What is this account to this campaign?` returns the role or
nothing, and every campaign-scoped check from here to task 075 asks it. Scattering `MasterUserId ==`
comparisons is how the fog-of-war rules eventually get a hole.

**Platform admin still grants nothing.** The resolver reads the roster and only the roster.

## Acceptance criteria

- [ ] A Master can invite an account; it appears as `Invited` and the invitee cannot yet see the
      campaign's content
- [ ] Accepting makes them `Active` and the campaign appears in their list
- [ ] Declining and leaving both work, and a departed member immediately loses visibility
- [ ] A Player cannot invite, remove, or change anyone's role
- [ ] The Master cannot leave their own campaign — there would be no Master
- [ ] Inviting the same account twice does not create two rows
- [ ] The migration backfills a `Master` row for every existing campaign, and drops the column
- [ ] A platform administrator is still nothing to a campaign they are not on
- [ ] Suite green, format clean, CI green

## Concepts to explain

- **Why two sources of truth for the same fact is a bug waiting to happen**, using the Master column
- **Membership as a state machine**, and why "invited" must not imply access
- **Backfilling in a migration**: moving data as part of a schema change, and why `Down` is harder
- **A single resolver** as the thing that makes later visibility rules auditable

## Risks and things to watch

- **The backfill is the risky part of the migration.** It moves real data; read it carefully, and
  make sure `Down` is honest about what it cannot restore.
- **An `Invited` member must not see content.** The easiest mistake here is treating any row in the
  membership table as membership.
- **Leaving must not orphan a campaign.** The Master is the one member who cannot leave.
- Do not let the resolver grow into a permission system. It answers one question.
