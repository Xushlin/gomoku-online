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

- [x] 6.1 The guard went **inside `GameEloApplier.ApplyAsync`**, not into `MakeMoveCommandHandler`. There are three end-of-game paths sharing that helper; guarding at the single exit means a fourth path added later is covered automatically, and no call site can forget. `ApplyAsync` takes `IGameRulesRegistry`; `MakeMove` passes the one it already has.
- [x] 6.2 **Confirmed: three call sites** (`MakeMove:65`, `Resign:51`, `TurnTimeout:50`). This task note was right that it would be missed. `Resign` / `TurnTimeout` did not resolve rules at all and each gained an `IGameRulesRegistry`. Original note: Check `GameEloApplier` and the resign / timeout paths (`ResignCommand`, `TurnTimeoutCommand`) — they end games too, and each is a separate place ELO gets applied. All of them need the same guard. **This is the step most likely to be missed**: the proposal only names `MakeMoveCommandHandler` because that is where the spec's requirement lives, but a tic-tac-toe game that ends by resignation would otherwise still move ratings.
- [x] 6.3 Tests: an unrated game ending by win / draw / resignation / timeout leaves both users' `Rating`, `Wins`, `Losses`, `GamesPlayed` untouched; the room still reaches `Finished` with an `EndReason`; the replay endpoint still works; `GET /api/leaderboard` is unchanged.

## 7. Registry-debt audit — the change's actual output

- [x] 7.1 Files edited outside the predicted scope (`Domain/Games`, `Domain/Ai`, the four Rooms handlers, the two validators, `RoomsController`, DI):

| File | Genuine coupling? | Note |
| --- | --- | --- |
| `Application/Abstractions/IRoomRepository.cs`<br>`Infrastructure/…/RoomRepository.cs` | **Genuine** | The lobby query has to know which game it is listing. Filtering in memory was the alternative and it is worse: the query eager-loads every room's `Game` + all `Moves`, so showing the tic-tac-toe lobby would read every gomoku game's move history. |
| `Features/Rooms/Common/GameEloApplier.cs`<br>`Resign/ResignCommandHandler.cs`<br>`TurnTimeout/TurnTimeoutCommandHandler.cs` | **Genuine, and the most useful find** | A game ends on **three** paths, not one. The proposal named only `MakeMoveCommandHandler` because that is where the spec requirement lives. Guarding there would have left tic-tac-toe rating-affecting on resignation and timeout. Neither handler resolved rules before, so each gained an `IGameRulesRegistry`. |
| `Application/Common/Validation/GameKeyValidation.cs` | Genuine (new file) | One definition of "this game key must be registered", shared by both create paths. Same reason the rule itself must go through the registry: a duplicated list eventually disagrees with the other copy and nobody notices the day it does. |
| `Features/Rooms/GetMyActiveRooms/GetMyActiveRoomsQuery.cs` | No — docs only | Pins a **deliberate** inconsistency: the lobby filters by game, "my active rooms" does not. Documented plus a test, so the next reader does not "fix" it into consistency. |
| `Features/Bots/ExecuteBotMove/ExecuteBotMoveCommand.cs` | No — incidental | One `<see cref>` pointing at the renamed interface. |
| `Api/Controllers/RoomsController.cs` (extra `using`) | No — incidental | Needed `Gewu.Domain.Games.Abstractions` for `GameKeys.Gomoku`. **No new project reference**: `Gewu.Api` still has none to `Gewu.Domain` (it sees it transitively), and the file already imported `Gewu.Domain.Ai` and `Gewu.Domain.Enums`. Layering rule intact. |

- [x] 7.2 Line counts, measured `git diff 2272f9e..HEAD`:

| Area | Lines | Verdict on "one class plus one registration" |
| --- | --- | --- |
| **Rules for the whole game** | **0** | The claim holds *exactly* where it was made. `BuiltInGameRules.TicTacToe` is one field, DI is one line. `Board` / `Position` / `Room` / win detection: **not one character**. 3×3 win detection was already tested before this change — the previous change had written the tests against a locally-constructed `(3,3,3)`. |
| Tic-tac-toe's own AI | 332 | `TicTacToeHardAi` 142, `TicTacToeMediumAi` 85, `TicTacToeBoard` 71, `TicTacToeAiFactory` 34. All of it is the game's *thinking*, none of it its *rules*. |
| Registry debt paid here | ~310 | Room creation ~115, AI registry ~100, unrated ~55, lobby filter ~39. |
| Everything (src + tests + specs) | 2,982 / -160 across 61 files | src 713, **tests 1,556**, specs 713. |

**The finding.** "One class plus one registration" was true of the *rules* and false of everything above them. The debt (~310 lines) cost about as much as the game itself (~332), and every line of it is a place gomoku's assumptions had leaked upward: room creation could not name a game, the lobby could not tell games apart, the AI layer had no key, and the rating pool was implicitly gomoku's. Finding that at 3×3 — where the game contributes *zero* inherent complexity — is exactly what this change was for. 中国象棋 would have paid the same 310 lines with its own difficulty layered on top, and the two would have been indistinguishable.

**Not fixed, recorded as debt:** `GameEndReason.Connected5 = 0` is named after gomoku's win condition and now describes tic-tac-toe's three-in-a-row too. Renaming it is out of scope: it is normative in `room-and-gameplay`, persisted as an int, and reaches the web client. It belongs with the `GomokuHub` → `MatchHub` rename in `generalize-match-contract` — which is now not merely a stale name but actively wrong, since tic-tac-toe rooms route through `/hubs/gomoku`.

**PR size.** 2,982 insertions is far past the 400-line convention, and calling that "fine" would be dishonest. Mitigating facts, not excuses: 76% is tests + specs (1,556 + 713), src is 713; and the two candidate split points both produce a PR with no consumer — the AI alone would ship a bot for a game no room can be created for, and the plumbing alone would ship a creatable game with nothing to play against. The web half is already deferred to `add-web-tictactoe`.

## 8. Ship

- [x] 8.1 `dotnet build Gewu.slnx` clean.
- [x] 8.2 `dotnet test Gewu.slnx` green.
- [x] 8.3 Manual smoke via HTTP against a running server (`:5145`). Moves go over SignalR, not REST, so the room was created with `humanSide: White` to let the `AiMoveWorker` drive the bot's first move — observable through `GET /api/rooms/{id}` with no hub client. Results:
  - `POST /api/rooms {gameKey:"xiangqi"}` → **400** `"'xiangqi' is not a game on this platform."`
  - `POST /api/rooms {name}` with no `gameKey` → room created, appears in the default (gomoku) lobby. Old clients unaffected.
  - `GET /api/rooms?gameKey=tictactoe` → `0`, `GET /api/rooms` → `1`. Lobbies are isolated.
  - `GET /api/rooms?gameKey=xiangqi` → **200** `[]`, not an error.
  - Hard tic-tac-toe bot played **(0,0)** — in bounds and a game-theoretically sound opening. Had it resolved gomoku's AI it would have picked near (7,7) and been rejected by `rules.IsInBounds`.
  - Resign the tic-tac-toe game → `result: BlackWin`, `endReason: Resigned`, replay returns 1 move; `GET /api/users/me` → **rating 1200, gamesPlayed 0** (unchanged).
  - Control: resign a gomoku game → **rating 1180, gamesPlayed 1**. The guard is conditional, not an unconditional early return.
- [x] 8.4 `openspec validate add-tictactoe --strict`.
- [ ] 8.5 PR description links this change, states the §7 numbers, and calls out the two deliberate temporary decisions (`IsRated`, the `"gomoku"` HTTP defaults) with their removal conditions.
