# 006 — Password hashing without ASP.NET Core Identity

**Date:** 2026-08-05
**Status:** accepted

## Context

Task 010 stores the first password, and that decision is permanent in a way most are not: every
password the platform ever holds is protected by whatever is chosen here, and changing it later
means rehashing on next login and living with two formats in the meantime.

ASP.NET Core Identity is the obvious candidate. It is the framework's own answer to accounts, it is
maintained by Microsoft, and it brings sign-in, lockout, two-factor, token generation and an EF
store already written.

The constraint it runs into is `.claude/rules/security.md`: **no email fields anywhere in the
schema, code, or UI.** Not as a preference — as a design rule this platform is built around, because
it collects no email addresses and does account recovery through an administrator instead.

## Decision

**ASP.NET Core Identity is not adopted.** No `IdentityDbContext`, no `IdentityUser`, no
`UserManager`, no `SignInManager`. The `User` entity is hand-written and owns exactly the columns
this platform needs.

**Its password hasher is adopted.** `PasswordHasher<TUser>` is used through a small
`IPasswordHasher` wrapper in the `Accounts` module. It needs no package reference:
`Microsoft.Extensions.Identity.Core` ships inside the ASP.NET Core shared framework, and referencing
it explicitly is an NU1510 error.

The wrapper exposes a three-valued result — `Failed`, `Success`, `SuccessButNeedsRehash` — because
the hasher reports when a stored hash was produced with a superseded work factor.

## Consequences

The schema stays exactly as wide as the platform needs. `IdentityUser` would have brought
`Email`, `NormalizedEmail`, `EmailConfirmed`, `PhoneNumber`, `PhoneNumberConfirmed`,
`TwoFactorEnabled`, `LockoutEnd`, `LockoutEnabled` and `AccessFailedCount` — the first five of which
are prohibited outright, and the rest of which are unused. A rule stated as "no email fields
anywhere" cannot survive a base class that ships four of them.

Passwords are still hashed by code Microsoft wrote and maintains. This is the part that mattered
most: PBKDF2 parameters, salt generation, constant-time comparison and format versioning are all
places where hand-written code fails silently and catastrophically, and none of them are visible in
testing. When Microsoft raises the work factor, the `SuccessButNeedsRehash` path already exists to
carry it.

The cost is everything Identity would have given for free. Sign-in, cookie issuing, lockout after
repeated failures, and token generation for recovery codes are now this project's to write —
tasks 013, 015 and 016. That is real work, and for a solo maintainer it is the strongest argument
against this decision. It is accepted because those pieces are small and well understood, whereas
excising email from Identity is neither.

Identity's own `IPasswordHasher<TUser>` interface is deliberately not the abstraction the rest of the
codebase sees. The wrapper means swapping to Argon2 later touches one file.

## Alternatives rejected

**Adopt Identity and simply not use the email columns.** They would exist in the schema, nullable and
empty. Rejected because a rule that is enforced by everyone remembering not to populate a column is
not enforced at all: the columns would be there for any future feature, any scaffolding tool, and
any contributor to fill in, and the day one of them does, the rule has been broken silently. The
schema is where "no email anywhere" is actually made true.

**Adopt Identity and customise the model to remove the email columns.** Possible — the properties
can be ignored in `OnModelCreating`. Rejected because Identity's own code paths reference them,
`UserManager` methods assume they mean something, and the result is a framework fought at every
turn rather than used. More work than writing a `User` entity, and permanently more confusing.

**Write the password hashing directly, over `Microsoft.AspNetCore.Cryptography.KeyDerivation`.**
Only a few dozen lines, and the algorithm is not a secret. Rejected without hesitation: the failure
modes are invisible. A hasher with a too-short salt, a reused salt, a comparison that leaks timing,
or no version marker to migrate from all pass every functional test and are discovered by an
attacker rather than by the maintainer. There is no upside to trade against that.

**Use BCrypt.Net or an Argon2 package.** Argon2id is, by most current advice, a stronger choice than
PBKDF2 — it resists GPU attack far better. Rejected for now because it means a third-party
dependency in the single most security-sensitive path in the system, maintained by strangers, versus
one that ships with the framework and is patched with it. The wrapper exists precisely so this can
be revisited cheaply if the advice hardens.
