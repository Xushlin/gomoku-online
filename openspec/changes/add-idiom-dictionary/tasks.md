## 0. Blocked — needs Michael's go-ahead before anything else

- [ ] 0.1 **Get explicit approval to download** `idiom.json` and `word.json` (~10 MB combined) from `https://github.com/pwxcoo/chinese-xinhua`. Every task below depends on this; nothing else in the change is blocked. If the answer is no, the fallbacks are: point the importer at a local copy Michael provides, or hand-pick a few hundred idioms as a starter artefact and defer the full import.
- [ ] 0.2 Record the upstream commit SHA that the download came from — it goes into the artefact header.

## 1. Importer tool

- [ ] 1.1 Create `backend/tools/IdiomImporter/` as a console project. Do NOT add it to `Gewu.slnx`'s API dependency graph and do NOT reference it from `Gewu.Api`.
- [ ] 1.2 Parse upstream `idiom.json` into records: `word`, `pinyin`, `explanation`, `derivation`, `example`. Ignore `abbreviation` (no consumer yet).
- [ ] 1.3 Derive the common-character set from upstream `word.json` (16,142 entries) and expose it to the tiering function.
- [ ] 1.4 Implement tiering as a **pure function** — no clock, no randomness — over the four signals: `CharCount == 4`, non-empty `example`, non-empty `derivation`, all characters in the common set.
- [ ] 1.5 Print the tier distribution plus a random sample of ~20 entries per tier. **Stop here and review with Michael** before fixing thresholds; the numbers decide, not the guess in `design.md`.
- [ ] 1.6 Emit `backend/data/idioms.curated.json` with a header block naming the upstream repo URL, its commit SHA, the generation date, and the thresholds used. Never emit `tierOverride`.
- [ ] 1.7 Create `NOTICE` at the repo root crediting chinese-xinhua under MIT, and add a data-credit line to both READMEs.

## 2. Domain

- [ ] 2.1 Add `Gewu.Domain/Idioms/Idiom.cs` — `Word`, `Pinyin`, `Explanation`, `Derivation`, `Example`, `CharCount`, `Tier`, `TierOverride`, plus an `EffectiveTier` computed member returning `TierOverride ?? Tier`. XML `<summary>` on every public member per project rules.
- [ ] 2.2 Add `Gewu.Domain/Idioms/IdiomChar.cs` — `IdiomId`, `Position`, `Char`. Created only through the `Idiom` aggregate so word and characters cannot disagree.
- [ ] 2.3 Add `Gewu.Domain/Idioms/IdiomTier.cs` for the tier values, and unit-test the `EffectiveTier` precedence (override wins; null override falls back).

## 3. Application

- [ ] 3.1 Add `Gewu.Application/Abstractions/IIdiomRepository.cs` with exactly the four methods from the spec. No `IQueryable`, no expression trees.
- [ ] 3.2 Table-driven unit tests for the tiering function covering: 4-char with example+derivation+common chars (tier 1), missing example, missing derivation, a rare character, and a non-4-char idiom.

## 4. Infrastructure

- [ ] 4.1 EF configurations for both entities: unique index on `Idioms.Word`; `IdiomChars` cascade-delete from `Idioms`; both `(Char, Position)` and `(Position, Char)` indexes.
- [ ] 4.2 Add `DbSet<Idiom>` / `DbSet<IdiomChar>` to `AppDbContext`.
- [ ] 4.3 Generate one migration, `AddIdiomDictionary`. Verify by reading it that it contains **no** `InsertData` / `HasData` idiom rows — schema and indexes only.
- [ ] 4.4 Implement `IdiomRepository`. Each method must be a single index-backed query; filter on `COALESCE(TierOverride, Tier) <= maxTier`.
- [ ] 4.5 Implement the seeder: on startup, if `Idioms` is empty, bulk-load the artefact and derive `IdiomChars` from each `Word`. Key idempotency on `Word`.
- [ ] 4.6 Wire the seeder into startup next to the existing migration-apply step. Confirm the ordering: migrations first, then seed.

## 5. Tests

- [ ] 5.1 Seeder idempotency: seed twice against SQLite, assert both `Idioms` and `IdiomChars` row counts are unchanged the second time.
- [ ] 5.2 Seeder correctness: a 4-character idiom yields exactly 4 `IdiomChars` rows with positions 0–3 matching the word character by character.
- [ ] 5.3 Repository integration tests against SQLite for all four reads, including tier filtering and the `FindByWordAsync` miss returning `null`.
- [ ] 5.4 Assert `TierOverride` survives: seed, set an override by hand, re-run the seeder, confirm the override is intact.
- [ ] 5.5 Assert the dictionary is unreachable: no controller or hub method added by this change touches `Idioms`.

## 6. Verification

- [ ] 6.1 `dotnet build Gewu.slnx` and `dotnet test Gewu.slnx` green; test count up by the new tests and no existing test changed.
- [ ] 6.2 Delete the local `gewu.db`, boot once: migrations apply, the seeder populates, row counts match the artefact. Boot a second time: row counts unchanged.
- [ ] 6.3 Sanity-check query performance on the populated DB — `FindContainingCharAsync` for a common character returns in single-digit milliseconds. If it does not, the index is not being used.
- [ ] 6.4 Confirm no new HTTP surface: `git diff` touches no controller and no hub.
