## Context

Live `RoomPage` already handles AI opponents transparently — once a room exists with an AI seat, the bot polls the move queue and plays via a server-side worker. The web client receives normal `MoveMade` SignalR events and doesn't care whether the opposing side is human or bot. So the entire delta for "support AI play" is at the *room creation* step.

Mirrors the existing `CreateRoomCommand` web flow almost verbatim:
- `ActiveRoomsCard` has a "Create room" button → opens `CreateRoomDialog` → posts `POST /api/rooms` → navigates `/rooms/:id`.
- We add a sibling: `AiGameCard` "New AI game" button → opens `CreateAiRoomDialog` → posts `POST /api/rooms/ai` → navigates `/rooms/:id`.

The only new variable is the difficulty selector. Three choices (`Easy / Medium / Hard`), default `Medium`. Three radio-style buttons in the dialog body, with `aria-pressed` toggling.

## Goals / Non-Goals

**Goals:**

- Surface AI play as a first-class lobby option.
- Reuse existing dialog / form / error-mapping conventions verbatim — no new patterns.
- Touch zero non-lobby code.

**Non-Goals:**

- Side picker (Black/White) — backend seats human as Black; carry over.
- "Replay vs AI from this position" — no.
- AI difficulty preview / sample game — no.
- Lobby "vs Bot" win-rate column — no current backend endpoint exposes per-difficulty stats; defer.
- Header-bar "quick AI" shortcut — keep entry in lobby card for v1.

## Decisions

### D1. Card lives next to find-player

**Decision:** Slot `AiGameCard` in the lobby's right column, between `MyActiveRoomsCard` and `FindPlayerCard`.

**Rationale:** The right column is the "what should I do" column — my games / find a person / play AI. Active-rooms (left column) lists *open human rooms*, which is a different concern.

### D2. Difficulty selector as `aria-pressed` button group, default Medium

**Decision:** Three `<button type="button" role="radio" aria-checked>` inside a `role="radiogroup"`. Active difficulty has `bg-primary text-bg`; inactive has neutral border. Reactive form holds the value.

**Rationale:** Three options is the right shape for a button group (vs a select, which adds a click + reading order). Matches Material design conventions for short discrete choices.

**Alternatives considered:**

- `<select>` element. Rejected — extra interaction, harder to theme with our token utilities.

### D3. Dialog returns the full `RoomState`, caller navigates

**Decision:** `CreateAiRoomDialog` closes with the `RoomState` it received from the server (or `undefined` on cancel). The `AiGameCard` subscribes to `closed` and navigates `/rooms/:id` only on a non-undefined result. Mirrors `CreateRoomDialog` (which closes with `RoomSummary`) — except this dialog returns a full state so we don't need a follow-up `getById` round trip.

**Rationale:**

- Backend's `POST /api/rooms/ai` returns `RoomStateDto` (per archived spec) — already the data we need.
- No need for a `Router` in the dialog itself; navigation is the card's concern.

### D4. Difficulty default = Medium

**Decision:** Default selection is Medium. Easy is "random legal moves, no strategy" (per `BotDifficulty.Easy` doc) and is too easy to be the welcome experience; Hard is the minimax search and may scare new users.

**Rationale:** Goldilocks. Backend's enum doc explicitly says Easy is "no strategy" — that's a tutorial-mode difficulty, not a default.

### D5. Empty-difficulty form state is invalid

**Decision:** The form requires a difficulty value (`Validators.required` on the FormControl). Since the default value is Medium and the radiogroup buttons each set the value on click, empty is unreachable in normal use — but defensive against keyboard tabbing oddities.

## Risks / Trade-offs

- **Risk: room name collisions when many users create AI rooms with same default name.** → Backend doesn't enforce unique room names; this is fine. UI doesn't suggest a default name (user types one).
- **Risk: 400 ProblemDetails for invalid difficulty (e.g. someone bypassing UI).** → Mitigation: TS string-literal union prevents typos at compile time; if it still happens, the dialog's error path shows `lobby.ai-game.errors.generic`.
- **Risk: AI bot's first move takes 800 ms (`Ai:MinThinkTimeMs`).** → Accepted; this is a backend feature so the user feels like the bot is "thinking". RoomPage already renders the turn indicator, so the experience is fine.
- **Trade-off: no side picker.** → Accepted. Adding it requires a backend command parameter that doesn't exist; defer to a follow-up if user testing demands it.

## Migration Plan

- Net-additive change — one card slot, one dialog file, one service method.
- No existing route, component, or i18n key is touched (new ones added in `lobby.ai-game.*`).
- Rollback = revert; no backend dependency.

## Open Questions

- **Q1: Should the AI room appear in `ActiveRoomsCard` like a normal Waiting/Playing room?** Yes — it's a regular room from the backend's perspective; `RoomsController.List` returns it. Other users can spectate it. No change needed.
- **Q2: Win-rate vs AI on profile / leaderboard?** Out of scope; backend doesn't separate human-vs-human and human-vs-bot stats.
