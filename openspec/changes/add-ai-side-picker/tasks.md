## 1. Domain — Room.SwapPlayers

- [ ] 1.1 Add `Room.SwapPlayers(DateTime now)` method in `backend/src/Gomoku.Domain/Rooms/Room.cs`. Asserts `Status == RoomStatus.Playing` AND `Game!.Moves.Count == 0`; throws `InvalidOperationException` otherwise. Swaps `BlackPlayerId` and `WhitePlayerId`. Doesn't touch `HostUserId` or `Game`.
- [ ] 1.2 Add unit tests in `Gomoku.Domain.Tests/Rooms/RoomTests.cs` (or wherever existing Room tests live):
  - Happy path: status=Playing, no moves → swap succeeds, ids exchanged, host unchanged, currentTurn unchanged.
  - Reject with moves recorded.
  - Reject with status=Waiting.
  - Reject with status=Finished.

## 2. Application — CreateAiRoomCommand + handler + validator

- [ ] 2.1 Update `CreateAiRoomCommand` record in `backend/src/Gomoku.Application/Features/Rooms/CreateAiRoom/CreateAiRoomCommand.cs` to add `Stone HumanSide` (positionally last).
- [ ] 2.2 Update `CreateAiRoomCommandHandler.Handle()` — after `room.JoinAsPlayer(bot.Id, now)`, branch:  if `request.HumanSide == Stone.White`, call `room.SwapPlayers(now)`. Otherwise no-op.
- [ ] 2.3 Update `CreateAiRoomCommandValidator` — `RuleFor(x => x.HumanSide).Must(s => s == Stone.Black || s == Stone.White).WithMessage("HumanSide must be Black or White.")`.
- [ ] 2.4 Update existing handler tests to pass `Stone.Black` for the new arg (default behaviour preserved).
- [ ] 2.5 Add a new handler test: `HumanSide = Stone.White` → resulting room has `BlackPlayerId == botId`, `WhitePlayerId == hostId`, `HostUserId == hostId`, `Game.CurrentTurn == Black` (bot's turn).
- [ ] 2.6 Add validator test: `Stone.Empty` rejected.

## 3. Api — CreateAiRoomRequest

- [ ] 3.1 Update `CreateAiRoomRequest` record in `backend/src/Gomoku.Api/Controllers/RoomsController.cs` to add `Stone? HumanSide` (nullable, optional in JSON).
- [ ] 3.2 Update the controller action: send `CreateAiRoomCommand(host, body.Name, body.Difficulty, body.HumanSide ?? Stone.Black)`.
- [ ] 3.3 If there's an existing API integration test (or smoke / e2e), add coverage for the `humanSide: "White"` branch returning a swapped room. (Project may not have one yet — file as a follow-up if absent.)

## 4. Web — RoomsApiService.createAiRoom

- [ ] 4.1 Add `BotSide` literal type alias in `frontend-web/src/app/core/api/models/room.model.ts`: `export type BotSide = 'Black' | 'White';`.
- [ ] 4.2 Update `RoomsApiService.createAiRoom` signature (abstract + `DefaultRoomsApiService`) to take optional `humanSide?: BotSide`. Implementation: build POST body conditionally — without `humanSide` field when arg is `undefined`, with field when defined.
- [ ] 4.3 Update `rooms-api.service.spec.ts` — split the existing `createAiRoom` test into two: 2-arg call body shape (existing assertion) + 3-arg call body shape (new humanSide field).

## 5. Web — CreateAiRoomDialog

- [ ] 5.1 Update `create-ai-room-dialog.ts` — form gains a `humanSide` `FormControl<BotSide>` defaulting to `'Black'`. Add `protected readonly sides: readonly BotSide[] = ['Black', 'White']` and `pickSide(s: BotSide)` method.
- [ ] 5.2 Update `create-ai-room-dialog.html` — copy the difficulty button-group block, rename to side group: legend `lobby.ai-game.side-label`, two buttons `Black` / `White` with `aria-checked` based on `form.controls.humanSide.value === side`.
- [ ] 5.3 Submit handler — pass `humanSide` as the third arg to `rooms.createAiRoom(...)`.
- [ ] 5.4 Spec updates in `create-ai-room-dialog.spec.ts`:
  - default side is Black,
  - clicking White then submit calls the service with `'White'` third arg,
  - existing tests pass `'Black'` as the third arg in their expected calls (or keep using the 2-arg form via the optional param — pick one and be consistent).

## 6. i18n

- [ ] 6.1 Add `lobby.ai-game.{side-label, side-black, side-white}` to `public/i18n/en.json` (English: "Your side", "Black", "White").
- [ ] 6.2 Mirror in `zh-CN.json` (执黑 / 执白 or 黑方 / 白方; pick whichever reads tightest).
- [ ] 6.3 Run parity flattener; confirm zero drift.

## 7. Final verification

- [ ] 7.1 Backend: `dotnet build` + `dotnet test` green.
- [ ] 7.2 Web: `npm run lint` + `npm run test:ci` + `npm run build` green.
- [ ] 7.3 `openspec validate add-ai-side-picker` clean.
- [ ] 7.4 Sanity-test by hand: open the AI dialog, pick White + Hard + a name, submit, land in `/rooms/:id` with bot showing as Black and AI's first move arriving via SignalR.
