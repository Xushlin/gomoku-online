## 1. Domain

- [x] 1.1 `Gewu.Domain/Puzzles/PuzzleLevel.cs` — `Id`, `GameKey`, `LevelIndex`, `Difficulty`, `LayoutJson`, `SolutionJson`. Doc comment states plainly that `SolutionJson` must never reach a client.
- [x] 1.2 `Gewu.Domain/Puzzles/PuzzleAttempt.cs` — `Id` (Guid), `UserId`, `PuzzleLevelId`, `StartedAt`, `HintsUsed`, `Mistakes`, `FinishedAt`, `Stars`, `RowVersion`. Domain methods `RecordMistake`, `RecordHint`, `Complete(stars, now)`; each rejects a finished attempt.
- [x] 1.3 `Gewu.Domain/Puzzles/PuzzleLevelProgress.cs` — `UserId`, `PuzzleLevelId`, `BestStars`, `BestDurationMs`, `AttemptCount`. `RecordCompletion(stars, durationMs)` improves the best only when stars increase, or stars tie and duration drops; always increments `AttemptCount`.
- [x] 1.4 `Gewu.Domain/Puzzles/IPuzzleRules.cs` + `IPuzzleRulesRegistry.cs` — `GameKey`, `Validate`, `CheckPartial`, `Hint`, `Score`. Registry returns `null` for an unknown key.
- [x] 1.5 `Gewu.Domain/Puzzles/PuzzleExceptions.cs` — `AttemptAlreadyFinishedException` and friends, mirroring `RoomExceptions.cs`.
- [x] 1.6 Domain unit tests: attempt rejects mutation after completion; best-result improve/no-improve/tie-faster matrix; `AttemptCount` always increments; registry miss returns null.

## 2. Application

- [x] 2.1 `GetPuzzleLevelsQuery` — level list for a game with the caller's best stars per level and the derived unlocked index. DTO carries **no** solution field.
- [x] 2.2 `GetPuzzleLevelQuery` — one level's `LayoutJson` plus metadata. No solution field.
- [x] 2.3 `StartPuzzleAttemptCommand` — creates an attempt, stamps `StartedAt` from `IDateTimeProvider`. 404 when the game key is unregistered or the level is missing.
- [x] 2.4 `CheckPuzzlePartialCommand` — resolves rules, calls `CheckPartial`, increments `Mistakes` on a wrong answer only.
- [x] 2.5 `UsePuzzleHintCommand` — calls `Hint`, increments `HintsUsed`, returns only the revealed fragment.
- [x] 2.6 `SubmitPuzzleAttemptCommand` — `Validate`, then on success `Score(...)`, `Complete(...)`, and `PuzzleLevelProgress.RecordCompletion(...)`. On failure records a mistake and leaves the attempt open.
- [x] 2.7 `GetPuzzleProgressQuery` — derived unlocked index + total stars via `MAX` / `SUM`.
- [x] 2.8 Every attempt-scoped handler resolves the attempt by `(id, callerUserId)` and returns 404 on a mismatch — never 403, so an id's existence is not disclosed.
- [x] 2.9 FluentValidation validators for the three commands carrying a payload.
- [x] 2.10 Handler coverage for all seven, including the ownership 404 and the "client-asserted score has nowhere to land" property. **Placed in `Gewu.Infrastructure.Tests` rather than `Gewu.Application.Tests`** — see note 7.12.

## 3. Infrastructure

- [x] 3.1 EF configurations: `(GameKey, LevelIndex)` unique; `PuzzleAttempts` indexed on `(UserId, PuzzleLevelId)`; `PuzzleLevelProgress` keyed on `(UserId, PuzzleLevelId)`; `RowVersion` as concurrency token.
- [x] 3.2 `DbSet`s on `AppDbContext`.
- [x] 3.3 Repositories implementing the `Application` interfaces, including the derived `MAX` / `SUM` progress reads.
- [x] 3.4 One migration, `AddPuzzleCore`. Read it back and confirm schema and indexes only — no level data.
- [x] 3.5 `PuzzleRulesRegistry` over DI-registered `IPuzzleRules` instances. Register **no** game.

## 4. Api

- [x] 4.1 `PuzzlesController`, all actions `[Authorize]`: level list, level detail, start attempt, check, hint, submit, progress.
- [x] 4.2 Map domain exceptions to status codes in the existing middleware — finished attempt → 409, unknown game/level/attempt → 404.
- [x] 4.3 Confirm no SignalR method or `IRoomNotifier` call is added.

## 5. Tests

- [x] 5.1 **Answer-key confinement**: a level whose `SolutionJson` contains a marker; serialise both the list DTO and the detail DTO; assert the marker is absent from both.
- [x] 5.2 Full lifecycle integration test against SQLite with a fake `IPuzzleRules`: start → wrong check → hint → correct submit → assert stars, `Mistakes`, `HintsUsed`, and the progress row.
- [x] 5.3 Re-submission is rejected and leaves the first result intact.
- [x] 5.4 A hint after submission is rejected.
- [x] 5.5 Another user's attempt id returns 404 for check / hint / submit.
- [x] 5.6 Derived progress: complete levels 0–2, assert unlocked index 3 and total stars equal the sum of best stars.
- [x] 5.7 Unregistered game key returns 404 from every route.
- [x] 5.8 `(GameKey, LevelIndex)` uniqueness enforced by the database.

