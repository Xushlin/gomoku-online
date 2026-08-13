## Why

`add-idiom-dictionary` supplied the data and `add-puzzle-core` supplied the machinery. Neither can be played: the puzzle rules registry is empty, so every endpoint it exposes returns 404. This change registers the first game and turns two archived infrastructure changes into something a person can actually do.

成语纵横 is also the change that tests whether the platform abstractions were right. If `IPuzzleRules` really is enough for a real game, adding 华容道 and 猜成语 later is cheap. If it is not, better to find out now with one game built on it than four.

## What Changes

### Level generator (offline tool)

`backend/tools/CrosswordGenerator/` — a second console tool alongside `IdiomImporter`, not referenced by `Gewu.Api`. It reads the seeded dictionary and emits `backend/data/levels/idiom-crossword.json`, committed to the repo.

Algorithm: place a tier-1 seed idiom horizontally, then repeatedly pick an occupied cell, look up idioms having that character at a compatible position (the `IdiomChars` reverse index exists precisely for this), and place one perpendicular. Difficulty comes from the number of interlocking idioms, how many cells are pre-filled, and how many distractor tiles get mixed into the tray.

**The generator is deterministic given a seed.** Same seed and same dictionary produce the same levels, so a level set is reproducible and reviewable rather than something that appeared once on someone's laptop.

### The placement invariant that makes puzzles legible

A newly placed cell that is *not* the intersection MUST have no occupied orthogonal neighbour. Without this rule the generator happily produces grids where two idioms run alongside each other and the adjacent characters read as garbage — technically solvable, obviously wrong. This is the single most important correctness property in the change and it gets its own tests.

### `IdiomCrosswordRules`

The `IPuzzleRules` implementation registered under `idiom-crossword`:

- `Validate` — every cell matches the solution.
- `CheckPartial` — one word slot; returns whether it is correct **and, when correct, the idiom plus its explanation**.
- `Hint` — reveals the next unrevealed cell in reading order.
- `Score` — the prototype's rule, unchanged: `cost = mistakes + hintsUsed`; 0 → 3 stars, ≤2 → 2, else 1.

### **BREAKING** — `PuzzlePartialResult` gains a payload

The prototype shows a 释义纸条 the moment an idiom completes, and the client cannot produce that text: it knows which characters it placed, but explanations live in the database and the dictionary has no HTTP surface by design.

So `PuzzlePartialResult` gains an optional `PayloadJson`, and `check` returns the solved idiom and its explanation. This is a requirement change to the archived `puzzle-core` capability, discovered by its first consumer — exactly what a first consumer is for. It is additive at the wire level (a new optional field) but changes a `Domain` interface, so it is marked breaking.

### Level data and seeding

Generated levels are committed and loaded by a seeder following the `IdiomSeeder` pattern: on startup, if `idiom-crossword` has no levels, insert them; otherwise no-op. Keyed on `(GameKey, LevelIndex)`.

### Out of scope

- **The Angular game.** `add-web-idiom-crossword` follows, with the ink/活字印刷 theme registered through `ThemeService` and the manifest flipped to `available`. Backend-only here keeps this reviewable and lets the level data be inspected before any UI depends on its shape.
- **Leaderboards.** Still `add-puzzle-leaderboard`.
- **Hand-curating tier overrides.** The generator draws from tier 1 (4,111 idioms), which is conservative enough for a first level set. Playtesting decides whether specific idioms need `TierOverride`.

## Capabilities

### New Capabilities

- `idiom-crossword`: the generator's placement rules and determinism guarantee, the level payload shape and its answer-free layout, `IdiomCrosswordRules` behaviour for all four operations, and level seeding.

### Modified Capabilities

- `puzzle-core`: `IPuzzleRules.CheckPartial` may now return a game-specific payload alongside the correctness verdict, and the `check` endpoint forwards it. One requirement changes; the answer-key confinement rule is unaffected — the payload carries only what the player has already earned by solving that word.

## Impact

- **New**: `backend/tools/CrosswordGenerator/`, `backend/data/levels/idiom-crossword.json`, `Gewu.Domain/Games/IdiomCrossword/` (rules + grid model), `Gewu.Infrastructure/Persistence/CrosswordLevelSeeder.cs`, DI registration, tests in `Gewu.Domain.Tests` and `Gewu.Infrastructure.Tests`.
- **Modified**: `IPuzzleRules` / `PuzzlePartialResult` (the payload), `CheckPuzzlePartialCommandHandler` and `PuzzleCheckResultDto` to forward it.
- **Migration**: none. `add-puzzle-core` already created the tables.
- **API**: no new endpoint. The existing puzzle routes stop returning 404 for `idiom-crossword` — that is the whole user-visible effect.
- **Tests**: the placement invariant, generator determinism, all four rules operations, level-payload answer-freedom, and seeder idempotency.
