## 1. Dependencies + typed DTOs

- [x] 1.1 `npm install --save @microsoft/signalr` — record the version in `package.json`. Verify the built artifact size; tree-shake-friendly imports (`HubConnectionBuilder`, `HubConnection`, `LogLevel`, `HubConnectionState`).
- [x] 1.2 Extend `src/app/core/api/models/room.model.ts` with `Stone`, `GameResult`, `GameEndReason`, `ChatChannel` string-literal unions plus `MoveDto`, `GameSnapshot`, `ChatMessage`, `GameEndedDto`, `UrgeDto` interfaces. Replace `RoomState.game: unknown | null` → `GameSnapshot | null` and `RoomState.chatMessages: readonly unknown[]` → `readonly ChatMessage[]`.
- [x] 1.3 Add `resign(roomId: string): Observable<GameEndedDto>` to the abstract `RoomsApiService` and implement on `DefaultRoomsApiService` as `POST /api/rooms/{id}/resign` with an empty object body.
- [x] 1.4 Update / add any existing tests that reference the now-narrower `RoomState.game` / `RoomState.chatMessages` unknown types (lobby + placeholder-era tests) to the new types. Confirm `npm run build` still green after steps 1.2–1.3.

## 2. `GameHubService`

- [x] 2.1 Create `src/app/core/realtime/game-hub.service.ts`:
  - Abstract `GameHubService` class with the signals / observables / methods listed in `specs/web-game-board/spec.md` (D2).
  - `DefaultGameHubService` impl: lazily builds `HubConnection` on first command; `accessTokenFactory: () => inject(AuthService).accessToken() ?? ''`; `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])`; `configureLogging(LogLevel.Warning)`.
  - Register `.on('RoomState' | 'PlayerJoined' | 'PlayerLeft' | 'SpectatorJoined' | 'SpectatorLeft' | 'MoveMade' | 'GameEnded' | 'ChatMessage' | 'UrgeReceived' | 'RoomDissolved', handler)` callbacks; each handler updates signals / subjects per spec.
  - `applySnapshot(state: RoomState)` publicly replaces the state signal.
  - `leaveRoom(id)` clears the `gameEnded` signal at the end (ready for next room entry).
  - `onreconnecting` / `onreconnected` / `onclose` listeners wire `connectionStatus` signal transitions.
