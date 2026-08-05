# 030 — `IGameSystem` interface + registry

**Status:** done
**Depends on:** 024
**Branch:** `task/030-igamesystem-interface-and-registry`

## Goal

The seam between the platform and the games it runs. The core gains a way to ask "which system is
this, and what does it say", and gains no knowledge of hit points, spell slots or armour class.

## Scope

In scope:
- `IGameSystem` in `Systems/`, per the contract in `.claude/rules/game-systems.md`
- A registry that resolves `(SystemId, Version)` to a module, and knows nothing else
- **Validating the pin that 020 has been storing unchecked**: creating a campaign against an unknown
  system or version is now refused
- Tests, including that an unknown pin is refused and a known one accepted

Explicitly out of scope (and which task covers it instead):
- **`Resolve`** — see below. The intent pipeline is 060–061 and the types it takes do not exist
- The JSON schema validator — 031, which consumes the schemas this interface exposes
- Any actual 5e content — 032
- `MigrateSheet` execution and the campaign upgrade flow — 080. The method exists; nothing calls it

## `Resolve` is deferred to 061, and this is a deviation

The binding contract declares `IntentResult Resolve(GameIntent intent, TableState state)`. Neither
`GameIntent` nor `TableState` exists: they are built by tasks 060 and 061, six tasks from here.

Defining them now would mean inventing the shape of the intent envelope and the whole live table
state before anything uses either — the speculative design the scope rules forbid, and a shape
almost certainly wrong for having been guessed. **030 ships the interface without `Resolve`; 061
adds it alongside the pipeline that calls it.** Recorded as a deviation.

Everything else in the contract lands here: `SystemId`, `Version`, both schemas, `RecomputeDerived`
and `MigrateSheet`.

## Approach

**The registry is a lookup, not a plugin host.** Modules are compiled into the application and
authored by the maintainer, so `.claude/rules/game-systems.md` needs no sandboxing and no discovery
protocol. A dictionary keyed on `(SystemId, Version)`, populated at registration.

**Version pinning becomes enforceable here.** 020 deliberately stored the pin without checking it,
because a hardcoded list would have been a second source of truth. The registry is the first source
of truth, so the check moves in.

**Schemas are exposed as documents, not as a validator type.** 031 owns validation; this interface
says what the schema *is*, and something else decides whether a document conforms.

## Acceptance criteria

- [ ] `IGameSystem` matches the binding contract except for the deferred `Resolve`
- [ ] The registry resolves a known `(SystemId, Version)` and returns nothing for an unknown one
- [ ] Creating a campaign with an unregistered system or version is refused
- [ ] Campaigns created before this task still load — their pins must still resolve
- [ ] No file under `Campaigns/`, `Accounts/` or `Sessions/` mentions a rule from any game
- [ ] Suite green, format clean, CI green

## Risks and things to watch

- **A rule leaking into core is a defect**, not a shortcut. The registry must stay ignorant.
- **Existing campaigns pin `dnd5e 1.0`** from testing. If 032 registers a different version, those
  rows stop resolving — decide deliberately rather than discovering it.
- Do not implement `MigrateSheet` logic. The method exists so 080 has something to call.
