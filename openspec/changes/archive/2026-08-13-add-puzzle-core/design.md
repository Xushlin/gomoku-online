## Context

The platform has one bounded context — matches — built around a `Room` with two seats, turns, spectators, chat, and ELO. Three registered games (成语纵横, 华容道, 猜成语) have none of those things: one player, no opponent, no turn order, no realtime, and a *level catalogue* instead of a match history. Forcing them through `Room` would turn it into a god aggregate, which the platform plan explicitly rules out.

The constraint that shapes everything here: **the leaderboard is in scope for 成语纵横**, so the client must not hold the answers. The `chengyu-crossword.html` prototype ships `grid[k].target` — the answer — to the browser, which is fine for a single-player demo and fatal for a ranked one.

Existing patterns reused rather than reinvented: an abstraction keyed by a string with DI-resolved implementations (themes, board skins, sound packs, the game catalogue, and the planned `IGameRules`); MediatR one-handler-per-file; FluentValidation on commands; optimistic concurrency via a domain-managed `RowVersion`.

## Goals / Non-Goals

**Goals:**

- No code path can send `SolutionJson` to a client.
- Every number a star rating depends on is produced by the server.
- Adding a puzzle game is one rules class plus one registration.
- The three games' wildly different level shapes (a character grid, a sliding-block layout, a definition prompt) all fit without schema changes.

**Non-Goals:**

- Leaderboards (next change), any concrete game, level generation, level authoring tools.
- Realtime anything. Puzzles are REST; a puzzle route must never open a hub connection.
- Cross-game unified scoring. Puzzle stars, ELO, and score-attack ladders stay separate on purpose.

## Decisions

### D1: `SolutionJson` is confined by the DTO, not by discipline

`PuzzleLevel` holds both `LayoutJson` and `SolutionJson`. The read model (`PuzzleLevelDto`) has no solution-shaped field at all, so leaking it requires *adding* a property rather than forgetting to remove one. A test serialises the DTO for a level whose solution contains a recognisable marker and asserts the marker is absent from the JSON.

*Alternative considered:* a single `PayloadJson` with the game's rules deciding which subtree is public. Rejected — it puts the confinement guarantee inside per-game code, so every new game could re-break it. Two columns make it structural.

### D2: `check` is a server call, and that is what makes mistakes trustworthy

The prototype validates an idiom the instant its cells fill. Preserving that feel without the answer key means asking the server. The side benefit is the important one: the mistake count becomes something the server *observed* rather than something the client *claims*.

This is the difference between a leaderboard that means something and one that rewards whoever edits the most JavaScript. It costs one request per completed word — for a crossword that is 2–7 per level, not per keystroke.

*Alternative considered:* trust a client-reported mistake count on submit. Rejected outright: the only reason to report it is to affect the score, and lying always improves it.

*Alternative considered:* drop mistakes from scoring and rank on time and hints only, both trivially server-observable, and let the client validate locally. Rejected because it needs the answer key on the client to give per-word feedback at all — the feedback, not the scoring, is what forces the server call.

### D3: Scoring inputs are server-observable; the formula is per-game

`IPuzzleRules.Score(hintsUsed, mistakes, duration)` returns 1–3 stars. All three inputs are server-side facts. The formula varies — 华容道 will want moves and time, 成语纵横 mistakes and hints — so it belongs to the game, while the *guarantee about the inputs* belongs to the platform.

The prototype's rule (`cost = mistakes + hintsUsed`; 0 → 3, ≤2 → 2, else 1) ports directly and will land with the crossword.

### D4: Progress is derived from attempts, never stored

Unlocked-level index is `MAX(LevelIndex WHERE completed) + 1`; total stars is `SUM(BestStars)`. Neither gets a column.

Denormalised counters are exactly the kind of state that ends up disagreeing with the rows that produced it — a failed transaction, a manual fix, a bug in one of two write paths, and now "you have 14 stars" and the level list disagree. Two indexed aggregates on a table holding at most one row per user per level cost nothing at this scale, and the numbers cannot be wrong.

*Revisit when:* a user has thousands of levels per game, which no planned game approaches.

### D5: `PuzzleLevelProgress` records the best result only, and only improves

One row per `(UserId, PuzzleLevelId)` holding `BestStars`, `BestDurationMs`, and `AttemptCount`. A submit updates it only when the new result is better — more stars, or equal stars in less time. Replaying a level can therefore never *lower* a rating, which is the behaviour every level-based game has and which the leaderboard depends on to be stable.

`AttemptCount` increments on every completion regardless, because it is a statistic rather than a score.

### D6: The attempt is the unit of authority, and it is single-use

`PuzzleAttempt` carries `StartedAt`, `HintsUsed`, `Mistakes`, `FinishedAt`, `Stars`, and a `RowVersion`. `hint` / `check` / `submit` all mutate it through domain methods that reject a finished attempt, so a client cannot keep spending hints after submitting, re-submit for a better roll, or hold an attempt open across sessions to game the clock in reverse.

Duration is `FinishedAt - StartedAt`, both server clocks via `IDateTimeProvider`.

Deliberately **not** decided here: whether a stale abandoned attempt should expire. There is no evidence about real session lengths yet, and a wrong timeout is worse than none — an attempt left open simply produces a bad time, which only hurts its owner.

### D7: Unknown game keys 404 at the registry

`IPuzzleRulesRegistry.For(gameKey)` returns null for an unregistered key and the handler maps that to 404. Since this change registers no game at all, every route it adds returns 404 in production until 成语纵横 lands — which is the honest answer for "that game does not exist here".

Tests register a small fake rules implementation, so the lifecycle is exercised end to end without inventing a real game inside a platform change.

## Risks / Trade-offs

- **[`check` is the chattiest endpoint on the platform]** → 2–7 calls per level, not per keystroke. It shares the existing per-user rate-limit bucket; flagged as the first candidate for its own tighter bucket if abuse appears. Each call is one indexed read plus one counter increment.
- **[Every new endpoint 404s until a game registers]** → Intended (D7), and the alternative — shipping a toy game inside the platform change — is worse.
- **[Derived progress means two aggregates per level-list request]** → Measured against real data before the crossword ships; at one row per user per level this is not a plausible bottleneck.
- **[An abandoned attempt records a terrible duration]** → Only affects that user's own best-result comparison, and D5 means it cannot make anything worse than it already was.
- **[`SolutionJson` in the same table as `LayoutJson`]** → A single careless `SELECT *` projection into a DTO could leak it. Mitigated structurally by D1 plus the serialisation test; the repository returns the entity only to handlers, never to the API layer.

## Migration Plan

One schema-only migration adding three tables. No data, no seeding — levels arrive with the game that owns them. Rollback is dropping the three tables; nothing else references them.

## Open Questions

- **Should an unfinished attempt expire?** Deferred until there is session-length evidence (D6).
- **Should `check` be rate-limited separately?** Deferred until there is a reason; noted so it is not a surprise later.
- **Do hints need to be idempotent per position?** Currently each `hint` call costs one and reveals the next unrevealed piece. Whether re-requesting an already-revealed piece should be free is a game-design question that the crossword will answer.
