## Why

Hints are near-useless in practice. `IPuzzleRules.Hint` receives only `alreadyRevealedCount` and reveals the Nth non-pre-filled cell in reading order, so the Nth hint always lands in the same place regardless of what the player has already solved.

Observed on 成语纵横 level 5: the player had solved the top of the grid and was stuck on `(6,0) (6,1) (6,2)` — 木 / 人 / 石. Those cells are 14th, 15th and 16th in reading order. Two hints were spent revealing 流 and 离, cells the player had **already filled and locked**. The counter incremented, a star was charged, and nothing changed on screen. Reaching a genuinely empty cell would have taken 14 hints.

`add-idiom-crossword`'s design D5 recorded this as an accepted risk — "a hint **may** reveal a cell the player already filled, which wastes it". That framing was wrong. Players fill top-down and hints reveal top-down, so the two move together: the waste is not occasional, it is close to guaranteed. Playtesting has now said so.

Note this is **not a code-versus-spec bug**. `idiom-crossword`'s "提示按阅读顺序推进" requirement says exactly what the code does, so the spec is the thing that needs correcting.

## What Changes

### **BREAKING** — `IPuzzleRules.Hint` takes client board state instead of a counter

```
Hint(solutionJson, layoutJson, alreadyRevealedCount)   →   Hint(solutionJson, layoutJson, stateJson)
```

`stateJson` is opaque to the platform, like every other game payload. 成语纵横 sends `{ "filled": ["0,4", "1,4", …], "selected": "6,0" }`.

The counter goes away entirely. `HintsUsed` still lives on the attempt and is still what scores — this only changes *which* cell gets revealed, never how many hints a player has spent.

### Reveal order, matching the prototype

1. If the client names a `selected` cell that exists and is not pre-filled → reveal that. This is `chengyu-crossword.html`'s behaviour: you point at the cell you are stuck on.
2. Otherwise → the first cell in reading order that the client has not filled.
3. If the client reports every cell filled → the first non-pre-filled cell in reading order. The grid being full of wrong answers is exactly when overwriting one with a correct character helps.

### Trusting client state is safe here

The client reports its own visible board — which cells hold a character, and where the cursor is. That is not secret, and the server keeps the only copy of the answers.

A client could lie to aim a hint at any cell it likes. That is not an exploit, it is the feature: the prototype lets you aim hints, and each one still costs a star through the server-side `HintsUsed`. Nothing about scoring moves.

## Capabilities

### Modified Capabilities

- `puzzle-core`: the `hint` contract carries opaque client state rather than a reveal counter.
- `idiom-crossword`: the reveal rule becomes selected-then-first-unfilled instead of reading order.
- `web-idiom-crossword`: the play page sends its filled set and selection with each hint request.

## Impact

- **Backend**: `IPuzzleRules`, `IdiomCrosswordRules`, `UsePuzzleHintCommand` + handler, `PuzzlesController`'s hint action gains a body, the fake rules in `PuzzleLifecycleTests`.
- **Web**: `PuzzleApiService.hint` gains a state argument; `Play` supplies it from `CrosswordState`.
- **Migration**: none. **Wire contract**: the hint endpoint gains an optional request body; an empty body still works and falls back to rule 2.
- **Tests**: reveal precedence (selected wins, then first unfilled, then first cell), a hint on an already-filled selection overwrites it, and a regression test for the reported case — with the top of the grid filled, the hint must land in the empty bottom row rather than on a solved cell.
