# 060 — Table engine actor

**Status:** not started
**Depends on:** 040 (SignalR hub), 054 (token movement), 024 (session entity)
**Branch:** `task/060-table-engine-actor`

## Goal

Replace the naive "hub method mutates the database and broadcasts" approach from Phase 1 with the
real engine: one single-threaded actor per active session, owning that table's state in memory and
processing all intents in sequence. Everything in Phase 2 builds on this.

## Scope

In scope:
- A per-session actor holding live table state (scene, tokens, initiative, pending rolls)
- An intent queue per actor; hub methods enqueue and return immediately
- Actor lifecycle: created when a session opens, disposed when it closes or goes idle
- A registry mapping session id → actor, safe for concurrent access from request threads
- Migration of existing token-movement flow onto the new path

Out of scope:
- The permission and resolution gates as separate concerns — task 061
- Visibility filtering — task 062 (until then, broadcasts stay as they are)
- Persistence, event log, snapshots — task 063
- Crash recovery — task 064

## Approach

One object per table owning its state, fed by a queue, drained by a single consumer loop. Nothing
outside the loop touches table state — hub methods only enqueue. Because exactly one thread ever
mutates a given table, no locking is needed around the state itself; the only concurrency concern
is the registry and the queue handoff.

Backpressure and failure need explicit answers, not defaults: what happens when the queue grows,
and what happens when processing an intent throws. An unhandled exception must not kill the actor
and silently freeze the table.

## Acceptance criteria

- [ ] All table mutations flow through the actor; no code path mutates table state from a request
      thread
- [ ] Token movement works exactly as before from the user's perspective
- [ ] Concurrent intents from multiple clients produce a deterministic, sequential result
- [ ] An exception while processing one intent does not stop the actor or lose subsequent intents
- [ ] Actor is disposed when the session closes; no leak across open/close cycles
- [ ] Tests: concurrent-intent ordering, exception isolation, lifecycle

## Concepts to explain

- The actor model: why single-threaded-per-entity removes an entire class of concurrency bugs, and
  what it costs in exchange
- How this maps onto .NET primitives — `Channel<T>`, background processing, and why this is
  preferable to locks around shared state
- Where SignalR's threading model ends and ours begins: what the hub guarantees, what it does not
- Why in-memory state with periodic persistence beats read-modify-write against the database on
  every intent, and what that trades away
- The failure modes being designed against, concretely: lost intents, out-of-order application,
  a wedged table, an actor that outlives its session

## Risks and things to watch

- Async-over-sync mistakes here are hard to debug later. No blocking calls inside the loop.
- The registry is genuinely concurrent even though the actors are not — that boundary is where
  race conditions will actually appear.
- This task rewrites working Phase 1 code. Keep the user-visible behaviour identical; if it
  changes, the rework is out of control.
