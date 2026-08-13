## Context

Eight games are registered; three of them are idiom games and none can begin without 成语 data. Upstream is [chinese-xinhua](https://github.com/pwxcoo/chinese-xinhua), MIT-licensed, offering `idiom.json` (31,648 entries — `word`, `pinyin`, `explanation`, `derivation`, `example`, `abbreviation`), `word.json` (16,142 characters with `strokes` / `radicals`), `ci.json` (264,434 词语) and `xiehouyu.json` (14,032, `riddle` / `answer`).

Constraints shaping this design:

- **No frequency data anywhere in the upstream set.** The single most important question for puzzle quality — is this idiom one a player could reasonably know? — cannot be answered directly from the data.
- **The three consumers want three different query shapes** over the same rows, and one of them (crossword generation) is a search that will run many times per generated level.
- **CI has no network access step today** and adding one would make the build depend on a third-party repository staying available.
- Layer rules: DB access only in `Infrastructure`, `Application` owns the interface, `Domain` has no outward dependencies.

## Goals / Non-Goals

**Goals:**

- A fresh clone plus `dotnet run` yields a populated, queryable idiom dictionary — no network, no manual step.
- Curation decisions are visible in code review and accumulate rather than being overwritten.
- Crossword generation's hot query is an index seek, not a scan.
- Zero API surface: nothing here is reachable by a client.

**Non-Goals:**

- Any gameplay, level generation, or endpoint.
- Importing `ci.json`, or `xiehouyu.json`'s riddles.
- Fixing the tier thresholds. The importer reports the distribution; the numbers are chosen against that report.

## Decisions

### D1: Commit the derived artefact, do not fetch at build time

The importer runs occasionally on a developer machine and writes `backend/data/idioms.curated.json`, which is committed. The build and CI never touch the network.

Three things this buys: the database is reproducible from a checkout, CI gains no new failure mode owned by a third party, and — most importantly — a change to curation shows up as a reviewable diff instead of as invisibly different behaviour after someone re-ran a script.

*Alternative considered:* fetch upstream during build or first run. Rejected — it makes every build depend on a GitHub repo's availability and makes "why did the puzzles change?" unanswerable from git history.

*Cost, stated plainly:* a ~1–3 MB JSON file in the repository, and an artefact that can drift from upstream without anyone noticing. Mitigated by recording the upstream commit SHA inside the artefact's header so the provenance of any row is traceable.

### D2: Schema-only migration; data via an idempotent seeder

`InsertData` with 31k rows produces a migration file nobody can review and that takes minutes to apply. Instead the migration creates the tables and a seeder loads the artefact when `Idioms` is empty.

This mirrors what the project already does for bot accounts — except bot accounts are three rows and legitimately live in a migration. The threshold between the two approaches is reviewability, and 31k rows is well past it.

*Consequence:* the database is no longer fully reproducible from migrations alone; it is reproducible from migrations plus a committed data file. This is a deliberate weakening of the "migrations are the schema truth" property and is why the seeder must be idempotent and keyed on `Word` rather than on row identity.

### D3: Tier, plus a `TierOverride` the importer never writes

`Tier` is computed from the four available signals: exactly four characters, non-empty `example`, non-empty `derivation`, and whether every character of the idiom appears in the common-character set derived from `word.json`. None of these is a frequency measure, and the design does not pretend otherwise — they are proxies, and the tier is a *hypothesis about difficulty*, not a fact.

`TierOverride` is nullable and the importer is forbidden from writing it. Every consumer reads `COALESCE(TierOverride, Tier)`. Hand-curation therefore survives re-imports permanently, which is the only way manual review effort compounds instead of evaporating.

*Alternative considered:* filter at import time and store only the good idioms. Rejected — it destroys information, makes "was this excluded on purpose?" unanswerable, and means every threshold change requires a re-import instead of a query change.

### D4: `IdiomChars` as a reverse index rather than `LIKE` queries

Crossword generation asks "which idioms have 山 at position 2?" repeatedly while placing intersections. Answering that with `WHERE Word LIKE '_山__'` is a table scan per intersection. A row per character with an index on `(Char, Position)` turns it into a seek, at the cost of ~126k narrow rows — trivial for SQLite and equally correct on SQL Server later.

Both `(Char, Position)` and `(Position, Char)` indexes exist because the chain game asks the mirror question (position fixed at 0, character varying) and the two access patterns favour opposite column orders.

### D5: Four repository methods, no general-purpose query API

`IIdiomRepository` exposes exactly what the three games need. No `IQueryable`, no filter object, no "search". Each method is one index-backed query with an explicit `maxTier`.

The temptation is a flexible query surface, on the grounds that three more games are coming. Rejected: a narrow interface keeps the layer boundary honest (no LINQ trees leaking out of `Infrastructure`), and each future need is one small addition whose access pattern gets thought about deliberately rather than absorbed into a generic method that quietly scans.

## Risks / Trade-offs

- **[Tier proxies do not actually track familiarity]** → The real mitigation is `TierOverride` plus playtesting, not cleverer heuristics. The importer prints the tier distribution and a random sample per tier so the thresholds are chosen against evidence. Accepted: the first generated levels will contain some unfair idioms, and fixing them is data entry, not code.
- **[Committed artefact drifts from upstream]** → Upstream commit SHA is recorded in the artefact header; refreshing is a deliberate, reviewable diff.
- **[Seeder makes a fresh DB slower to boot]** → One bulk insert on an empty database only; a `COUNT(*)` on every later boot.
- **[Vendoring MIT data]** → Permitted with attribution. `NOTICE` plus a README credit; the artefact header names the source repository and commit.
- **[31k idioms is a lot of Chinese text in a repo]** → Only the curated projection is committed, and only the fields the games read.

## Migration Plan

1. Get the go-ahead for the upstream fetch (this is the one blocking step).
2. Run the importer, review the printed tier distribution and samples, pick thresholds, commit the artefact.
3. Apply the schema migration; boot once and confirm the seeder populates and is idempotent on the second boot.

Rollback: revert the commit and drop the two tables. Nothing else reads them.

## Open Questions

- **Tier thresholds.** Deliberately deferred to implementation, against the importer's report.
- **Whether 猜成语 should use `Explanation` or `Derivation` as its prompt.** A game-design question for `add-idiom-guess`; both columns are stored so either works.
- **Traditional characters.** Upstream is simplified only. If the platform ever wants a `zh-TW` locale this becomes a real conversion problem; nothing here forecloses it.
