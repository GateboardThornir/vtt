# 040 — SignalR hub scaffold

**Status:** done
**Depends on:** 035
**Branch:** `task/040-signalr-hub-scaffold`

## Goal

A real-time channel per session: authenticated connections, one group per table, and a connection
lifecycle that behaves when people close laptops. Everything live in Phase 2 rides on this.

## Scope

In scope:
- A `TableHub` requiring an authenticated, active account — the cookie from 013 carries it
- Joining a session's group, refused unless the caller is on the campaign's roster **and** the
  session is open
- Connection lifecycle: join, leave, disconnect, and a roster of who is currently connected
- A typed client contract so 044 and Phase 2 extend it rather than inventing message names
- Integration tests over a real connection, not a mocked hub

Explicitly out of scope (and which task covers it instead):
- Chat — 041, the first real message on this channel
- Dice — 042
- The table engine, intents, deltas, visibility filtering — 060 onwards
- Reconnection with a grace timer and snapshot resync — 065
- WebRTC signalling — 090

## Approach

**Authorisation happens on join, not on connect.** A connection proves who you are; a group proves
what you may see. The check is 021's resolver plus "is this session open", and it runs server-side
on every join — a client that calls `JoinSession` for a campaign it is not on gets nothing.

**Groups are per session, not per campaign.** Two sessions of one campaign are different tables, and
a closed session has no live audience at all.

**The hub is thin.** It authenticates, joins, leaves and broadcasts. It holds no game state: that is
the table actor's job at 060, and a hub that accumulates state is a hub that cannot be replaced.

**Cookie authentication carries over.** The browser sends the same session cookie on the WebSocket
handshake, so nothing new is issued and nothing new can leak.

## Acceptance criteria

- [ ] An anonymous connection is refused
- [ ] A member of the campaign can join an open session's group
- [ ] A stranger cannot join, and learns nothing about whether the session exists
- [ ] Joining a session that is not open is refused
- [ ] Participants see each other join and leave
- [ ] A disconnect removes the participant
- [ ] Tests drive a real connection end to end
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **A hub method is a public endpoint.** Every one needs the same authorisation as an HTTP route;
  forgetting is easier here because there is no visible URL.
- **Group names are derived from the session id** and must never be taken from the client verbatim.
- Do not put game state in the hub. 060 owns that, and duplicating it is how the two disagree.
