## 1. puzzle-core interface change

- [x] 1.1 `PuzzlePartialResult` gains `PayloadJson` (nullable). Doc comment states it is only populated on a correct verdict and why that keeps answer-key confinement intact.
- [x] 1.2 `PuzzleCheckResultDto` gains the forwarded payload; `CheckPuzzlePartialCommandHandler` passes it through. A wrong verdict MUST forward null.
- [x] 1.3 Update the fake rules in `PuzzleLifecycleTests` for the new shape and add a case asserting a wrong verdict carries no payload.

## 2. Grid model + rules (Domain)

- [x] 2.1 `Gewu.Domain/Games/IdiomCrossword/CrosswordLayout.cs` + `CrosswordSolution.cs` — the serialisable payload shapes. Layout: extent, cells, pre-filled cells with characters, tray, word slots `(row, col, direction, length)`. Solution: character per cell, plus word + explanation per slot.
- [x] 2.2 `CrosswordDirection` enum (horizontal / vertical).
- [x] 2.3 `IdiomCrosswordRules` implementing `IPuzzleRules` with `GameKey = "idiom-crossword"`.
- [x] 2.4 `Validate` — every cell matches; a single mismatch fails.
- [x] 2.5 `CheckPartial` — takes a slot index plus the characters the player put there; on a match returns the word and explanation as payload, on a mismatch returns no payload.
- [x] 2.6 `Hint` — reveals the `alreadyRevealedCount`-th non-pre-filled cell in reading order (row-major).
- [x] 2.7 `Score` — `cost = mistakes + hintsUsed`; 0 → 3, ≤2 → 2, else 1.
- [x] 2.8 Register in DI: `services.AddSingleton<IPuzzleRules, IdiomCrosswordRules>()`. Confirm this is the only file outside `Games/IdiomCrossword/` that the registration touches.

## 3. Generator (offline tool)

- [x] 3.1 `backend/tools/CrosswordGenerator/` console project. Not referenced by `Gewu.Api`; not added to the API dependency graph.
- [x] 3.2 Read the **committed curated artefact** (tier 1 only), not the database through `IIdiomRepository` as this task originally said — see note 7.2.
- [x] 3.3 Placement loop: seed idiom horizontally at origin, then repeatedly pick an occupied cell, look up idioms with that character at a compatible position, and place perpendicular.
- [x] 3.4 **Enforce the adjacency invariant at placement time**: every non-intersection cell must have zero occupied orthogonal neighbours.
- [x] 3.5 All randomness from an explicit seed. No `Random.Shared`, no clock, no ambient source.
- [x] 3.6 Bounded retries per placement; on exhaustion emit the level with fewer idioms than requested and log it rather than looping.
- [x] 3.7 Difficulty config: three explicit dials (idiom count, pre-filled cell count, distractor count) producing a ladder that starts where the prototype's six levels do.
- [x] 3.8 Audit pass over every generated level: adjacency re-verified; each declared slot resolves to exactly one dictionary idiom; tray multiset covers all non-pre-filled cells. A level failing audit is not emitted.
- [x] 3.9 Emit `backend/data/levels/idiom-crossword.json` with a header recording seed, dictionary upstream commit, generation date, and the difficulty config.

## 4. Seeding (Infrastructure)

- [x] 4.1 `CrosswordLevelSeeder` following `IdiomSeeder`: if no `idiom-crossword` rows exist, insert; else no-op. Idempotent on `(GameKey, LevelIndex)`.
- [x] 4.2 Wire into startup after migrations, alongside the idiom seeder.

## 5. Tests

- [x] 5.1 Adjacency invariant: a hand-built grid where a candidate placement would sit parallel-adjacent is rejected; the same placement at a legitimate intersection is accepted.
- [x] 5.2 Determinism: generate twice with one seed, assert byte-identical output; generate with two seeds, assert different output.
- [x] 5.3 `Validate` — exact match passes, one wrong cell fails.
- [x] 5.4 `CheckPartial` — correct slot returns word + explanation; wrong slot returns no payload.
- [x] 5.5 `Hint` — three successive calls reveal reading-order cells 1, 2, 3, skipping pre-filled ones.
- [x] 5.6 `Score` — the (0,0)→3, (1,1)→2, (0,3)→1 table.
- [x] 5.7 Layout answer-freedom: serialise a generated level's layout, assert it contains no full idiom, no explanation, and no non-pre-filled cell character.
- [x] 5.8 Seeder idempotency: seed twice, row count unchanged.
- [x] 5.9 Audit rejects a deliberately broken level (tray missing a needed character).

