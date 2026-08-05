# 041 — Table text chat

**Status:** done
**Depends on:** 040
**Branch:** `task/041-table-text-chat`

## Goal

People at a table can talk, and what they said is still there tomorrow. The first real traffic on
the hub, and the carrier every dice roll will later ride on.

## Scope

In scope:
- A `ChatMessage` entity scoped to a session: author, body, in-character or out-of-character, time
- Sending through the hub, persisted then broadcast to the session's group
- Loading recent history when a client joins
- Rejecting a message from anyone not admitted to that table
- Integration tests over a real connection

Explicitly out of scope (and which task covers it instead):
- Dice — 042, which produces messages of a different kind on this same channel
- Private and Master-only visibility — 043. Everything here is visible to the whole table
- The UI — 044
- Editing, deleting, reactions, typing indicators

## Approach

**Persist, then broadcast.** A message that was shown but not stored is a message that vanishes on
refresh, and this is a record of play people will read back.

**In-character is a property of the message, not a separate channel.** The spec asks for the
distinction, not for two rooms; a flag keeps the history in one ordered sequence.

**The hub re-checks admission on every send.** Being in a group is not proof of anything later —
someone removed from a roster mid-session must stop being able to talk.

## Acceptance criteria

- [ ] A member of an open session sends a message and everyone in the group receives it
- [ ] The message survives a reconnect: history loads on join
- [ ] Someone who is not admitted cannot send, and cannot read history
- [ ] A member removed from the roster mid-session can no longer send
- [ ] In-character and out-of-character round-trip
- [ ] Empty or oversized messages are refused
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **Re-check admission on send**, not just on join. Group membership outlives the right to it.
- History is a disclosure surface: it must be scoped to the session and the caller's admission.
