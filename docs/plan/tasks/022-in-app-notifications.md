# 022 — In-app notifications

**Status:** not started
**Depends on:** 021 (something worth being notified about)
**Branch:** `task/022-in-app-notifications`

## Goal

Things that happen to you while you are not looking become visible when you return. The platform
collects no email addresses, so this is the *only* channel it has — every flow that would otherwise
have sent a message needs it.

## Scope

In scope:
- A `Notification` entity: recipient, kind, parameters, read state, timestamp
- Emission from the flows that already exist and currently tell nobody: a campaign invitation (021),
  an account being approved or rejected (014)
- Listing your notifications, the unread count, marking one or all read
- A notification bell in the UI, translated
- Integration tests, including that notifications are strictly per-recipient

Explicitly out of scope:
- Real-time delivery. A notification appears on the next request; SignalR arrives at 040 and can
  push these later if it turns out to matter
- Session scheduling and RSVP notifications — 077, which is where those flows exist
- Any digest, grouping, or preference. Fifty people, a handful of notification kinds

## Approach

**Store a kind and its parameters, never a sentence.** The interface is bilingual, so a notification
written as English prose on the server arrives at the client untranslatable. The same reasoning as
task 012's error codes: the server says `campaign_invitation` with a campaign name, and the client
renders it in whichever language is on.

**Reading is scoped to the recipient, not filtered in the UI.** A notification belongs to exactly one
account and no query returns anyone else's. This is small enough to get right by construction and is
the same rule that fog of war will need at 062.

**Emission lives with the flow, not in a hook.** The roster service raises the invitation
notification because that is where the invitation happens. A generic event bus would be
infrastructure for a system with more than three publishers.

## Acceptance criteria

- [ ] Inviting somebody produces a notification for them and for nobody else
- [ ] Approving and rejecting an account notify that account
- [ ] The unread count is correct, and marking read reduces it
- [ ] One account cannot read, or mark read, another's notifications — asserted directly
- [ ] Notifications carry a kind and parameters, never a rendered sentence
- [ ] The bell renders in both languages
- [ ] Suite green, format clean, CI green

## Concepts to explain

- **Why the payload is structured rather than prose**, and what that buys a bilingual interface
- **Per-recipient scoping** as a query concern rather than a display concern
- **Why there is no event bus yet**, and what would justify one

## Risks and things to watch

- **A notification must never carry content the recipient is not entitled to.** Today they carry a
  campaign name; when handouts and secrets exist, this is a place a leak could hide.
- **Marking read is a write on someone else's row if the scoping is wrong.** Test it directly.
- Do not let this become a general-purpose activity feed. It exists because there is no email.