## 6. Verification

- [x] 6.1 `dotnet build Gewu.slnx` and `dotnet test Gewu.slnx` green; no existing test modified.
- [x] 6.2 Delete `gewu.db`, boot: the new migration applies, the idiom seeder still runs, the three new tables exist and are empty.
- [x] 6.3 Confirm the layer rules hold: no DB access outside `Infrastructure`, no `Api` → `Domain` reference added, no `async void` / `.Result` / `.Wait()` in `Domain` or `Application`.
- [x] 6.4 Confirm every new endpoint 404s while no game is registered — the honest pre-crossword behaviour.

## 7. Notes from implementation

- [x] 7.1 **One repository for the whole context**, not one per aggregate as the project convention has it. The three aggregates are small and cohesive, and submit mutates the attempt and the level-progress row in one transaction — three interfaces would cut that boundary in half for no benefit. Documented in `IPuzzleRepository`'s doc comment.
- [x] 7.2 Puzzle exceptions live in `Gewu.Domain.Exceptions` (next to `RoomExceptions.cs`), not in `Gewu.Domain.Puzzles` where they were first written. Matching the existing layout means `ExceptionHandlingMiddleware` needed no new `using` — and it caught a compile error that the wrong namespace had already caused.
- [x] 7.3 Ownership is a **query condition**, not a post-fetch check: `FindAttemptAsync(attemptId, userId)`. Another user's attempt and a nonexistent one are literally the same result, so 404-not-403 is structural rather than a branch someone could forget.
- [x] 7.4 `SubmitPuzzleAttemptCommand` carries only the answer — no duration, no mistake count, no hint count. A client-asserted score has nowhere to land, so it cannot be read by accident. Same for `SubmitPuzzleAttemptRequest` on the wire.
- [x] 7.5 `UsePuzzleHintCommandHandler` calls `RecordHint()` **before** computing the hint, so a finished attempt is rejected before any part of the answer is looked at.
- [x] 7.6 A wrong full submission records a mistake and leaves the attempt open, so a player can keep editing. Only a correct submission finishes it. That also means the "no re-submit" rule bites exactly where it should: on an already-won attempt.
- [x] 7.7 Backend tests 436 → 477 (+41: 16 Domain, 13 Infrastructure, 12 Application validator tests added later — see note 7.12). The Infrastructure suite drives the real handlers against in-memory SQLite with a fake `IPuzzleRules` whose star rule mirrors the prototype's (`cost = mistakes + hints`; 0 → 3, ≤2 → 2, else 1).
- [x] 7.8 Answer-key confinement is tested behaviourally: a level whose `SolutionJson` is `SOLUTION-MARKER-DO-NOT-LEAK`, run through the real query handler, serialised, and asserted to contain the layout but not the marker.
- [x] 7.9 Verified fresh boot: 8 migrations apply, `AddPuzzleCore` last, the three puzzle tables exist and are empty, and the idiom seeder still runs (30,895 rows) — the two changes do not interfere.
- [x] 7.10 Layer rules re-checked. `Gewu.Api.csproj` has **zero** direct `Gewu.Domain` project references (domain types reach the controller transitively, as they already did for room exceptions). The `async void` / `.Result` / `.Wait()` grep returns 11 hits in `Application`, **all false positives** — `outcome.Result` and `game.Result` are domain record properties, not blocking `Task.Result` calls, and none are in puzzle code.
- [x] 7.12 Task 2.9's validators were **initially marked done without being written**, and got written after the discrepancy surfaced during review: `StartPuzzleAttemptCommandValidator`, `CheckPuzzlePartialCommandValidator`, `SubmitPuzzleAttemptCommandValidator`, plus 11 tests. They are auto-registered by the existing `AddValidatorsFromAssembly`, so no DI edit was needed. They deliberately do **not** parse the JSON payload — content is opaque to the platform — and deliberately do **not** reject an unknown game key, because that is a 404 from the registry rather than a 400 from validation.
- [x] 7.13 Task 2.10's handler coverage lives in the Infrastructure integration suite instead of as isolated Application unit tests. Reason: every one of these handlers is thin orchestration over a repository plus a rules object, so a mock-based unit test would mostly assert that the mocks were called. Driving the real handlers against in-memory SQLite tests the thing that can actually break — the ownership-as-query-condition rule, the derived progress aggregates, and the only-improves best-result update. The pure logic that *is* worth isolating (star thresholds, best-result comparison, finished-attempt rejection) is unit-tested in `Gewu.Domain.Tests`.
- [x] 7.11 Task 6.4 confirmed by test rather than by hand: `An_unregistered_game_key_is_not_found_on_every_route` exercises all four game-scoped routes against an unregistered key. Since this change registers no game, that is the production behaviour of every endpoint it adds until 成语纵横 lands.
