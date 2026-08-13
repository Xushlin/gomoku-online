## Why

This repository is becoming a multi-game platform named **格物 / Gewu** — idiom games (成语纵横 / 成语接龙 / 猜成语), plus 五子棋, 一字棋, 中国象棋, 华容道, 俄罗斯方块. Every assembly, namespace, and the `DbContext` is currently named `Gomoku.*`, which stops being merely inaccurate and starts being actively misleading the moment a second game lands (`Gomoku.Domain` holding idiom crossword levels).

Renaming is cheapest right now and gets more expensive every week:

- The working tree is clean and there are no other in-flight branches to conflict with a 250-file mechanical sweep.
- There is **no deployed instance** — no deploy pipeline in `.github/workflows/`, no Dockerfile, no `appsettings.Production.json`, and the only database is the gitignored local `gomoku.db`. Nothing downstream is pinned to these identifiers.
- Every subsequent platform change (game catalog, puzzle context, match generalization) then gets authored under the final names instead of being written twice.

This change is deliberately **behaviour-preserving**. It renames identifiers and nothing else, so the proof of correctness is "CI is green and no contract moved". Everything that would change observable behaviour has been split out into its own change (see *Out of scope*).

## What Changes

### Solution and projects

- `backend/Gomoku.slnx` → `backend/Gewu.slnx`.
- `Gomoku.{Domain,Application,Infrastructure,Api}` → `Gewu.{Domain,Application,Infrastructure,Api}` — directory, `.csproj` filename, `AssemblyName`, `RootNamespace`, and all `ProjectReference` paths. Layer dependency direction is unchanged.
- `Gomoku.{Domain,Application}.Tests` → `Gewu.{Domain,Application}.Tests`, re-registered in the renamed `.slnx`.
- `backend/smoke/AiSmoke` project references updated to the new paths.

### Namespace sweep

- `Gomoku.*` → `Gewu.*` across 187 source files and 68 test files (every file in `backend/src` references a `Gomoku.*` namespace, so the sweep is total). Includes `global using` declarations and the `using DomainMove = ...` / `using SubMove = ...` aliases in `Room.cs` / `GewuDbContext`.

### Persistence

- **`GomokuDbContext` → `AppDbContext`** — deliberately *not* `GewuDbContext`. The class name is referenced by all 6 existing migrations and their `.Designer.cs` files; a platform-neutral name means a future rename never touches migration history again. `GomokuDbContextModelSnapshot` → `AppDbContextModelSnapshot`.
- Migration files are renamed at the namespace level only. Their class names, migration IDs, and `Up`/`Down` bodies are untouched, so `__EFMigrationsHistory` stays valid and no new migration is generated.
- Connection string `Data Source=gomoku.db` → `Data Source=gewu.db`.

### Web

- `package.json` / `package-lock.json` / `angular.json` project name `gomoku-web` → `gewu-web` (including the three `buildTarget` references).
- `index.html` `<title>GomokuWeb` → `Gewu`.
- `shell/header/header.html` brand text — currently the hardcoded literal `Gomoku` — becomes the i18n key `header.brand` (zh-CN 「格物」 / en "Gewu"). This is the **one requirement-level change** in this proposal: the brand is now a language-dependent display string, and a hardcoded literal violates the project's "no hardcoded display strings in templates" rule.
- DTO doc comments referencing `Gomoku.Application.Common.DTOs` updated to `Gewu.*`.

### Docs, tooling, CI

- `CLAUDE.md` (20 occurrences), `README.md` (16), `README.zh-CN.md` (15), `start-dev.cmd` (5), `.github/` PR + issue templates.
- `.github/workflows/ci.yml` — three `dotnet` steps referencing `Gomoku.slnx`.
- Live spec tree: identifier-only rewrite in the 12 spec files that name `Gomoku.*` paths or types in prose. **No requirement semantics change** — see *Capabilities*.

### Out of scope — each deferred for a concrete reason

