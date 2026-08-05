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

## Tests

Vitest with jsdom and React Testing Library. Test files are colocated with the component
(`App.tsx` / `App.test.tsx`), and `describe`/`it`/`expect` are imported explicitly — globals are
off deliberately.

Query the way a user perceives the page: by role, label and text, never by class name or component
internals. Tests written that way survive markup refactors; tests coupled to structure do not.

Stub the network at `fetch`, not at the wrapper that calls it. Stubbing the wrapper only proves a
component called a function; stubbing `fetch` proves it handles what the server actually sends —
including error statuses, which are answers rather than exceptions.

jsdom does not lay out or paint. Anything depending on real geometry, true event ordering or a
WebGL context — the Pixi canvas above all — cannot be meaningfully covered here.

## Performance on the table

The canvas must stay responsive with hundreds of sprites. Batch scene-graph updates per animation
frame rather than per incoming message. Never allocate textures inside a render loop. Profile
before optimising, but treat dropped frames during token movement as a defect, not a nice-to-have.
