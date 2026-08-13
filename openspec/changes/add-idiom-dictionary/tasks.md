## 0. Blocked — needs Michael's go-ahead before anything else

- [x] 0.1 **Get explicit approval to download** `idiom.json` and `word.json` (~10 MB combined) from `https://github.com/pwxcoo/chinese-xinhua`. Every task below depends on this; nothing else in the change is blocked. If the answer is no, the fallbacks are: point the importer at a local copy Michael provides, or hand-pick a few hundred idioms as a starter artefact and defer the full import.
- [x] 0.2 Record the upstream commit SHA that the download came from — it goes into the artefact header.

## 1. Importer tool

- [x] 1.1 Create `backend/tools/IdiomImporter/` as a console project. Do NOT add it to `Gewu.slnx`'s API dependency graph and do NOT reference it from `Gewu.Api`.
- [x] 1.2 Parse upstream `idiom.json` into records: `word`, `pinyin`, `explanation`, `derivation`, `example`. Ignore `abbreviation` (no consumer yet).
- [~] 1.3 **Superseded — see 7.1.** `word.json` was downloaded and inspected, then not used: at 16,142 characters it covers effectively the whole language, so intersecting idiom characters with it excludes almost nothing. Replaced by `MinCharFrequency`, computed from the idiom corpus itself.
- [x] 1.4 Implement tiering as a **pure function** — no clock, no randomness. Signals as built (revised per 7.1): `CharCount == 4`, `example` present *excluding the `"无"` sentinel*, `derivation` present likewise, and `MinCharFrequency`. Lives in `Domain`, not in the importer.
- [x] 1.5 Print the tier distribution plus a deterministic sample of 20 entries per tier. **Did not stop for review** — Michael had asked for the change to be carried through, so thresholds were chosen against the sampled output and the reasoning plus the residual error rate reported instead (7.3). Thresholds are tunable data and `TierOverride` makes a wrong call cheap to correct, so this was judged a reversible decision rather than a gate.
- [x] 1.6 Emit `backend/data/idioms.curated.json` with a header block naming the upstream repo URL, its commit SHA, the generation date, and the thresholds used. Never emit `tierOverride`.
- [x] 1.7 Create `NOTICE` at the repo root crediting chinese-xinhua under MIT, and add a data-credit line to both READMEs.

## 2. Domain

- [x] 2.1 Add `Gewu.Domain/Idioms/Idiom.cs` — `Word`, `Pinyin`, `Explanation`, `Derivation`, `Example`, `CharCount`, `Tier`, `TierOverride`, plus an `EffectiveTier` computed member returning `TierOverride ?? Tier`. XML `<summary>` on every public member per project rules.
- [x] 2.2 Add `Gewu.Domain/Idioms/IdiomChar.cs` — `IdiomId`, `Position`, `Char`. Created only through the `Idiom` aggregate so word and characters cannot disagree.
- [x] 2.3 Add `Gewu.Domain/Idioms/IdiomTier.cs` for the tier values, and unit-test the `EffectiveTier` precedence (override wins; null override falls back).

## 3. Application

- [x] 3.1 Add `Gewu.Application/Abstractions/IIdiomRepository.cs` with exactly the four methods from the spec. No `IQueryable`, no expression trees.
- [x] 3.2 Table-driven unit tests for the tiering function covering: 4-char with example+derivation+common chars (tier 1), missing example, missing derivation, a rare character, and a non-4-char idiom.

## 4. Infrastructure

- [x] 4.1 EF configurations for both entities: unique index on `Idioms.Word`; `IdiomChars` cascade-delete from `Idioms`; both `(Char, Position)` and `(Position, Char)` indexes.
- [x] 4.2 Add `DbSet<Idiom>` / `DbSet<IdiomChar>` to `AppDbContext`.
- [x] 4.3 Generate one migration, `AddIdiomDictionary`. Verify by reading it that it contains **no** `InsertData` / `HasData` idiom rows — schema and indexes only.
- [x] 4.4 Implement `IdiomRepository`. Each method must be a single index-backed query; filter on `COALESCE(TierOverride, Tier) <= maxTier`.
- [x] 4.5 Implement the seeder: on startup, if `Idioms` is empty, bulk-load the artefact and derive `IdiomChars` from each `Word`. Key idempotency on `Word`.
- [x] 4.6 Wire the seeder into startup next to the existing migration-apply step. Confirm the ordering: migrations first, then seed.

## 5. Tests

- [x] 5.1 Seeder idempotency: seed twice against SQLite, assert both `Idioms` and `IdiomChars` row counts are unchanged the second time.
- [x] 5.2 Seeder correctness: a 4-character idiom yields exactly 4 `IdiomChars` rows with positions 0–3 matching the word character by character.
- [x] 5.3 Repository integration tests against SQLite for all four reads, including tier filtering and the `FindByWordAsync` miss returning `null`.
- [x] 5.4 Assert `TierOverride` survives: seed, set an override by hand, re-run the seeder, confirm the override is intact.
- [x] 5.5 Assert the dictionary is unreachable: no controller or hub method added by this change touches `Idioms`.

