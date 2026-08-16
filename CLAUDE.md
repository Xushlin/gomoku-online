# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**格物 / Gewu** — a multi-platform online game hall. Planned games: idiom games (成语纵横 / 成语接龙 / 猜成语), 五子棋, 一字棋, 中国象棋, 华容道, 俄罗斯方块. 「格」 means grid cell, which is what they all have in common.

Three games ship today, and between them they establish the two kernels every later game reuses:

- **五子棋 (gomoku)** — the *match* kernel: players register, create/join rooms, play real-time matches (via SignalR) with room chat, spectator chat, and urge-opponent shortcuts; ELO-based ranking with special icons for the top three; human-vs-AI with multiple difficulties; game-record storage and replay.
- **成语纵横 (idiom-crossword)** — the *puzzle* kernel: a level catalogue, server-authoritative attempts (the answer key never leaves the server), server-counted mistakes and hints, star scoring, and per-level best records.
- **一字棋 (tictactoe)** — the match kernel's **proof**, not an extension of it. Its entire rule set is `NInARowRules("tictactoe", 3, 3, 3)`; it contributed zero lines of win detection. Human-vs-AI only, and therefore **unrated**: with no human-vs-human mode its only opponents are bots, bot games are rated, so a ladder over it would rank Easy-bot grinding rather than skill. That is now enforced by the invariant `IsRated ⇒ SupportsHumanVsHuman` rather than left to a comment. See the `add-tictactoe` audit for what adding the game revealed about the registry.

Games fall into three categories that deliberately do **not** share one aggregate — see the platform roadmap below:

| Category | Games | Realtime | Core concepts |
| --- | --- | --- | --- |
| Turn-based adversarial | 五子棋, 一字棋, 中国象棋, 成语接龙 | SignalR | room, two seats, turn order, move sequence, ELO, spectators, replay |
| Single-player levels | 成语纵横, 华容道, 猜成语 | none (REST) | level catalogue, progress, stars, hints, time leaderboard |
| Single-player score-attack | 俄罗斯方块 | none (submit at end) | run record, score validation, periodic leaderboard |

## Current phase

**Three games ship**: 五子棋 (the original), 成语纵横 (the first puzzle game), and 一字棋 (the change that priced what a second board game costs). Detail:

- [x] 4-layer Clean Architecture solution skeleton (`backend/Gewu.slnx`)
- [x] OpenSpec initialized (`openspec/config.yaml`); each shipped change is archived under `openspec/changes/archive/<date>-<name>/`
- [x] **Backend MVP** — auth, rooms, gameplay, AI (Easy / Medium / Hard, with side-picker), ELO, replay, presence, observability, rate limiting. Live specs in `openspec/specs/`.
- [x] **Web client v1** (`frontend-web/`) — Angular 21, Tailwind v4, Material/CDK, Transloco (`zh-CN` + `en`). Auth pages, lobby, real-time game board, replay player, public profiles, find-player search, AI room creation with side-picker, sound effects (Wood + Chiptune packs), board skins (Wood + Classic), themes (Material + System + Ink) × dark/light, presence dots.
- [x] GitHub Actions CI runs on every push and PR (`backend` + `web` jobs in parallel).
- [x] **`add-platform-catalog`** — `GameManifest` registry (`src/app/games/index.ts`) + the `/games` catalogue page. Adding a game to the catalogue = one manifest entry + two i18n keys.
- [x] **Idiom vertical** — `add-idiom-dictionary` (30,895 curated idioms committed at `backend/data/idioms.curated.json`), `add-puzzle-core` (`PuzzleLevel` / `PuzzleAttempt` / `PuzzleLevelProgress` + the `IPuzzleRules` registry, server-authoritative, answer key never leaves the server), `add-idiom-crossword` + `add-web-idiom-crossword` (成语纵横, 12 generated levels, playable at `/g/idiom-crossword`).
- [x] **`add-game-rules-registry`** — `IGameRules` / `IGameRulesRegistry` / `NInARowRules(key, rows, cols, winLength)`; `Board` is now rows×cols rather than square; `Room` carries a `GameKey`. Deliberately Domain-only at the time.
- [x] **`add-tictactoe`** + **`add-web-tictactoe-ai`** — 一字棋, playable at `/g/tictactoe`. The rules cost **zero lines**; everything else was registry debt the previous change had left above the Domain layer, and paying it cost about as much as the game itself (~310 lines vs ~332). Read `openspec/changes/archive/2026-08-14-add-tictactoe/tasks.md` §7 before starting 中国象棋 — it is the priced list of what a new board game actually costs. Also added: an AI registry (`IGameAiFactory` / `IGameAiRegistry`), `GameKey` on the three room DTOs, and a board whose dimensions are inputs rather than a constant.

