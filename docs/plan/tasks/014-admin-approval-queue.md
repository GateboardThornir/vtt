# 014 — Admin approval queue

**Status:** not started
**Depends on:** 013 (a signed-in caller to check)
**Branch:** `task/014-admin-approval-queue`

## Goal

An administrator can see who has registered and decide whether they stay. This is the last step of
the account lifecycle: an invite gets someone registered, and this gets them in.

## Scope

In scope:
- **The platform-role column**, deferred from 010 and now unavoidable — an approval queue that
  anyone can call is not an approval queue
- Listing `Pending` accounts, approving one (`Pending` → `Active`) and rejecting one
  (`Pending` → `Disabled`)
- Disabling an `Active` account and re-enabling a `Disabled` one, because "reject" and "disable" are
  the same transition and pretending otherwise would mean two code paths for one state change
- Endpoints under `/api/admin/accounts`, refusing anyone who is not an administrator
- The `create-account` command marks its account as an administrator, closing the honesty gap ADR
  008 recorded
- Integration tests: every transition, every refusal, and that a non-administrator gets nothing

Explicitly out of scope (and which task covers it instead):
- **The general authorisation infrastructure** — 016 owns policies and the campaign-role resolver.
  This card adds the one check it needs and no framework around it
- The admin screen — 017
- Notifying someone that they were approved — 022
- Recovery codes — 015
- Deleting an account. Accounts are disabled, never deleted, so that invites and future campaign
  history keep their references

## Approach

**The role column lands here rather than at 016**, and the deviation is worth recording. 010 left it
out as speculative because nothing read it; that is no longer true, and 016 cannot be the owner of a
column that 014 must already enforce against. 016 keeps what it was actually for: the policy
infrastructure and the campaign-role resolver.

**Two values, not a permission system.** `Member` and `Admin`, as text for the same readability
reason as `AccountState`. Anything finer — per-capability grants, multiple admins with different
powers — is a system for a platform with more than fifty users.

**Approve and reject are one operation.** Both are "an administrator sets an account's state", and
the legal transitions are a small table rather than four endpoints. Modelling them separately would
duplicate the guard that stops, say, a `Disabled` account being approved without ever having been
pending.

**The check is on the endpoint, not in the cookie.** 013 deliberately put no roles in the cookie
because it is frozen until reissued; the role is read from the database on each request that needs
it. At this scale that is one indexed lookup.

**Being an administrator grants no campaign access**, per `.claude/rules/security.md`. Nothing here
may become a general-purpose override, and 021 must not reach for it.

## Acceptance criteria

- [ ] A migration adds the role column with an explicit table name and a sensible default for
      existing rows
- [ ] `create-account` produces an administrator; registration produces a member
- [ ] An administrator can list pending accounts, approve one, and reject one
- [ ] An approved account can then sign in; a rejected one is told it is disabled
- [ ] A disabled account can be re-enabled
- [ ] **A member calling any of these endpoints gets 403 and no data** — including the list, which
      would otherwise leak who has registered
- [ ] An unauthenticated caller gets 401
- [ ] Illegal transitions are refused rather than silently applied
- [ ] `dotnet build` zero warnings, `dotnet format` clean, suite green, CI green

## Concepts to explain

- **Why the role is read per request instead of carried in the cookie**, and what breaks if you do
  the convenient thing
- **Authorisation as a server-side decision**: why hiding a button is not access control
- **State machines in a domain model** — legal transitions as data rather than scattered `if`s
- **Adding a column to a table with rows in it**: what a default does during a migration and what it
  leaves behind afterwards

## Risks and things to watch

- **The list endpoint is the disclosure risk**, not the mutations. Approving an account you should
  not have is loud; reading the roster quietly is not.
- **Platform admin must never imply campaign access.** The moment it does, the campaign-scoped
  visibility rules in `security.md` have a hole that no test covers.
- The bootstrap command becoming an administrator makes shell access equal to platform ownership.
  That was already effectively true and is now explicit; it belongs in ADR 008's consequences.
- Do not build a permission table. Two roles, and the moment a third is wanted, that is a design
  conversation rather than a schema change.
