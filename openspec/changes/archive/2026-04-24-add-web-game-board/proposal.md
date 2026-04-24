## Why

Four web changes have landed — scaffold, auth, lobby, and a throwaway `/rooms/:id` placeholder — but you still can't actually *play*. The backend's entire game surface (15×15 board, move validation, win detection, turn timeout, chat, urge, resign, room lifecycle, ELO on end) has been shipped for weeks and sits behind a single SignalR hub at `/hubs/gomoku` plus a REST resign endpoint. This change fills that gap: the `/rooms/:id` placeholder is replaced with a real game page that connects to the hub, renders the board, drives moves, chats, and handles disconnects / reconnects / game-end transitions end-to-end.

Research into the backend also surfaced some corrections worth carrying into the design up front: the server→client event is named `RoomState` (not `RoomStateChanged`), resign is REST-only (not a hub method), the default turn timeout is 60 s (not 30 — that's the urge cooldown), `UrgeReceived` is a direct-to-user message (not a group broadcast), and `GameEndReason.Connected5` covers the draw case too. Everything below bakes in those facts.

## What Changes

- **Replace `/rooms/:id` placeholder** with a real `RoomPage` at `src/app/pages/rooms/room-page/`. The route path, lazy-load pattern, and `canMatch: [authGuard]` guard are unchanged; only the loaded component is swapped. The old `room-placeholder/` folder is deleted.
- **`GameHubService`** — abstract-class-as-DI-token (same pattern as `AuthService` / `LobbyDataService`) living at `src/app/core/realtime/game-hub.service.ts`. Wraps a single `HubConnection` to `/hubs/gomoku` with `accessTokenFactory: () => authService.accessToken() ?? ''`. Lazy connection — the hub opens on the first `joinRoom()` call, not at app boot (honours CLAUDE.md's "首次订阅时才连"). Exposes:
  - `state: Signal<RoomState | null>` — authoritative current room state, fed by `RoomState` events + merged with `MoveMade` / `ChatMessage` / `GameEnded` deltas.
  - `connectionStatus: Signal<'disconnected' | 'connecting' | 'connected' | 'reconnecting'>`.
  - `gameEnded: Signal<GameEndedDto | null>` — set on `GameEnded`, cleared on room leave.
  - `urged$: Observable<UrgeDto>` — transient stream for toast display (not a signal).
  - `roomDissolved$: Observable<{ roomId: string }>` — transient, triggers navigate-to-home.
  - Methods: `joinRoom(id)`, `leaveRoom(id)`, `joinSpectatorGroup(id)`, `makeMove(id, row, col)`, `sendChat(id, content, channel)`, `urge(id)`. All return `Promise<void>` so callers can `await` and catch server-side rejections (surfaced as `HubException` with the domain message).
- **Reconnection strategy** — `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])`. On the library's `onreconnecting` → set `connectionStatus='reconnecting'`; on `onreconnected` → re-`joinRoom` (and `joinSpectatorGroup` if the user is a spectator) + re-fetch `GET /api/rooms/{id}` as the canonical rehydration source, since the server does not push a snapshot on reconnect. On `onclose` after exhausted reconnects → `connectionStatus='disconnected'` + a banner in `RoomPage` with a manual Retry button.
- **`Board` component** — `src/app/pages/rooms/room-page/board/board.ts`. 15×15 CSS grid, each cell a `<button>` whose state is `'Empty' | 'Black' | 'White'`. Click calls `hub.makeMove(roomId, row, col)` — optimistic UI is **off**; the board re-renders off the `MoveMade` event / authoritative `state()`. Cells disabled unless `state().status === 'Playing'` AND `state().game.currentTurn === mySide()` AND cell is empty. Last move is highlighted via a CSS class. Spectators see a non-interactive board (all cells `disabled` with `aria-disabled`).
- **`RoomSidebar` + room info** — room name / host / black and white seats / observer count / status badge / turn countdown / resign + leave buttons (both only shown if the user is a player). Countdown derives from `state().game.turnStartedAt + state().game.turnTimeoutSeconds` minus a 1 Hz "now" Signal.
- **`ChatPanel`** — two CDK-based tabs (`Room` and `Spectator`). Players see only the Room tab; spectators see both. Each tab renders the subset of `state().chatMessages` matching its channel + any appended via the `ChatMessage` event. Submit button sends via `hub.sendChat` with the active channel. 500-char client-side limit matches the server. Error states (e.g. 403 spectator-channel-as-player) surface as a transient banner above the input.
- **Urge button + toast** — player-only button (disabled on the player's own turn per `NotOpponentsTurnException` rule; also disabled for 30 s after a successful urge to mirror the server cooldown). Urge recipient sees a translated toast at the top of the page for ~4 s via a page-local `urgeToast` Signal driven by `hub.urged$`.
- **Game-ended dialog** — on `GameEnded` event, open a CDK `Dialog` with `{ result, winnerUserId, endReason }` → renders translated "You won / You lost / Draw" + reason ("Five in a row" / "Resigned" / "Timed out") + "Back to lobby" primary button that navigates to `/home`. The board remains behind the dialog showing the final position. Close (X) button dismisses the dialog but leaves the user on the room page as a read-only viewer.
- **Resign flow** — player-only button with a confirmation CDK dialog ("Resign? You'll lose this game."). Confirm → `POST /api/rooms/{id}/resign` via the existing `RoomsApiService` (one new method). The `GameEnded` broadcast lands on everyone including the resigner, which is what opens the game-ended dialog.
- **Leave room flow** — player-only button (when game is `Waiting` or `Playing`) → `POST /api/rooms/{id}/leave` via `RoomsApiService.leave()` (already exists) → on success navigate to `/home`. Navigating away without clicking Leave keeps the player in the room server-side (intentional — domain contract).
- **Initial hydration** — `RoomPage.ngOnInit` reads `:id` from route, calls `rooms.getById(id)` to populate state immediately, then calls `hub.joinRoom(id)` (also `joinSpectatorGroup(id)` if the REST snapshot shows we're a spectator). Subsequent `RoomState` events overwrite the snapshot atomically. No REST polling.
- **`RoomState` DTO gets fully typed** — the scaffold left `state.game`, `state.chatMessages`, nested `MoveDto` as `unknown`. This change pins them (backend source was verified during the lobby change). Fields added to `src/app/core/api/models/room.model.ts`: `GameSnapshot`, `MoveDto`, `ChatMessageDto`, enum string-literal unions for `Stone`, `GameResult`, `GameEndReason`, `ChatChannel`.
- **i18n** — a new `game.*` tree in both `public/i18n/en.json` and `public/i18n/zh-CN.json`: board labels (cell aria-labels `Row N Column M`, last-move highlight), turn indicator, countdown, chat send / channel labels / error messages, urge button + toast, resign confirmation, game-ended dialog (three outcomes × three reasons), reconnecting banner, disconnected banner + retry. Parity check continues at zero drift.
- **REST change**: `RoomsApiService.resign(roomId): Observable<GameEndedDto>` added. No backend changes.
- **Tests** (Vitest):
  - `GameHubService`: state-machine — connect → join → state arrives → signal populated; `MoveMade` + in-session `RoomState` both update `state()`; `GameEnded` populates `gameEnded()`; reconnect path re-invokes `joinRoom` + refetches state. Uses a stub `HubConnection` exposing `.on` / `.invoke` that tests drive.
  - `Board`: cells disabled unless it's my turn; click dispatches `hub.makeMove` with the right coords; spectator view is read-only; last-move highlight lands on the right cell.
  - `ChatPanel`: player sees only Room tab; spectator sees both; submit calls `hub.sendChat` with the active channel; 500-char limit is enforced client-side.
  - Resign: confirmation dialog gates the REST call.
  - Game-ended dialog: opens when `gameEnded()` turns non-null; clicking "Back to lobby" navigates to `/home`.

Out of scope:
- Sound effects on move (mentioned as "可后续做" in the brief — explicit non-goal).
- Move history scrollback UI / game replay — `add-web-replay-and-profile`'s concern.
- In-game emoji reactions or quick-text chat — future UX polish.
- Mobile gesture support for the board (pinch-zoom, pan) — later if user testing asks.
- Prerendering a read-only "finished game" view via deep link — also replay's job.
- Lobby-wide SignalR hub — still no backend channel for room-list deltas; lobby polling stays.

## Capabilities

### New Capabilities
- `web-game-board`: the `/rooms/:id` page contract — hub client shape (`GameHubService`), lazy connect policy, reconnection + rehydration protocol, board rendering + click rules, player vs spectator role derivation, chat (two-channel), urge button + cooldown, resign confirmation flow, game-ended dialog, leave-room flow, i18n `game.*` key structure, error handling for `HubException` / 409 concurrent-move / 429 urge-too-frequent.

### Modified Capabilities
- `web-lobby`: the "临时占位组件 `/rooms/:id`" requirement is **removed** — `add-web-game-board` owns that route now. The separate lobby requirement about click-to-join navigating to `/rooms/:id` is unchanged (lobby still drives the navigation; only the destination component flips).

## Impact

- **New folders / files**:
  - `src/app/core/realtime/` — `game-hub.service.ts` + spec.
  - `src/app/pages/rooms/room-page/` — `room-page.ts`, `room-page.html`, `board/board.ts`+`.html`, `sidebar/sidebar.ts`+`.html`, `chat/chat-panel.ts`+`.html`, `dialogs/game-ended-dialog.ts`+`.html`, `dialogs/resign-confirm-dialog.ts`+`.html`, plus specs.
  - `src/app/core/api/models/` — expand `room.model.ts` with full DTO types.
- **New runtime dep**: `@microsoft/signalr`. (Already alluded to in CLAUDE.md's web tech stack; not yet installed.)
- **Deleted**:
  - `src/app/pages/rooms/room-placeholder/room-placeholder.ts` + `.html` + `.spec.ts`.
- **Modified files**:
  - `src/app/app.routes.ts` — `/rooms/:id` `loadComponent` now points at `RoomPage`.
  - `src/app/app.config.ts` — provide `GameHubService` + default impl.
  - `src/app/core/api/rooms-api.service.ts` — add `resign(roomId): Observable<GameEndedDto>`.
  - `src/app/core/api/models/room.model.ts` — concretise `RoomState.game` / `.chatMessages` / nested DTOs; add `Stone`, `GameResult`, `GameEndReason`, `ChatChannel` string-literal unions; add `GameEndedDto`, `UrgeDto`, `MoveDto`, `GameSnapshot`, `ChatMessage`.
  - `public/i18n/en.json` + `public/i18n/zh-CN.json` — append `game.*` keys.
- **Bundle size note**: `@microsoft/signalr` is ~30 KB gzipped. It ships in the `/rooms/:id` lazy chunk (not the eager lobby chunk) — the home page keeps its current size. Anticipated chunk: ~40–50 KB gzip for the room page incl. SignalR client; well under the 200 KB lazy-chunk ceiling.
- **Backend impact**: none — hub and resign endpoint are already live.
- **Breaking change to `web-lobby`**: placeholder requirement removed. No migration needed since the route path is unchanged and `add-web-lobby` never claimed the placeholder was permanent (it was explicitly marked "throwaway" in its proposal).
- **Enables next change** (`add-web-replay-and-profile`) — the `GameSnapshot` / `MoveDto` types are reused by the replay player; the `Board` component can be reused in a read-only mode for replay too.
