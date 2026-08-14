## 1. Domain — dimension-agnostic board

- [x] 1.1 `Position`: drop the `[0..14]` upper bound and the `BoardSize` / `MaxIndex` constants; keep the negative-coordinate guard throwing `InvalidMoveException`.
- [x] 1.2 `Board`: take `(rows, cols, winLength)` in the constructor; expose them as properties; replace the `Size` / `WinLength` constants. `IndexOf` MUST use `Cols`, not a square assumption.
- [x] 1.3 `Board.Clone()` preserves dimensions.
- [x] 1.4 `Board` bounds-checks on `GetStone` / `PlaceStone` against its own dimensions.

## 2. Domain — rules

- [x] 2.1 `Gewu.Domain/Games/Abstractions/IGameRules.cs` — `GameKey`, `Rows`, `Cols`, `WinLength`, `CreateBoard()`, `IsInBounds(Position)`. Doc comment states implementations must be stateless because instances are shared across concurrent rooms.
- [x] 2.2 `Gewu.Domain/Games/Abstractions/IGameRulesRegistry.cs` — `For(gameKey)` returning `null` for unknown keys, mirroring `IPuzzleRulesRegistry`.
- [x] 2.3 `Gewu.Domain/Games/NInARow/NInARowRules.cs` — parameterised implementation; reject non-positive dimensions and a `winLength` larger than `max(rows, cols)`.
- [x] 2.4 `GomokuRules` as a named registration of `NInARowRules("gomoku", 15, 15, 5)` — a constant, not a subclass.

## 3. Domain — Room and Game

- [x] 3.1 `Room.GameKey` (string, non-empty), set by `Room.Create`.
- [x] 3.2 `Room.PlayMove(userId, position, now, rules)` — bounds-check via `rules.IsInBounds` before touching the board; build the board through `Game.ReplayBoard(rules)`.
- [x] 3.3 `Game.ReplayBoard(IGameRules rules)`.
- [x] 3.4 Confirm no other `Room` / `Game` method needs the rules — resign, timeout, urge, chat, spectate are all game-agnostic.

## 4. Application

- [x] 4.1 Resolve rules from `room.GameKey` in the four handlers that play or replay: `MakeMove`, `ExecuteBotMove`, `GetRoomState`(replay for board reconstruction, if it does), `GetGameReplay`. Grep for `ReplayBoard` and `PlayMove` to find them all.
- [x] 4.2 `CreateRoom` / `CreateAiRoom` set `GameKey`. The "reject an unknown key" half is **not implemented and not needed yet** — see note 8.11.
- [x] 4.3 No DTO changes. Verify by diff that `Common/DTOs/` is untouched.

## 5. Infrastructure

- [x] 5.1 `GameRulesRegistry` over DI-registered `IGameRules`; register gomoku.
- [x] 5.2 `RoomConfiguration`: map `GameKey`, required, max length 64.
- [x] 5.3 One migration `AddRoomGameKey` — column with default `'gomoku'` plus a backfill for existing rows.
- [x] 5.4 Adapt `GomokuAiFactory` / the AI implementations to the new `Board` constructor. The AI stays gomoku-only.

## 6. Tests

- [x] 6.1 Every existing `Board` / win-detection test still passes, constructing the board with gomoku's dimensions. **No existing assertion weakened** — with one spec-mandated exception, the `Position` bound tests; see note 8.3.
- [x] 6.2 `NInARowRules` at 3×3×3: three in a row wins horizontally, vertically and on both diagonals; two does not.
- [x] 6.3 The same three-in-a-row returns `Ongoing` on a 15×15×5 board — the win length really is per-game.
- [x] 6.4 Non-square board: `(3, 5, 3)` accepts `(2, 4)` and rejects `(3, 0)`; proves `IndexOf` uses columns.
- [x] 6.5 `NInARowRules` constructor rejects non-positive dimensions and an unwinnable `winLength`.
- [x] 6.6 Registry resolves `gomoku` and returns `null` for an unknown key.
- [x] 6.7 `Position` rejects negatives and now accepts 15 — the bound moved, so assert both halves.
- [x] 6.8 `Room.PlayMove` rejects an out-of-bounds position with `InvalidMoveException` and appends no move.

## 7. Verification

