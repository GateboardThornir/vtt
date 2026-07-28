# Implementation roadmap

End-to-end plan, from empty repository to official release. Every entry is one task = one branch =
one review. Tasks are deliberately small: roughly one working session each.

**How to use this.** Pick the lowest-numbered task whose dependencies are met. Expand it into a
full card in `tasks/NNN-*.md` using `tasks/TEMPLATE.md` if it does not exist yet, get the card
approved, then enter plan mode for the file-level plan. Update `PROGRESS.md` when done.

Dependencies are listed only where they are not simply "the previous task".

---

## Phase 0 — Foundations

Goal: a repository that builds, tests, and runs locally, with nothing game-specific in it.

| ID | Task | Notes |
|---|---|---|
| 001 | Repo scaffold | Solution layout, `src/Server`, `src/Client`, `tests/`, `.editorconfig`, `.gitignore`, README |
| 002 | Docker Compose dev environment | Postgres service, app config via env vars, health endpoint responding |
| 003 | EF Core setup + initial migration | DbContext, connection config, migration workflow documented |
| 004 | Frontend scaffold | Vite + React + TS strict, dev proxy to the API, one rendered page |
| 005 | Test harness | xUnit + Vitest wired, one meaningful test each, integration test hitting real Postgres |
| 006 | CI pipeline | GitHub Actions: build, test, on every push |

## Phase 1 — First playable

Goal: the maintainer's group can run a real session, with voice on Discord.
Exit criterion: a session is actually played end to end.

### Accounts

| ID | Task | Notes |
|---|---|---|
| 010 | User entity + password hashing | No email fields. Account states: Pending, Active, Disabled |
| 011 | Invite tokens | Admin generates single-use, expiring tokens |
| 012 | Registration via invite URL | Consumes token, creates Pending account |
| 013 | Login / logout | Cookie sessions; Pending accounts rejected with a clear reason |
| 014 | Admin approval queue | Approve / reject pending registrations |
| 015 | Admin recovery codes | Single-use code lets the user set a new password; admin never sees it |
| 016 | Authorization policies | Platform role (admin/member) + campaign role resolution helper |
| 017 | Auth UI | Login, register-with-invite, pending screen, admin queue screen |

### Campaigns

| ID | Task | Notes |
|---|---|---|
| 020 | Campaign entity | Creator becomes Master. Pins `(SystemId, Version)` at creation |
| 021 | Campaign roster | Master invites members; accept / decline; leave |
| 022 | In-app notifications | Model + UI. Consequence of having no email — every flow needs this |
| 023 | Campaign list and detail UI | Depends on 021, 022 |
| 024 | Session entity | Create a session within a campaign; open / close it. Scheduling comes in Phase 2 |

### Game system foundation

| ID | Task | Notes |
|---|---|---|
| 030 | `IGameSystem` interface + registry | Core knows no game rules. Version pinning enforced |
| 031 | JSON schema validation infrastructure | Validate documents against a module's schema before persisting |
| 032 | 5e module skeleton + sheet schema v1 | Abilities, proficiency, HP, AC, skills. Deliberately narrow |
| 033 | `RecomputeDerived` for 5e | Modifiers, saves, skill totals, passive perception |
| 034 | Character entity | Create character in a campaign; sheet stored as JSONB; ownership |
| 035 | Character sheet UI | Read + edit, derived fields visibly computed, Master can edit any sheet |

### Chat and dice

| ID | Task | Notes |
|---|---|---|
| 040 | SignalR hub scaffold | Authenticated connections, per-table groups, connection lifecycle |
| 041 | Table text chat | Persisted, scoped to the session |
| 042 | Dice expression parser + roller | Server-side rolling; system supplies semantics. Never roll on the client |
| 043 | Roll visibility | Public / private / Master-only, enforced server-side (depends on 042) |
| 044 | Chat and roll UI | IC/OOC distinction, roll rendering |

### Map and tokens

| ID | Task | Notes |
|---|---|---|
| 050 | Asset upload service | Per-campaign quotas, ACL-gated serving endpoint, no public paths |
| 051 | Scene entity | Map image + grid configuration; Master creates scenes |
| 052 | PixiJS canvas scaffold | Render map, pan, zoom, grid overlay. Mounted outside React's render cycle |
| 053 | Token entity + placement | Strict 1:1 ownership; Master places anything |
| 054 | Token movement | Client sends intent, server validates and broadcasts; optimistic drag with reconciliation |
| 055 | Master-hidden layers | Prep scenes and hidden tokens never sent to player clients |
| 056 | Table shell UI | Canvas + sidebar + chat in one usable layout |

