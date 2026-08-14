## Why

`add-game-rules-registry` claimed that a new board game is "one class plus one registration". Nothing has tested that claim. The registry has exactly one entry, and an abstraction with one implementation is indistinguishable from a rename.

一字棋 is the cheapest possible falsification test. It is n-in-a-row on a smaller board — `NInARowRules("tictactoe", 3, 3, 3)`, already written down as the target in `game-rules-registry`'s own spec — so **anything** this change has to touch beyond that one line is a place where the registry did not actually decouple the games. Finding those places now, at 3×3, costs far less than finding them while implementing 中国象棋.

There are three such places, and this change pays for all of them:

1. **No room can be created for a non-gomoku game.** `CreateRoomCommand(HostUserId, Name)` has no game key; `CreateRoomCommandHandler:38` and `CreateAiRoomCommandHandler:67` both hard-code `GameKeys.Gomoku`. The `room-and-gameplay` spec already names this as this change's debt: *"等到 `add-tictactoe` 让调用方能选棋种时，那条路径 MUST 先校验键已登记再建房。"*
2. **The lobby cannot tell the games apart.** `GetRoomListQuery` has no parameters and returns every active room, so a 一字棋 room would appear in the 五子棋 lobby and vice versa.
3. **The AI layer is gomoku-shaped.** `MediumAi:19` hard-codes `BoardCenter = 7` (the centre of a 15×15 board) and `HardAi:341` hard-codes `length >= 5`. `GomokuAiFactory` is reachable only as a static class with no notion of a game key.

## Scope

**Backend only.** The web client follows in `add-web-tictactoe` — same split as `add-idiom-crossword` / `add-web-idiom-crossword`, and for the same reason: two ~400-line PRs review far better than one 800-line one, and the backend is independently testable.

At the end of this change 一字棋 is fully playable through the REST + SignalR API and has no UI. The catalogue manifest stays `status: 'planned'` until the web change flips it.

## What Changes

### Room creation learns a game key

- `CreateRoomCommand(HostUserId, Name, GameKey)` and `CreateAiRoomCommand(..., GameKey)`.
- Both validators reject a key that does not resolve in `IGameRulesRegistry` → **400**, not 404: the room does not exist yet, so this is a malformed request, not a missing resource. (Contrast with the move path, which returns 404 because there the room *does* exist and points at a game this build does not know.)
- The REST request bodies gain an optional `gameKey`, defaulting to `"gomoku"` when absent. Existing clients keep working unchanged — this is the one place where a default is worth the ambiguity, because the alternative is breaking the shipped web client for no gain.

### The lobby filters by game

- `GetRoomListQuery(string GameKey)` — no longer parameterless.
- `GET /api/rooms?gameKey=…`, defaulting to `"gomoku"`. An unknown key returns an empty list, not an error: "no rooms for that game" and "no such game" are indistinguishable to a lobby, and 404-ing a list endpoint is worse than returning nothing.
- `GET /api/users/me/active-rooms` is **not** filtered. It answers "where am I currently playing", and the answer should span games — that is the one place a player wants them mixed.

### AI gets the same registry shape as rules and puzzles

```
IBoardGameAi          { SelectMove(board, myStone) }          // renamed from IGomokuAi
IGameAiFactory        { GameKey; Create(difficulty, random) } // per-game
IGameAiRegistry       { For(gameKey) → IGameAiFactory | null }
```

Third time this shape appears (`IPuzzleRulesRegistry`, `IGameRulesRegistry`, now this). `GomokuAiFactory` becomes an `IGameAiFactory` instance rather than a static class; `TicTacToeAiFactory` joins it.

`EasyAi` is **reused as-is** — it already iterates `board.Rows` / `board.Cols` and picks a uniform random empty cell, which is correct on any board. That is the one piece of evidence so far that the registry work paid off, and it is worth stating plainly because the other two AI classes are the counter-evidence.

`TicTacToeMediumAi` (win → block → centre → corner → random) and `TicTacToeHardAi` (exhaustive minimax) are new. At 3×3 the reachable state count is 5,478, so Hard searches the **entire** game tree with no depth limit, no heuristic and no α-β tuning — it plays perfectly by construction rather than by evaluation quality.

### 一字棋 is unrated, on purpose and temporarily

`IGameRules.IsRated` (default `true`); `NInARowRules("tictactoe", 3, 3, 3, isRated: false)`. `MakeMoveCommandHandler` skips the whole ELO block when the room's game is unrated.

Two reasons, and the second is the load-bearing one:

- There is exactly **one** rating pool today, and it is the 五子棋 ladder in everything but name. Letting 一字棋 results move it would silently corrupt the only leaderboard the platform has.
- 一字棋 is a *solved* game: two competent players draw every time, and `TicTacToeHardAi` cannot be beaten. A rating over that measures who blunders first, converging to noise. There is nothing to rank.

`IsRated` is **scaffolding with a scheduled demolition date**: `add-per-game-rating` (roadmap step 2) gives every game its own `UserGameStats` row and deletes the flag. It is written down as such in the spec so it does not quietly become permanent.

## Impact

- **Affected specs:** `game-rules-registry` (ADDED), `room-and-gameplay` (MODIFIED ×3), `ai-opponent` (MODIFIED ×2, ADDED ×3), `elo-rating` (MODIFIED ×1).
- **Affected code:** `Gewu.Domain/Games`, `Gewu.Domain/Ai`, `Gewu.Application/Features/Rooms/{CreateRoom,CreateAiRoom,GetRoomList,MakeMove}`, `Gewu.Application/Features/Bots`, `Gewu.Infrastructure/DependencyInjection`, `Gewu.Api/Controllers/RoomsController`.
- **No migration.** `Room.GameKey` already exists and is already populated.
- **No breaking wire change.** Every new request field and query parameter defaults to `"gomoku"`; the shipped web client is untouched and keeps working.
- **Out of scope:** seats (`BlackPlayerId` / `WhitePlayerId` stay — X maps to black, O to white), JSON move payloads, the `GomokuHub` → `MatchHub` rename, per-game ratings, and the whole web client. Each is billed to the change that actually needs it.
- **Two gomoku-shaped names that 一字棋 makes actively wrong, deliberately left alone.** `GameEndReason.Connected5 = 0` will now be written for three-in-a-row, and tic-tac-toe rooms will route through `GomokuHub` at `/hubs/gomoku`. Both are normative in `room-and-gameplay`; the enum is additionally persisted as an integer and reaches the web client. Renaming either from here means rewriting four specs and a migration for a cosmetic gain, so both go with `generalize-match-contract`, which has to rewrite those specs anyway. What changes today is the argument for doing it: these were stale names before, and are wrong ones now.
