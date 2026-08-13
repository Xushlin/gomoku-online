## Context

Two archived changes lead here. `add-idiom-dictionary` seeded 30,895 idioms with an `IdiomChars` reverse index built specifically so a crossword generator could ask "which idioms have 山 at position 2?" — 4,111 of them are tier 1. `add-puzzle-core` built the level/attempt/best-result model, the server-authoritative validation flow, and an `IPuzzleRules` registry that is currently empty, so every puzzle endpoint 404s.

This change is the first consumer of both, which makes it the test of whether those abstractions were designed or merely guessed.

The reference is `chengyu-crossword.html`, a working prototype whose data shape (`{w, r, c, d, m}` per idiom, plus `hints` and `extra`) maps almost directly onto a level payload — with one change that is the entire point of `add-puzzle-core`: the answers move to the server side of the split.

## Goals / Non-Goals

**Goals:**

- A generated level is always solvable and never ambiguous.
- The same seed and dictionary always produce the same level set.
- Level data is reviewable as a diff before any UI depends on it.
- The layout the client receives contains no answer.
- Adding this game touches no file belonging to `puzzle-core` except the one interface change it justifies.

**Non-Goals:**

- The Angular game, the ink theme, flipping the manifest to `available`.
- Runtime level generation. Levels are data.
- Leaderboards.
- Multi-character-count idioms. Tier 1 is four-character by construction, and the generator assumes it.

## Decisions

### D1: The non-intersection adjacency rule is the correctness core

When placing a perpendicular idiom, every cell except the shared intersection MUST have no occupied orthogonal neighbour.

Without it the generator produces grids where two idioms run parallel one cell apart, and the characters that end up side by side read as nonsense that the player cannot distinguish from a real constraint. The puzzle stays technically solvable and becomes obviously broken. This is the property most likely to be silently violated by a plausible-looking implementation, so it is checked at placement time *and* re-verified over every emitted level by a separate audit pass.

*Alternative considered:* allow parallel adjacency and validate only the declared word slots. Rejected — it makes the grid unreadable, which is a gameplay bug rather than a correctness technicality.

### D2: Deterministic generation from an explicit seed

The generator takes a seed and uses it for every random choice; identical seed and dictionary yield identical output. The seed is recorded in the emitted file's header alongside the dictionary's upstream commit.

This is what makes a level set an artefact rather than an event. A bad level can be traced, reproduced, and fixed; a regenerated set diffs cleanly; and a reviewer can re-run the tool to confirm the committed file is what the tool actually produces.

*Cost:* the generator cannot use `Random.Shared` or any ambient source, which the tests enforce by generating twice and comparing.

### D3: Layout carries slots, tray, and pre-filled cells — never the answers

`LayoutJson` holds the grid extent, which cells exist, which cells are pre-filled (with their characters), the tile tray, and the word slots as `(row, col, direction, length)`. `SolutionJson` holds the character for every cell plus each word and its explanation.

The tray is not a leak: it contains exactly the characters needed plus distractors, so it reveals the multiset but not the assignment — which is the puzzle. Pre-filled cells reveal their own characters deliberately; the prototype does the same, and they are the foothold that makes a level approachable.

### D4: `CheckPartial` returns a payload, and that is a real interface change

The prototype shows the idiom's explanation the instant a word completes. The client cannot produce that text — explanations are in the database and the dictionary has no HTTP surface. So the server must return it on a correct check.

`PuzzlePartialResult` therefore gains an optional `PayloadJson`. This modifies an archived capability, which is the honest outcome: `puzzle-core` was designed without a real game, and the first real game found a gap. Recording it as a `puzzle-core` delta rather than smuggling the explanation through some crossword-specific side channel keeps the abstraction usable for 华容道 and 猜成语, both of which will want to say something on a correct partial answer.

Answer-key confinement is unaffected: the payload describes a word the player has just solved. It reveals nothing about unsolved parts of the grid.

### D5: Hints reveal in reading order, not at the player's cursor

`IPuzzleRules.Hint` receives `alreadyRevealedCount` and nothing about the player's selection, so the Nth hint reveals the Nth non-pre-filled cell in reading order. Deterministic, requires no client state, and cannot be gamed.

The prototype reveals the *selected* cell, which is friendlier. Matching it would mean sending the cursor to the server and trusting it — for a ranked game that is a trade in the wrong direction. Accepted consequence: a hint may reveal a cell the player already filled correctly, which wastes it. If playtesting says that matters, the fix is to pass the set of cells the player has *filled* (not the answers) and reveal the first unfilled one — an additive change to the signature, deliberately not made on speculation.

### D6: Levels are seeded, following `IdiomSeeder`

On startup, if `idiom-crossword` has no rows in `PuzzleLevels`, insert the committed set; otherwise no-op. Keyed on `(GameKey, LevelIndex)`, the unique constraint `add-puzzle-core` already declared. Same trade-off already accepted for the dictionary: the database is reproducible from migrations *plus* committed data.

### D7: Difficulty is three dials, not a magic number

`Difficulty` on the level is derived from interlocking idiom count (2→7), pre-filled cell count, and distractor tile count. The generator emits a ladder that mirrors the prototype's six levels, then continues.

Keeping all three dials explicit in the generator's config means retuning the curve is a config edit and a regenerate, not an algorithm change.

## Risks / Trade-offs

- **[A generated level is unsolvable or ambiguous]** → The adjacency invariant (D1) plus an audit pass over every emitted level: each declared slot must resolve to exactly one dictionary idiom, and every non-pre-filled cell must be reachable from the tray multiset. A level failing audit is not emitted.
- **[Generation stalls on a sparse character]** → Bounded retries per placement, then fall back to fewer idioms for that level and log it. A level with 4 idioms where 5 were asked for is fine; an infinite loop is not.
- **[Tier 1 is only 4,111 idioms, so levels repeat characters across the set]** → Acceptable and arguably good: familiar characters are what make the puzzle fair. Revisit by widening to tier 2 if the set feels stale.
- **[Modifying an archived capability]** → Correct process (a `puzzle-core` delta), and the reason it is legitimate is that the change was discovered by building a real consumer rather than imagined. The alternative — a crossword-shaped back door — would leave the same gap for the next two puzzle games.
- **[Committed level data can drift from the generator]** → The header records seed and dictionary commit, and determinism means a reviewer can regenerate and diff.

## Migration Plan

No migration; `add-puzzle-core` created the tables. Sequence: build the generator → generate against the seeded dictionary → audit → commit the level file → register the rules → boot and confirm the puzzle endpoints stop 404ing for `idiom-crossword`.

Rollback: revert. The seeder only ever inserts, and `PuzzleLevels` rows for one game key are trivially removable.

## Open Questions

- **How many levels should ship?** The prototype has six. The generator can emit any number; the first set is sized during implementation once the difficulty curve is visible.
- **Should the hint reveal at the cursor?** Deferred (D5) — needs playtesting, and the current rule is the conservative one.
- **Does a wrong `check` deserve a different response than "incorrect"?** The prototype shakes the cells and moves on. Anything richer (which character is wrong) is a difficulty decision, not a technical one.
