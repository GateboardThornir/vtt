# 015 — Admin recovery codes

**Status:** done
**Depends on:** 014 (an administrator to issue them)
**Branch:** `task/015-admin-recovery-codes`

## Goal

Someone who has forgotten their password can get back in, without the platform having an email
address and without the administrator ever knowing their password. This is the whole of account
recovery on a platform that collects no contact details.

## Scope

In scope:
- A `RecoveryCode` entity: bound to one account, hashed, expiring, single-use
- An administrator issues one for a named account; the plaintext is shown **once** and delivered
  out-of-band, in person or over a messaging app
- The holder redeems it by supplying the code and a new password, and is never asked for the old one
- **Extracting the token generation and hashing shared with 011** — the part that is genuinely
  identical, and only that part
- Integration tests: the round trip, every refusal, single use under concurrent redemption, and that
  the code is never stored or returned in plaintext after issue

Explicitly out of scope (and which task covers it instead):
- Any UI — 017
- Notifying anyone — 022
- The user changing their own password while signed in. Related but a different flow with a
  different check (it needs the current password), and nothing in Phase 1 asks for it
- Invalidating existing sessions on reset — see the risk below; recorded rather than skipped

## Approach

**The administrator never sets or learns the password**, per `.claude/rules/security.md`. They mint
a code; the code lets its holder choose their own password. That is the whole reason this is not
simply "an admin can reset a password to something and tell you what it is".

**Share the token primitive, not the workflow.** Task 011 deliberately refused to generalise from
one example. With two, the honest answer is visible: generation and hashing are byte-for-byte the
same problem and become a shared `SecureToken`; the entities, expiries and redemption rules are not
the same and stay separate. Sharing what merely rhymes is how a wrong abstraction gets locked in.

**A shorter life than an invite.** An invite is a scheduling convenience; a recovery code is a live
credential for an existing account, handed over in a conversation. Hours, not days.

**Redemption is one conditional statement**, exactly as in 011 — matched on the hash, unspent, and
unexpired — and the password is replaced in the same transaction, so a lost race changes nothing.

**Issuing a code does not disclose whether the account exists**… to the *administrator* it does, and
that is fine: they are already looking at the account list. The redemption endpoint is the
unauthenticated one, and it says only that the code did not work.

## Acceptance criteria

- [ ] An administrator can issue a code for an account; a member cannot, and an anonymous caller
      cannot
- [ ] The plaintext appears in the issuing response and **nowhere in the database**
- [ ] Redeeming with a valid code sets the new password, and the account can sign in with it
- [ ] The old password stops working
- [ ] A code works once: a second redemption is refused, and concurrent redemptions produce exactly
      one success
- [ ] An expired code is refused; expiry is proven by moving the fake clock
- [ ] A code for a disabled account does not resurrect it — recovery restores access to the password,
      not to the platform
- [ ] The new password is held to the same length rule as registration
- [ ] Nothing logs the code or the password

## Concepts to explain

- **Why recovery is admin-mediated here**, and what that trades away compared with an email link
- **Out-of-band delivery** as the security boundary: the code is only as safe as the channel it
  travels over, and that channel is a human conversation
- **When to extract a shared abstraction** — one example is a coincidence, two is evidence, and even
  then only the identical part
- **Why a reset does not sign existing sessions out**, and what it would take to make it

## Risks and things to watch

- **An existing session survives a password reset.** If someone else has the account's cookie, the
  reset does not evict them. Fixing this needs a value in the cookie that a reset can invalidate —
  worth doing, and it belongs with the session work rather than being bolted on here. Record it.
- **The code is a live credential in a chat log.** A short lifetime is the mitigation; there is no
  technical fix for a channel outside the platform.
- **A recovery code must not change the account's state or role.** It restores access to the
  password only, and a disabled account stays disabled.
- Do not let this grow into a general "reset anything" admin power.
