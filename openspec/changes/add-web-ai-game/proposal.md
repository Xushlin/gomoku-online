## Why

The web app has 0 entry points for "play against the AI" despite the backend shipping `POST /api/rooms/ai` (with `Easy / Medium / Hard` difficulty) months ago via `add-ai-opponent` + `add-ai-opponent-hard`. Today the only way for a user to try the AI is curl. New users hit the lobby, see "Active rooms" empty (because nobody is online), and bounce — there's no way to *just play* without finding a human opponent.

Gap is small and obvious: a lobby card with "New AI game" + a difficulty picker, opening a dialog that posts to `POST /api/rooms/ai` and then navigates to `/rooms/:id`. From there the live `RoomPage` already handles bot-controlled white-side moves (the bot polls and plays via a server-side worker, so the existing client doesn't need to know it's an AI).

## What Changes

- **`RoomsApiService.createAiRoom(name, difficulty)`** — new method on the existing service. `POST /api/rooms/ai` with body `{ name, difficulty: 'Easy' | 'Medium' | 'Hard' }`. Returns the full `RoomState` (same shape as `getById`), so the navigation reuses the room id immediately.
- **`BotDifficulty` string-literal union** in `room.model.ts`: `'Easy' | 'Medium' | 'Hard'` — mirrors the backend's `JsonStringEnumConverter` output.
- **New lobby card "Play vs AI"** at `src/app/pages/lobby/cards/ai-game/`. Single primary button "New AI game" → opens a CDK Dialog. Card is purely an entry point — no rich content, no polling, no list. Slot it next to the find-player card on the right column.
- **`CreateAiRoomDialog`** at `src/app/pages/lobby/dialogs/create-ai-room-dialog/`. Mirrors `CreateRoomDialog` (same room-name input rules: required, min 3, max 50, non-whitespace) plus a difficulty selector (three radio-style buttons: Easy / Medium / Hard) defaulting to Medium. On submit → `rooms.createAiRoom(name, difficulty)` → on success close dialog with the new RoomState → caller (the AiGameCard) navigates to `/rooms/:id`. Failure handling identical to `CreateRoomDialog` (ProblemDetails 400 → field error, network → generic banner).
- **`AiGameCard`** at `src/app/pages/lobby/cards/ai-game/` — opens the dialog on click; on resolved `RoomState`, navigates `/rooms/:id`. Mirrors how `ActiveRoomsCard.openCreateDialog` works for human rooms, just with a different dialog + an automatic navigate.
- **i18n** — new `lobby.ai-game.*` subtree (title, button, dialog title, name label/placeholder, difficulty labels for `easy`/`medium`/`hard` + `difficulty-label`, submit/cancel/loading copy, generic + network errors). Both `en.json` and `zh-CN.json`, parity zero drift.
- **Tests** (Vitest):
  - `rooms-api.service.spec.ts` — `createAiRoom('Hard match', 'Hard')` → POST to `/api/rooms/ai` with body `{ name: 'Hard match', difficulty: 'Hard' }`.
  - `pages/lobby/dialogs/create-ai-room-dialog/create-ai-room-dialog.spec.ts` — submit with valid input calls `createAiRoom` + closes with the room; bad name blocks submit; difficulty selection switches the outgoing arg; 400 maps to field error; default difficulty = Medium.
  - `pages/lobby/cards/ai-game/ai-game.spec.ts` — opening dialog is a no-op until the dialog closes; on resolved `RoomState`, navigates `/rooms/:roomId`; cancel (`undefined` close) does NOT navigate.

Out of scope:
- Showing AI opponent's "thinking" indicator — the live `RoomPage` already shows whose turn it is via `currentTurn`; the bot's move arrives as a normal `MoveMade` event after its server-side think delay (configurable via `Ai:MinThinkTimeMs`). No client change needed.
- Picking which side (Black/White) the human plays — backend always seats the caller as Host=Black, AI as White (matches `add-ai-opponent` spec). Adding a side picker is a separate concern.
- A menu of "common AI room names" / quick presets — keep parity with the human Create Room dialog (just enter a name).
- Showing the user's win-rate vs each difficulty on the card — interesting but needs a backend endpoint we don't have.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `web-lobby`: adds the `ai-game` card to the dashboard and the create-AI-room dialog; the existing card-grid + polling cadence + theming rules carry over unchanged.

## Impact

- **New folders / files**:
  - `src/app/pages/lobby/cards/ai-game/ai-game.{ts,html}` + spec
  - `src/app/pages/lobby/dialogs/create-ai-room-dialog/create-ai-room-dialog.{ts,html}` + spec
- **Modified files**:
  - `src/app/core/api/rooms-api.service.ts` — `createAiRoom(name, difficulty): Observable<RoomState>` added on the abstract + default impl.
  - `src/app/core/api/models/room.model.ts` — add `BotDifficulty` string-literal union.
  - `src/app/pages/lobby/lobby.{ts,html}` — import + slot `<app-ai-game-card />` in the right column.
  - `public/i18n/{en,zh-CN}.json` — `lobby.ai-game.*` subtree.
- **Backend impact**: none — `POST /api/rooms/ai` is already live.
- **Bundle**: tiny — one card + one dialog. Lobby chunk grows by ≈3 KB gzip.
- **Enables**: a follow-up "AI opponent stats / leaderboard against bots" idea later if there's user demand.
