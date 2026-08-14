# Tasks — add-tictactoe

> Ordering rule: every step below is *backend only*. The catalogue manifest stays
> `status: 'planned'` — flipping it belongs to `add-web-tictactoe`, and flipping it
> here would advertise a game with no page to open.

> Audit rule: this change exists to measure what a second board game costs. Any file
> outside `Games/`, `Ai/` and the four handlers listed below that turns out to need
> editing is **registry debt** and MUST be recorded in §7, not fixed silently.

## 1. Rules — the part that should be one line

- [x] 1.1 `IGameRules` gains `bool IsRated { get; }`. XML doc names `add-per-game-rating` as the change that deletes it.
- [x] 1.2 `NInARowRules` ctor gains `bool isRated = true`; existing gomoku registration unchanged (defaults to `true`).
- [x] 1.3 `BuiltInGameRules.TicTacToe = new NInARowRules("tictactoe", 3, 3, 3, isRated: false)`.
- [x] 1.4 Register it in `Gewu.Infrastructure/DependencyInjection.cs` (one `AddSingleton<IGameRules>` line).
- [x] 1.5 `GameKeys.TicTacToe = "tictactoe"`.
- [x] 1.6 Tests: registry resolves `"tictactoe"` with `3/3/3`, `IsRated == false`; gomoku still `15/15/5`, `IsRated == true`; win detection on 3×3 for row / column / both diagonals; a full 3×3 board with no line is a draw.

## 2. Room creation learns a game key

- [x] 2.1 `Room.Create(..., string gameKey)` — non-empty guard, sets `GameKey`. **Already done by `add-game-rules-registry`** — cost zero here.
- [x] 2.2 `CreateRoomCommand(HostUserId, Name, GameKey)` — `GameKey` required, non-nullable.
- [x] 2.3 `CreateAiRoomCommand(..., string GameKey)` — same.
- [x] 2.4 Both handlers pass it through instead of hard-coding `GameKeys.Gomoku`.
- [x] 2.5 Both validators inject `IGameRulesRegistry` and reject unresolvable keys → 400. **No inline whitelist** — two lists of game keys will disagree eventually and nobody will notice the day they do.
- [x] 2.6 `CreateRoomRequest` / `CreateAiRoomRequest` gain optional `gameKey`, defaulting to `"gomoku"` **in the controller only**.
- [x] 2.7 Tests: valid key accepted; unknown key → validation failure; absent key in the request body → `"gomoku"`; `Room.Create` with blank key throws.

## 3. Lobby filters by game

- [x] 3.1 `GetRoomListQuery(string GameKey)` — required.
- [x] 3.2 Handler filters on `Room.GameKey`. Check whether the repository query needs the predicate pushed down to EF rather than filtering in memory; if the current shape loads all active rooms, push it down.
- [x] 3.3 `GET /api/rooms?gameKey=` — optional query param, defaults to `"gomoku"`.
- [x] 3.4 Confirm `GetMyActiveRoomsQuery` is **not** filtered, and add a test that pins that on purpose (a future reader will otherwise "fix" the inconsistency).
- [x] 3.5 Tests: mixed-game DB returns only the requested game; unknown key returns empty list + 200, not an error; no query string → gomoku only.

## 4. AI registry

- [x] 4.1 Rename `IGomokuAi` → `IBoardGameAi` (file + all references). Mechanical.
- [x] 4.2 Fix the "board is full" check to use `board.Rows * board.Cols` rather than the literal 225. **Code was already correct** (`EasyAi` uses `board.CellCount`); only the interface's doc comment claimed 225. Fixed the comment.
- [x] 4.3 `IGameAiFactory { GameKey; Create(difficulty, random) }` + `IGameAiRegistry { For(gameKey) }` in `Domain/Ai`; `GameAiRegistry` implementation in `Infrastructure/Games` next to `GameRulesRegistry`.
- [x] 4.4 `GomokuAiFactory` static class → `IGameAiFactory` instance. Branches unchanged.
- [x] 4.5 `ExecuteBotMoveCommandHandler` resolves the factory via `IGameAiRegistry.For(room.GameKey)`; unresolvable → 404, matching the rules-resolution path.
- [x] 4.6 Register both factories in DI.
- [x] 4.7 Tests: registry resolves both keys, returns `null` for unknown; each difficulty yields the right runtime type; `ExecuteBotMove` against a room with an unknown game key returns 404 without an unhandled exception.

