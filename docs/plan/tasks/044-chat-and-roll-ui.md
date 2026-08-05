# 044 — Chat and roll UI

**Status:** done
**Depends on:** 043
**Branch:** `task/044-chat-and-roll-ui`

## Goal

A table you can sit at: talk, roll, and see what everybody else is doing — the first screen in the
project that is live rather than request/response.

## Scope

In scope:
- A session view: participants, chat log, message box, dice box
- One SignalR connection per session, owned by a single connection manager, per
  `.claude/rules/frontend.md`
- In-character and out-of-character distinguished visibly
- Rolls rendered from their faces — `[4, 6] + 3 = 13` — never recomputed
- Visibility chosen when rolling, with Master-only offered only to the Master
- Both languages; component tests with the hub stubbed

Explicitly out of scope:
- The canvas, tokens, fog — 050 onwards
- Reconnection with a grace timer — 065
- Typing indicators, editing, reactions

## Approach

**One connection, one owner.** The rules require a single connection manager that components
subscribe to; components never open their own. That is what makes 065's reconnection a change in one
place.

**Render, never recompute.** The roll arrives with its faces and its total. Adding them up in the
client would be a second implementation of the same rule, and the one that disagrees is always the
one nobody tested.

**Master-only is offered only to a Master.** The server refuses a player who asks anyway — that is
tested at 043 — so this is courtesy, not enforcement.

## Acceptance criteria

- [ ] Joining an open session shows participants, history and arrivals
- [ ] Sending a message shows it to everyone in the view
- [ ] In-character and out-of-character are visually distinguishable
- [ ] A roll renders its faces and total as the server sent them
- [ ] A player is not offered Master-only; a Master is
- [ ] Both languages; tests, lint, typecheck, build clean; CI green

## Risks and things to watch

- **Do not compute a total in the client.** Two implementations of one rule.
- A component opening its own connection breaks 065 before it is written.