- [x] **`add-game-capabilities`** — `IGameRules.SupportsHumanVsHuman` plus the invariant `IsRated ⇒ SupportsHumanVsHuman`, enforced in the `NInARowRules` constructor **and** by a test that walks the registry.

  The roadmap used to say "`add-per-game-rating` must delete `IGameRules.IsRated`". That was wrong, and the reason matters more than the correction. `IsRated` had two justifications: it protected the single shared rating pool, and 一字棋 ratings are meaningless. Per-game rating removes the first but not the second — 一字棋 has no human-vs-human mode, its only opponents are bots, and bot games *are* rated (the `ai-opponent` D7 anti-arbitrage rule), so a 一字棋 ladder would rank whoever farmed the Easy bot most. Splitting the pool does not fix that. The deeper problem was the field's *shape*: a hand-maintained boolean meaning "should this game be rated" is a **judgement**, and judgements expire silently. Constraining it by an invariant against a structural fact removes the need to remember. **A TODO in a comment is not a mechanism.**

- [x] **`add-per-game-rating`** — `UserGameStats(UserId, GameKey, …)` is now the single source of truth for ELO and win/loss records; `User` no longer carries `Rating` / `GamesPlayed` / `Wins` / `Losses` / `Draws` and keeps **no mirror field** (a mirror is a second source of truth whose drift shows up as the leaderboard and the profile page disagreeing, with nothing to catch it). The three ladders (ELO / puzzle time+stars / score) still stay separate on purpose — this change unifies nothing.

  Shipped as **expand → contract**, and that split is the reusable lesson. The roadmap used to claim the change "cannot be made smaller" because dropping the columns forces every reader to change in one commit. That was wrong: `AddUserGameStats` (create table + backfill, **columns untouched**) is pure addition, keeps the tree green, and lands the riskiest part — the migration — where it can be reviewed and tested alone. `DropUserRatingColumns` follows once the readers have moved. Ordering is guaranteed by migration timestamps, so **a future migration squash must not reorder them.**

  EF's generated `Down` for the drop was wrong in the same way `AddRoomGameKey`'s `defaultValue: ""` was: `AddColumn(defaultValue: 0)` would restore everyone at rating 0 while the real data sat unread in `UserGameStats`. It now carries the data back by hand, with a test — because nobody walks the rollback path until they need it. Read the archived `tasks.md` §8 before touching schema again; it also records that `DROP COLUMN` on SQLite is a non-atomic table rebuild.

- [x] **`add-web-per-game-rating`** — every rated game gets its own ladder at `/g/:gameKey/leaderboard`, and the profile page gained a game switcher. Backed by a new read-only `GET /api/games`, a projection of `IGameRulesRegistry` (which grew an `All` member to make projecting possible).

  The choice worth remembering is *why the endpoint exists* rather than a `rated: boolean` on the front-end `GameManifest`. The manifest already holds one deliberate copy of server data — the board dimensions — and that one is tolerable because a mismatch shows up as a visibly wrong number of cells and the server rejects out-of-range moves. A `rated` copy has neither safety net: its failure mode is *a permanently empty ladder*, which looks exactly like a new game nobody has played. **Whether a copy is acceptable depends not on how small it is but on whether being wrong would ever be noticed.** One catalogue test (tic-tac-toe must have no ladder link) is the executable form of that argument; if it disappears, the copy has crept back.

  Also settled here: `gamesPlayed === 0` renders "no games yet" rather than the server's initial `1200`, because rendering the payload verbatim reads as *a beginner who has played*. When 中国象棋 ships that will be true of nearly every user.

