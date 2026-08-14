## Context

`Room.PlayMove` reaches straight into `Board`, which declares `Size = Position.BoardSize` (15) and `WinLength = 5` as compile-time constants, and `Position`'s constructor throws for anything outside `[0..14]`. Win detection itself is already generic — an incremental four-direction run-length scan parameterised only by those two numbers.

So the gomoku-specific part of the match context is smaller than it looks: two constants and a constructor guard. Everything else — rooms, seats, turn order, spectators, chat, urging, timeout, resignation, ELO, replay — is game-agnostic already.

The puzzle context set the pattern to copy: an interface keyed by a game string, a registry that returns `null` for unknown keys, and handlers that map that to 404. 成语纵横 registered under it without touching a single platform file, which is the evidence that the shape works.

## Goals / Non-Goals

**Goals:**

- A second board game costs one rules class and one registration.
- Zero wire-contract change: no DTO, controller, hub or web file is touched, and no `web-*` spec needs a delta.
- Gomoku's behaviour is bit-identical, proven by its existing tests passing unmodified.
- `Domain` keeps zero outward dependencies.

**Non-Goals:**

- Seats, JSON move payloads, the hub contract, per-game AI, per-game rating.
- Any game other than gomoku. `add-tictactoe` follows.

## Decisions

### D1: Rules are a parameter, not a dependency

`Room.PlayMove(userId, position, now, rules)` takes the rules as an argument. `Room` does not hold a registry, and `Domain` does not know one exists.

The alternative — injecting `IGameRulesRegistry` into the aggregate — would put a lookup service inside an entity and make `Room` unconstructable in a test without a registry. Passing the rules keeps the aggregate a pure function of its arguments, which is what makes the existing domain tests readable.

The cost is that every caller must resolve the rules first. There are four, all handlers, all of which already resolve a room by id.

### D2: `Position` keeps a negative check, loses the upper bound

A negative row or column is nonsense on every conceivable board, so that guard stays in the value object. The upper bound depends on the game, so it moves to `IGameRules.IsInBounds`, checked by `Room.PlayMove` before touching the board.

The exception type is deliberately unchanged: `InvalidMoveException` still comes out, so `ExceptionHandlingMiddleware` still returns 409 and the API contract does not move. Only the line that throws it does.

*Alternative considered:* keep `Position` bounded and add a second `SmallPosition` type. Rejected — two coordinate types is worse than one unbounded one, and the bound genuinely is a game property.

### D3: `NInARowRules` covers both games, because n-in-a-row already generalises

Gomoku is `(15, 15, 5)`. Tic-tac-toe is `(3, 3, 3)`. The win scan is identical; only the constants differ. Writing a second implementation would be duplicating an algorithm to change two numbers.

This is the strongest argument for doing the registry *now* and the seat rename *later*: the registry immediately buys a whole game, while the seat rename buys nothing until xiangqi.

### D4: `Board` stays mutable and cloneable for the AI's sake

`Board.Clone()` and `Reset()` exist because the AI search plays and unplays moves thousands of times. Making `Board` immutable would be cleaner in isolation and would wreck the bot's performance. Dimensions become instance fields; the mutation model is untouched.

### D5: `GameKey` is a plain string with a backfilled default

`Rooms.GameKey` is `TEXT NOT NULL` defaulting to `'gomoku'`, and the migration backfills existing rows. Not an enum, because the whole point is that games are added without editing a shared type — the same reason the game catalogue and the puzzle registry both key on strings.

An unknown key resolves to `null` and becomes a 404, exactly as in `puzzle-core`. A room can only be created with a key the registry knows, so a 404 here means data written by an older or broken build.

### D6: The wire contract is deliberately frozen

`MakeMoveCommand(userId, roomId, row, col)`, `MoveDto`, `GomokuHub.MakeMove` and every DTO stay exactly as they are. Tic-tac-toe will use the same hub method with smaller coordinates.

This is what keeps the change reviewable: the diff is confined to `Domain`, four handlers, one EF configuration and one migration, and *every* existing test — including the whole web suite — is a regression test for it.

## Risks / Trade-offs

- **[Board dimensions become runtime data, so an index bug becomes possible where a constant made it impossible]** → `IndexOf` and the run-length scan are covered by the existing win-detection suite at 15×15 and by new tests at 3×3, where off-by-one errors are far easier to trigger.
- **[Passing rules to `PlayMove` is easy to get wrong at a call site]** → Four call sites, all resolving from `room.GameKey`, so a mismatch would need someone to deliberately pass another game's rules.
- **[`Black` / `White` naming will read oddly for tic-tac-toe]** → Accepted and temporary. The alternative is doing the seat rename now, in a change that gains nothing from it, and shipping a diff several times this size.
- **[A room whose `GameKey` is not registered becomes unplayable]** → 404 rather than a crash, same as the puzzle context. Only reachable via hand-edited data.

## Migration Plan

One migration adding `Rooms.GameKey` with default `'gomoku'` and backfilling. Existing rooms keep playing; existing games replay identically because the rules resolved for `'gomoku'` are the same 15×15×5 constants the code used before.

Rollback: revert and drop the column.

## Open Questions

- **Should `IGameRules` also own turn order?** Gomoku and tic-tac-toe both alternate strictly, so the question has no evidence behind it yet. It surfaces properly with a game that passes or moves twice.
- **Where does per-game AI register?** Out of scope here; `add-tictactoe` will answer it for a trivial bot, and the answer will probably be a `(GameKey, Difficulty)` registry mirroring this one.
