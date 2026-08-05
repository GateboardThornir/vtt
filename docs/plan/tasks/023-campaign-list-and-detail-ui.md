# 023 — Campaign list and detail UI

**Status:** not started
**Depends on:** 021, 022
**Branch:** `task/023-campaign-list-and-detail-ui`

## Goal

Campaigns become usable without `curl`: create one, see the ones you are in, manage a roster, answer
an invitation, and notice that you have been invited at all.

## Scope

In scope:
- A campaign list showing what you are in and what you are to it
- Creating a campaign, with the pinned system and version
- A campaign detail page with its roster; the Master can invite and remove, a Player can leave
- Pending invitations, accept and decline
- The notification bell from 022, with an unread count and translated messages
- Italian and English throughout; component tests for each screen

Explicitly out of scope:
- Sessions inside a campaign — 024
- Characters, the table, scenes — later phases
- Styling beyond legibility

## Approach

**The client renders roles, it does not enforce them.** Whether the invite control appears follows
from what the server said the caller's role is; the server refuses regardless. The distinction
matters because a Player who reaches the endpoint anyway must be refused by the server, not by the
absence of a button.

**Notifications render from a kind and a parameter.** 022 deliberately sends no prose; the client
owns the sentence in both languages.

## Acceptance criteria

- [ ] Create, list and open a campaign; the pinned system is visible
- [ ] A Master sees invite and remove controls; a Player does not, and cannot invite via the API
- [ ] An invitation appears both as a notification and in a pending list, and can be accepted or
      declined
- [ ] Leaving a campaign removes it from the list
- [ ] The unread count drops when notifications are read
- [ ] Every string is translated; the switcher changes all of them
- [ ] Frontend tests, lint, typecheck and build clean; CI green

## Risks and things to watch

- **Hiding a control is not authorisation.** The server has already decided.
- A campaign the caller cannot see is a 404; the UI must show "not found", never a blank page.
- Keep every call in `src/api` rather than scattering `fetch` through components.