- [x] **`generalize-match-domain`** — the enabling refactor for 中国象棋. `Room.PlayMove` no longer knows how a board works: it checks room state, player identity, and turn, then hands everything else to `IGameRules.Apply(history, intent, side)`. Bounds, occupancy, and move legality all belong to the rules now. `Move` gained nullable `FromRow`/`FromCol`, so a move is `(from?) → to`. `WinLength`/`CreateBoard` moved down to `INInARowRules`. `GameEndReason.Connected5` → `Decided` (underlying value still `0`, so no data migration).

  Two roadmap items turned out to be **wrong on inspection, not merely stale**. It said "two seats + JSON move payloads": 象棋 needs neither. `BlackPlayerId`/`WhitePlayerId` *are* two seats — 红/黑 is a display-layer reading, not a structural change. And every xiangqi move is exactly `from → to` (no castling, en passant, or promotion), so two nullable columns cover both game families while staying queryable, EF-mapped, and strongly typed at replay. **JSON would have been paying for a requirement that does not exist and that 象棋 would not have created.**

  The reusable lesson is about testing, not design. Plans called for two optional parameters on the SignalR hub's `MakeMove`. **SignalR does not apply C# optional-parameter defaults** — a 3-arg invocation against a 5-param method is rejected outright, which would have broken every published client on its next move. Domain, Application, and Api unit tests all passed; none of them cross SignalR's argument binding. `AiSmoke` caught it, because it does not know the refactor exists. That is the whole argument for keeping an end-to-end smoke that speaks the real transport.

- [x] **`add-xiangqi`** — 中国象棋的**规则**. `XiangqiRules` owns its own piece model and 10×9 board entirely inside the rules; the aggregate never sees them.

  **`generalize-match-domain`'s acceptance criterion held: `Room`/`Game`/`Move` were not touched at all.** `XiangqiThroughRoomTests` proves it by playing real xiangqi through the real aggregate. The change is a new `Games/Xiangqi/` directory, two registration lines, and one DI line.

  In this game **`Stone.Black` is 红**. `Game` starts on `Stone.Black` and xiangqi is red-first, so reading Black as red costs zero Domain change — `Stone` has always meant "first mover / second mover", and 红/黑 is how the display paints it. That is the bet `generalize-match-domain` placed, now collected.

  Two implementation choices worth keeping: flying-generals is **not** a special case (it is exactly "the enemy general can capture down that file", so it folds into the same check test as self-check); and **stalemate is a loss**, unlike chess — the one rule most likely to be miscopied from a western-chess implementation.

  It also removed a **fake** test. `AllBuiltInRules()` claimed in its own comment to walk the registry so that "adding 中国象棋 is automatically covered" — but its data source was a hand-written `{ Gomoku, TicTacToe }`, so xiangqi would have slipped past the `IsRated ⇒ SupportsHumanVsHuman` invariant in silence. That is precisely the failure the comment predicted, about a mechanism the comment got wrong. `BuiltInGameRules.All` is now the single list, feeding both DI and the test.

Not yet done — platform roadmap, in this order:

1. Lobby generalization — `/home` is still gomoku's lobby, and 一字棋 therefore has human-vs-AI only, reachable only from `/games`. Parameterising it means rewriting `/home` as a normative path in five web specs, so it waits for the first game that genuinely needs human-vs-human: 中国象棋.
2. `add-xiangqi-ai` then `add-web-xiangqi`. The AI is the piece that gives 象棋 an opponent at all today (human-vs-human waits on lobby generalization), and it is *not* gomoku-sized: no exhaustive search is possible, so it needs a depth-limited alpha-beta with a real evaluation. The web client needs a `MovePiece` wrapper on `GameHubService` — deliberately not written yet, since a method with no caller is code nobody can verify. Only once 象棋 has an opponent does `SupportsHumanVsHuman` / `IsRated` become a live question, and the invariant keeps that from being forgotten.
3. Then `add-idiom-chain`, `add-klotski`, `add-xiangqi-*`, `add-score-attack-core` + `add-tetris`.

Discipline: **do not start a new game until the previous one is archived.** Seven games × (rules + AI + UI + i18n + tests) will otherwise all rot half-finished.

Deferred follow-ups, each with a reason:

