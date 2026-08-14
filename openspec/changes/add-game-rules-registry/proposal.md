## Why

成语纵横 proved the puzzle half of the platform: `IPuzzleRules` plus a registration was enough for a whole game. The match half has no equivalent — `Room.PlayMove` calls `Board` directly, `Board` hardcodes 15×15, and `Position` refuses any coordinate outside `[0..14]` in its constructor. A second board game cannot be added without editing gomoku's code.

This change gives match games the same shape the puzzle context already has, and stops there.

## Deviation from the roadmap order, and why

`CLAUDE.md` sequences this as `generalize-match-domain` → `migrate-match-persistence` → `generalize-match-contract` → `add-tictactoe`: seats, JSON move payloads and the hub contract all at once, split into three PRs.

Two problems with that. First, the three cannot actually be separate: renaming `Room.BlackPlayerId` breaks the EF configuration, every handler, the DTO mapping and the hub in the same commit, so "Domain first" does not compile on its own. Second, and more usefully — **一字棋 does not need any of it**. Tic-tac-toe is a two-player grid game whose moves are a row and a column, exactly like gomoku. The seat rename and the JSON payload are forced by 中国象棋, whose moves are from→to, and they should be paid for by the change that needs them.

So this change delivers the registry and leaves the representation alone. `add-tictactoe` follows immediately and cheaply; `generalize-match-seats` waits for xiangqi.

## What Changes

### Board dimensions become data

- `Board` takes `(rows, cols, winLength)` instead of hardcoding 15 / 15 / 5.
- `Position` stops enforcing `[0..14]`. It keeps rejecting negatives — a negative coordinate is nonsense on any board — and bounds-checking moves to the rules, which are the only thing that knows how big the board is.

The same `InvalidMoveException` still surfaces for an out-of-range move; what moves is *where* the check happens, and with it one status code — see the wire-contract note below.

### `IGameRules` + registry

```
IGameRules   { GameKey; Rows; Cols; WinLength; CreateBoard(); IsInBounds(position) }
IGameRulesRegistry  { For(gameKey) → IGameRules | null }
```

`NInARowRules` implements it for any `(rows, cols, winLength)`. Gomoku registers as `NInARowRules("gomoku", 15, 15, 5)`. Adding 一字棋 will be `NInARowRules("tictactoe", 3, 3, 3)` plus one registration — no new algorithm, because n-in-a-row already is the algorithm.

Registry mirrors `IPuzzleRulesRegistry` exactly: resolve by key, `null` for unknown, handlers map that to 404.

### `Room` learns which game it is

- `Room.GameKey` (string, `'gomoku'` for every existing row).
- `Room.PlayMove(userId, position, now, rules)` — rules are **passed in** by the handler, not injected. `Domain` keeps zero outward dependencies; `Application` resolves the registry.
- `Game.ReplayBoard(rules)` likewise.

### Out of scope, and who pays for it later

- **Seats.** `BlackPlayerId` / `WhitePlayerId` stay. Tic-tac-toe's X and O map onto them; the rename is `generalize-match-seats`, driven by xiangqi.
- **JSON move payloads.** `Move` keeps `Row` / `Col` / `Stone`. Also xiangqi's bill.
- **The wire contract**, with one deliberate exception. No DTO, controller, hub method or web file changes; `MakeMove(roomId, row, col)` still works and every `web-*` spec is untouched. The exception: an out-of-range coordinate like `(20, 20)` now returns **409 instead of 400**, because the validator runs before the room — and therefore the game key — is known, so it can only enforce the game-independent half of the rule (non-negative). This is arguably the more correct status anyway: `(20, 20)` is a well-formed request that is illegal in gomoku and would be legal on a hypothetical 21×21 board, which is "this move is not allowed in this game" rather than "your request is malformed". Negative coordinates stay 400. Unreachable from the web client, which only renders cells that exist.
- **AI.** `GomokuAiFactory` stays gomoku-only; it just adapts to `Board`'s new constructor. A bot for a second game is that game's problem.

## Capabilities

### New Capabilities

- `game-rules-registry`: the `IGameRules` contract, `NInARowRules`, the registry's resolve-or-404 rule, and the "one class plus one registration" extension guarantee.

### Modified Capabilities

- `gomoku-domain`: `Board` and `Position` become dimension-agnostic; win detection is stated in terms of the rules' `WinLength` rather than a constant 5.
- `room-and-gameplay`: `Room` carries a `GameKey`, and `PlayMove` takes the rules it should be judged by.

## Impact

- **New**: `Gewu.Domain/Games/Abstractions/` (`IGameRules`, `IGameRulesRegistry`), `Gewu.Domain/Games/NInARow/NInARowRules.cs`, `Gewu.Infrastructure/Games/GameRulesRegistry.cs`.
- **Modified**: `Board`, `Position`, `Room`, `Game`, the four handlers that play or replay moves, `RoomConfiguration`, the AI factory's board construction, and the domain tests that construct boards.
- **Migration**: one, adding `Rooms.GameKey` with a `'gomoku'` default and backfill.
- **Wire contract**: no web work, no client change, no hub change, no DTO change. One status code moves: an out-of-range coordinate becomes 409 rather than 400 (negatives stay 400). Not reachable from the web client.
- **Tests**: `NInARowRules` at 3×3×3 and 15×15×5, bounds rejection, registry resolve-and-miss, and every existing win-detection test still passing against the parameterised board.
