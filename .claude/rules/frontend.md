---
paths:
  - "src/Client/**/*.{ts,tsx}"
  - "src/Client/**/*.css"
---

# Frontend conventions

## Architecture

React owns everything **around** the table canvas: navigation, sheets, chat, handouts, dialogs.
PixiJS owns the canvas itself. The boundary is strict — React never manipulates Pixi display
objects directly, and Pixi never renders UI chrome. They communicate through a thin adapter layer
that translates state deltas into scene-graph operations.

The canvas is mounted once and lives outside React's render cycle. Re-rendering the React tree
must never rebuild the Pixi application; a React re-render that resets the map view is a bug.

## State

Server state is the source of truth. The client holds a projection of what the server has sent —
never a speculative copy the server has not confirmed. Optimistic updates are allowed only for
token dragging (visual only, reconciled on server response); everything else waits for the server.

Never derive game rules on the client. Displaying a computed value the server sent is fine;
computing an attack outcome locally is not — it will diverge from the server and mislead the player.

## Real-time

One SignalR connection per session, owned by a single connection manager. Components subscribe
to typed events; they never hold their own connection. Handle reconnect explicitly: on reconnect,
request a fresh snapshot and replace local state rather than merging.

## i18n

All user-facing strings go through the translation layer from the first line of UI code —
retrofitting i18n is far more expensive than doing it inline. Keys are namespaced by feature.
Bundled SRD content stays English and is not translated.

## Components

Function components with hooks. No class components. Colocate a component with its styles and
tests. Keep components small enough to read in one screen; extract when a component grows past
roughly 150 lines. Props typed explicitly, no `React.FC`.

## Performance on the table

The canvas must stay responsive with hundreds of sprites. Batch scene-graph updates per animation
frame rather than per incoming message. Never allocate textures inside a render loop. Profile
before optimising, but treat dropped frames during token movement as a defect, not a nice-to-have.
