# 017 — Auth UI

**Status:** not started
**Depends on:** 016 (every endpoint these screens call)
**Branch:** `task/017-auth-ui`

## Goal

The account flows become usable by a person rather than by `curl`. Sign in, register from an
invitation link, understand why a pending account cannot get in, and approve people as an
administrator — closing the Accounts vertical slice that 010 opened.

## Scope

In scope:
- **The i18next infrastructure**, moved here from 098 by task 004's deviation, with Italian and
  English from the first string. This is the first UI, and it is the whole reason the move happened
- Routing, which the scaffold deliberately omitted
- Screens: sign in, register with an invite token from the URL, "awaiting approval", and an
  administrator's account queue
- A typed API client wrapping the endpoints from 012–016, replacing the diagnostic `fetchHealth`
- Session state: who is signed in, available to the whole tree, refreshed from `GET /api/session`
- Component tests for the screens, including the states that matter — refused sign-in, pending
  account, expired invite

Explicitly out of scope:
- Campaign screens — 023
- The live table, canvas and chat — 044 onward
- Password change while signed in; nothing in Phase 1 exposes it
- Styling beyond what makes the screens legible. A design system is a decision with no requirements
  behind it yet
- The health diagnostic page from 004, which this replaces

## Approach

**Every string goes through translation from the first one.** That is the rule in
`.claude/rules/frontend.md` and the reason the infrastructure moved out of Phase 3: retrofitting is
expensive precisely because it is boring, so it never happens. Keys are namespaced by feature.

**The server sends codes, not sentences.** 012 already returns `invite_expired` rather than English
prose, exactly so the client can translate it. The screens map codes to keys; any unmapped code
falls back to a generic message rather than showing the raw code.

**Session state comes from the server, not from the sign-in response.** After signing in the client
asks `GET /api/session`, and that answer is the source of truth. It is the same discipline the rest
of the platform follows — the client holds a projection of what the server said, never a guess.

**401 and 403 mean different things and the UI must treat them so.** 401 is "sign in"; 403 is "you
are signed in and this is not for you". Collapsing them produces a sign-in screen that appears in
front of an already-signed-in user.

**The invite token arrives in the URL and is never displayed.** It is a credential; it goes from the
query string into the request body and nowhere else — not into a visible field, not into a log.

## Acceptance criteria

- [ ] Every user-facing string resolves through i18next, in both Italian and English, with a working
      language switcher
- [ ] Signing in lands on a signed-in view; signing out returns to the sign-in screen
- [ ] A `Pending` account signing in sees the awaiting-approval screen, not a wrong-password error
- [ ] Visiting the register route with a token registers an account and explains what happens next
- [ ] Registration errors are shown as translated sentences, never as raw codes
- [ ] An administrator sees the pending queue and can approve and reject; a member never sees the
      link or the route
- [ ] A page refresh keeps the session
- [ ] `npm run test`, `lint`, `typecheck` and `build` all clean; CI green

## Concepts to explain

- **How i18next resolves a key**, what a namespace is for, and why the language switcher changes
  strings without a reload
- **Client-side routing**: what the router replaces, and why a deep link still works
- **Where session state lives** in a React tree, and why context beats prop-drilling for it
- **Why the client never decides permissions** — hiding a link is convenience, and the server has
  already refused

## Risks and things to watch

- **Hiding the admin link is not access control.** The server refuses regardless; the hidden link is
  only there so members are not offered something that will fail.
- **The token in the URL ends up in browser history.** Nothing can be done about that from here, and
  it is one more reason invites expire.
- Do not let the API client accumulate ad-hoc `fetch` calls scattered across components — one module
  owns the endpoints, so 023 extends rather than duplicates it.
- Translation keys are cheap to add and expensive to rename. Namespace them properly now.
