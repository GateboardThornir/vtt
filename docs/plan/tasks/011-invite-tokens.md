# 011 — Invite tokens

**Status:** not started
**Depends on:** 010 (an account to attribute an invite to)
**Branch:** `task/011-invite-tokens`

## Goal

An administrator can mint a single-use, expiring invite token, and the platform can check one and
spend it exactly once. This is the whole perimeter of a closed platform: registration is possible
only through a token, so everything about how these are generated, stored and consumed is a security
decision rather than a convenience.

## Scope

In scope:
- An `Invite` entity in the `Accounts/` module: who created it, when, when it expires, and whether
  and by whom it has been consumed
- Token generation from a cryptographic random source, in a URL-safe form, **returned exactly once**
  at creation and never retrievable afterwards
- **Only a hash of the token is stored.** The plaintext exists in the response and in whatever the
  admin pastes into a message, and nowhere else
- Expiry, which means adopting `TimeProvider` — the clock decision deferred from 010
- Consumption that is **atomic**: two people redeeming the same token simultaneously must produce
  exactly one success
- A service exposing create, validate and consume, with typed outcomes rather than booleans —
  "expired", "already used" and "no such token" are different facts
- The migration, with the table named explicitly (`invites`) per the lesson from 010
- Unit tests for token generation and the state machine; integration tests for persistence, expiry
  and the single-use guarantee under concurrent redemption

Explicitly out of scope (and which task covers it instead):
- **The registration endpoint that consumes a token, and the invite URL's shape** — 012. This card
  produces the token and the rules; 012 puts it in a URL and creates an account with it
- **Who is allowed to create an invite.** The service takes a creator's id; the authorisation policy
  that says only an administrator may call it is 016
- The admin screen for generating and copying an invite — 017
- In-app notification of an invite — 022
- Recovery codes — 015. They are the same *shape* (single-use, expiring, hashed) and deliberately
  not generalised here; two use cases is not yet a pattern, and one of them does not exist
- Revocation of an unused invite — see the open question below
- Rate limiting invite creation, and any lockout behaviour — nothing authenticates yet

## Open questions

**1. Should an unused invite be revocable?** The roadmap says only "single-use, expiring". Deleting
a row is a perfectly good revocation and needs no schema for it, so the recommendation is **out of
scope**, with the note that revocation-as-a-state (rather than a delete) only matters if the admin
screen at 017 wants to show revoked invites as history.

**2. How long is an invite valid, and is that configurable?** A fixed default is enough for a group
of under fifty people, and the value should live as a named constant rather than a magic number.
Making it configurable per-invite is 017's problem if it ever becomes one. A shorter window is safer
and this platform's invites are delivered by hand over a messaging app, so the recommendation is
days, not weeks.

**3. The first administrator is still unresolved** (recorded in `PROGRESS.md` from 010). This card is
**not** blocked by it: the service takes a creator's id and does not care what role that account
holds. The gap becomes real at 012, when a registration flow has to run end to end.

## Approach

**The token is a secret, and the database stores only its hash.** If invites were stored in plaintext,
anyone who read the database — a backup on object storage, a leaked dump, a `SELECT` from a
misconfigured tool — could register accounts on a platform whose entire access control is "you must
have been invited". `.claude/rules/security.md` already classes invite tokens with credentials and
forbids logging them; storing them in the clear would be the same mistake with a longer half-life.

**Hashed, but not with the password hasher.** This is the interesting part and worth understanding
rather than copying: a password needs a deliberately slow hash because humans choose passwords from
a tiny space and an attacker with the database will guess billions of them. A 256-bit random token
has no such space to search — guessing one is not a thing that happens — so the only property needed
is that the stored value cannot be reversed. A single fast SHA-256 gives that, and running PBKDF2
over a random token would add latency to every redemption while buying nothing.

**Consumption has the same race as username uniqueness, and the same answer.** "Read the invite, see
that it is unconsumed, then mark it consumed" has a window between the read and the write in which a
second redemption can pass the same check. Two accounts from one invite defeats the point. The fix
is a single conditional statement — mark it consumed *where it is not already consumed* — and treat
"no rows affected" as the losing side of the race. Checking in application code and hoping is not a
fix.

