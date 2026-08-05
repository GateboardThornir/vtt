# 012 — Registration via invite URL

**Status:** done
**Depends on:** 011 (a token to consume)
**Branch:** `task/012-registration-via-invite-url`

## Goal

Someone holding an invite can turn it into an account. Together with a one-off command that creates
the very first account, this closes the loop: an empty database becomes a working platform with a
registered user on it. It is the first task whose output a human can actually operate.

## Scope

In scope:
- **A one-off command to create the first account**, resolving the bootstrap gap recorded in
  `PROGRESS.md` since 010. Run by hand, password typed at a prompt, never read from configuration
- An HTTP endpoint that accepts an invite token, a username and a password, and creates a `Pending`
  account
- **Consuming the invite and creating the account as one atomic operation** — either both happen or
  neither does
- Input validation at the request boundary: username shape and length, password minimum length
- The query-parameter name the invitation URL carries, so 017 has something to build against
- A considered answer to what the endpoint tells an unauthenticated caller when a token is bad
- Integration tests covering the happy path, every rejection, and the case where two people race to
  spend one invite

Explicitly out of scope (and which task covers it instead):
- **Logging in.** No session, no cookie, no authentication of any kind — 013. Registering does not
  sign you in, and the response carries no credential
- Approving a `Pending` account — 014
- Any authorisation rule, including who may issue an invite or run the bootstrap command — 016
- The registration screen and the real invitation URL a person clicks — 017
- Notifying an administrator that someone registered — 022
- Rate limiting and abuse protection. Registration is gated by an unguessable token, and nothing
  authenticates yet
- A platform-role column. The bootstrap account is `Active`, not `Admin`, because "admin" is not yet
  a thing the schema can express — see below

## The bootstrap account, precisely

**Settled 2026-08-05: a one-off CLI command, folded into this card.**

The command creates an account in `Active` state — the single account that never went through an
invite and never needed approving, because there was nobody to approve it. Everything after it comes
through the normal path.

It is worth being honest that this does **not** create an *administrator*: 010 deliberately left the
platform-role column to 016, so no account can be marked admin yet. What it creates is the account
that will be marked admin when roles exist, and which in the meantime can issue invites because
nothing yet checks who may. That gap closes at 016, and until then the platform's protection is that
it is not deployed.

The password is typed at a prompt and echoed nowhere. It must not be a command-line argument —
arguments land in shell history and in the process list, which is exactly where a credential must
never be.

## Approach

**Registration is one transaction, and the ordering is forced.** The invite's `consumed_by_user_id`
points at `users`, so the account must exist before the invite can record who spent it. But if
consuming then fails — because someone else spent the token first — the account must not survive.
Insert the user, consume the invite, and commit only if the consumption reported success; otherwise
roll back and the account never existed. Doing this without a transaction leaves orphaned accounts
that hold a username nobody can use.

**The concurrency guarantee is already built.** 011's conditional `UPDATE` means exactly one of two
simultaneous redemptions wins. What 012 adds is making sure the *loser* leaves nothing behind, which
is the transaction's job. The test from 011 proved the invite side; this card needs the equivalent
proof for the account side.

**What a bad token is told.** A token is 256 bits of randomness, so anyone presenting a real one is
its intended holder, and telling them precisely what is wrong costs nothing and saves a confused
conversation. The recommendation: distinguish *expired* and *already used* — both facts the holder
is entitled to — but answer anything unrecognised with a single generic rejection, since confirming
whether an arbitrary string is a real token is the one thing that would help an attacker.

**Password rules follow length, not theatre.** A minimum length and nothing else: no forced symbol,
no digit requirement, no maximum below something generous, no composition rules. Those rules push
people toward `Password1!` and are current guidance's canonical example of what not to do. The
minimum should be chosen and written down as a constant, and it should be longer than the eight
characters that were fashionable a decade ago.

**Validation lives at the boundary.** Per `.claude/rules/backend.md`, the request DTO is where a
username's allowed characters and length are checked; `User` already caps the column but that is
storage, not policy. The domain assumes valid input.

## Acceptance criteria

- [ ] From an empty database: run the command, create an account, issue an invite as that account,
      register a second account through the endpoint. Done by hand, end to end, not only in tests
- [ ] The bootstrap command does not accept the password as an argument, and does not echo it
- [ ] Running the bootstrap command twice with the same username fails cleanly on the unique index
- [ ] A registration with a valid token creates exactly one `Pending` account and marks the invite
      consumed by it
- [ ] Registering with an expired, already-used or unrecognised token creates **no** account, and the
      responses match the disclosure decision above
- [ ] **Two simultaneous registrations against one invite produce exactly one account** — proven with
      genuinely parallel requests, and no orphaned user row left behind
- [ ] A username that is already taken is rejected without a server error, including when the clash
      differs only in case
- [ ] The response body contains no password, no hash, no token, and no session
- [ ] Nothing logs the password or the token
- [ ] `dotnet build` zero warnings, `dotnet format` clean, full suite green, CI green

## Concepts to explain

- **Minimal API endpoints**: how a request becomes typed parameters, where model binding happens, and
  what returns a 400 before your code runs at all
- **Database transactions in EF Core** — what `SaveChanges` does and does not already give you, why
  two writes that must both stick need an explicit one, and what "roll back" costs
- **Why the insert order is forced by the foreign key**, and how that interacts with needing to undo
- **Reading a password without echoing it**, and why a command-line argument is the wrong place for
  a secret — process lists and shell history are both readable
- **A CLI verb inside a web application**: how the entry point can do something other than serve,
  and why that is worth care given `dotnet ef` and the integration tests both execute `Program.cs`
- **Information disclosure in registration**: what an error message tells an attacker, why username
  enumeration matters on public sites, and why the calculation is different behind an invite
- **Why password composition rules were abandoned** by current guidance in favour of length

## Risks and things to watch

- **`Program.cs` is executed by more than the server.** `dotnet ef` runs it to build the model, and
  the integration tests run it through `WebApplicationFactory`. A command branch that triggers on the
  wrong arguments would break the migration tooling or the test suite in a way that looks unrelated
  to its cause. Branch narrowly on an explicit verb and on nothing else.
- **An orphaned account is worse than a failed registration.** If the invite is lost to a race and the
  user row survives, that username is taken forever by an account nobody can sign into. The
  transaction is what prevents it, and the test must actually exercise the losing side.
- **Never log the password or the token**, including inside exception messages and validation errors.
- **The bootstrap command is a permanent back door if it is left able to run.** It only creates
  accounts and requires shell access to the server, which is an acceptable footing — but it should
  not grow the ability to change an existing account's password without revisiting that judgement.
- The registration response must not become a login "for convenience". 013 owns sessions, and an
  endpoint that quietly issues one would put authentication outside the task that reviews it.
- Resist building the registration form. The endpoint is testable with an HTTP client, and the screen
  is 017's, after i18n infrastructure lands there.
