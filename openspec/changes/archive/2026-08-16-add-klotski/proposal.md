## Why

华容道 is the platform's second puzzle game and the first one whose answer is a **path**. `generalize-puzzle-rules` just made `IPuzzleRules` able to express that; this change is where the bill is collected.

It is also the cheapest remaining game: it needs no room, no SignalR, no ELO, no AI. It runs entirely on the puzzle kernel 成语纵横 established.

## Scope

Backend only: rules, a solver, the level set, and one DI registration. **No UI** — that is `add-web-klotski`, and until it lands 华容道 stays `planned` in the catalogue, which is the honest state.

## What Changes

### `KlotskiRules : IPuzzleRules`

A 5×4 board with rectangular pieces. A submission is a list of one-cell slides; `Validate` replays them from the level's `LayoutJson` and accepts only if every slide was legal **and** 曹操 ended on the exit.

### 华容道 has no secret, and that changes where its authority comes from

成语纵横's server authority rests on **withholding**: the answer key never leaves the server, so a client cannot fake a solve. 华容道 withholds nothing — the pieces, the board, the exit, and the rules of sliding are all public and all on the client, because a client that could not judge a slide could not animate one.

Its authority rests on **re-execution** instead: the server replays every move the player claims to have made, from the level's own starting position, and refuses to score anything it could not reproduce. Same platform rule (`计分只用服务端可观测信号`), a completely different mechanism.

That is why `SolutionJson` carries only `{ "minMoves": N }` — the star threshold — rather than a stored solution. There is nothing else to keep.

### `minMoves` is **computed**, not quoted

The published step counts for 横刀立马 differ by convention (whether a straight run of one piece counts as one move or several), and quoting a number from memory would be exactly the kind of unverifiable claim `add-xiangqi-ai` refused to make.

So the level artefact's `minMoves` comes from an **A\* search with an admissible heuristic** run offline by a generator tool, and a test re-derives it. Whatever the number turns out to be, it is a number this repo can reproduce.

### Hints are searched, not stored

A stored optimal path is useless three moves after the player leaves it. `Hint` runs the same A\* from the **player's reported position** and returns the next move on a shortest path — which is why it is worth a star. Missing or unparseable state falls back to a shortest move from the initial layout, per the platform contract.

### Scoring is by step count

`Score` reads the validated submission's length and compares it to `minMoves` from `SolutionJson`: 3 stars at ≤ 1.0×, 2 at ≤ 1.4×, otherwise 1. Hints subtract.

`Mistakes` is **structurally always 0** for this game — nothing increments it unless the client calls `check`, and 华容道's client never needs to. The formula therefore must not depend on it, which is exactly the case `generalize-puzzle-rules` wrote into the spec.

## Acceptance criterion

Inherited from `generalize-puzzle-rules`, and checked by `git diff --name-only` rather than asserted: **this change must not modify a single file under `Gewu.Domain/Puzzles/`, `Gewu.Application/Features/Puzzles/`, or the puzzle endpoints.**

## Non-goals

- No UI, no catalogue change (`status` stays `planned` until `add-web-klotski`).
- No new endpoint, no new DTO, no schema change. Levels arrive as a committed artefact through a seeder, exactly as 成语纵横's did.
- No `check` usage. The implementation exists because the interface requires it; the client will not call it.
