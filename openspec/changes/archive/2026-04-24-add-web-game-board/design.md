## Context

The room lifecycle is the full product in microcosm: auth (JWT on the hub query string), room membership (REST), real-time state (SignalR), user input (moves, chat, urge, resign), and recoverable networks (reconnect). The previous four web changes locked every cross-cutting pattern we need — guarded lazy routes, Signal-backed services, ProblemDetails mapping, CSS-variable styling, translated UI, CDK dialogs, test harnesses — so this change is "plug into the hub and render the game", not "invent new patterns."

The sharp edges are six, in order of gravity:

1. **Server authority vs. optimistic UI.** Server validates every move; a move the client thinks is legal may be rejected (occupied cell, not your turn, concurrent move). Optimistic rendering leads to ugly rubber-banding. The design chooses "wait for the `MoveMade` event" as the update source; acceptable because the backend round-trip is <100 ms in practice.
2. **Reconnection is non-trivial.** The hub doesn't ship a snapshot on reconnect — the client must re-`JoinRoom` and re-fetch `GET /api/rooms/{id}` to rehydrate. Events during the disconnection window are permanently lost; only the REST snapshot can be trusted.
3. **Role derivation is not on the wire.** The server doesn't tell the client "you are Black"; the client computes it by comparing `auth.user().id` against `state.black.id` / `state.white.id` / `state.spectators[]`. This has to be a computed Signal that updates when state flips.
4. **Two distinct "leaves."** `hub.leaveRoom(id)` unsubscribes from SignalR groups; `POST /api/rooms/{id}/leave` abandons the player slot. Conflating them in one button lets users accidentally forfeit by navigating away.
5. **Chat channel authorization.** Players can't post to Spectator channel; spectators can't see Room-only messages from the player perspective — wait, re-reading the spec: spectators *do* see Room chat (they receive it via group `room:{id}`, not `room:{id}:spectators`), and spectators' Spectator-channel messages never reach players. So the channel split is asymmetric; the UI has to reflect that.
6. **Connection auth lifecycle.** The JWT in the query string is fixed at connect time. If the token expires mid-connection, the server closes. SignalR's auto-reconnect re-invokes `accessTokenFactory`, so passing a factory that reads the current (possibly-refreshed) `AuthService.accessToken()` is the right move.

## Goals / Non-Goals

**Goals:**

- Replace the `/rooms/:id` placeholder with a fully playable page — moves land, game ends, chat flows, resign works, reconnect is invisible in the common case.
- Wire SignalR lazily so anonymous / lobby-only users never open a hub connection.
- Make the page survive a 30-second network blip by reconnecting + rehydrating from REST; during the blip, show a visible "reconnecting…" state.
- Keep server authoritative for move / rule validation — client pre-filter is UX latency, not a second source of truth.
- Ship a `Board` component reusable enough to be driven by `add-web-replay-and-profile` later (read-only mode).
- All translatable, all token-themed, all accessible (keyboard + aria-label on every cell), all 375 px usable.

**Non-Goals:**

- Move sound effects / move animations beyond a last-move highlight.
- Move history scrollback, PGN-style move list, move undo. All replay concerns.
- Lobby-wide SignalR hub.
- Zoom/pan gestures on the board.
- A persistent spectator experience across room dissolves.
- Custom SignalR transports / protocol negotiation — stick with library defaults (WebSocket → LongPolling fallback).
- Multi-tab coordination (same user playing from two tabs is fine; both show the same state via independent connections).

## Decisions

### D1. Lazy hub connection, single shared instance per browser tab

**Decision:** `DefaultGameHubService` holds a single `HubConnection`. `joinRoom(id)` is the first caller that causes `connection.start()` to run. Subsequent `joinRoom(otherId)` on the same connection just calls `connection.invoke('LeaveRoom', oldId)` → `invoke('JoinRoom', newId)`. There is one connection per tab; navigating from one room to another reuses it.

**Rationale:**

- Matches CLAUDE.md's "首次订阅时才连" — lobby never triggers a connection.
- Reusing the connection across rooms avoids a ~150 ms reconnect each time (slower than navigating with HTTP would be).
- Single connection keeps auth-token handling simple.

**Alternatives considered:**

- **Connection per RoomPage instance.** Rejected: every navigation to a new room incurs handshake + auth + server-side group-join latency. Feels slower. Also leaks on navigations that don't call `stop()` cleanly.
- **App-scoped always-on connection starting at login.** Rejected: the lobby has nothing to subscribe to, so this just burns backend resources and a websocket slot for every signed-in user.

