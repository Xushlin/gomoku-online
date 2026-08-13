## Why

`add-idiom-dictionary` put 成语 data in the database. Nothing can play it yet: the platform has a match context (rooms, seats, turns, ELO) and nothing at all for the *single-player levels* category, which covers three of the eight registered games — 成语纵横, 华容道, 猜成语.

The reason this is its own change rather than part of 成语纵横: Michael asked for a leaderboard from day one, and a ranked puzzle game cannot ship the answer key to the client. That single requirement decides the whole shape of the context — where validation runs, what the client is allowed to know, and which signals a star rating may be computed from. Getting it right once, with no game attached, is much cheaper than retrofitting it under a finished crossword.

## What Changes

### The client never receives an answer

- `PuzzleLevel` stores `LayoutJson` (sent to the client) and `SolutionJson` (**never** leaves the server). The read DTO has no field that could carry it.
- Validation, hints, and scoring all run server-side against `SolutionJson`.
- A test asserts the level DTO cannot serialise a solution — the guarantee is enforced, not documented.

### Attempt lifecycle

`POST /api/games/{gameKey}/levels/{index}/attempts` → start (server stamps `StartedAt`)
`POST /api/puzzle-attempts/{id}/check` → validate a *partial* answer; server counts mistakes
`POST /api/puzzle-attempts/{id}/hint` → server reveals one piece and increments `HintsUsed`
`POST /api/puzzle-attempts/{id}/submit` → validate the full answer, award stars, record best result

`check` exists because of the answer-key rule: the prototype validates each idiom the moment its cells are full, and the client cannot do that without the answers. Making it a server call both preserves that feel and makes the mistake count **trustworthy** — mistakes become a server-observed signal rather than a number the client reports about itself.

### Scoring uses only server-observable signals

Stars are computed from `HintsUsed`, `Mistakes`, and the server-measured duration. All three are produced by the server: hints are issued by it, mistakes are counted by it during `check`, and the clock is its own. Nothing the client asserts about its own performance is trusted.

The star *formula* is per-game (`IPuzzleRules.Score`), because 华容道 scores on moves and 成语纵横 on mistakes and hints.

### `IPuzzleRules` registry

Domain-side abstraction keyed by `GameKey`, resolved by DI — the same registry shape already used for themes, board skins, sound packs, and the game catalogue:

- `Validate(solution, submission)` — full answer.
- `CheckPartial(solution, partial)` — one word / one region.
- `Hint(solution, layout, alreadyRevealed)` — the next piece to reveal.
- `Score(hintsUsed, mistakes, duration)` — 1–3 stars.

Adding a puzzle game is one rules class plus one registration. **No game is registered by this change** — the registry rejects unknown keys with a 404, and the tests exercise it through a fake.

### Progress is derived, not stored

Three tables: `PuzzleLevels`, `PuzzleAttempts`, `PuzzleLevelProgress` (best stars and best duration per user per level).

There is deliberately **no** `HighestUnlockedIndex` column: it is `MAX(completed LevelIndex) + 1`, and total stars is `SUM(BestStars)`. Both are queries. Denormalised progress counters are the kind of state that drifts from the attempts that produced it, and nothing here needs the write-time saving.

### Out of scope

- **Leaderboards.** They read what this change produces, so they can only be built after it. `add-puzzle-leaderboard` follows. The expensive half — making the underlying numbers trustworthy — is delivered here.
- **Any concrete game.** No crossword, no level generator, no level data. `add-idiom-crossword` is next.
- **Level authoring tooling.** Levels arrive as data; how they are produced is each game's problem.

## Capabilities

### New Capabilities

- `puzzle-core`: the level / attempt / progress model, the answer-key confinement rule, the attempt lifecycle and its REST contract, the `IPuzzleRules` registry, and the server-observable-signals-only scoring rule.

### Modified Capabilities

(none.) Additive: new tables, new endpoints under new routes, no change to any existing behaviour, contract, or table.

## Impact

- **New**: `Gewu.Domain/Puzzles/` (level, attempt, level-progress, `IPuzzleRules`, star result), `Gewu.Application/Features/Puzzles/` (7 handlers), `Gewu.Infrastructure` configs + repositories, one migration, `PuzzlesController`, `Gewu.Application.Tests` + `Gewu.Infrastructure.Tests` additions.
- **Migration**: one, schema-only. Three tables.
- **API**: five new endpoints, all `[Authorize]`. No existing route touched.
- **Web**: none. This change is backend-only; the client arrives with 成语纵横.
- **Rate limiting**: `check` is called once per completed word, so it is the chattiest authenticated endpoint on the platform. It goes in the existing per-user bucket and the proposal notes it as the first candidate for a tighter one if abuse shows up.
- **Tests**: rules-registry resolution, star scoring, answer-key confinement, the full attempt lifecycle, idempotent re-submission, best-result-only-improves, and that a second attempt cannot reuse a finished attempt id.
