# 007 — Invite tokens are stored hashed, with a fast hash

**Date:** 2026-08-05
**Status:** accepted

## Context

This platform is invitation-only. There is no public registration, no discovery, and no way in
except a token an administrator generated. That makes the invite token the entire perimeter: whoever
holds a valid one can create an account.

`.claude/rules/security.md` already treats invite tokens as credentials — it forbids logging them
alongside passwords, session cookies and recovery codes. Task 011 had to decide what the *database*
holds, and with what.

## Decision

**The database stores only a SHA-256 hash of the token.** The plaintext is returned exactly once, at
issue, and cannot be recovered afterwards by anyone, including the administrator who created it.

**Tokens come from `RandomNumberGenerator`**, 32 bytes, encoded base64url — 43 URL-safe characters.

**The hash is a single pass of SHA-256, deliberately not the password hasher.**

**Redemption is one conditional `UPDATE`**, matching on the hash and on the invite being unspent and
unexpired. Zero rows affected means this caller lost.

## Consequences

A leaked database is not a supply of working invitations. Backups go off-site to object storage
(`docs/architecture.md`), dumps get made during restore drills, and any of those reaching the wrong
hands would otherwise hand over the ability to create accounts on a private platform. Hashing makes
the stored form useless on its own.

The asymmetry with passwords is the part worth understanding, because copying the password approach
here would be wrong in a way that looks careful. A password is chosen by a human out of a space small
enough to search, so the defence is to make each guess expensive — that is what PBKDF2 and its work
factor buy. A 32-byte token from a CSPRNG has 256 bits of entropy; nobody searches that space, ever,
regardless of how fast the hash is. The only property needed is that the stored value cannot be
turned back into the token, and one pass of SHA-256 provides exactly that. Running PBKDF2 over it
would add its full work factor to every redemption and defend against an attack that does not exist.

**The plaintext is unrecoverable, and that is a feature with a cost.** An administrator who loses the
token before delivering it has to issue a new invite. This must read as deliberate in the admin UI at
task 017, or it will look like the system lost their data.

The conditional update means the single-use guarantee lives in PostgreSQL rather than in application
logic. That is the same conclusion task 010 reached about username uniqueness, and for the same
reason: between a read and a write there is a window, and concurrency lives in windows.

Hashing means the lookup is an exact match on an indexed column, so redemption stays a single index
seek. A scheme that required comparing against every row — anything with a per-row salt — would have
been meaningfully worse here, which is another reason the password approach does not transfer.

## Alternatives rejected

**Store the token in plaintext.** Simplest, and it would allow an admin screen to re-display an
invite they had lost. Rejected because it converts any read of the database into the ability to
register accounts, and because the convenience it buys — recovering a lost invite — is served just
as well by issuing a new one, which costs nothing.

**Hash with the password hasher for consistency.** Superficially attractive: one way of handling
secrets, no reasoning about which is which. Rejected because it applies an expensive defence to a
threat that does not exist while slowing every redemption, and because "we use the same tool
everywhere" is how a codebase stops thinking about what it is actually defending against. The
asymmetry is real and worth being explicit about, which is why this ADR exists.

**Use a `Guid` as the token.** Available, unique, no encoding needed. Rejected outright: version 4
carries 122 bits with no guarantee of cryptographic quality across platforms, and version 7 — used
for primary keys elsewhere in this schema — encodes a timestamp by design, so a meaningful part of
it is predictable. Neither is a security token, however random they look.

**Enforce single use with an application-level check.** Read the invite, verify it is unspent, then
save. Rejected, and demonstrated to be wrong rather than argued about: with that implementation, all
sixteen concurrent redemptions in the test succeeded, producing sixteen valid registrations from one
invite.

**A revocation flag.** Considered and left out. Deleting the row revokes the invite completely, needs
no schema and no state machine. Worth revisiting only if the admin screen at 017 wants revoked
invites visible as history, which is a display requirement rather than a security one.
