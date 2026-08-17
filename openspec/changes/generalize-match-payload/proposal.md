## Why

成语接龙 is the next game, and it is the first match game with **no board and no coordinates**. A move is an idiom — a string. It is legal if the dictionary has it, if its first character matches the last character of the previous idiom, and if nobody has said it yet this game. The game ends when a player cannot answer in time.

Nothing about that fits the kernel as it stands:

- `MoveIntent(Position? From, Position To)` — `To` is **not** nullable. Every move must land on a square.
- `Move` persists `Row` / `Col` as non-nullable `int`.
- `IGameRules.Rows` / `Cols` are non-nullable `int` on the **base** interface, so every registered game must claim a board size.

### This is the bet `generalize-match-domain` placed, now called

That change considered a JSON move payload and rejected it, in writing:

> **不用 JSON 载荷列。** 象棋的每一步都恰好是 from -> to……两个可空列就覆盖了两类棋种……**真出现不规则走子时再加列。**

And in the roadmap note: *"JSON would have been paying for a requirement that does not exist and that 象棋 would not have created."*

That reasoning was correct and it has now expired exactly as it predicted. 象棋 would not have created the requirement; 成语接龙 does. The instruction left behind — *add a column when an irregular move actually appears* — is what this change carries out. **A deferral that names its own trigger is the good kind**, and this is the trigger.

### What it did not anticipate

Adding a `Text` column is the easy half. The hard half is that `Row` / `Col` must become **nullable**, which weakens a shape two existing games rely on. The alternative — storing `Row = 0, Col = 0` for an idiom — is forbidden by the kernel's own rule, stated in bold on `MoveIntent`:

> **MUST NOT 用一个合法值表示「没有起点」**

Using `(0,0)` to mean "this move has no square" is the same sin one field over.

So `Move` becomes a sum type in a relational table: positional or textual, never both, never neither. The repo already crossed this line once with nullable `From`. The difference is that a two-state discriminant could be left to a doc comment, and a three-field one cannot — **an invariant nothing enforces is a sentence, not a rule**, which this session has now paid for four times. The discriminant is checked in the constructor and walked by a test.

### And the board itself is an assumption

`IGameRules.Rows` / `Cols` sit on the base interface. `INInARowRules` was split off precisely so 象棋 would not have to implement `WinLength` / `CreateBoard` it does not have — the comment says an interface must only carry what is true of every implementation, and that *"骗人的实现是下一个人删不掉的东西"*. `Rows` / `Cols` were left behind because 象棋 has a board; 成语接龙 does not.

Returning `0, 0` would not just be untidy. `GameDescriptorDto` publishes those numbers, and the web's `boardSizeFor` treats `rows <= 0` as "unknown" and substitutes **15×15**. A chain game would be described to every client as a gomoku board.

## What Changes

### A move is positional **or** textual

```csharp
MoveIntent.Place(to)        // gomoku, tic-tac-toe
MoveIntent.Slide(from, to)  // xiangqi
MoveIntent.Say(text)        // idiom chain
```

`To` becomes `Position?` and a `Text` is added, on `MoveIntent`, `PlayedMove` and the `Move` entity. Exactly one shape is populated; both constructors reject anything else, and a test enumerates the illegal combinations rather than trusting the factories to be the only path.

`Text` is a plain column, not JSON. The reason the earlier change gave for avoiding JSON still holds and is not weakened by this game: an idiom is one scalar, so a column stays queryable, EF-mapped and strongly typed at replay. JSON would buy extensibility nothing has asked for.

### Board dimensions move off the base interface

A new `IBoardGameRules : IGameRules` carries `Rows` / `Cols`; `INInARowRules` extends it, `XiangqiRules` implements it, and a chain game implements neither. `GameDescriptorDto.Rows` / `Cols` become nullable, and the web treats "no board" as a distinct case from "unknown board" — the same distinction `remove-manifest-board` drew between *the descriptor has not arrived* and *this key is not a game*.

### What this change does not do

It does not add 成语接龙. No dictionary loading, no chain rules, no hub method, no UI. This is the seam only, and the game lands next as `add-idiom-chain` with the same acceptance criterion the previous two games carried: **`git diff --name-only` contains no kernel file.**

That criterion is also the honest answer to the question this repo keeps having to ask. The seam is being shaped against a game that does not exist yet, from a rule set written down but not implemented — which is how `generalize-match-domain` was shaped against 象棋, and how `generalize-puzzle-rules` was shaped against 华容道. Both held. Neither was *proven* until the game landed.

## Impact

- Affected specs: `room-and-gameplay` (the `Move` shape and its invariant), `game-rules-registry` (the interface split, the descriptor's nullable dimensions), `web-game-board` (`boardSizeFor` gains a third case).
- Affected code: `MoveIntent` / `PlayedMove` / `Move`, `IGameRules` / `INInARowRules` / `XiangqiRules`, `GameDescriptorDto` + its projection, one EF migration, `games/board-size.ts`.
- **Migration:** `Row` / `Col` → nullable, add `Text`. Widening columns, so `Up` is pure addition. `Down` narrows and must reject rather than silently truncate any textual move — the same failure `add-per-game-rating`'s hand-written `Down` was fixed for.
- No production data exists, which makes the migration cheap but does **not** make the `Down` path optional: nobody walks it until they need it.
