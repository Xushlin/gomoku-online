## Why

The AI room creator always seats the human on Black and the bot on White. Black plays first in gomoku, so the human always opens — every AI game starts the same way. Two reasonable user requests we can't currently support:

1. "Let me play White and watch how the AI opens." Defensive practice; learn the bot's openings.
2. "Let me play Black against AI Hard." Currently this is forced and there's no choice — but UX-wise we should still expose it as a choice so the option of White becomes explicit.

The change is end-to-end: domain (one tiny new method on `Room`), application (one new field on `CreateAiRoomCommand`), Api (one DTO field), web (one form control on the dialog).

## What Changes

### Domain (Gomoku.Domain)

- **`Room.SwapPlayers(DateTime now)`** new public method. Swaps `BlackPlayerId` and `WhitePlayerId`. Valid **only** when `Status == Playing` AND `Game.Moves.Count == 0` (i.e., the game has just started, nobody's moved yet). Otherwise throws `InvalidOperationException("Cannot swap players after the first move.")`. Doesn't touch `HostUserId` (host stays the human regardless of side), doesn't touch `Game.CurrentTurn` (Black still plays first; we just reassign who's on Black).

### Application (Gomoku.Application)

- **`CreateAiRoomCommand`** gains a new field `HumanSide: Stone` (`Stone.Black` or `Stone.White`). The existing 3 fields stay; new field is positionally last. Default at the C# call site is `Stone.Black` for backward compatibility (the API DTO defaulting is documented separately below).
- **`CreateAiRoomCommandHandler`** — after `room.JoinAsPlayer(bot.Id, now)`, if `request.HumanSide == Stone.White`, call `room.SwapPlayers(now)`. Result: `BlackPlayerId == bot.Id`, `WhitePlayerId == host.Id`, host still the human. AI worker poll loop picks up its turn and plays move 1 immediately — no worker-loop changes needed.
- **Validator** — `HumanSide` enum-typed so no invalid string slips through; existing rules unchanged.

### Api (Gomoku.Api)

- **`CreateAiRoomRequest`** record gains `HumanSide: Stone`. JsonStringEnumConverter already in place, so wire format is `"Black" | "White"`. Field is **optional** in the wire JSON: missing/null → backend defaults to `Black` for backward compat with existing clients.

### Web (frontend-web)

- **`RoomsApiService.createAiRoom(name, difficulty, humanSide?)`** — third arg is optional `BotSide` (= the existing `Stone` literal type; alias added for clarity). When provided, the POST body includes `humanSide: 'Black' | 'White'`; when omitted, body is the existing 2-field shape.
- **`CreateAiRoomDialog`** — form gains a `humanSide` control (Black / White button radio group, identical UX to the Easy / Medium / Hard difficulty group), default `Black`.
- **i18n** — `lobby.ai-game.{side-label, side-black, side-white}`. en + zh-CN, parity zero drift.

### Tests

- **Domain**: `Room.SwapPlayers` happy path + invariants (rejects non-Playing, rejects post-first-move).
- **Application**: handler test for `HumanSide = White` produces a room where black=bot and white=host.
- **Api**: request DTO accepts `"humanSide": "White"`; missing field defaults to Black.
- **Web**:
  - `rooms-api.service.spec.ts` — new test covers `createAiRoom('x', 'Medium', 'White')` body shape.
  - `create-ai-room-dialog.spec.ts` — default side = Black; switching to White flows through to the outgoing arg.

Out of scope:
- A "Random side" option — single binary choice for v1 keeps the dialog tidy.
- Changing the host concept (host is still the human; only the seating swaps).
- Side picker for human-vs-human rooms — those have a different semantic ("creator picks black by convention"), would be a separate change.
- Ranked / unranked annotation when playing as White — out of scope here.

## Capabilities

### New Capabilities
(none)

### Modified Capabilities

- `room-and-gameplay` — adds the `Room.SwapPlayers` operation as a new requirement.
- `ai-opponent` — `CreateAiRoomCommand` and the REST contract gain the `HumanSide` field; one existing handler-flow requirement is rewritten to include the optional swap step.
- `web-lobby` — `RoomsApiService.createAiRoom` signature and `CreateAiRoomDialog` gain the side selector; existing requirements modified to match.

## Impact

- **Backend modified files**: `Gomoku.Domain/Rooms/Room.cs`, `Gomoku.Application/Features/Rooms/CreateAiRoom/{Command,Handler,Validator}.cs`, `Gomoku.Api/Controllers/RoomsController.cs` (the `CreateAiRoomRequest` record). Tests under `Gomoku.Domain.Tests` and `Gomoku.Application.Tests`.
- **Web modified files**: `core/api/rooms-api.service.ts`, `core/api/models/room.model.ts` (a `BotSide` alias), `pages/lobby/dialogs/create-ai-room-dialog/{ts,html,spec}`, `public/i18n/{en,zh-CN}.json`. Tests under `rooms-api.service.spec.ts` + dialog spec.
- **Backend EF migration**: none — no schema change; `BlackPlayerId` / `WhitePlayerId` columns already exist and accept any user id.
- **Bundle**: web side adds ~0.5 KB raw.
- **Backwards compatibility**: clients that omit `humanSide` get the existing default (Black). Existing AI rooms in storage are unaffected (we only change the *creation* path).
