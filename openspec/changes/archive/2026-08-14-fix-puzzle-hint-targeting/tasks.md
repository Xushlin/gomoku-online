## 1. Contract

- [x] 1.1 `IPuzzleRules.Hint(solutionJson, layoutJson, stateJson)` — replace `alreadyRevealedCount` with an opaque nullable client-state string. Doc comment states it must never influence scoring and why trusting it is safe.
- [x] 1.2 `UsePuzzleHintCommand` carries `StateJson`; the handler passes it through unchanged.
- [x] 1.3 `PuzzlesController` hint action accepts an optional body `{ stateJson }`. A missing body must still succeed.

## 2. Crossword rules

- [x] 2.1 `CrosswordHintState` record — `Filled` (cell keys) and `Selected` (cell key or null).
- [x] 2.2 `IdiomCrosswordRules.Hint` precedence: valid non-given `selected` → first cell not in `filled` → first non-given cell.
- [x] 2.3 An invalid or pre-filled `selected` is ignored, not rejected. Malformed `stateJson` degrades to rule 2.

## 3. Web

- [x] 3.1 `PuzzleApiService.hint(attemptId, state)` sends the state as a JSON string, same shape as `check` / `submit`.
- [x] 3.2 `CrosswordState` exposes a `hintState()` returning `{ filled, selected }` from what it already tracks.
- [x] 3.3 `Play.hint()` passes it. Confirm `applyHint` already frees a wrong tile before writing — it does; assert it.

## 4. Tests

- [x] 4.1 Selected wins: with `filled` covering the first 13 cells and `selected` at a later cell, that cell is revealed.
- [x] 4.2 Selected is honoured even when it is in `filled`.
- [x] 4.3 No selection → first cell not in `filled`.
- [x] 4.4 Invalid selection (nonexistent, or pre-filled) → falls back, no throw.
- [x] 4.5 All cells filled → first non-given cell.
- [x] 4.6 Malformed / null `stateJson` → still returns a cell.
- [x] 4.7 **Regression for the reported bug**: build level 5's real layout, mark the solved top as filled, and assert the revealed cell is one of the empty bottom-row cells — never an already-filled one.
- [x] 4.8 Web: `hint()` sends the state; `CrosswordState.hintState()` reports filled + selected correctly.
- [x] 4.9 Web: a hint landing on a wrongly-filled cell returns that tile to the tray.

## 5. Verification

- [x] 5.1 `dotnet test Gewu.slnx` and `ng test` green; `npm run lint` clean.
- [x] 5.2 Replayed the reported scenario against the live API rather than the browser — see note 6.3.
- [x] 5.3 Confirm `HintsUsed` still increments once per request regardless of the state sent.

## 6. Notes from implementation

- [x] 6.1 **A `default`-struct sentinel bug, caught before it shipped.** The first cut used `selected != default` to mean "found it", but `CrosswordCell` is a `record struct`, so `default` is the perfectly valid cell `(0,0)` — selecting the top-left cell would have been read as "no selection" and silently fallen through to rule 2. Rewritten with an explicit `CrosswordCell?` and a local `Find`. There is a test named after it: `A_selection_at_the_origin_is_honoured`.
- [x] 6.2 The counter is gone from the interface entirely rather than kept alongside the state. Two ways to say "where should the hint go" is one too many, and the counter was the wrong one.
- [x] 6.3 Task 5.2 said to replay the bug in the browser on level 5. Level 5 is locked until levels 1–4 are cleared, so it was replayed against the **live API** on level 1 with the same geometry instead: mark every revealable cell filled except the last, ask for a hint, assert it lands on the empty one. Verified output: revealable `['0,1','0,2','0,3','1,0','2,0']`, only `2,0` unfilled, hint revealed `2,0`「合」— where the old rule would have revealed `0,1`. Also confirmed selection targeting (`selected=0,3` → `0,3`), that `hintsUsed` still counts 1, 2, 3 by call, and that a body-less request still returns 200.
- [x] 6.4 Backend tests 561 → 574 (+13 hint targeting), web 298 → 305 (+7). Lint clean, both builds clean.
- [x] 6.5 Two lint/type detours worth recording: vitest infers a mock's call-arg tuple from the arrow's declared parameters, so `vi.fn(() => …)` types `calls[0]` as `[]`; and adding parameters purely to fix that trips `no-unused-vars`. Resolved by having the mock record its arguments into an array the test asserts on — which reads better than either workaround.