- `squash-migration-baseline` — squash the 11 migrations into one. Needs deltas because `ai-opponent` has requirements named after `AddBotSupport` / `AddHardBotAccount`, `room-and-gameplay` after `AddGameEndReason`, and `user-management` now names `AddUserGameStats` / `DropUserRatingColumns` — the last pair with a normative ordering constraint the squash must preserve. Cheap while the DB is still local-only (no production data exists).
- `backend/smoke/AiSmoke` is **broken and has been since `add-leaderboard-pagination`**: step 7 deserializes `/api/leaderboard` into `List<LeaderboardEntry>`, but the endpoint returns `PagedResult<T>`. It is not in `Gewu.slnx` and CI does not run it, which is exactly why nobody noticed. Either wire it into CI or delete it — a smoke test outside CI rots silently and then lies about coverage.
- `GameManifest.board` (front-end copy of the board dimensions) is now **deletable at any time** — `GET /api/games` already carries `rows` / `cols`. Its doc comment still says to wait for `generalize-match-contract`; that condition is stale. Fold the deletion into whichever change next touches the manifest.
- `gomoku:*` → `gewu:*` localStorage keys — normative in five web specs, and renaming logs everyone out; needs a read-old/write-new shim.
- `GomokuHub` → `MatchHub` and `/hubs/gomoku` → `/hubs/match`. `GameEndReason.Connected5` is **done** (`generalize-match-domain` renamed it to `Decided`); the hub name is what remains, and it rides along with lobby generalization, which must rewrite those specs anyway.
- `logs/gomoku-.log` Serilog filename and the `GOMOKU_*` env-var prefix — both normative in specs (`observability`, `api-ops`). The env-var prefix is **not implemented**: `Program.cs` never calls `AddEnvironmentVariables("GOMOKU_")`, so the documented `GOMOKU_JWT__SIGNINGKEY` / `GOMOKU_CORS__ALLOWEDORIGINS__0` are silently ignored and only the unprefixed `JWT__SIGNINGKEY` works. That is a live ops trap, not just a naming wart — fix the code or the spec, but do not leave them disagreeing.

The connection-string key is `ConnectionStrings:Default`, **not** `:DefaultConnection`, so the env var that overrides it is `ConnectionStrings__Default` (verified working — pointing it at a scratch file is how the `add-per-game-rating` migration was smoke-tested against a copy of the dev database). An earlier note claiming that env var "does not take effect" was simply using the wrong key name.

Other platforms, unchanged from before:

- `frontend-desktop/` — empty. Electron wrap of the Angular app.
- `frontend-mobile/` — empty. Flutter + Material 3.

## Workflow — OpenSpec is mandatory

**Never write implementation code without an approved OpenSpec proposal.** This is a hard rule, not a preference.

1. **Propose** — for each new feature, create a change directory at `openspec/changes/<change-name>/` containing `proposal.md`, `tasks.md`, and `specs/`. Use `/opsx:propose` or `/openspec-propose`.
2. **Review** — the user reads the proposal and requests edits. Wait for explicit approval before touching code.
3. **Implement** — once approved, work through `tasks.md` item by item, checking off as you go. Use `/opsx:apply`.
4. **Archive** — when done, `openspec archive <change-name>` moves spec deltas from `changes/` into the live `openspec/specs/` tree and renames the change directory under `archive/`.

Pure bug fixes that bring code into compliance with an existing spec don't need a new proposal — fix the code, commit. Spec-level corrections that document already-shipped behaviour can ship as a tiny `fix-spec-<name>-drift` change.

## Tech stack

### Backend (`backend/`) — .NET 10

- ASP.NET Core Web API, target `net10.0` on every project (nullable + implicit usings enabled)
- **MediatR** for CQRS — every write is a `Command`, every read is a `Query`, one handler per file
- **EF Core** — SQLite for local dev, SQL Server when scaling
- **SignalR** for real-time play and chat
- **FluentValidation** for input validation, **Serilog** for logging, **JWT** for auth
- Tests: **xUnit** + **FluentAssertions** + **Moq**

The solution file is `.slnx` (XML), not `.sln`. The `dotnet` CLI handles it transparently; older tooling may not.

### Web (`frontend-web/`) — phase 1

- **Angular 21** + TypeScript strict mode
- **Tailwind CSS v4** + **Angular Material** + **`@angular/cdk`** (overlays / dialogs / a11y)
- **Transloco** for runtime i18n (initial locales: `zh-CN`, `en`; adding a new locale = drop one JSON file + register one line)
- `@microsoft/signalr` client (lazy-imported on first hub call to keep it out of the main bundle)
- State: **Angular Signals** first; NgRx only for genuinely complex flows
- Tests: **Vitest** (not Karma/Jasmine)

