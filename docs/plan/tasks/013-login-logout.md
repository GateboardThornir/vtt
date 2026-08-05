# 013 — Login / logout

**Status:** done
**Depends on:** 012 (accounts that exist and can be signed into)
**Branch:** `task/013-login-logout`

## Goal

An `Active` account can sign in and out, and the server can tell who is making a request. Every
authorisation rule from 016 onward, and every hub connection from 040, assumes this exists.

## Scope

In scope:
- Cookie authentication: `HttpOnly`, `SameSite=Lax`, `Secure` outside development, per
  `.claude/rules/security.md`
- `POST /api/session` to sign in, `DELETE /api/session` to sign out, `GET /api/session` to report
  who the caller is
- **`Pending` and `Disabled` accounts are refused with a distinguishable reason**, per the roadmap —
  a pending applicant must be told to wait, not told their password is wrong
- The rehash-on-login path that 010 built and nothing has used: when the stored hash used a
  superseded work factor, replace it while the plaintext is briefly in hand
- Integration tests: the round trip, every refusal, cookie attributes, and that a wrong password and
  an unknown username are indistinguishable

Explicitly out of scope (and which task covers it instead):
- Authorisation policies and roles — 016. This task answers *who you are*, never *what you may do*
- The login screen — 017
- Approving a `Pending` account — 014
- Password reset and recovery codes — 015
- SignalR authentication — 040, which will reuse this cookie
- Rate limiting and lockout after repeated failures. Worth doing, but it belongs with the admin
  tooling that would surface it; recorded as deferred rather than silently skipped

## Approach

**Cookie authentication without ASP.NET Core Identity**, continuing ADR 006. The framework's cookie
handler is independent of Identity: it issues and validates the cookie, and the application decides
what claims go in it. Sessions are the cookie itself — signed and encrypted by data protection —
rather than rows in a table, which is what `docs/architecture.md` means by server-side sessions at
this scale.

**A refused login must not say which half was wrong.** An unknown username and a wrong password get
the same answer, because a login form that distinguishes them is a tool for discovering who has an
account. Account *state* is different: someone who typed the right password for a pending account
has proved they own it, so telling them it is awaiting approval discloses nothing they do not
already know and saves a support conversation.

**Verify before state.** Check the password first and the account state second, so a wrong password
against a pending account still returns the generic failure — otherwise the state message becomes an
oracle for valid usernames.

**Rehash while the plaintext is available.** `PasswordVerification.SuccessButNeedsRehash` has existed
since 010 with no consumer. Login is the only moment the plaintext exists, so it is the only place a
stored hash can be upgraded without asking anyone to change their password.

## Acceptance criteria

- [ ] Signing in with correct credentials sets a cookie that is `HttpOnly`, `SameSite=Lax`, and
      `Secure` when not in development
- [ ] `GET /api/session` reports the signed-in account, and 401s without a cookie
- [ ] Signing out clears the cookie, and the session no longer works
- [ ] A wrong password and an unknown username produce **identical** responses
- [ ] `Pending` and `Disabled` accounts are refused with their own distinguishable reasons, but only
      once the password has been verified
- [ ] A hash stored with an outdated work factor is replaced on successful login, and the account can
      still sign in afterwards
- [ ] No response body ever contains a password or a hash
- [ ] `dotnet build` zero warnings, `dotnet format` clean, suite green, CI green

## Concepts to explain

- **What a cookie session actually is here**: a signed, encrypted payload the browser stores and
  returns, versus a session identifier pointing at server state — and why the first suits this scale
- **`HttpOnly`, `Secure` and `SameSite`**, each in terms of the attack it prevents
- **Claims and `ClaimsPrincipal`** — how "who is this" travels through a request
- **Data protection keys**, and why losing them signs everybody out
- **Username enumeration**, and why the same response for two different failures matters
- **Rehashing on login** as the mechanism that lets a work factor rise over years

## Risks and things to watch

- **Data protection keys default to the local filesystem.** A container without a persistent volume
  regenerates them on restart and silently invalidates every session. This bites at 101, and it
  should be recorded there rather than discovered.
- The cookie must never carry anything but identity. No roles, no campaign membership, nothing
  authorisation depends on — a cookie is fixed until it is reissued, and a revoked permission that
  lives in one stays valid until then.
- Verifying state before password would turn the state message into a username oracle.
- No lockout means an attacker can try passwords indefinitely. Acceptable while the platform is
  undeployed and a private group; not acceptable indefinitely.
