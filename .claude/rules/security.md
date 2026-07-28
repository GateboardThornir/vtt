# Security invariants

These apply to every task without exception. When a task appears to require breaking one of them,
stop and raise it rather than working around it.

## Information disclosure is the primary threat

The realistic attacker here is a curious player with browser DevTools open, not a stranger on the
internet. Every payload leaving the server must be filtered to what its specific recipient is
entitled to see:

- Fog of war: unrevealed map regions and the tokens inside them are not sent.
- Master preparation: unrevealed scenes, encounters, notes and staged assets are not sent.
- Per-player secrets: a handout restricted to player A never appears in player B's payload.
- Hidden rolls: Master-only rolls are not sent to players, not even as an opaque event.

There is no exception for convenience, and no "the client will hide it" path. A test asserting
that a filtered payload lacks the hidden data is required for every feature that has hidden data.

## Authentication and accounts

- Username + password only. **No email fields anywhere in the schema, code, or UI.**
- Passwords hashed with the framework's current recommended algorithm and work factor.
  Never store, log, or transmit a password or its hash outside the auth flow.
- Registration requires a valid, unconsumed, unexpired invite token, and lands in `Pending`.
  Pending accounts cannot authenticate.
- Admin recovery codes are single-use, time-limited, and generated server-side. The admin never
  sets or sees a user's password — the code lets the user set their own.
- Sessions are server-side cookies, `HttpOnly`, `Secure`, `SameSite=Lax`.

## Authorization

Check on the server, on every request and every intent — never rely on the UI having hidden a
control. Two layers: platform role (admin vs member) and campaign role (Master vs Player vs
non-member). Non-membership of a campaign means total invisibility of that campaign's data.

Token control is strictly 1:1: a player may act on their own character's token only. The Master
may act on anything in their own campaign. Being platform admin grants **no** campaign access.

## Uploads

Validate content type and size on the server; never trust the client-declared type. Store outside
the web root and serve through an authorising endpoint so ACLs apply — never a public static path.
Randomise stored filenames. Enforce per-campaign quotas at upload time.

## Logging

Never log credentials, session cookies, recovery codes, or invite tokens. Never log the content of
private handouts or hidden rolls. Log the intent type and outcome, not the secret payload.
