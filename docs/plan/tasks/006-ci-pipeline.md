# 006 — CI pipeline

**Status:** not started
**Depends on:** 005 (there must be suites worth running)
**Branch:** `task/006-ci-pipeline`

## Goal

Every push builds, tests and style-checks itself on GitHub, so a broken `main` is caught by a
machine rather than by the next session. This closes Phase 0 and discharges the three gates
deferred into it.

## Scope

In scope:
- `.github/workflows/ci.yml`: build and test both halves on every push and on pull requests
- Two jobs running in parallel — backend and frontend — because a frontend lint failure should not
  wait behind a container-backed test suite
- Toolchain versions read from the files developers already use: `global.json` for the .NET SDK,
  `.nvmrc` for Node. CI must not pin them a second time
- Dependency caching for NuGet and npm
- **The three deferred gates become binding**: `dotnet format --verify-no-changes` (deferred from
  001), `npm run lint` and `npm run typecheck` (deferred from 004), and both test suites (005)
- **Fixing the six violations that turning the format gate on exposes** — see below. This is the
  substance of the card, not a footnote
- `README.md`: a status badge and a sentence on what CI enforces

Explicitly out of scope (and which task covers it instead):
- **Deployment of any kind** — building a server image, pushing it to a registry, `compose pull` on
  the VPS. That is task 101, and this card must not grow a deploy step "while it is in there"
- Branch protection rules. They are GitHub repository settings, not files in the repository, so
  they cannot be committed — see the note under Risks
- Coverage upload, Dependabot, CodeQL and dependency scanning. Security review is task 104
- Release tagging and versioning. Nothing is released until Phase 3
- A build matrix across operating systems or SDK versions. One developer, one Linux VPS, one
  supported configuration — a matrix would buy coverage of platforms nobody runs

## What turning on the format gate exposes

`dotnet format --verify-no-changes` currently **exits 2 with six violations**. They fall into three
groups, and each needs a decision rather than a blind reformat:

**1. The EF-generated migration (2 violations).** `20260805091403_InitialCreate.cs` is written by
`dotnet ef` with a UTF-8 BOM and a block-scoped namespace; `.editorconfig` asks for no BOM and
file-scoped namespaces. Hand-editing generated files is pointless — the next migration reintroduces
both. This was flagged as a watch item when the migration pipeline was built and has now bitten
exactly as predicted. The fix belongs in `.editorconfig`, scoping the migrations directory as
generated code.

**2. The async-suffix rule versus test names (3 violations).** `dotnet_naming_rule.async_methods_end_in_async`
requires every `async` method to end in `Async`, which catches the xUnit tests —
`HealthEndpointReturnsOkWhenTheDatabaseIsReachable` would become
`HealthEndpointReturnsOkWhenTheDatabaseIsReachableAsync`. That fights
`.claude/rules/backend.md`, which says test names describe the behaviour asserted. The suffix is a
useful convention for an API surface a caller has to reason about, and pure noise on a test method
nobody calls. Recommendation: scope the rule to `src/` and exempt `tests/`.

**3. A `private const` caught by the private-field rule (1 violation).**
`dotnet_naming_symbols.private_field_symbols` uses `applicable_kinds = field`, and a `const` is a
field, so `PostgresFixture.EnvironmentVariable` is asked to become `_environmentVariable`. That is
wrong — constants are PascalCase in every .NET codebase. This is a latent defect in `.editorconfig`
from task 001 that no code happened to trigger until now. The rule needs to exclude constants.

The card's position: **`.editorconfig` is what changes, not the code.** All three are cases of a
style rule being wrong or over-broad, not of code being badly written. Rewriting six correct things
to satisfy a rule that should not apply to them is how a style gate loses its credibility.

## Approach

**CI checks what a developer can check.** Every command in the workflow must be one that runs
locally in the same form — `dotnet test`, `npm run lint`. If CI grows a step nobody can reproduce
on their machine, a red build becomes a guessing game.

**The backend job needs Docker; the frontend job does not.** GitHub's `ubuntu-latest` runners ship
with a working Docker daemon, so the Testcontainers integration tests run there unchanged. That was
the main risk ADR 005 accepted, and it costs nothing here beyond the image pull.

**Style is checked in CI, never in the build.** ADR 001 settled that `EnforceCodeStyleInBuild` stays
off, so formatting never fails a build mid-task. CI is where the drift gets caught — which is
precisely why this card is where the gate turns on.

**Trigger on push and pull request.** The working pattern is task branch → merge → push, so pushes
are what matter today; pull requests are covered so the workflow keeps working if that ever changes.

## Acceptance criteria

- [ ] A push runs the workflow, and both jobs pass on a clean tree
- [ ] `dotnet format --verify-no-changes` exits **0** locally and in CI
- [ ] The backend integration tests genuinely run on the runner — verified by reading the log for
      the container starting, not by the suite merely reporting success
- [ ] Deliberately breaking each gate fails the build, one at a time: a formatting violation, a lint
      error, a type error, a backend test, a frontend test
- [ ] A second run on an unchanged lockfile hits the dependency caches
- [ ] The whole workflow finishes in a few minutes, not tens of them
- [ ] `dotnet build`, `dotnet test` and the frontend suite still pass locally; `git status` clean

## Concepts to explain

- **GitHub Actions' model**: events trigger workflows, workflows contain jobs, jobs run on runners
  and contain steps. What is parallel by default and what forces sequencing
- **What a hosted runner is** — a fresh virtual machine per job, which is why caching exists and why
  nothing persists between jobs unless it is an artifact
- **Cache keys and `restore-keys`**: why the key is a hash of the lockfile, what a partial restore
  buys, and how a stale cache produces failures that look like flakiness
- **Why CI reads `global.json` and `.nvmrc`** rather than declaring versions itself — one source of
  truth, and version drift between a developer's machine and CI is a debugging trap
- **Three different enforcement mechanisms, deliberately kept apart**: compiler warnings (fail the
  build, always), analyzers (`AnalysisLevel`, fail the build), and `dotnet format` style rules
  (fail CI only). Why ADR 001 put style in the third category
- **Why a green CI is not a protected branch.** The check reports a result; something has to refuse
  the merge

## Risks and things to watch

- **CI is advisory until branch protection is enabled**, and branch protection lives in GitHub's
  settings, not in this repository. Until it is switched on by hand, a red build blocks nothing and
  a broken `main` is still possible. The card should end with that as an explicit manual step, and
  `PROGRESS.md` should record whether it was done.
- **Private repositories consume Actions minutes** from a monthly allowance. Two jobs per push, with
  a container pull in one of them, is modest — but a workflow that runs on every push to every
  branch adds up, and it is worth watching rather than discovering as a bill.
- **`dotnet format` behaviour is tied to the SDK version.** CI and the developer machine agree today
  because both read `global.json`, but a `rollForward: latestFeature` means they can drift onto
  different patch releases. If formatting ever passes locally and fails in CI, this is the first
  place to look.
- **The Testcontainers image pull happens on every run** unless cached, adding time to every build.
  Measure before optimising; the fix, if needed, is not obvious and may not be worth it.
- **Do not let this card acquire a deploy step.** Phase 0 ends with a repository that builds, tests
  and runs locally. Deployment is task 101, after there is something worth deploying.
- Resist expanding the fixes in the section above into a general `.editorconfig` overhaul. Three
  scoped corrections, each justified by a violation that actually occurred.
