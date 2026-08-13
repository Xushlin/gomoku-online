## Why

Three of the eight games in the registry are idiom games (成语纵横, 成语接龙, 猜成语) and none of them can start without a 成语 dataset in the database. They need different things from it — crossword generation needs "which idioms contain character X at position i", chain needs "which idioms start with character X", guess needs "an idiom plus its explanation" — but all three need the same table, so it is built once, here, with no gameplay attached.

The second reason to isolate this change: **curation is the product risk, not the import.** [chinese-xinhua](https://github.com/pwxcoo/chinese-xinhua) (MIT) ships 31,648 idioms, and the large majority are obscure classical entries nobody would recognise. A crossword generated from the raw set is not a hard puzzle, it is an unfair one. Getting the "which idioms are fair game" question wrong poisons all three games, so it gets its own change, its own reviewable data artefact, and no deadline pressure from a half-built game waiting on it.

## What Changes

### Curated data artefact (committed)

- A one-off fetch-and-transform step turns the upstream `idiom.json` into **`backend/data/idioms.curated.json`**, which is committed to the repo.
- Committing the derived artefact rather than fetching at build time means: the database is reproducible from a clean checkout with no network access, CI needs no new step, and the curation decisions are reviewable in a diff. Upstream is MIT; attribution goes in a new `NOTICE` file and in `README`.
- The fetch step is **not** part of the build and is not run by CI. It is a documented, occasional developer action.

### Schema

- **`Idioms`** — `Id`, `Word` (unique), `Pinyin`, `Explanation`, `Derivation`, `Example`, `CharCount`, `Tier`, `TierOverride`.
- **`IdiomChars`** — `IdiomId`, `Position`, `Char`, indexed on `(Char, Position)` and on `(Position, Char)`. This is the reverse index every crossword generator query needs; without it, generation degenerates into a full scan per intersection.
- One migration creating both tables. **No `InsertData`** — 31k rows of seed data in a migration file would be unreviewable. Data arrives via an idempotent seeder (below).

### Tiering instead of a hard cut

The upstream data carries no frequency signal, so "is this idiom common?" cannot be answered exactly. Rather than guess once and bake it in, the importer computes a `Tier` (1 = safe for puzzles, 2 = plausible, 3 = obscure) from the signals that *do* exist — exactly four characters, non-empty `example`, non-empty `derivation`, and whether every character appears in the common-character set derived from upstream `word.json`.

`TierOverride` is a nullable column no importer ever writes, so hand-curation accumulates permanently instead of being flattened by the next re-import. Games query by tier, which turns "which idioms are fair" from a code decision into tunable data.

### Seeder

- On startup, if `Idioms` is empty, load `idioms.curated.json`. Idempotent, keyed on `Word`, and a no-op on every subsequent boot.
- Runs for dev and test databases alike, so a fresh clone plus `dotnet run` yields a populated dictionary — the same property `AddBotSupport` gives the bot accounts today.

### Query surface (`Application`)

`IIdiomRepository` with exactly the three reads the three games need, plus one for the importer's own verification:

- `FindByWordAsync(word)` — chain validation ("is this a real idiom?").
- `FindContainingCharAsync(char, position, maxTier)` — crossword generation.
- `FindStartingWithCharAsync(char, maxTier)` — chain candidate lookup.
- `GetRandomAsync(maxTier, count)` — guess-the-idiom question selection.

No HTTP endpoint. Nothing in this change is reachable from the API surface — the games that expose it come later.

### Out of scope

- **All gameplay.** No levels, no generation, no puzzle context, no endpoints. `add-puzzle-core` and `add-idiom-crossword` follow.
- **`ci.json` (264k 词语) and `word.json` beyond the character-frequency use above.** No game needs them yet; importing 264k rows to be unused is cost without benefit.
- **`xiehouyu.json` (14,032 歇后语, fields `riddle` / `answer`).** It is the natural data source for a 猜歇后语 mode and is deliberately deferred — 猜成语 is served by `Explanation` on the table this change creates, and a second content type should arrive with the game that uses it.
- **Deciding the exact tier thresholds.** The importer computes and reports the distribution; picking cut-offs is a review conversation over real numbers, done during implementation, not guessed here.

## Capabilities

### New Capabilities

- `idiom-dictionary`: the curated artefact and its provenance, the `Idioms` / `IdiomChars` schema and its indexes, the tiering rules and the `TierOverride` guarantee, the idempotent seeder, and the `IIdiomRepository` read contract.

### Modified Capabilities

(none.) Additive schema, additive files, no existing behaviour touched.

## Impact

- **New**: `backend/data/idioms.curated.json`, `NOTICE`, `Gewu.Domain/Idioms/`, `Gewu.Application/Abstractions/IIdiomRepository.cs`, `Gewu.Infrastructure/Persistence/{Configurations,Repositories}` entries, one migration, a seeder, and an importer tool under `backend/tools/IdiomImporter/` (not referenced by `Gewu.Api`).
- **Migration**: one, schema-only.
- **Database**: two new tables. `Idioms` at ~31k rows and `IdiomChars` at ~126k rows is small for SQLite; the reverse index is what keeps generation queries sub-millisecond.
- **Startup**: one `COUNT(*)` on `Idioms` per boot, plus a one-time bulk insert on a fresh database.
- **API surface**: unchanged. No endpoint, no DTO, no hub method.
- **Tests**: tiering is a pure function and gets table-driven unit tests; the seeder gets an idempotency test (seed twice, row count unchanged); `IIdiomRepository` implementations get integration tests against SQLite for each of the four reads.

## Blocked on

The first implementation step downloads `idiom.json` and `word.json` (~10 MB combined) from the upstream GitHub repository. **That fetch needs Michael's explicit go-ahead** — nothing else in this change is blocked, but the artefact cannot be produced without it. Options if the answer is no: point the importer at a local copy, or vendor a smaller hand-picked seed set and defer the full import.
