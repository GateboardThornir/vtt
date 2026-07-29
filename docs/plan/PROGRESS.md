# Progress ledger

Updated at the end of every completed task, after review approval. This file is how work resumes
across sessions — a Claude Code session starts with no memory of the last one, and this is what it
reads to find out where things stand.

Keep entries short. One line per task, plus notes only where something surprising happened.

## Current state

**Phase:** 0 — Foundations
**Next task:** 002 — Docker Compose dev environment
**Blocked on:** nothing

## Completed

<!-- Format: | ID | Task | Date | Notes | -->

| ID | Task | Date | Notes |
|---|---|---|---|
| 001 | Repository scaffold | 2026-07-29 | Layout and build conventions in ADR 001. Server on port 5080, `/health` live |

## Deviations from the plan

Record every departure from `roadmap.md` and why. A plan that quietly diverges from reality is
worse than no plan.

| Task | What changed | Why |
|---|---|---|
| — | — | — |

## Open questions

Things surfaced during implementation that need a decision before they block something.

| Question | Raised in | Status |
|---|---|---|
| — | — | — |

## Deferred

Work consciously postponed, with the task it should attach to.

| Item | Deferred to | Reason |
|---|---|---|
| Automated test for `GET /health` | 005 | Endpoint testing is 005's subject; 001 ships a placeholder test and verifies `/health` by hand |
| `dotnet format --verify-no-changes` gate | 006 | Style is not enforced by the build, so CI is where drift gets caught |