### Desktop (`frontend-desktop/`) — phase 2

Electron wrapping the Angular app.

### Mobile (`frontend-mobile/`) — phase 3

Flutter + Material Design 3, `signalr_netcore` client.

## Backend architecture

### Layer dependency direction (strict)

```
Domain  ← Application  ← Infrastructure
                       ← Api
```

- **`Gewu.Domain`** — entities, value objects, domain events. Zero outward dependencies.
- **`Gewu.Application`** — use cases (MediatR handlers), DTOs, interfaces for infrastructure concerns. Depends on `Domain` only.
- **`Gewu.Infrastructure`** — EF Core, persistence, external adapters. Implements `Application` interfaces.
- **`Gewu.Api`** — ASP.NET host, HTTP endpoints, SignalR hubs, DI composition root.

Preserve the direction when adding project references. **Never** have `Api` reference `Domain` directly; **never** put DB access outside `Infrastructure`.

### DDD aggregates

- **Room aggregate** (root: `Room`) contains `Game` + `Chat`. All mutations to game state and chat go through `Room`.
- **User aggregate** (root: `User`) is independent.
- Value objects (no identity, immutable): `Move`, `BoardPosition`, `Score`.
- Domain events (e.g. `MoveMade`, `GameEnded`) published via MediatR.

### CQRS conventions

- Commands: `CreateRoomCommand`, `MakeMoveCommand`, ...
- Queries: `GetRoomListQuery`, `GetLeaderboardQuery`, ...
- One handler per file; the file name matches the command/query.
- **SignalR hubs route messages only** — they dispatch to MediatR and push results back. No business logic in hubs.

### Hard rules

- **Domain and Application must not use `async void`, `.Result`, or `.Wait()`.** Use `async Task` / `await` end-to-end.
- Database access is **only** allowed in `Infrastructure`.
- Public methods need at least an XML `<summary>` doc comment.
- Interfaces use the `I` prefix (`IRoomRepository`).
- C# files: PascalCase.

### Required tests

Don't ship without unit tests for:
- Domain core logic — **win detection** and **ELO calculation** in particular.
- Every Application handler.
- Frontend: components and services with real logic (pure display components can skip).

`Gewu.Domain.Tests` and `Gewu.Application.Tests` exist. If an Api-level integration test project is added, name it `Gewu.Api.Tests` and register it in `Gewu.slnx`. The test csprojs declare `Xunit` as a global using — don't add `using Xunit;` in test files.

## Frontend conventions (Angular)

### Naming & structure

- Filenames: **kebab-case**. Classes: PascalCase.
- Use **standalone components** (Angular 17+ style). Don't create new NgModules unless unavoidable.
- Prefer **Signals** over `BehaviorSubject` for local state.
- When Tailwind class strings get long, extract via `@apply` into a custom utility.
- All HTTP lives in `core/api/`. Components must not call `HttpClient` directly.

### Design & UX (hard rules)

- **Dark mode must work from day one.** Mechanism: `ThemeService` (Signal) toggles a `.dark` class on `<html>`; colors come from CSS variables (`--color-bg`, `--color-primary`, etc.); Tailwind's `dark:` variant assists. Never hard-code dark colors in components (no `bg-gray-900` / `text-white`).
- **Responsive (mobile-first).** Every route MUST work at **375 px** width, then progressively enhance via `sm: / md: / lg: / xl:` up to 1440 px+. Tailwind's default breakpoints are sufficient — don't write CSS for one specific resolution.
- **Modern UX.** CSS transitions over JS animations. Visible `focus-visible` ring. Skeleton placeholders to avoid layout shift. Every interactive element keyboard-reachable. Respect `prefers-reduced-motion`. Loading / empty / error states all need real UI — no plain `"loading…"` text.

### Performance

- **Lazy loading is mandatory.** Every route outside the root shell (shell + login) uses `loadComponent` / `loadChildren`. Each lazy chunk stays **under 200 KB gzipped**; split if larger.
- `<img loading="lazy">` is the default; only above-the-fold first-paint images are exempt.
- The SignalR client is constructed inside a service and **only connects on first subscription** — never at app bootstrap. A user who never opens a game page should never establish a hub connection.

### Architecture — SOLID, easy to extend