## 5. Tic-tac-toe AI

- [x] 5.1 `TicTacToeAiFactory : IGameAiFactory` — `Easy` returns `EasyAi` (**reused unchanged**), `Medium` / `Hard` return the new classes.
- [x] 5.2 `TicTacToeMediumAi` — win → block → centre → corner → random, `Random` injected for tie-breaks.
- [x] 5.3 `TicTacToeHardAi` — exhaustive minimax, no depth limit, no evaluation function, no heuristic candidate generation.
- [x] 5.4 Tests for Medium: takes the win when both a win and a block exist; blocks when there is no win; opens at centre.
- [x] 5.5 **Property test for Hard.** Reformulated during implementation: "never loses from any legal position" is **false** — a position can be lost before Hard moves (double-threat reachable only via opponent blunders). The test now asserts Hard lands exactly on each position's game-theoretic value, checked against an independently written negamax. Spec corrected to match. Original wording: Walk the entire game tree with Hard on one side and an exhaustive opponent on the other, both as X and as O; assert the result multiset contains only wins and draws. This is the test the whole change is for — a solved game is the only place a bot's optimality is *provable* rather than merely plausible.
- [x] 5.6 Test: Hard vs Hard is a draw.
- [x] 5.7 Verify `MediumAi.cs` / `HardAi.cs` have no behavioural diff — only the `IGomokuAi` → `IBoardGameAi` rename.

## 6. Unrated games

- [ ] 6.1 `MakeMoveCommandHandler` skips the ELO block when `rules.IsRated == false`. Reuse the `IGameRules` instance it already resolved for the move — do not resolve twice.
- [ ] 6.2 Check `GameEloApplier` and the resign / timeout paths (`ResignCommand`, `TurnTimeoutCommand`) — they end games too, and each is a separate place ELO gets applied. All of them need the same guard. **This is the step most likely to be missed**: the proposal only names `MakeMoveCommandHandler` because that is where the spec's requirement lives, but a tic-tac-toe game that ends by resignation would otherwise still move ratings.
- [ ] 6.3 Tests: an unrated game ending by win / draw / resignation / timeout leaves both users' `Rating`, `Wins`, `Losses`, `GamesPlayed` untouched; the room still reaches `Finished` with an `EndReason`; the replay endpoint still works; `GET /api/leaderboard` is unchanged.

## 7. Registry-debt audit (fill in during implementation)

- [ ] 7.1 List every file edited outside `Domain/Games`, `Domain/Ai`, the four Rooms handlers, the two validators, `RoomsController` and DI registration. For each, state whether it was genuine coupling or incidental.
- [ ] 7.2 Record the actual net line count and compare it against the "one class plus one registration" claim in `game-rules-registry`. If the gap is large, say so plainly in the PR description — that number is the change's main finding and it is worth more than the game.

## 8. Ship

- [ ] 8.1 `dotnet build Gewu.slnx` clean.
- [ ] 8.2 `dotnet test Gewu.slnx` green.
- [ ] 8.3 Manual smoke via HTTP: create a tictactoe AI room on Hard, play it out, confirm the bot never loses and no rating moved.
- [ ] 8.4 `openspec validate add-tictactoe --strict`.
- [ ] 8.5 PR description links this change, states the §7 numbers, and calls out the two deliberate temporary decisions (`IsRated`, the `"gomoku"` HTTP defaults) with their removal conditions.