## 6. Verification

- [x] 6.1 `dotnet build Gewu.slnx` and `dotnet test Gewu.slnx` green; no existing test broken by the `PuzzlePartialResult` change.
- [x] 6.2 Delete `gewu.db`, boot: migrations apply, idioms seed, crossword levels seed, second boot inserts nothing.
- [x] 6.3 Exercise the real API end to end for `idiom-crossword`: level list → start attempt → wrong check → correct check (assert the explanation comes back) → hint → submit → assert stars and the progress row.
- [x] 6.4 Confirm every other unregistered game key still 404s.
- [x] 6.5 Eyeball a few generated levels for legibility — the thing tests cannot check is whether a puzzle feels fair.

## 7. Notes from implementation

- [x] 7.1 **The grid, audit, and generator moved into `Gewu.Domain/Games/IdiomCrossword/`** after first being written inside the tool. Reason: the adjacency invariant is the change's core correctness property, and a tool outside `Gewu.slnx` cannot be unit-tested. This follows the precedent `IdiomImporter` set by delegating tiering to `IdiomTiering` — "the tool must never own a second copy of the rule". The tool now holds only I/O, the difficulty ladder, and JSON emission. Verified the refactor changed nothing: regenerating produced a byte-identical artefact.
- [x] 7.2 Task 3.2 said to read the dictionary through `IIdiomRepository`; it reads `data/idioms.curated.json` instead. 31k idioms fit in memory, the artefact is fixed (so determinism is stronger), and a tool that needs no running SQLite still works a year from now. The reverse index is built in-memory from the same data.
- [x] 7.3 **Corpus is 1,171 idioms, not the 4,111 stated in an earlier chat summary.** Tier 1 is 1,171 (3.8%) — the archived `add-idiom-dictionary` design recorded this correctly; the larger number was a reporting error. 1,171 tier-1 four-character idioms with explanations was enough for all 12 levels with zero audit rejections.
- [x] 7.4 12 levels emitted, 0 rejected, ladder from 2 idioms / 4×4 up to 12 idioms / 12×10.
- [x] 7.5 Backend tests 477 → 533 (+56: 51 crossword Domain tests, 5 seeder Infrastructure tests). The 51 split as 9 grid-invariant, 9 audit-rejection, 12 generator/determinism, 21 rules behaviour.
- [x] 7.6 `CheckPartial`'s payload is forwarded **only** on a correct verdict. The lifecycle test's fake rules deliberately returns a payload on the *wrong* branch too, so the test proves the handler discards it rather than trusting rules implementations to behave.
- [x] 7.7 Switched the rules' JSON encoder to `UnsafeRelaxedJsonEscaping` after the first E2E showed payloads coming back as `合而...`. Functionally fine but it inflates every response and makes logs unreadable; the generator already made the same choice.
- [x] 7.8 Known wart, inherent to the opaque-payload design: `payloadJson` is a JSON string *inside* a JSON response, so the web client must parse twice. Consistent with how `partialJson` / `submissionJson` go in — the platform cannot embed game payloads as objects because it does not understand them. Worth a helper on the web side.
- [x] 7.9 Full E2E against the live API confirmed: 12 levels listed with only level 0 unlocked; `klotski` still 404; level detail carries no answer (`合而为一` and `solution` both absent); wrong check → mistakes=1 with null payload; correct check → word + explanation; hint → `(0,1)='而'`; submit → 2 stars (cost = 1 mistake + 1 hint), newBest; re-submit → 409; progress → unlocked=1, stars=2; level 1 then unlocked with level 0 at bestStars=2.
- [x] 7.10 Second boot re-verified idempotent: zero "Seeded" log lines, levels still 12, idioms still 30,895.