- **Single responsibility.** A component does one thing. Container components (fetch data, dispatch events) and presentational components (pure rendering of inputs) are separate — don't mix.
- **Dependency inversion.** When a service has plausible alternative implementations (mock for tests / future API client / different state backend), use an **abstract class as the DI token**. Inject by token, not by concrete class.
- **Open/closed.** Adding a new theme / locale / difficulty MUST be a "drop one config file or one TS file" change — no edits to existing components.
- Composition over inheritance. Cross-cutting behavior goes in directives / pipes, not base classes.
- Components stay under 200 LOC. If larger, extract a service or store.

### Dialogs & overlays

Dialogs / popovers / overlays MUST use **Angular CDK** (`@angular/cdk/dialog` or `@angular/cdk/overlay`). Material's `MatDialog` is fine (it wraps CDK). **Never** hand-roll `<div>` + `*ngIf` modals — focus trap, ESC handling, backdrop, ARIA attributes are all required, and CDK gives them for free.

### i18n

- Use **Transloco** (or Angular i18n + ICU runtime switching).
- Initial locales: `zh-CN` (Simplified Chinese) + `en`. Files at `public/i18n/<locale>.json`. Flat keys / dotted paths (`room.join.button`).
- Templates **MUST NOT hard-code** Chinese or English display strings — always `{{ 'key.path' | transloco }}`. Date / number formatting goes through Angular's `formatDate` / `formatNumber` with a locale parameter.
- The active language is held by `LanguageService` (Signal), persisted to `localStorage`. Resolution order: `localStorage` → `navigator.language` → `en` fallback.
- Adding a new locale = drop a new `i18n/<locale>.json` + add one entry to `LanguageService.supported` + register Angular locale data in `core/i18n/register-locales.ts`. No other file changes.

### Theme switching (Material / System / future)

- Themes are kept in a registry: `ThemeService.register(name, tokens)`. `tokens` is a CSS variable bag (`--color-primary`, `--color-surface`, `--radius-card`, `--shadow-elevated`, …). Switching = setting `data-theme="<name>"` on `<html>` and persisting to `localStorage`.
- Two themes ship: `material` (Angular Material default palette + Material radii / shadows) and `system` (Apple / Fluent-ish minimal — smaller radii, lighter shadows).
- **Dark/Light is an orthogonal axis to the theme.** Each theme has light + dark token sets. `ThemeService` exposes two signals (`themeName` and `isDark`) that switch independently.
- Component styles MUST reference CSS variables, never literal colors. "This button uses theme-blue" = `var(--color-primary)`, not `#2962FF`.
- Adding a new theme = drop one tokens file + one `ThemeService.register(...)` call. No component changes.

The same registry pattern applies to **board skins** (`BoardSkinService`, currently `wood` + `classic`) and **sound packs** (`SoundService`, currently `wood` + `chiptune`).

### Frontend tests

- Vitest covers: services / stores with logic, components with conditional branches, the i18n pipe, cross-cutting services like `ThemeService` / `LanguageService` / `SoundService`. Pure presentational components can skip.
- For side-effecting paths (dialogs, route guards, SignalR subscriptions), use TestBed + `ComponentHarness` integration tests.

## Common commands

From `backend/`:

```bash
dotnet build Gewu.slnx                              # build all
dotnet test  Gewu.slnx                              # run all tests
dotnet run   --project src/Gewu.Api                 # http://localhost:5145, https://localhost:7082

dotnet test tests/Gewu.Domain.Tests                 # single project
dotnet test tests/Gewu.Domain.Tests \
  --filter "FullyQualifiedName~WinDetectionTests.Diagonal"   # single test
```

### EF Core migrations

Install once: `dotnet tool install --global dotnet-ef`. Run from `backend/`:

```bash
# Add a migration (name in PascalCase, describes the intent)
dotnet ef migrations add AddUserAndRoom \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  --output-dir Persistence/Migrations

# Apply to the configured DB
dotnet ef database update \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api

# Roll back the last migration (only before it has been merged / pushed)
dotnet ef migrations remove \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api

# Generate an idempotent SQL script for review / production apply
dotnet ef migrations script --idempotent \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o migrations.sql
```

Rule: never edit a migration that has already been merged to `main` — add a new one instead.

### Frontend

```bash
npm install
npm start           # ng serve (with the dev proxy → :5145)
npm run build
npm test            # Vitest, watch mode
npm run test -- --run   # Vitest, single run (CI mode)
npm run lint
```

