# 024 — Session entity

**Status:** done
**Depends on:** 021 (a roster to play with)
**Branch:** `task/024-session-entity`

## Goal

A campaign can have sessions: a Master creates one, opens it when play begins and closes it when it
ends. Everything the live table does from Phase 2 hangs off an open session.

## Scope

In scope:
- A `Session` entity: campaign, title, state (`Planned` | `Open` | `Closed`), timestamps
- The Master creates, opens and closes; any active member of the campaign can see them
- At most one open session per campaign — two live tables in one campaign is not a state that means
  anything
- Integration tests, including that a Player cannot open or close, and that a stranger sees nothing

Explicitly out of scope (and which task covers it instead):
- **Scheduling and RSVP** — 077. The roadmap is explicit that 024 is create/open/close only
- Anything that happens *at* the table: chat, dice, scenes, tokens — 040 onward
- Reopening a closed session. Whether that should be possible is a real question and nothing needs
  an answer yet
- Session notifications — 077, with the scheduling that makes them useful

## Approach

**One open session per campaign, enforced where it can actually hold.** A partial unique index — one
row per campaign where the state is `Open` — makes it a database guarantee rather than a check that
races. This is the third time this pattern has come up, after usernames and invites.

**A session's visibility is its campaign's.** No new rule: the campaign-role resolver from 021
answers, and a stranger gets the same 404 a campaign gives.

**States move forward only.** `Planned → Open → Closed`, using the same small transition table as
accounts and memberships.

## Acceptance criteria

- [ ] The Master creates a session; it starts `Planned`
- [ ] Opening makes it `Open`; closing makes it `Closed`
- [ ] A second session cannot be opened while one is open — and the guarantee is the database's
- [ ] A Player can list sessions but cannot create, open or close
- [ ] A stranger gets 404 for everything, matching the campaign's behaviour
- [ ] Closed sessions stay listed — the history is the point
- [ ] Migration read before committing
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **A partial unique index is easy to get subtly wrong** in a migration; verify by trying to break it.
- Closing a session must not delete anything. Phase 2's event log will hang off these rows.
- Do not add scheduling. 077 owns it and will want the notification flow in view.