**Typed outcomes, not booleans.** A caller needs to distinguish "this token never existed" from
"this token expired" from "someone already used this", because 012 will want to say different things
about them — carefully. The *service* knows the difference; how much of it reaches an unauthenticated
stranger is 012's decision, and the safe default is not much.

**`TimeProvider` is adopted here.** Task 010 sidestepped the clock by passing `createdAt` in as a
parameter, which was right when nothing depended on the current time. Expiry does. The framework's
`TimeProvider` is the standard abstraction, `TimeProvider.System` is the registration, and
`FakeTimeProvider` lets a test move time forward without sleeping. Tests that wait for real seconds
to pass are how a suite becomes something nobody runs.

## Acceptance criteria

- [ ] Two invites generated in a row have different tokens, and the tokens are URL-safe with no
      characters that need escaping
- [ ] The plaintext token appears **nowhere** in the database — verified by selecting the row and
      searching it for the token
- [ ] A token validates before its expiry and is rejected after it, proven by moving a fake clock
      rather than by sleeping
- [ ] Consuming a token twice succeeds exactly once, and the second attempt reports that it was
      already used
- [ ] **Concurrent redemption of the same token yields exactly one success** — asserted with genuinely
      parallel attempts, not sequential ones
- [ ] A consumed invite records when it was consumed and by which account
- [ ] An unknown token is rejected without disclosing whether it ever existed
- [ ] No token value is written to any log
- [ ] `dotnet build` zero warnings, `dotnet format` clean, full suite green, CI green

## Concepts to explain

- **Cryptographic randomness versus ordinary randomness.** Why `Random` and `Guid.NewGuid()` are both
  wrong for a security token, what "predictable" means concretely, and what
  `RandomNumberGenerator` does differently
- **Entropy, and how much is enough.** Why 256 bits means nobody guesses one, and how that figure
  relates to the length of the string a user pastes into a URL
- **Why this token is hashed with SHA-256 while a password needs PBKDF2** — the difference between a
  secret chosen by a human and one chosen by a CSPRNG, and why the right answer changes with it
- **Base64url**, and why a token that travels in a URL cannot use standard base64
- **Check-then-act races, again.** The same shape as the username uniqueness problem in 010, in a
  different disguise — and why the database is where it gets resolved both times
- **`TimeProvider`**: why reading the clock directly makes code untestable, what injecting time buys,
  and how `FakeTimeProvider` replaces a `Thread.Sleep` that would otherwise be in the test suite
- **What an attacker learns from an error message.** Why "no such invite" and "invite expired" may be
  useful to the service and unwise to the stranger holding the token

## Risks and things to watch

- **Never log the token.** Not at creation, not on a failed redemption, not in an exception message.
  `.claude/rules/security.md` is explicit, and a token in a log file is a token in a backup.
- **The plaintext is returned once and is then unrecoverable.** If the admin loses it, the invite is
  dead and a new one must be minted. That is the correct behaviour — it is what "only the hash is
  stored" means — but it must be obvious in the UI at 017, or it reads as data loss.
- **Do not reach for `Random` or `Guid`.** A `Guid` is not a security token: version 4 has 122 bits
  and no guarantee of cryptographic quality, and version 7 — used for the primary key in 010 —
  deliberately encodes a timestamp and is therefore partly predictable by construction.
- **Resist generalising with recovery codes.** Task 015 needs something with the same shape, and
  building the abstraction now, from one example, is how a wrong abstraction gets locked in. Let 015
  decide whether to share.
- **The concurrency test must actually be concurrent.** Two sequential calls prove nothing about the
  race; the test needs parallel tasks and would have to fail if the conditional update were replaced
  by a read-then-write.
- Adopting `TimeProvider` does not mean revisiting `User.Register` — it keeps taking a timestamp, and
  the caller is where the clock is read.