- [x] 7.1 `dotnet build Gewu.slnx` and `dotnet test Gewu.slnx` green.
- [x] 7.2 Delete `gewu.db`, boot: migrations apply, both seeders run, a room can be created and played.
- [x] 7.3 Run the `AiSmoke` end-to-end tool — it plays real moves through the real hub, so it is the proof the wire contract did not move. (Note: it has a known pre-existing crash on its final leaderboard step; steps 1–6 are what matter here.)
- [x] 7.4 Confirm the wire contract is untouched: `git diff` shows no change under `Gewu.Api/Controllers`, `Gewu.Api/Hubs`, `Common/DTOs`, or `frontend-web/`.
- [x] 7.5 Grep the move path for `if (gameKey ==` style branching — there must be none.

## 8. Notes from implementation

- [x] 8.1 **The migration EF generated was wrong and would have broken existing data.** `AddColumn` defaulted `GameKey` to `""`, which resolves to no rules, which is a 404 — every room created before the migration would have become unplayable. Corrected to default `'gomoku'` and to run an explicit `UPDATE Rooms SET GameKey = 'gomoku'` backfill rather than trusting the default to reach existing rows.
- [x] 8.2 **One status code moved, and the proposal originally claimed it had not.** `MakeMoveCommandValidator` enforced `[0..14]`, but it runs before the room — and therefore the game — is known, so it can only keep the game-independent half (non-negative). An out-of-range coordinate is now 409 instead of 400. Caught while wiring the validator, then written into the proposal, a new spec requirement, and the validator's own doc comment instead of being left as a silent change. Negatives stay 400; the web client cannot reach the path because it only renders cells that exist.
- [x] 8.3 The eight failing `Position` tests were the old upper-bound assertions — the exact behaviour the `gomoku-domain` delta moves. Rewritten to assert both halves of the new split: `Position` accepts `(15, 0)`, and `BuiltInGameRules.Gomoku.IsInBounds` rejects it. This is the only place existing assertions changed, and it changed because the spec says so.
- [x] 8.4 `ExecuteBotMoveCommandHandler` held a hand-copied replay loop whose own comment said "same logic as `ReplayBoard`". Two copies of a rule are two things that drift, so it now calls `room.Game.ReplayBoard(rules)`.
- [x] 8.5 The AI now reads `board.Rows` / `board.Cols` / `board.CellCount` instead of `Position.BoardSize` / `MaxIndex`. It is still registered for gomoku only, but it is no longer *hardcoded* to 15×15 — a bonus, not a goal.
- [x] 8.6 Test helpers rather than literals: `GomokuBoards.New()` (Domain) and `GomokuRules.Registry` (Application) both delegate to the real `BuiltInGameRules.Gomoku`. Writing `new Board(15, 15, 5)` in tests would mean the rules could change without a single test noticing.
- [x] 8.7 Backend tests 533 → 561 (+28: 18 `NInARowRules`, 5 registry, 5 rewritten bound tests). The other 533 are the regression suite that proves gomoku still behaves identically.
- [x] 8.8 Verified end to end with `AiSmoke`: a real client registered, created an AI room, connected to the hub, played moves, received bot replies, and got `GameEnded result=BlackWin`. That is the proof the wire contract held. Its final leaderboard step still crashes on the known pre-existing paged-response bug, which is unrelated and already filed.
- [x] 8.9 Fresh boot verified: 9 migrations, `AddRoomGameKey` last, `Rooms.GameKey` present, both seeders running.
- [x] 8.10 Confirmed by `git status` that nothing under `Gewu.Api/Controllers`, `Gewu.Api/Hubs`, `Common/DTOs` or `frontend-web/` changed, and by grep that the move path contains no `gameKey == "..."` branching.
- [x] 8.11 Task 4.2 asked for creation to reject an unregistered game key. It is not implemented, because neither creation path accepts a caller-supplied key — both write `GameKeys.Gomoku`, so an unknown key cannot arise there. Writing a guard against an impossible input would be untestable code pretending to be a safeguard. The spec requirement was rescoped to say so, and the check moves to `add-tictactoe`, where a caller first gets to choose. What *is* implemented is the reachable case: if a room's stored `GameKey` does not resolve — hand-edited data, or a downgraded build — the move handler returns 404 rather than throwing.