### D2. Hub service API shape — Signals for state, Observables for transient events

**Decision:**

```ts
abstract class GameHubService {
  abstract readonly state: Signal<RoomState | null>;
  abstract readonly connectionStatus: Signal<ConnectionStatus>;
  abstract readonly gameEnded: Signal<GameEndedDto | null>;
  abstract readonly urged$: Observable<UrgeDto>;
  abstract readonly roomDissolved$: Observable<RoomDissolvedDto>;

  abstract joinRoom(roomId: string): Promise<void>;
  abstract leaveRoom(roomId: string): Promise<void>;
  abstract joinSpectatorGroup(roomId: string): Promise<void>;
  abstract makeMove(roomId: string, row: number, col: number): Promise<void>;
  abstract sendChat(roomId: string, content: string, channel: ChatChannel): Promise<void>;
  abstract urge(roomId: string): Promise<void>;

  abstract applySnapshot(state: RoomState): void; // used by the REST rehydration path
}

type ConnectionStatus = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';
```

- `state` is mutated by: `applySnapshot()` (REST rehydration), `RoomState` events (full replacement), `MoveMade` events (append to `state.game.moves` + flip `currentTurn` + advance `turnStartedAt`), `ChatMessage` events (append to `state.chatMessages` bucketed by channel), `PlayerJoined`/`PlayerLeft`/`SpectatorJoined`/`SpectatorLeft` (update seats / spectators list).
- `gameEnded` is set on the event; it's a signal because the board page renders it in a dialog that reacts to the value. Cleared when the user leaves the room.
- `urged$` and `roomDissolved$` are `Subject`-backed Observables because they're transient UI triggers (toast, navigate-away) — not state to render persistently.

**Rationale:**

- Signals for renderable state, Observables for triggers. Standard pattern in the codebase.
- `applySnapshot` is publicly callable because REST rehydration happens outside the hub service (the room page loads the snapshot via `RoomsApiService.getById`, then hands it to the hub service). Keeps REST boundary in `core/api/` not `core/realtime/`.
- Everything returns `Promise<void>` — the actual results are events, not RPC returns. Callers `await` to catch `HubException` rejections from the server.

**Alternatives considered:**

- **One Subject-of-state**. Rejected: RxJS state stream where Signals would do is noise given the rest of the codebase.
- **Full event log as a Signal**. Rejected: state is the view, not the log. Replay will want the log; this change doesn't.

### D3. `accessTokenFactory` wires to `AuthService.accessToken()`

**Decision:**

```ts
new HubConnectionBuilder()
  .withUrl('/hubs/gomoku', {
    accessTokenFactory: () => authService.accessToken() ?? '',
  })
  .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 30_000])
  .build();
```

**Rationale:**

- SignalR calls `accessTokenFactory` on every connect attempt including auto-reconnects. Reading the signal means the factory always returns whatever is fresh after any interceptor-driven refresh.
- An empty string triggers an `Unauthorized` handshake if the user is somehow signed out — RoomPage guards this case by not calling `joinRoom` if `!auth.isAuthenticated()`.
- The reconnect schedule (0s / 2s / 5s / 10s / 30s, then stop) is the SignalR default flavor, enough to survive Wi-Fi hiccups without hammering the server.

**Alternatives considered:**

- **Rebuild the connection on token refresh.** Rejected: the server holds the original token's lifetime as the connection deadline. When the server closes at 15-minute expiry, auto-reconnect picks up the new token via the factory. No active rebuild needed.

### D4. Reconnect protocol — REST snapshot is canonical

**Decision:** On `onreconnecting` → set `connectionStatus='reconnecting'`, RoomPage shows a banner. On `onreconnected` → set `connectionStatus='connected'`, then the service orchestrator (a small glue in RoomPage, not in the hub service) runs:

1. `hub.joinRoom(roomId)` — re-join the group.
2. If the local `state()` shows we were a spectator, `hub.joinSpectatorGroup(roomId)`.
3. `rooms.getById(roomId)` → `hub.applySnapshot(snapshot)` — canonical rehydration; any events during the disconnect window are irrecoverable, so trust the REST snapshot.

On `onclose` (reconnection exhausted) → `connectionStatus='disconnected'`, banner shows a "Retry" button that calls `hub.reconnect()` (which is just `connection.start()` plus the rehydration steps).