## Git — branches & commits

Use **GitHub Flow** plus **Conventional Commits**.

**Branches** — `main` is protected and always deployable. Branch off `main` for every change; PR back to `main`:

```
feat/<slug>      new feature       feat/room-chat
fix/<slug>       bug fix           fix/elo-draw-calc
refactor/<slug>  refactor          refactor/move-validator
docs/<slug>      docs only         docs/signalr-contract
chore/<slug>     build / misc      chore/upgrade-ef-core
test/<slug>      tests only        test/win-detection-edge
```

Slugs are kebab-case. Tie to an OpenSpec change name when one exists (e.g. `feat/add-domain-core`).

**Commits** — [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body — explain WHY, not WHAT]

[optional footer — BREAKING CHANGE: ... / Refs: #123]
```

Types: `feat` `fix` `docs` `style` `refactor` `perf` `test` `build` `ci` `chore` `revert`. Scope is the module (`domain`, `api`, `web`, `infra`, etc.). Subject is in the imperative mood, ≤ 72 chars, no trailing period. Breaking changes get a `!` after the type/scope **and** a `BREAKING CHANGE:` footer.

Examples:

```
feat(domain): add five-in-a-row win detection
fix(api): return 409 instead of 500 when room is full
refactor(web)!: replace BehaviorSubject with Signals in game store
```

A single commit can be in English or Chinese, but pick one — don't mix within a single commit. Don't commit failing tests.

## Code review

All merges to `main` go through a PR. Direct push is forbidden.

### PR requirements

- **Link to the OpenSpec change.** The PR description states the corresponding `openspec/changes/<name>/`. If there is no associated change, explain why one isn't needed (pure docs, build fix, spec-drift correction).
- **Size.** Each PR ideally stays under **400 lines of net change** (excluding auto-generated migration / lock files). Split when larger.
- **CI must be green.** `dotnet build` + `dotnet test` + the web `npm run lint` + `npm test -- --run` must all pass before requesting review.
- **At least 1 approval** to merge. PRs touching architecture, security, or DB schema benefit from 2 approvals.
- Merge style: **Squash merge**. Use Conventional Commits format and keep the PR number (`feat(domain): ... (#42)`).

### Author self-check (before opening a PR)

- [ ] Layer direction intact: `Domain` has no outward dependencies; `Application` only depends on `Domain`; DB access only in `Infrastructure`; `Api` does not directly reference `Domain`
- [ ] No `async void` / `.Result` / `.Wait()` in `Domain` or `Application`
- [ ] SignalR hub is purely a router; business logic lives in handlers
- [ ] Public methods have an XML `<summary>`; interfaces start with `I`
- [ ] Unit tests cover: win detection / ELO / new handlers / web services with logic
- [ ] No secrets / connection strings / sensitive `appsettings.*.json` values committed
- [ ] OpenSpec `tasks.md` is checked off; latest progress reflected in the PR description

### Reviewer focus (in priority order)

1. **Correctness and business logic** — win detection rules, ELO formula, forbidden moves, timeouts, reconnection. Are edge cases tested?
2. **Architecture and dependency direction** — any sneaky cross-layer calls? Are DB / HTTP details leaking into `Application` or `Domain`?
3. **Concurrency and async** — deadlock risk? `async` used correctly? SignalR group / connection lifecycle right?
4. **Security** — input validation (FluentValidation), authn/authz, SQL injection, XSS, JWT verification, spectators must not be able to send move commands.
5. **Test quality** — not just coverage; do tests assert *behavior* rather than *implementation*? Is mocking abused?
6. **Readability** — naming, function length, comments that explain *why* rather than *what*.
7. **Performance** — only raise this when there's a metric or profile to point at. Don't ask for optimization on a hunch.

### Comment etiquette

Prefix review comments so the author knows what's required vs. optional:

- `must:` — must change, otherwise no merge (correctness / security / architectural violation)
- `should:` — strongly suggested change; the author needs a reason to keep their version
- `nit:` — minor / style; the author's call
- `question:` — pure question, not a request for change
- `praise:` — when something's well-done, say so

Authors can decline `should` / `nit` items but should briefly explain why. Don't approve while a `must:` is unresolved.

## Shell

Windows host, bash shell. Use Unix syntax in commands (`/dev/null`, forward slashes), not `NUL` / backslashes.
