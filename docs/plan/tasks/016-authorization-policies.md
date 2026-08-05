# 016 — Authorization policies

**Status:** done
**Depends on:** 015
**Branch:** `task/016-authorization-policies`

## Goal

Authorisation becomes something an endpoint declares rather than something each handler remembers to
do. Task 014 proved the check works; this makes it structural, so a new endpoint is protected by
saying so rather than by copying a guard.

## Scope

In scope:
- A named policy for "is a platform administrator", backed by an authorisation requirement that
  reads the role from the database per request
- `/api/admin/*` protected by declaring the policy, with the hand-rolled guard from 014 deleted
- A named policy for "is a signed-in, active account" — 013 checks the cookie, but a `Disabled`
  account's cookie still authenticates until it expires, which is a live gap
- Tests that the policies refuse what they should, including the disabled-mid-session case

Explicitly out of scope:
- **The campaign-role resolver** — see below. It has nothing to resolve against until 021
- Any UI — 017
- Per-campaign policies, resource-based authorisation, and anything about Masters and Players

## The campaign-role resolver moves to 021

The roadmap gives 016 "platform role (admin/member) + campaign role resolution helper". The first
half is buildable; the second is not, because campaigns arrive at 020 and membership at 021. A
resolver written now would have no table to read, no test that exercises it, and no consumer.

**Decision: 016 ships the platform-role infrastructure, and the campaign-role resolver moves to
021**, which is where campaign membership is created and where the first thing that needs it lives.
Recorded as a deviation.

## Approach

**A policy, not a helper function.** The framework already has the shape: a requirement, a handler
that decides, and a named policy an endpoint attaches. The difference from 014's guard is that
forgetting it is visible — an endpoint with no policy is obviously unprotected, whereas an endpoint
missing a call to a helper looks exactly like one that made it.

**Still read per request.** The requirement handler queries the database, for the reason 013 gave:
a cookie is frozen until reissued, so anything baked into it outlives its revocation.

**Active-account checking closes a real gap.** Today, disabling an account stops it signing in but
does not stop the cookie it already has. Every authenticated endpoint should require an active
account, which makes disabling take effect on the next request rather than at cookie expiry.

## Acceptance criteria

- [ ] `/api/admin/*` refuses members with 403 and anonymous callers with 401, with no bespoke guard
      code left in the endpoint handlers
- [ ] An account disabled mid-session loses access on its **next request**, without signing out
- [ ] An administrator demoted mid-session loses admin access on its next request
- [ ] Adding an endpoint without a policy is visibly unprotected — the protection is declared
- [ ] Suite green, format clean, CI green

## Concepts to explain

- **Requirements, handlers and policies** — how ASP.NET Core decomposes "may this caller do this"
- **Why declarative beats imperative here**: what a missing call looks like versus a missing
  attribute
- **Why authorisation reads the database**, and the cost of that at this scale
- **Authentication versus authorisation**, which 013 and 016 now cleanly separate

## Risks and things to watch

- **A database read per authorised request.** Fine for fifty users; worth remembering when the table
  engine starts issuing many small requests.
- Platform admin still grants no campaign access, and 021 must not make it.
- The policies must fail closed: an unknown account, a deleted account or an unparseable claim is a
  refusal, never a pass.