**Rationale:**

- Server doesn't push a snapshot on reconnect; REST is the only way to catch up.
- Putting the reconnect glue in RoomPage rather than GameHubService keeps the service stateless about "which room the user is in". The service knows "a room" only as long as someone is calling its methods.

**Alternatives considered:**

- **GameHubService owns currentRoomId and self-rejoins.** Considered — cleaner in isolation but couples the service to `RoomsApiService` for rehydration, and ties the service lifecycle to a single room (breaks D1's reuse-across-rooms goal).

### D5. No optimistic moves; disable-and-wait during `makeMove`

**Decision:** Clicking a cell:

1. Sets a local `boardSubmitting = signal(true)` in `RoomPage`.
2. Calls `hub.makeMove(id, row, col)`.
3. Awaits the promise. The promise resolves when the hub's `invoke` acks (which happens after the server dispatches but *before* the broadcast arrives — which is fine, because the `MoveMade` broadcast will also arrive to the same connection).
4. On success: lets `MoveMade` flow through and update `state`, clears `boardSubmitting`.
5. On `HubException`: parses the message; shows a translated toast / inline error; clears `boardSubmitting`; re-fetches `GET /api/rooms/{id}` → `applySnapshot` to recover (concurrent-move case handles itself via this rehydration).

During `boardSubmitting === true`, all cells are `disabled`. This is a <200 ms window in practice.

**Rationale:**

- Simpler than reconciling optimistic state with server authoritative state.
- The "no double-click" property falls out for free.
- Rubber-banding on move rejection is an ugly edge case we eliminate entirely.

**Alternatives considered:**

- **Optimistic placement with rollback on `HubException`.** Rejected: ugly in the rejection case; complicates the board's Signal wiring; saves at most 100 ms in the happy path.

### D6. Role derivation as a computed Signal

**Decision:**

```ts
readonly mySide = computed<'black' | 'white' | 'spectator'>(() => {
  const state = hub.state();
  const userId = auth.user()?.id;
  if (!state || !userId) return 'spectator';
  if (state.black?.id === userId) return 'black';
  if (state.white?.id === userId) return 'white';
  return 'spectator';
});

readonly myTurn = computed<boolean>(() => {
  const side = mySide();
  const turn = hub.state()?.game?.currentTurn;
  return (side === 'black' && turn === 'Black') || (side === 'white' && turn === 'White');
});
```

**Rationale:**

- One-line semantics; updates atomically whenever the state flips.
- Consumers read `mySide()` once per render; no imperative listeners.

### D7. Turn countdown — a 1 Hz "now" Signal

**Decision:** `RoomPage` creates a `now = signal<number>(Date.now())` and kicks `setInterval(() => now.set(Date.now()), 1_000)` on init, tearing down on destroy. The countdown is:

```ts
readonly turnRemainingMs = computed<number>(() => {
  const game = hub.state()?.game;
  if (!game) return 0;
  const start = new Date(game.turnStartedAt).getTime();
  const deadline = start + game.turnTimeoutSeconds * 1_000;
  return Math.max(0, deadline - now());
});
```

Displayed as `M:SS` via a small format helper. When `turnRemainingMs() === 0`, show "0:00" — the server-side poller will push a `GameEnded` event within ~5 s (poller period).

**Rationale:**

- Signals don't auto-tick; a visible `setInterval` is the honest way to drive a countdown. 1 Hz is fine for a minute-scale timer.
- Putting the interval in the page (not the service) keeps the service lifecycle independent of the renderer.

### D8. Chat panel — asymmetric visibility

**Decision:**

- `ChatPanel` reads `hub.state()?.chatMessages` and splits into Room / Spectator buckets.
- Tabs shown: player sees only the Room tab; spectator sees two tabs.
- Send: the active tab is the channel on the outgoing `hub.sendChat`.
- Player attempts Spectator channel → client-side prevention (tab not offered); backend would reject with 403 if somehow invoked.
- 500-char limit mirrors the server. Enforced via `Validators.maxLength(500)` on the input.

**Rationale:**

- Two tabs is the simplest UI that matches the model. Slack-style side-by-side would need too much horizontal space at 375 px.

### D9. Urge button cooldown mirrored client-side

**Decision:** When `hub.urge()` resolves successfully, `urgeCooldownUntil = Date.now() + 30_000`. Button `disabled` while `now() < urgeCooldownUntil`. 429 responses from the server override the local cooldown state (sync it to server truth). The urge button is also disabled on the player's own turn (backend would throw `NotOpponentsTurnException`) and for spectators.

**Rationale:**

- Reduces wasted hub invocations.
- Server still has the last word via 429 — the client-side cooldown is UX, not auth.

### D10. Game-ended dialog is a one-shot on `gameEnded()` signal flip

**Decision:** `RoomPage` `effect(() => { ... })` watches `hub.gameEnded()`. When it turns non-null (from null), the effect calls `dialog.open(GameEndedDialog, { data: hub.gameEnded()! })`. The dialog shows localized copy based on (a) whose perspective (mySide vs. winnerUserId), (b) end reason. Primary button → `/home`. Close (X) leaves the user on a frozen, read-only room view.

The signal is cleared when the user leaves the room (unsubscribes) so a subsequent room view starts fresh.

**Rationale:**

- `effect()` semantics map one-to-one with "render a side effect when the signal becomes non-null." Alternative approaches (imperative event listener) would leak without explicit cleanup.

### D11. Resign flow — confirm, then REST

**Decision:** Resign button → opens a small confirmation CDK Dialog ("Resign? You'll lose this game."). Confirm → `rooms.resign(id)` (REST). Success: nothing happens client-side except the existing `GameEnded` broadcast reaching the subscriber and opening the game-ended dialog via D10. Failure: translated toast.

Keep resign REST-only (matching the backend), not a hub method. The response value is `GameEndedDto` but we ignore it — the event-driven path is authoritative.

**Rationale:**

- Prevents single-click accidental resign.
- REST response and hub event arrive at roughly the same time; the event path is authoritative so we don't double-handle.

### D12. Navigating away does NOT leave the player slot

**Decision:** `ngOnDestroy` calls `hub.leaveRoom(id)` (group unsubscribe) only. It does NOT call `POST /api/rooms/:id/leave`. A player who closes the tab stays a player server-side (consistent with backend domain: opponent cannot force-win via disconnect). To actually abandon the room as a player, the user must click the explicit "Leave room" button in the sidebar.

**Rationale:**

- Matches backend semantics ("Leave during Playing: opponent cannot force-win").
- Accidental navigations don't cost the user a game.

### D13. @microsoft/signalr dependency

**Decision:** Add `@microsoft/signalr` as a runtime dep. Import only the tree-shakeable pieces (`HubConnectionBuilder`, `HubConnection`, `LogLevel`). Reported ~30 KB gzip; bundled into the `room-page` lazy chunk only — eager lobby bundle is unaffected.

**Rationale:**

- The canonical client. Angular community wrappers exist but bring nothing we need.
- Tree-shaking keeps it out of the main chunk.

### D14. i18n layout

**Decision:** New `game.*` tree. Subtrees:

- `game.room.{name-label, host-label, seat-black, seat-white, status-waiting, status-playing, status-finished}`
- `game.board.{cell-aria-label, last-move-label}` — cell aria uses Transloco interpolation: `"Row {{row}} Column {{col}}"` / `"第 {{row}} 行 第 {{col}} 列"`.
- `game.turn.{your-turn, opponent-turn, black-turn, white-turn, countdown-label}`
- `game.actions.{resign, resign-confirm-title, resign-confirm-body, resign-confirm-ok, leave, urge}`
- `game.chat.{title, tab-room, tab-spectator, send, placeholder, empty, max-length-error, forbidden-error}`
- `game.urge.{toast, button-disabled-own-turn, button-disabled-cooldown}`
- `game.ended.{title-win, title-lose, title-draw, reason-connected-5, reason-resigned, reason-timeout, back-to-lobby, dismiss}`
- `game.errors.{generic, network, not-your-turn, invalid-move, concurrent-move-refetched, urge-cooldown}`
- `game.connection.{reconnecting, disconnected, retry, connected}`

**Rationale:** Matches the translation key style we've been using — flat dot paths, kebab-case leaves.

### D15. What we deliberately don't build

- **Sound effects / animations beyond CSS transitions on last-move highlight.** The scope brief explicitly defers audio.
- **Move replay / scrollback.** Replay change's job.
- **Emoji reactions / quick-chat.** Polish, later.
- **Mobile board gestures (pinch-zoom, pan).** The 15×15 grid fits a 375 px screen at ~24 px per cell. Good enough without gesture support.
- **Multi-room tabs.** One tab = one room; two tabs = two independent connections. This is fine.

## Risks / Trade-offs

- **Risk: SignalR auto-reconnect silently fails mid-reconnect (e.g., backend rolling deploy).** → Mitigation: the `onclose` handler surfaces a "Disconnected" banner with an explicit Retry button. Worst case, user reloads; state rehydration via REST works on any fresh page load.
- **Risk: Multiple `MoveMade` events arrive out of order under heavy reconnect churn.** → Mitigation: each `MoveMade` carries a monotonic `ply`. On merge, drop events whose `ply <= lastAppliedPly`. Keeps state consistent even if deliveries interleave.
- **Risk: Player loses their turn to a timeout while the tab is backgrounded.** → Accepted. The game has a turn timeout by design; being away means you lose. No attempt to pause for inactivity.
- **Risk: Chat spam.** → No client-side rate limit; backend has no rate limit on chat today either. If this becomes a problem, add throttling in a follow-up change.
- **Risk: Token refresh during an active hub connection closes the connection.** → Mitigation: auto-reconnect re-reads the factory, so the close is invisible — a "reconnecting" flash in the banner for ~200 ms. In the rare case refresh itself fails, the interceptor kicks the user to `/login`; the hub disconnects cleanly as the user navigates away.
- **Risk: `GameEnded` without `MoveMade` (resign / timeout paths) leaves board state that looks "mid-turn".** → Mitigation: the `RoomState` event that precedes `GameEnded` updates `status` to `Finished`; the board's `disabled` derivation checks `state.status === 'Playing'`. Confirmed by spec.
- **Risk: Initial `RoomsApiService.getById` fails with 404 (room already dissolved).** → Mitigation: RoomPage shows the translated "room not found" state with a back-to-lobby link (same as the old placeholder's 404 state).
- **Risk: `HubException` messages are English-only and not typed.** → Accepted for now. The user-facing translated copy is chosen by error class (message keyword detection: `"not your turn"` → `game.errors.not-your-turn`, `"invalid move"` → `game.errors.invalid-move`, concurrent-move via 409 response shape when it slips out via the hub as a different error class — actually per research, the hub rethrows the exception and the message is the English exception message, so `DbUpdateConcurrencyException` message substring detection handles it). If a future change adds a typed error code to the hub, the client switches to reading that code. Documented as a follow-up.
- **Risk: Two players in the same room from one browser (same user account).** → Not supported by the backend (`AlreadyInRoomException`). UI surfaces as a generic "couldn't join" error.
- **Risk: The 1 Hz countdown `setInterval` keeps the tab alive even when idle.** → Accepted. RoomPage is a foregrounded-by-intent view; battery cost is negligible. Destroyed on navigate-away.
- **Trade-off: @microsoft/signalr in the lazy room chunk (not main).** → Accepted. Lobby → first room entry adds ~30 KB gzip to the first network hit into the room. Subsequent room-to-room reuses the cached chunk.
- **Trade-off: no move optimism.** → Accepted. <200 ms latency under good conditions; move-rejection handling is much simpler. Revisit only if users complain about "laggy" board.

## Migration Plan

- The `/rooms/:id` route path is unchanged. The `loadComponent` target swaps from `RoomPlaceholder` to `RoomPage`. Bookmarks / in-flight lobby navigations don't break.
- `web-lobby` spec loses the "临时占位组件" requirement in the same commit that lands this change (the removed-requirement delta is part of the change's specs/).
- Rollback: revert the commit → the placeholder returns, the hub service is gone, everything still builds. There's no database or persisted client state to roll back.

## Open Questions

- **Q1: Move sound toggle in the future?** Likely yes if users ask. When added, respect `prefers-reduced-motion` as a proxy for "don't make noise either" unless we add a dedicated audio-on toggle. Not in scope for this change.
- **Q2: Board dimensions for ultra-wide screens?** The spec says 15×15 with CSS grid. On a 2560 px desktop the board has room to grow, but a fixed max-size (say 600 px) keeps the stones at a comfortable grip-size and leaves the sidebar breathable. Settle on `max-w-[600px]` + `aspect-square` for MVP.
- **Q3: Should `GameEndedDialog` auto-dismiss after N seconds?** No — the user should acknowledge. If UX testing shows users don't dismiss the dialog fast enough and feel stuck, revisit.