- **`GomokuHub` → `MatchHub`, `/hubs/gomoku` → `/hubs/match`.** The hub URL is normative in four specs (`api-ops`, `observability`, `room-and-gameplay`, `web-game-board`). Phase 2's `generalize-match-contract` has to write deltas for all four anyway when `MakeMove` becomes `SubmitMove`, so the rename rides along there for free instead of buying four delta files now.
- **`gomoku:*` localStorage keys** (`refresh`, `lang`, `theme`, `dark`, `sound-muted`, `sound-pack`, `board-skin`). Normative in five web specs (`web-auth`, `web-i18n`, `web-shell`, `web-sound`, `web-theming`), and renaming them silently logs the user out and resets every preference. Deferred to its own change with a one-time read-old-key/write-new-key shim.
- **Squashing the 6 migrations into a fresh baseline.** `ai-opponent` carries a requirement literally titled "`AddBotSupport` migration 插入 2 个 bot 账号" plus one for `AddHardBotAccount`, and `room-and-gameplay` has one for the `AddGameEndReason` backfill. A squash rewrites those requirements and must prove the three bot account GUIDs survive — that is a different proof obligation than "CI is green", so it becomes `squash-migration-baseline`.
- **Moving gomoku files into `Domain/Games/Gomoku/`.** Belongs with `generalize-match-domain`, which introduces the `IGameRules` registry that gives the folder its meaning.
- **Renaming the repository directory on disk** (`gomoku-online` → `gewu`). Manual, post-merge — it would invalidate the active worktree path and every IDE/tooling reference mid-change.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `web-shell` — two ADDED requirements: the header brand link renders the new i18n key `header.brand` instead of a hardcoded literal, and `header.brand` joins the bilingual parity set. The brand name is now language-dependent (格物 / Gewu), so it cannot stay a literal.
- `web-lobby` — one MODIFIED requirement. Its "模板零硬编码" scenario explicitly exempted the brand literal `Gomoku` from the no-hardcoded-strings scan. That exemption is now dead — the brand goes through i18n — so it is removed, making the requirement strictly stronger. (The carve-out was also misplaced: the brand lives in `shell/header/header.html`, which was never inside the globs that scenario scans.)

No other capability changes. In particular, twelve live spec files mention `Gomoku.*` namespaces, project paths, or `GomokuDbContext` inside otherwise unrelated requirement prose. Rewriting those identifiers changes **no** requirement semantics: no endpoint, wire contract, storage key, migration name, DB column, or observable behaviour moves. Authoring twelve delta spec files that are near-verbatim copies of the originals would bury the actual diff, so that sweep is applied directly to `openspec/specs/` as an identifier-only rewrite. `design.md` (D4) records this as a deliberate, bounded deviation from the delta flow, along with the mechanical check that keeps it honest.

## Impact

- **Backend**: all of `backend/src` (187 files) and `backend/tests` (68 files) — namespace lines and the `AppDbContext` rename; `Gewu.slnx`; 6 migration files + snapshot (namespace lines only); `backend/smoke/AiSmoke`.
- **Web**: `package.json`, `package-lock.json`, `angular.json`, `index.html`, `shell/header/header.html` + `header.spec.ts`, the new `header.brand` key in `public/i18n/{en,zh-CN}.json`, three `core/api/models/*.ts` doc comments.
- **Spec deltas**: `specs/web-shell/spec.md` (2 ADDED requirements). Identifier-only sweep across 12 live spec files, applied directly.
- **EF migration**: none. No schema change, no new migration, `__EFMigrationsHistory` untouched.
- **Database**: the connection-string filename changes, so the local dev DB is effectively new on first run. Verified there is no deployed instance and no data of value; `gomoku.db` can simply be deleted (or renamed to `gewu.db` to keep local test rooms).
- **Wire contracts**: unchanged. Same REST routes, same hub URL and method names, same JSON shapes, same localStorage keys, same JWT claims.
- **Bundle**: no meaningful change (one i18n key added).
- **Risk**: low but wide. The sweep is mechanical and the 68 existing test files are the safety net — a missed namespace fails at compile time, not at runtime. The one genuinely manual judgement is the `AppDbContext` rename inside the migration designers.
- **Ordering**: MUST land before `add-platform-catalog` and before any phase-1 idiom work, so no new code is written under the old names.