## Phase 2 — The real table

Goal: the platform replaces the paper. Rules automation, fog of war, secrets.
Note: 060–065 rework the naive Phase 1 sync into the real engine. This is intentional —
Phase 1 exists to learn what the table actually needs before hardening it.

| ID | Task | Notes |
|---|---|---|
| 060 | Table engine actor | Single-threaded per-session loop, in-memory state, intent queue |
| 061 | Intent pipeline | Auth → permission → resolution gates, typed envelopes, typed rejections |
| 062 | Per-recipient visibility filter | The security boundary. Tests asserting absence of hidden data are mandatory |
| 063 | Event log + snapshots + pruning | Append on apply, snapshot on scene change / 5 min, prune behind snapshots |
| 064 | Crash recovery | Rehydrate a table from snapshot + log on restart |
| 065 | Reconnection handling | 90s grace timer, snapshot resync on return |
| 066 | Fog of war data model | Server-side revealed-region masks per player |
| 067 | Fog of war rendering + reveal tools | Pixi masking; Master brush/polygon reveal (depends on 066) |
| 068 | Initiative tracker | Advisory: displays order, enforces nothing |
| 069 | Auto-skip on disconnect | Pointer advances if the grace timer expires on that character's turn |
| 070 | 5e automation: attacks | First real `Resolve` handler: to-hit, advantage/disadvantage, crits |
| 071 | 5e automation: damage and HP | Damage types, resistances, unconscious/death state |
| 072 | Master override path | Direct write bypassing rule validation; schema floor enforced; `RecomputeDerived` after |
| 073 | 5e automation: conditions | Apply, track, expire; effects on other rolls |
| 074 | 5e automation: resources | Spell slots, limited-use features, rests |
| 075 | Handouts + per-player ACL | Campaign-wide default, per-item per-player exceptions |
| 076 | Handout reveal UI | Master reveals during play; secrets never reach unauthorised clients |
| 077 | Session scheduling + RSVP | Schedule ahead, per-session RSVP, notifications |
| 078 | SRD ingestion pipeline | Python, offline; emits documents conforming to the 5e compendium schema |
| 079 | Compendium browse + Master uploads | Search, filter, share to players; schema-conformant uploads participate in automation |
| 080 | System version migration tooling | `MigrateSheet` execution, campaign upgrade flow, backup before migrate |

## Phase 3 — Full vision

Goal: everything in the spec. Ends with the official release.

| ID | Task | Notes |
|---|---|---|
| 090 | WebRTC signaling over SignalR | Offer/answer/ICE relay between table participants |
| 091 | P2P mesh audio | Peer connections, join/leave, mute (depends on 090) |
| 092 | Video tracks | Camera toggle, bandwidth-conscious defaults |
| 093 | coturn deployment + credentials | Ephemeral TURN credentials issued by the server |
| 094 | Media UI | Participant tiles, mute/deafen, connection quality indicator |
| 095 | Ambient audio library + playback control | Master-controlled play/pause/seek/volume for the table |
| 096 | Audio sync client | Clients fetch the file and obey commands — no media streaming through the server |
| 097 | Character export | Portable JSON, player-initiated, includes system id + version |
| 098 | i18n completion | i18next infrastructure, full IT + EN string coverage, language switcher |
| 099 | Deep 5e automation pass | Spellcasting, saving throws, areas of effect, concentration |
| 100 | Backups | Nightly `pg_dump` + asset sync to R2/B2, restore procedure documented and tested |
| 101 | Production deployment | Caddy TLS, compose on the VPS, CI deploy, environment configuration |
| 102 | Admin tooling | Storage dashboard, quota management, account administration |
| 103 | Canvas performance pass | Profile with realistic scenes; batch scene-graph updates per frame |
| 104 | Release checklist | Restore drill, security review against `security.md`, SRD licence attribution |

---

## Standing rules for this plan

- **Never skip ahead.** If a task seems blocked, the dependency is usually a real gap — surface it.
- **A phase is not done until it has been played.** Phase exits are validated by a real session,
  not by a green test suite.
- **The plan is expected to change.** When real play contradicts a card, amend the card and record
  why in `PROGRESS.md`. Do not silently deviate.
