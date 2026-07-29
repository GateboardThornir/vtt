# 001 — Repository layout and build conventions

**Date:** 2026-07-29
**Status:** accepted

## Context

The first code in an empty repository fixes decisions that every later task inherits: how projects
are divided, what namespace every file declares, which .NET version the project lives on, and how
strict the build is. Changing any of them later means touching every file in the repository, so
they are settled now and recorded here.

Constraints in play: a single developer, a modular monolith (`docs/architecture.md`), a target of
fewer than fifty users, and a rules engine that is the hardest code in the project and benefits
most from the compiler being pedantic.

## Decision

**One server assembly, folders as module boundaries.** `src/Server` is a single project.
`Accounts/`, `Campaigns/`, `Table/`, `Systems/` and the rest are folders inside it, not separate
projects. Modules talk through public interfaces in their own folder.

**`Vtt.` namespace prefix.** Directories stay `src/Server` and `tests/Server.Tests`, but the
projects are `Vtt.Server` and `Vtt.Server.Tests`, and root namespaces match.

**`net10.0`,** the current LTS, pinned to the `10.0.1xx` SDK feature band in `global.json` with
`rollForward: latestFeature`.

**Warnings are errors,** repository-wide, via `Directory.Build.props`. Nullable reference types are
enabled in the same place.

**Code style is not enforced by the build.** `.editorconfig` carries the conventions and drives the
IDE and `dotnet format`; `EnforceCodeStyleInBuild` stays off.

**Classic `.sln`,** not the newer `.slnx`.

## Consequences

A single assembly makes cross-module refactoring cheap while the boundaries are still being
discovered, and keeps build times trivial. It also removes the compiler as an enforcer of module
boundaries: nothing physically stops `Table/` from reaching into the internals of `Accounts/`.
That discipline now rests on review and on `.claude/rules/backend.md`. If a boundary starts being
violated repeatedly, that is the evidence for splitting the assembly — not before.

The `Vtt.` prefix costs four characters per file and buys unambiguous identifiers. `Server.Table`
and `Server.Systems` would have been uncomfortably close to framework and BCL vocabulary.

Warnings-as-errors from the first commit means the codebase never accumulates a warning backlog,
and nullability annotations stay honest. The price is real: an occasional false positive must be
suppressed explicitly rather than ignored, and a warning introduced by an SDK upgrade will break
the build until it is dealt with. Both are acceptable while the codebase is small; both would have
been unaffordable to retrofit once it is not.

Leaving style out of the build keeps `dotnet build` from failing over a misplaced brace during a
task, at the cost of formatting drift going unnoticed until someone runs `dotnet format`. Task 006
adds `dotnet format --verify-no-changes` to CI, which closes that gap at the right moment.

## Alternatives rejected

**A project per module** (`Vtt.Accounts.csproj`, `Vtt.Table.csproj`, …). This is the honest way to
make module boundaries compiler-enforced, and it is genuinely tempting for a project whose central
risk is the table engine tangling with everything else. Rejected because the boundaries are not yet
known — the first six months of this project will move code between modules repeatedly, and each
move would mean editing project references and fighting circular dependencies. The modular monolith
is explicitly the architecture; a folder is the cheapest representation of a boundary that is still
provisional.

**Bare `Server` / `Server.Tests` names,** matching the folders exactly. Shorter, and one less
concept. Rejected: `Server` is a common enough identifier that `using Server.Systems;` reads
ambiguously, and the cost of the prefix is paid once per file by an editor's autocomplete.

**Enforcing style in the build.** Maximum consistency, and the argument for it is real — a solo
developer has nobody else to catch drift in review. Rejected for now because a build that fails on
formatting interrupts work at the worst moment; the CI gate in task 006 catches the same drift
without that cost. Worth revisiting if formatting noise ever shows up in diffs.

**`.slnx`,** the newer XML solution format — and notably the default that `dotnet new sln` produces
on .NET 10, so this decision is actively swimming against the SDK. It is genuinely cleaner: no
GUIDs, no merge conflicts on the solution file. Rejected only because the solution file is read by
CI actions, the C# Dev Kit and any tooling adopted later, and the classic format is the one all of
them are certain to handle. The benefit is cosmetic and the format is trivial to switch to later
with `dotnet sln migrate`.
