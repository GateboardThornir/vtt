---
paths:
  - "src/Server/Systems/**/*.cs"
  - "src/Server/Systems/**/*.json"
  - "tools/srd-ingest/**/*.py"
---

# Game system modules

## The contract

A system module implements `IGameSystem`. The platform core knows nothing about hit points, spell
slots, or armour class — if core code contains a rule from a specific game, that is a defect.

```csharp
public interface IGameSystem
{
    string SystemId { get; }        // "dnd5e"
    string Version { get; }         // semver, pinned per campaign

    JsonSchema CharacterSheetSchema { get; }
    JsonSchema CompendiumEntrySchema { get; }

    SheetDocument RecomputeDerived(SheetDocument sheet);
    IntentResult Resolve(GameIntent intent, TableState state);
    SheetDocument MigrateSheet(SheetDocument sheet, string fromVersion);
}
```

`RecomputeDerived` is deliberately separate from `Resolve` because Master overrides write raw
fields without passing through `Resolve`. It must be called after **every** sheet write.

`Resolve` returns either a state delta or a rejection carrying a player-facing reason. It must be
pure with respect to the passed state: it computes a delta, it does not mutate.

## Versioning — the hard rule

Every campaign pins `(SystemId, Version)` at creation. A campaign never moves to a new version
implicitly. Upgrading is an explicit, user-initiated operation that runs `MigrateSheet` for every
affected document inside a transaction, with a backup taken first.

Before shipping any change that alters a sheet or compendium schema shape:
1. Bump the module version.
2. Implement the migration path from the previous version.
3. Test the migration against realistic documents from the old version.

Shipping a breaking schema change without a migration is the one mistake that destroys years of
campaign data. Refuse to do it.

## Master overrides

Overrides bypass **rule** validation — the Master may set values the rules would forbid, on
purpose. They do **not** bypass **schema** validation: types, required fields, and enum membership
(e.g. a condition must be one this system defines) are always enforced. Without that floor, one
bad override corrupts a sheet in a way that surfaces much later, mid-session.

## SRD content

Only SRD-licensed material enters the repository or the seeded database. The ingestion pipeline
lives in `tools/srd-ingest/` (Python, offline, run manually) and emits documents conforming to the
module's compendium schema. It is not part of the runtime.
