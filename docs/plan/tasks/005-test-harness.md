# 005 — Test harness

**Status:** not started
**Depends on:** 003 (a real database to test against), 004 (a frontend to test)
**Branch:** `task/005-test-harness`

## Goal

Both halves of the stack have a test setup someone would actually write tests in: xUnit reaching a
real Postgres through the running application, and Vitest rendering React components. From here on,
"add a test for it" is a small step rather than a decision about tooling — which matters most at
tasks 062 and 070, where the tests are the specification.

## Scope

In scope:
- **Integration testing infrastructure for the backend**: an ephemeral Postgres per test run,
  migrations applied to it, and the application started against it in-process
- **One meaningful backend integration test**: `GET /api/health` returns 200 and reports the
  database healthy, exercised through the real HTTP pipeline against the real database. This
  discharges the "automated test for `GET /health`" deferred from task 001
- **Vitest wired into `src/Client`** with jsdom and React Testing Library
- **One meaningful frontend test**: `App` renders its three states — healthy, server-reported
  unhealthy, unreachable — with the network stubbed
- Separating tests that need Docker from tests that do not, so a run without Docker fails with a
  clear reason rather than a connection timeout
- Replacing the `TestHarnessTests` placeholder from task 001, which has done its job
- Conventions written down: where tests live, how they are named, what belongs in a unit test
  versus an integration test
- `README.md` and `docs/dev-setup.md`: how to run each suite, and what needs to be running

Explicitly out of scope (and which task covers it instead):
- **Running any of this in CI** — task 006. This card makes the suites runnable; 006 makes them
  mandatory, and that is also where the frontend lint and typecheck gates land
- Coverage measurement and thresholds. A coverage number over a codebase with no domain logic is
  theatre; revisit when there is something worth covering
- End-to-end browser testing (Playwright and similar). Nothing yet has a flow long enough to
  justify it, and the frontend is one diagnostic page
- Database cleanup between tests (Respawn or equivalent). There are no domain tables yet, so there
  is nothing to reset — this attaches to task 010, which creates the first one
- Test data builders, fixtures for domain objects, fake `IGameSystem` implementations — 030 onward
- Performance, load and mutation testing

## Approach

**An ephemeral database, not the development one.** Integration tests must not point at the compose
Postgres: they would delete data the maintainer is mid-session with, and a test suite whose first
casualty is your own campaign will not be run. Testcontainers starts a throwaway `postgres:18`
container for the test run and destroys it afterwards, so the tests own their database completely
and can be as destructive as they like.

The cost is honest and worth stating: **`dotnet test` starts requiring Docker.** The project already
requires Docker to run at all, so this is a smaller step than it sounds, but it does mean the test
suite has a moving part that can fail for reasons unrelated to the code. Tests that need it should
be marked, so a run without Docker says so instead of timing out.

**The application is started in-process, not mocked.** `WebApplicationFactory` boots the real
`Program.cs` — real DI, real middleware, real EF Core — and issues requests through an in-memory
transport rather than a socket. That is what makes the health test meaningful: it proves the wiring,
which is exactly the part a unit test cannot reach.

Two consequences to plan for. `Program.cs` resolves the connection string **before** `Build()` and
throws if it is missing, so the factory has to supply configuration earlier than the usual
`ConfigureAppConfiguration` hook runs. And `Program` is not a public type in a minimal-API project,
so it has to be made reachable from the test assembly — a change to production code that exists
purely for testing, which is a real if minor cost.

**Migrations are applied by the fixture**, once, before any test runs. This is not a contradiction
of ADR 003: that decision is about the *application* never migrating at startup. A test fixture
building a database it owns is the explicit, deliberate application that ADR asks for.

**Frontend tests are colocated** with the component, per `.claude/rules/frontend.md`. The network is
stubbed at `fetch`, not at the `fetchHealth` wrapper — stubbing the wrapper would test that the
component calls a function, whereas stubbing `fetch` tests that it handles what the server actually
sends, including the 503 that is a real answer rather than an error.

## Acceptance criteria

- [ ] `dotnet test` passes from a clean clone with Docker running, and does not touch the compose
      development database — verified by checking its data is intact afterwards
- [ ] The health integration test fails, with a comprehensible message, if the endpoint stops
      returning 200 or stops reporting the database
- [ ] Running the backend suite without Docker produces a clear statement of what is missing
- [ ] `npm run test` in `src/Client` runs Vitest and passes
- [ ] The frontend test covers all three states, and fails if a state stops rendering
- [ ] `npm run build` and `npm run typecheck` still pass with test files present — tests must not
      leak into the production bundle, and must not break the type build
- [ ] `npm run lint` passes on test files, with test globals recognised rather than silenced
- [ ] The task-001 `TestHarnessTests` placeholder is gone
- [ ] `dotnet build` clean with zero warnings; `git status` clean

## Concepts to explain

- **What a test double actually costs.** Why `.claude/rules/backend.md` bans the EF in-memory
  provider for integration tests: it is not Postgres, it has different semantics for JSONB,
  transactions and concurrency, and a suite that passes against it can fail against the real thing
- **Testcontainers' model**: the library as a Docker client, container lifetime tied to a fixture,
  random host ports, and why that makes parallel runs safe
- **xUnit's lifecycle**: what is constructed per test versus shared, what `IClassFixture` and
  `ICollectionFixture` mean, and why an expensive database container belongs in the widest scope
  available while test *data* does not
- **`WebApplicationFactory`**: booting the real application in-process, what `TestServer` replaces
  (the socket, not the pipeline), and why that is a genuinely different kind of test from
  instantiating a controller
- **The testing pyramid, honestly.** Where this project's risk actually lives — visibility
  filtering, permissions, rules resolution — and why that argues for concentrating effort there
  rather than chasing a coverage percentage
- **Vitest and jsdom**: what a simulated DOM is and is not, and where the simulation leaks
  (layout, real event ordering, anything involving actual painting)
- **React Testing Library's premise**: querying by what a user perceives — roles, labels, text —
  rather than by component internals, and why tests written that way survive refactors

## Risks and things to watch

- **`dotnet test` now needs Docker.** New coupling, and a new class of failure that has nothing to
  do with the code. Marking the tests that need it is what keeps the failure legible.
- **Container startup cost is paid per test run.** Keep the container in the widest fixture scope;
  if the suite ever starts a container per test class, the run time will quietly become the reason
  nobody runs it.
- **The fail-fast connection-string check runs before `Build()`.** The factory must have supplied
  configuration by then. Expect this to be the fiddliest part of the task, and do not work around it
  by weakening the check — that check is a task 002 deliverable and it is correct.
- **Do not assert the unhealthy path in an integration test.** With the database unreachable, the
  health check takes roughly 16 seconds (recorded in `PROGRESS.md`, deferred to 101). A test
  asserting a 503 would inherit that latency.
- **Colocated frontend tests are inside `tsconfig.app.json`'s `include`**, so `tsc -b` type-checks
  them and `npm run build` sees them. They need Vitest's types available and must not end up in the
  bundle. Verify both, rather than assuming.
- **Making `Program` reachable is a production-code change for a test's benefit.** Small, standard,
  and worth naming out loud rather than slipping in.
- Resist writing tests for things this card does not cover. The temptation at a test-harness task is
  to backfill tests for tasks 001–004; those tasks are done and reviewed, and untargeted backfill is
  how a one-session card becomes three.