- [x] 2.2 Guard against double-connect: `joinRoom` / etc. check `connection.state` and only call `connection.start()` if the state is `Disconnected`.
- [x] 2.3 `MoveMade` handler dedup: track `lastAppliedPly: number | null`; drop events whose `ply <= lastAppliedPly` (handles cross-reconnect replays). Reset on `applySnapshot` (derive `lastAppliedPly` from the snapshot's last move ply).
- [x] 2.4 Register in `app.config.ts`: `{ provide: GameHubService, useClass: DefaultGameHubService }` (app-scoped; the service internally tolerates the hub connection being reused across rooms).
- [x] 2.5 Public `reconnect(): Promise<void>` method — used by the "disconnected" banner's Retry button. Internally calls `connection.start()` if disconnected; no-op if already connected.

## 3. RoomPage shell + routing

- [x] 3.1 Delete `src/app/pages/rooms/room-placeholder/` (component + template + spec). Confirm no residual imports.
- [x] 3.2 Create `src/app/pages/rooms/room-page/room-page.ts` + `room-page.html` — standalone, OnPush. Imports: child components (Board, Sidebar, ChatPanel), CDK Dialog, TranslocoPipe.
- [x] 3.3 Update `src/app/app.routes.ts` — `/rooms/:id`'s `loadComponent` now imports `RoomPage`.
- [x] 3.4 `RoomPage` `ngOnInit` sequence per spec D4 / D5 / D7: read route param → `rooms.getById(id)` → `hub.applySnapshot` → `hub.joinRoom(id)` → conditional `joinSpectatorGroup` → start 1 Hz `now` signal via `setInterval` → subscribe `hub.urged$` / `hub.roomDissolved$` / `effect(hub.gameEnded)`.
- [x] 3.5 `ngOnDestroy`: `clearInterval` on the now timer, `hub.leaveRoom(id)`, unsubscribe all side-effect subscriptions.
- [x] 3.6 404 handling on initial `getById`: show translated `game.room.not-found` + back-to-lobby link in place of the main layout. Do NOT call `hub.joinRoom`.
- [x] 3.7 Generic REST error on initial `getById`: show `game.errors.generic` banner + retry button. Same gate on hub calls.
- [x] 3.8 Reconnection banner: `@if (hub.connectionStatus() === 'reconnecting')` → translated `game.connection.reconnecting`. `@if (hub.connectionStatus() === 'disconnected' && page is still mounted)` → `game.connection.disconnected` + Retry button calling `hub.reconnect()`.
- [x] 3.9 Reconnection rehydration: on `connected` after `reconnecting`, re-run the init rehydration sequence (joinRoom + optional joinSpectatorGroup + `getById` + `applySnapshot`). Use an `effect` on `connectionStatus` with an edge-trigger check.
- [x] 3.10 `hub.roomDissolved$` subscription → `router.navigateByUrl('/home')`.

## 4. Board component

- [x] 4.1 Create `src/app/pages/rooms/room-page/board/board.ts` + `board.html` — standalone, OnPush. Inputs: `state: InputSignal<RoomState | null>`, `mySide: InputSignal<'black' | 'white' | 'spectator'>`, `submitting: InputSignal<boolean>`, `readonly: InputSignal<boolean>` (default `false`). Output: `(cellClick)="emitted"` with `{ row, col }`.
- [x] 4.2 Template: `<div>` with CSS grid `grid-template-columns: repeat(15, 1fr); aspect-ratio: 1; max-width: 600px; background: var(--color-surface); border: 1px solid var(--color-border)`. 225 `<button type="button">` cells. Per-cell `aria-label` via `game.board.cell-aria-label` with `{{ row }}` / `{{ col }}` interpolation (1-based for users).
- [x] 4.3 Per-cell `disabled` logic (computed in the template from the inputs): `state()?.status !== 'Playing' || readonly() || submitting() || mySide() === 'spectator' || !myTurn() || stoneAt(row, col) !== 'Empty'`. Expose `stoneAt(row, col): Stone` as a helper method that derives from `state().game?.moves` (replay in order; default Empty).
- [x] 4.4 Rendered stones: black → filled circle using `var(--color-text)` for a light-mode contrast, or more simply a `::before` pseudo with `background: var(--color-text)`; white → `background: var(--color-bg)` + `border: 2px solid var(--color-border)`. Use inline `style="background: var(--color-*)"` ONLY when a utility isn't available; prefer token utility classes.
- [x] 4.5 Last-move highlight: compute `lastMove(): MoveDto | null` = `state()?.game?.moves.at(-1) ?? null`; on the matching cell add a CSS class that applies `box-shadow: 0 0 0 2px var(--color-primary) inset` or equivalent. Include an `aria-describedby` pointing to an off-screen span containing `game.board.last-move-label`.
- [x] 4.6 Click handler emits `(cellClick)` with `{ row, col }`. RoomPage wraps this and calls `hub.makeMove(id, row, col)` with the submit-and-disable flow per spec D5.
- [x] 4.7 Readonly mode (`readonly() === true`): every cell `disabled`; no click emission.

## 5. Sidebar

- [x] 5.1 Create `src/app/pages/rooms/room-page/sidebar/sidebar.ts` + `sidebar.html` — standalone, OnPush. Inputs: `state: InputSignal<RoomState | null>`, `mySide: InputSignal<'black' | 'white' | 'spectator'>`, `turnRemainingMs: InputSignal<number>`. Outputs: `(resign)`, `(leave)`, `(urge)`, `(stopSpectating)` (optional — only if a reverse-spectate endpoint exists).
- [x] 5.2 Render room name / host / seats / status badge / current-turn indicator using the translation keys listed in the spec.
- [x] 5.3 Turn countdown: format `turnRemainingMs()` as `M:SS`; add `text-danger` class when `turnRemainingMs() <= 10_000`.
- [x] 5.4 Player-only buttons: Resign (opens confirm dialog via `Dialog.open(ResignConfirmDialog)`; on confirm emits `(resign)`), Leave (directly emits `(leave)`), Urge (emits `(urge)`; `disabled` per spec's Urge Requirement).
- [x] 5.5 RoomPage wires: `(resign)` → `rooms.resign(id).subscribe({...})`; `(leave)` → `rooms.leave(id).subscribe(() => router.navigateByUrl('/home'))`; `(urge)` → `hub.urge(id)` + cooldown tracking.

## 6. Chat panel

- [x] 6.1 Create `src/app/pages/rooms/room-page/chat/chat-panel.ts` + `chat-panel.html`. Inputs: `state: InputSignal<RoomState | null>`, `mySide: InputSignal<'black' | 'white' | 'spectator'>`. Internal state: `activeChannel = signal<'Room' | 'Spectator'>('Room')`, `inputControl` reactive form.
- [x] 6.2 Tabs: player sees only Room; spectator sees both (Room + Spectator). Use plain `<button>` elements with `role="tab"` + `aria-selected` switching. CDK's `CdkMenu` isn't appropriate for tabs; using semantic HTML is fine.
- [x] 6.3 Message list: `state()?.chatMessages` filtered by `message.channel === activeChannel()`, sorted by `sentAt` ascending, auto-scroll to bottom on new message (via `effect` on the filtered length).
- [x] 6.4 Input: `Validators.required, Validators.maxLength(500)`. Send button disabled on invalid / empty-after-trim / max-length exceeded / hub not connected. On submit: emit `(send)` with `{ content, channel }`; RoomPage calls `hub.sendChat(id, content, channel)`, on success clears the input, on failure (403, 400) shows translated banner.
- [x] 6.5 Forbidden banner (`game.chat.forbidden-error`): shown if the user somehow ends up with a send rejection; auto-dismissed after 3 s.
- [x] 6.6 Spectator sending to Room tab: works normally (spec allows, backend accepts).

## 7. Dialogs

- [x] 7.1 Create `src/app/pages/rooms/room-page/dialogs/game-ended-dialog.ts` + `.html`. Consumes `DIALOG_DATA` for `{ result, winnerUserId, endReason, myUserId, mySide }`. Renders translated title / reason / primary + secondary buttons.
- [x] 7.2 Title selection logic: draw → `game.ended.title-draw`; mySide win → `title-win`; otherwise → `title-lose`. Reason → one of the three `game.ended.reason-*`.
- [x] 7.3 Primary button → `DialogRef.close('home')`, caller navigates `/home`. Secondary → `DialogRef.close('stay')`, no-op.
- [x] 7.4 Create `dialogs/resign-confirm-dialog.ts` + `.html`. Renders title / body / cancel / confirm. Confirm → `close(true)`. Cancel or X → `close(false)`.
- [x] 7.5 `RoomPage` wires: `effect(() => { const ended = hub.gameEnded(); if (ended && !isEndedDialogOpen()) openEndedDialog(ended); })`. Track `isEndedDialogOpen` with a local signal to prevent double-open.

## 8. Urge toast

- [x] 8.1 In `room-page.html`, a top-of-page conditional banner: `@if (urgeToast()) { <div class="bg-surface text-text border-border shadow-elevated ...">{{ 'game.urge.toast' | transloco }}</div> }`.
- [x] 8.2 `RoomPage` subscribes to `hub.urged$`; on each emission sets `urgeToast.set(true)` and `setTimeout(() => urgeToast.set(false), 4000)` (clears prior timeout if chained).
- [x] 8.3 Local urge-sent cooldown: `urgeCooldownUntil = signal<number>(0)`; `canUrge = computed(() => now() >= urgeCooldownUntil())`; on successful `hub.urge` set `urgeCooldownUntil.set(Date.now() + 30_000)`.
- [x] 8.4 Button state: `disabled = !canUrge() || myTurn() || mySide() === 'spectator' || state()?.status !== 'Playing' || connectionStatus() !== 'connected'`.
- [x] 8.5 429 `UrgeTooFrequentException` path: align `urgeCooldownUntil` to `now + 30_000` (server is the truth, local estimate was optimistic).

## 9. Error mapping

- [x] 9.1 Create `src/app/pages/rooms/room-page/hub-error.mapper.ts` exporting `hubErrorToKey(err: unknown): string` per spec's error-key table. Case-insensitive substring matching on the error's `message`.
- [x] 9.2 Concurrent-move special case: the mapper returns `game.errors.concurrent-move-refetched` AND the caller (RoomPage in the `makeMove` failure path) triggers `rooms.getById(id) → hub.applySnapshot`.
- [x] 9.3 Non-HubException errors (e.g., `Error("No connection")` from a hub method called while disconnected) map to `game.errors.network`.

## 10. i18n keys

- [x] 10.1 Extend `public/i18n/en.json` with the full `game.*` tree from the spec.
- [x] 10.2 Extend `public/i18n/zh-CN.json` with the same keys in 简体中文.
- [x] 10.3 Run the parity flattener; confirm zero drift.
- [x] 10.4 Verify cell `aria-label` interpolation: `"Row {{row}} Column {{col}}"` / `"第 {{row}} 行 第 {{col}} 列"` produce readable screen-reader output.

## 11. Tests

- [x] 11.1 `src/app/core/realtime/game-hub.service.spec.ts`:
  - Construct service with a stub `HubConnection` (or inject a stub factory); call `joinRoom('r-1')`; assert `connection.start()` was called exactly once; subsequent `joinRoom('r-2')` does not re-start.
  - Feed a `RoomState` event payload into the stub's `.on('RoomState', ...)` handler; assert `state()` returns the payload.
  - Feed a `MoveMade` event; assert `state().game.moves` appended + `currentTurn` flipped.
  - Feed a `MoveMade` with `ply <= lastAppliedPly`; assert it is dropped.
  - Feed `GameEnded`; assert `gameEnded()` becomes non-null.
  - Simulate `onreconnecting` / `onreconnected` callbacks; assert `connectionStatus()` transitions.
  - `applySnapshot` replaces state wholesale + resets `lastAppliedPly` from the snapshot's moves.
- [x] 11.2 `src/app/pages/rooms/room-page/board/board.spec.ts`:
  - Cell click on my turn emits `(cellClick)` with correct `{row, col}`.
  - Cell click on opponent's turn does NOT emit (button disabled).
  - Spectator mode: all 225 cells disabled.
  - Last-move cell has the highlight class.
  - Readonly mode: all cells disabled regardless of turn.
- [x] 11.3 `src/app/pages/rooms/room-page/chat/chat-panel.spec.ts`:
  - Player: only Room tab in the DOM.
  - Spectator: both tabs in the DOM.
  - 500-char limit disables send and surfaces the error.
  - Switching tab changes the outgoing `channel` arg on send emission.
- [x] 11.4 `src/app/pages/rooms/room-page/dialogs/game-ended-dialog.spec.ts`:
  - Title selection across win / lose / draw permutations.
  - Primary button closes with `'home'`.
- [x] 11.5 `src/app/pages/rooms/room-page/dialogs/resign-confirm-dialog.spec.ts`:
  - Confirm → `close(true)`; Cancel → `close(false)`.
- [x] 11.6 Sidebar spec (abbreviated): countdown format; `text-danger` class at ≤10s; player-only buttons hidden for spectators.
- [x] 11.7 `src/app/pages/rooms/room-page/hub-error.mapper.spec.ts`: every branch of the mapping table returns the expected key; unknown → generic.
- [x] 11.8 `src/app/core/api/rooms-api.service.spec.ts`: add a test for `resign(id)` → POST to `/api/rooms/{id}/resign`.
- [x] 11.9 `src/app/pages/rooms/room-page/room-page.spec.ts` (smoke): mount with stubbed `GameHubService` + `RoomsApiService` + `AuthService`; verify `getById` called with `:id`; verify `hub.joinRoom` called afterwards; verify `hub.leaveRoom` called on destroy; verify reconnection banner renders when `connectionStatus === 'reconnecting'`.
- [x] 11.10 `npm run test:ci` — green.

## 12. Cross-cutting polish

- [x] 12.1 Grep sweep: no hex / rgb / hsl outside `tokens.css` + `core/theme/themes/`.
- [x] 12.2 Grep sweep: no hardcoded Tailwind palette (`bg-gray-*`, `text-white`, etc.).
- [x] 12.3 Grep sweep: no CJK in `src/app/pages/rooms/room-page/` templates (CJK only allowed in test fixtures, same rule as before).
- [x] 12.4 `inject(HttpClient)` grep: only in `core/api/` + `core/i18n/transloco-loader.ts` + `core/auth/auth.service.ts` + test files.
- [x] 12.5 `bypassSecurityTrust*` grep: zero matches.
- [x] 12.6 No `console.log` of tokens (access / refresh) — continued rule from auth.
- [x] 12.7 Component LOC: Room 253, Board 79, Sidebar 63, ChatPanel 101, GameEndedDialog 52, ResignConfirm 24 — verified via `wc -l` (RoomPage 3 lines over 250 soft budget, others well under).

## 13. Manual verification (DevTools + real backend)

- [ ] 13.1 Alice + Bob scenario: Alice creates a room → Bob joins from lobby → both land on `/rooms/:id`; both see the board. Alice (Black) plays (7,7) → move appears on both boards within ~100 ms. Bob plays. Game continues.
- [ ] 13.2 Win path: one side gets 5-in-a-row; both clients see the `GameEndedDialog` with correct "You won / You lost" perspective.
- [ ] 13.3 Resign path: Alice resigns; confirm dialog → OK → `GameEnded` broadcast → both clients show dialog with reason `Resigned`.
- [ ] 13.4 Turn timeout: wait 60s on Alice's turn; within ~5s of timeout both clients see `GameEnded` with reason `TurnTimeout` and the opposite color as winner.
- [ ] 13.5 Reconnect path: on Alice's tab, DevTools → Network → "Offline" → wait 5s → "Online". Verify `reconnecting` banner shows, then disappears, and the board state is consistent (re-fetched from REST snapshot). Bob sees no disruption (his connection is independent).
- [ ] 13.6 Disconnect exhaustion: disable network for > 1 min on Alice's tab; banner shows `disconnected` + Retry. Re-enable network, click Retry — re-connects and rehydrates.
- [ ] 13.7 Chat: Alice sends "hello" on Room; Bob sees it. Carol enters as spectator; sends on Room — both players see it. Carol sends on Spectator — only other spectators see it; Alice and Bob don't.
- [ ] 13.8 Urge: Alice urges Bob (on Bob's turn) — Bob sees toast; Alice's button disabled for 30 s. Alice tries to urge on her own turn — button disabled, error-free.
- [ ] 13.9 Room dissolve: Alice creates a room but before Bob joins, Alice dissolves it (must be from lobby via delete button — out of scope for this change; simulate via API curl). Bob is mid-navigation into `/rooms/:id` → receives `RoomDissolved` → auto-navigates back to `/home`.
- [ ] 13.10 Visual sweep: `/rooms/:id` in material/system × light/dark × en/zh-CN = 8 screens. Spot-check that the board grid lines are visible in all 4 theme combos (common regression: dark theme lines blend into background).
- [ ] 13.11 375 px viewport: board full-width, sidebar below board, chat below sidebar. Vertical scroll works; no horizontal scroll; all buttons reachable.
- [ ] 13.12 Token-refresh mid-game: let Alice's access token expire naturally (wait 15 min between moves, or force via DevTools by clearing `localStorage['gomoku:refresh']` momentarily); confirm the hub auto-reconnects with the fresh token and the game continues.

## 14. Final verification

- [x] 14.1 `npm run lint` passes.
- [x] 14.2 `npm run test:ci` passes — 112/112 tests across 23 files.
- [x] 14.3 `npm run build` passes. Initial: 462.73 KB raw / 119.00 KB gzip (baseline 116 KB → +3 KB, well under 250 KB target). `room-page` lazy chunk: 6.52 KB gzip; SignalR lazy `index` chunk: 12.80 KB gzip — loaded only on first hub call, not at app boot. Total room-page path ≈ 19.3 KB gzip, well under 200 KB ceiling.
- [x] 14.4 `openspec validate add-web-game-board` is clean.
