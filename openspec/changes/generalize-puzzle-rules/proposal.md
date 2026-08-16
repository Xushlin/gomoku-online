## Why

`IPuzzleRules` cannot express 华容道, and the way it fails is one this repo has now seen three times: **an interface shaped by its only implementation, believed general because a second implementation from the same family never contradicted it.**

`add-puzzle-core` wrote the seam and 成语纵横 filled it. The spec even claims the seam is proven —「新增一个单人关卡游戏 MUST 只需要:一个 `IPuzzleRules` 实现 + 一处 DI 注册」— and there is a test for it. But that test registers a **fake** rules implementation shaped like the crossword. A fake cannot contradict the assumption it was written under.

华容道 contradicts it in two places:

### 1. `Validate(solutionJson, submissionJson)` has no layout

成语纵横's answer is **positional**: every cell's correct content is in `SolutionJson`, so a submission can be judged against the answer alone. 华容道's answer is a **path** — a sequence of slides — and a path can only be checked against **where it started**. The starting position is the level's `LayoutJson`, and `Validate` never receives it.

`Hint(solutionJson, layoutJson, stateJson)` already takes both halves of the level. `Validate` and `CheckPartial` not taking them was never a decision; it was the crossword's shape showing through.

### 2. `Score(hintsUsed, mistakes, duration)` cannot score a sliding puzzle

The live puzzle-core spec already says, in the requirement about scoring:

> 星级**公式**按游戏而异(**华容道计步数**、成语纵横计错误与提示)

So step-count scoring was foreseen and written down — and then **not provided for**. The signature has no way to see the moves, and no way to see the level's known minimum to compare them against.

Worse for 华容道 specifically: `Mistakes` is structurally always `0` for it. Nothing increments that counter unless a game calls `check`, and 华容道 has no reason to — unlike the crossword, its client can judge a slide by itself, because the rules are public and the board is public. Scoring on `hintsUsed + mistakes` would hand three stars to everyone who solved it without a hint, in 81 moves or in 800.

## What Changes

Every `IPuzzleRules` method now receives **both halves of the level** plus its own payload:

```csharp
PuzzleValidationResult Validate(string solutionJson, string layoutJson, string submissionJson);
PuzzlePartialResult   CheckPartial(string solutionJson, string layoutJson, string partialJson);
PuzzleHintResult      Hint(string solutionJson, string layoutJson, string? stateJson);   // unchanged
int                   Score(PuzzleScoreInput input);
```

`PuzzleScoreInput` carries the three server-observed signals that existed before (`HintsUsed`, `Mistakes`, `Duration`) plus the level and the **validated** submission.

**Passing the submission to `Score` does not weaken "no client-reported performance numbers".** The rule exists because a client saying "I made 0 mistakes" is unverifiable. A client saying "here are my 81 moves" is a different kind of statement: the server **replayed every one of them** and refused to score at all until they solved the puzzle. A number the server had to reconstruct before accepting is a server-observed fact, not a self-report. That distinction is written into the spec so nobody later reads this as a loophole.

## Non-goals

- **No new game.** This change registers nothing; 华容道 arrives in `add-klotski`.
- **No behaviour change for 成语纵横.** Its scoring ignores every new input; its tests must pass with only mechanical call-site edits.
- No change to `PuzzleLevel`, `PuzzleAttempt`, `PuzzleLevelProgress`, any endpoint, any DTO, or any migration.

## Acceptance criterion

The same one `generalize-match-domain` set and then collected: **after this change, adding 华容道 must not modify a single file under puzzle-core.** `add-klotski` is where that gets checked, and it is checked by `git diff --name-only`, not by assertion.

## Impact

- `puzzle-core`: three requirements modified (the `IPuzzleRules` shape, the scoring contract, the `check` contract).
- `idiom-crossword`: none. Its spec describes behaviour, and its behaviour does not change.