## 6. Verification

- [x] 6.1 `dotnet build Gewu.slnx` and `dotnet test Gewu.slnx` green; test count up by the new tests and no existing test changed.
- [x] 6.2 Delete the local `gewu.db`, boot once: migrations apply, the seeder populates, row counts match the artefact. Boot a second time: row counts unchanged.
- [x] 6.3 Sanity-check query performance on the populated DB — `FindContainingCharAsync` for a common character returns in single-digit milliseconds. If it does not, the index is not being used.
- [x] 6.4 Confirm no new HTTP surface: `git diff` touches no controller and no hub.

## 7. Notes from implementation

- [x] 7.1 **Two of the four proposed tier signals were worthless, and only the real file showed it.** Upstream encodes absence as the string `"无"`, not as empty — 19,208 of 30,895 idioms have `example: "无"` — so "non-empty example" would have been true for essentially every row. And `word.json` is a 16,142-character dictionary covering effectively the whole language, so "all characters are common" excludes almost nothing. Both were replaced by `MinCharFrequency`, a document-frequency proxy computed from the corpus itself. `design.md` D3 and the spec were rewritten before any code was written; a `HasContent` test pins the `"无"` behaviour so it cannot silently regress.
- [x] 7.2 Upstream has **30,895** idioms, not the 31,648 its README advertises. Last upstream commit is `fe6d6c2` (2019-01-17) — the repo is dormant, which helps reproducibility.
- [x] 7.3 Thresholds chosen by sampling and reading the results, not by picking round numbers. Final: tier 1 = 1,171 (3.8%), tier 2 = 11,409 (36.9%), tier 3 = 18,315 (59.3%). **Roughly a fifth of tier 1 still reads as obscure**, and recall is worse than precision — `岌岌可危` and `草船借箭` both land in tier 3 because they contain a low-frequency character. Harmless by construction: tier 3 is excluded from generation but still valid for chain validation. `TierOverride` plus playtesting is the fix.
- [x] 7.4 Artefact is **5.93 MB**, not the 1–3 MB `design.md` guessed. Two deliberate choices drive that: all 30,895 words are kept (dropping tier 3 would make 成语接龙 reject legitimate answers), and the file is one JSON object per line sorted by word so a re-import diffs down to "which idioms changed tier" — which is the whole point of committing it. Prose is retained for tiers 1–2 only, which saved ~2.3 MB.
- [x] 7.5 The artefact carries `tier`, and the seeder **recomputes it with `IdiomTiering.Classify` and throws on mismatch**. This was not in the plan; it keeps the artefact diff-reviewable while leaving exactly one source of truth for tiering. Covered by a test that feeds a deliberately wrong tier.
- [x] 7.6 Tiering lives in `Domain` (`IdiomTiering`), not in the importer as `tasks.md` 3.2 implied, and takes `MinCharFrequency` as a parameter. So the importer and the seeder run the same table-tested function instead of holding two copies of the rule. The importer references `Gewu.Domain` for exactly this.
- [x] 7.7 Added `MinCharFrequency` as a stored column (not in the original schema list) so "why is this one tier 3?" is a query rather than a re-import.
- [x] 7.8 `Idiom.Id` is an `int` identity rather than the strongly-typed `Guid` the aggregates use. It is reference data with no distributed-id concern, and `IdiomChars` has ~127k rows pointing at it.
- [x] 7.9 **New test project `Gewu.Infrastructure.Tests`**, registered in `Gewu.slnx`. `CLAUDE.md` only anticipated `Gewu.Api.Tests`; repository and seeder tests are Infrastructure-level, and they run against real in-memory SQLite rather than EF's InMemory provider because what is under test *is* SQL behaviour — `COALESCE` filtering, `char` comparison, cross-table joins. The InMemory provider would pass these vacuously via LINQ-to-Objects.
- [x] 7.10 Backend tests 390 → 436 (+46: 29 Domain, 17 Infrastructure). `dotnet build` clean.
- [x] 7.11 Verified end to end: deleted `gewu.db`, booted → `Seeded 30895 idioms`, 127,576 character rows, tier split exactly matching the importer's report. Second boot logged no seeding and left both counts unchanged. `EXPLAIN QUERY PLAN` on the crossword lookup shows `SEARCH TABLE IdiomChars USING INDEX IX_IdiomChars_Position_Char` then a primary-key lookup — an index seek, no scan — returning 27 rows in 0.39 ms.
- [x] 7.12 Seeding is wired into the existing Development-only `db.Database.Migrate()` block, matching the current startup pattern. Non-Development environments need migrations and seeding run explicitly; there is no such environment yet.
