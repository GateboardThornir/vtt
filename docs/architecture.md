# Architecture

Companion to `functional-spec.md`. That document says *what* the platform does; this one says
*how it is built*. Decisions here are settled unless explicitly revisited.

## Stack

| Layer | Choice | Why |
|---|---|---|
| Backend | ASP.NET Core (C#), modular monolith | Strongest language available to the maintainer; static typing pays off most in the rules engine, the hardest code in the project |
| Real-time | SignalR | WebSocket transport with reconnection and group management built in; doubles as the WebRTC signaling channel |
| Frontend | React + TypeScript (Vite) | Largest ecosystem and example base for a solo developer; mature i18n |
| Canvas | PixiJS (WebGL) | DOM/SVG collapses under fog-of-war compositing and hundreds of sprites |
| Database | PostgreSQL | Relational core plus JSONB for system-defined documents, in one engine |
| Tooling | Python | Offline pipelines only, notably SRD ingestion |
| Deployment | Docker Compose on a single 4 GB VPS | Matches the <50-user scale; no orchestration complexity to maintain |

Rejected: full-stack TypeScript (one language, shared types) — defensible, but SignalR plus a
statically typed rules engine outweighed the context-switching cost.

## Deployment topology

A single Hostinger VPS runs Caddy (TLS termination, reverse proxy), the ASP.NET application,
PostgreSQL, and coturn, via Docker Compose. CI builds an image on GitHub Actions; deployment is
`compose pull && compose up -d`.

Live audio and video **do not touch the server**: participants form a peer-to-peer WebRTC mesh,
with the server providing only signaling (over SignalR) and STUN. coturn provides TURN relay as a
fallback for participants whose NAT blocks direct connection — this path does consume server
bandwidth and is the exception, not the norm. A mesh is viable because tables are 4–6 people;
it degrades beyond that, which is outside the product's scope.

Nightly `pg_dump` plus asset sync to off-site object storage (Cloudflare R2 or Backblaze B2).
Backups leave the VPS: a single box holding the only copy of multi-year campaigns is the
project's largest data risk.

## Real-time table engine

**Actor per table.** Each active session gets one server-side object holding its live state
(scene, tokens, fog, initiative, pending rolls) in memory, processed by a single-threaded loop.
All intents for that table funnel through that loop, so there is no lock contention and no
possibility of two intents racing into an inconsistent state.

**Intent pipeline.** Every action from either role is an envelope
`{ actorId, intentType, payload }` passing through four stages in fixed order:

1. **Auth** — is this connection who it claims to be?
2. **Permission** — does the actor control the target? (strict 1:1 token ownership; Master
   controls everything in their own campaign)
3. **Resolution** — gameplay intents route to the active `IGameSystem` handler; Master overrides
   take the direct-write path, bypassing rule validation but not schema validation
4. **Visibility filter** — the resulting delta is filtered per recipient before broadcast

Both resolution paths emit the same delta type, so overrides get no backdoor around the filter.

**Persistence.** Applied intents append to an event log. Full snapshots are written on scene
change and every 5 minutes, whichever comes first. Once a snapshot is durable, preceding event
rows are pruned: the log exists for crash recovery, not for session replay. A crash mid-session
therefore costs nothing meaningful, and the live database stays small regardless of session length.

**Reconnection.** A dropped connection starts a 90-second grace timer. On reconnect within the
window, the client receives a fresh snapshot and replaces its local state. If the timer expires
while it is that character's turn, the initiative pointer advances to the next entry — this moves
the *displayed* pointer only, consistent with initiative being advisory throughout, and does not
lock the player out on return.

## Game systems

See `.claude/rules/game-systems.md` for the interface and the versioning discipline, which is
binding. In summary: hybrid packaging — declarative JSON schemas for sheet and compendium shape,
C# modules for automation — authored by the maintainer only, so no third-party code sandboxing is
required. Campaigns pin a system version; upgrades are explicit and migrated.

## Data model outline

Relational: `users`, `invites`, `recovery_codes`, `campaigns`, `campaign_members` (role: gm |
player), `sessions`, `rsvps`, `characters`, `scenes`, `tokens`, `assets`, `handouts`,
`acl_entries`, `notifications`, `table_events`, `table_snapshots`.

JSONB, validated against the pinned system version's schemas: character sheet documents,
compendium entries, token/actor definitions, snapshot payloads.

The split is deliberate — anything the platform must query, join, or authorise against is
relational; anything whose shape is defined by a game system is a document.

## Security posture

Detailed in `.claude/rules/security.md`, which is binding. The governing principle: the realistic
threat is a player with DevTools, so server-side visibility filtering is a security boundary, not
a display concern. Authentication is username/password with server-side cookie sessions, no email
anywhere; invite tokens and admin recovery codes are single-use.

## What is deliberately not here

No microservices, no message broker, no Kubernetes, no Redis, no CQRS/event-sourcing framework,
no SFU. At <50 users and a handful of concurrent tables, each of these adds operational burden a
solo maintainer pays for daily and buys capacity that will never be used. If a specific bottleneck
appears, it gets solved then, with evidence.
