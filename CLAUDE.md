# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**格物 / Gewu** — a multi-platform online game hall. Planned games: idiom games (成语纵横 / 成语接龙 / 猜成语), 五子棋, 一字棋, 中国象棋, 华容道, 俄罗斯方块. 「格」 means grid cell, which is what they all have in common.

**Nine games ship.** They fall into three categories that deliberately do **not** share one aggregate:

| Category | Games | Realtime | Core concepts |
| --- | --- | --- | --- |
| Turn-based adversarial | 五子棋, 一字棋, 中国象棋, 成语接龙, 斗地主, 挖坑 | SignalR | room, N seats, turn order, move sequence, ELO, spectators, replay |
| ↳ what the card games add | 斗地主, 挖坑 | — | **hidden per-seat state**, a **server-only setup** (the deal), **three** seats, a first mover decided by the deal, and a settlement in points rather than ELO |
| Single-player levels | 成语纵横, 华容道, 猜成语 (planned) | none (REST) | level catalogue, progress, stars, hints, time leaderboard |
| Single-player score-attack | 俄罗斯方块 | none (submit at end) | run record, score validation, periodic leaderboard |

**Three kernels, and each has three authority models to keep straight** — this is the one piece of history worth carrying eagerly, because getting it wrong designs the next game wrong:

- **match** (`Room` / `Game` / `IGameRules`) — the server owns legality. Proven general by 中国象棋 (`from → to`, 10×9) and 成语接龙 (no board at all), not by 一字棋, which is gomoku in miniature.
- **puzzle** (`PuzzleLevel` / `IPuzzleRules`) — authoritative two different ways: 成语纵横 *withholds* the answer, 华容道 *replays* every claimed move. Same platform rule, opposite mechanism.
- **score-attack** (`ScoreRun`) — replays **placements, not keystrokes**. No registry behind it, because *a switch with one arm is a switch*.

Everything else about how each game landed — including which client judges its own rules and why the four answers differ — is in [`JOURNAL.md`](JOURNAL.md).

## Current phase

**Nine games ship** — 五子棋, 成语纵横, 一字棋, 中国象棋, 华容道, 成语接龙, 俄罗斯方块, 斗地主, 挖坑. 99 archived changes. The three kernels are built and each has been proven by a second game that was not a variant of the first.

### Where the history lives

Everything about *how it got here* is in **[`JOURNAL.md`](JOURNAL.md)** — one entry per change, in merge order, recording what it cost and what turned out to be false. Read it through **`.claude/skills/gewu-history/SKILL.md`**, which indexes it by game and by kernel seam and loads on demand.

It is not in this file because this file is loaded in full on every session whatever the task. The journal was 89% of it (~50k tokens), so a one-line i18n fix paid the same price as adding a tenth game — and the attention went the wrong way: every change appended a journal entry while the guidance below quietly went stale. **The part loaded unconditionally is the part that must not rot.**

The two lists below stayed here on purpose. Their value is that they arrive **before you ask**; a lazily-loaded trigger only fires if you already suspected it.

### Deferred, with triggers

Each of these was decided, written down, and left. **A deferral that names its own trigger is the good kind** — three of them have since fired on schedule.

| Deferred | Trigger |
| --- | --- |
| The Production no-signing-key exception names `GOMOKU_JWT__SIGNINGKEY`, a prefix the runtime **measurably ignores** — only `Jwt__SigningKey` is read. One line in `Program.cs` + an assertion on the message. | **The next backend change of any size.** |
| Renaming `SayWord` — it is in fact the generic text-payload path (the server only builds `MakeMoveCommand(Text:)` and never reads the game key), but the name was coined for 成语接龙 and now misleads in three places. | The day a **third** text-payload game lands. |
| `WakengScoring.Settle` / `DoudizhuScoring.Settle` have **no production caller**. Both games settle in points, and ELO is a two-player model. | The platform needs a **points ladder**. |
| The per-seat broadcast fan-out has no end-to-end test — projection is unit-tested and the group function is exhaustive by construction, but "three real SignalR connections each receive only their own" is unasserted, so a typo in `ViewGroupName` turns nothing red. | A `Gewu.Api.Tests` project exists. |
| `AddGameSetup`'s `Down` drops the column, and 斗地主 + 挖坑 now write it. A merged migration is not edited, so the bill is **one new guarded migration** — the cost is per *column*, not per game. `GameSetupMigrationTests` is the list of keys whose data it must carry back. | Anyone needs to roll back past it. |
| `squash-migration-baseline` — **measured and declined**, so do not re-litigate: the 14 migrations are 400 lines applying in 259 ms, and squashing deletes the 16 tests that stop at a *named intermediate* migration, which is the only point where "did the data move correctly" is observable. | A provider change, ~100 migrations, or an actual deployment. |
| The bundle budget is **480 kB** with ~6.7 kB free. It has fired five times and was never raised — once it was *lowered*. | When it fires: ask **what is eager that need not be**, and measure by stubbing, not by reasoning. |

Open questions waiting on the user: 红桃四 and 三带 rules (links promised). Not yet done: 斗地主's play-assist has never been driven in a browser — the client code is shared with 挖坑, whose two paths were measured.

### Traps this project keeps re-learning

The *generic* traps — a pipe eating the exit code, a mutation that fails to build, an assertion green on an empty collection — are the same in every repo and are not repeated here; keep them in your own global guidance. These are the ones specific to **this** codebase:

- **A hand-written list posing as a registry.** Fixed **five** times (`add-xiangqi`, `enforce-human-vs-human`, `enforce-ai-availability`, the sound-pack list twice) and it recurred *seven lines below* a fix. Every "walk every game / pack / skin" test must derive its data from the production list — `BuiltInGameRules.All`, `BuiltInGameAis.All`, `PACK_LOADERS`, `SOUND_EVENTS`. If you fix one, **grep for the siblings**; "I just fixed this class of problem" is a reason to look, not to relax.
- **A one-sided walk asserts nothing.** A registry walk needs both outcomes present in the sample (一字棋 is the only unrated versus game, and it is what keeps several walks from degenerating into empty loops). Prefer "exactly one" over "at least one" — "exactly" goes red when the second case lands, which is when to ask whether the two needs are really the same thing.
- **`--no-build` measures whatever is on disk.** A *failed* build followed by a passing `--no-build` run looks identical to two successes; `shutil.copy2` preserving mtime makes MSBuild compile nothing and report 0 errors. Hit three times. When mutation-testing, force a rebuild and check the file count.
- **A mutation that fails to build is not a mutation.** `@if (false)` in a template and an exception-throwing stub both produce "exit 1 with no test run", which reads as a kill. Hit twice.
- **An empty collection passes every layout assertion.** 375 px checks must run with the **longest real content** on screen — the dictionary's 15-character idiom, a 20-character username (that is the registration cap, so a longer invented string is not honest), 19 cards, 44 blocks. Three of the four overflow defects were invisible on empty data.
- **shrink-to-fit vs. the card table's fan formula.** `100%` inside `transform: translate()` resolves against the *element*, not the container, so the fan's step silently computes to 0 or goes negative. Hit **four** times. `frontend-web/scripts/check-styles.mjs` pins the invariants by filename — it runs under `npm run lint`, not vitest, and a file move will (correctly) break it.
- **The Browser pane does not composite when it is not displayed.** `document.timeline.currentTime` freezes at 0, zoneless change detection and `effect()` never run, so every DOM read after an interaction is stale. Layout metrics (`scrollWidth`/`clientWidth`) *are* still valid. `window.ng.applyChanges(window.ng.getComponent(el))` forces a pass and makes effect-driven behaviour testable there.
- **Counting by grep ≠ counting by failing test ≠ counting by requirement.** All three have been wrong here, in that order.
- **`openspec validate --strict` validates spec *shape*, never spec *truth*.** It was 37/37, 38/38 and 41/41 green across every drift this repo has found, including a Scenario that had never been implemented (four occurrences) and a live spec contradicting the code for 36 commits. **The signal is a merged PR whose change directory is still in `openspec/changes/`** — check that list.
- **Archive in merge order, and that is necessary but not sufficient.** MODIFIED replaces a requirement wholesale, so when two unarchived changes touch one requirement, one must be hand-merged. Generate MODIFIED bodies by *extracting from the live spec and patching*, never by retyping — retyping is how an unrelated sentence gets silently reverted. Renaming a requirement needs a `RENAMED` block or archive aborts.
- **SignalR applies no C# optional-parameter defaults, in either direction.** A client sending fewer *or more* arguments than the method declares is rejected in the binding layer — before any filter, and below the configured log level, so it is invisible from both ends. Adding a parameter to a live hub method is a breaking change; add a method.

### Discipline

**Do not start a new game until the previous one is archived.** Nine games × (rules + AI + UI + i18n + tests) will otherwise all rot half-finished. The rule is narrower than the failure it must prevent: `enable-xiangqi-human-play` was not a game, so nothing stopped it sitting unarchived for 36 commits with the live spec contradicting the code.

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

Test projects live under `backend/tests/` — read the directory rather than a list here. There is **no Api-level project**; if one is added, name it `Gewu.Api.Tests` and register it in `Gewu.slnx`. That absence is load-bearing: it is why the per-seat SignalR fan-out has no end-to-end test. The test csprojs declare `Xunit` as a global using — don't add `using Xunit;` in test files.

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
- The shipped themes live in `core/theme/themes/` — **read the directory, this line does not enumerate them.** It used to say "two themes ship" and was wrong from the day `ink` landed; the live spec's requirement *title* said the same thing while its own Scenario said three. A count in prose has no compiler.
- **Dark/Light is an orthogonal axis to the theme.** Each theme has light + dark token sets. `ThemeService` exposes two signals (`themeName` and `isDark`) that switch independently.
- Component styles MUST reference CSS variables, never literal colors. "This button uses theme-blue" = `var(--color-primary)`, not `#2962FF`.
- Adding a new theme = drop one tokens file + one `ThemeService.register(...)` call. No component changes.

The same registry pattern applies to **board skins** (`BoardSkinService`, `core/theme/skins/`) and **sound packs** (`SoundService`, `core/sound/packs/index.ts`, lazily `import()`ed — they were 8.69 kB of first paint for audio that cannot play before the first user gesture).

**Neither list is enumerated here on purpose.** The previous version of this line said board skins were "`wood` + `classic`" — wrong since `midnight` shipped, and it went stale while a whole change (`fix-spec-web-shell-pack-count`) was busy hunting two *other* copies of the sound-pack list without looking at this one. Read the directory. A skin or pack that omits a token **fails to compile**, and walking tests derive from `PACK_LOADERS` / the skin registry, so the code cannot disagree with itself — only prose can.

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

# Generate a SQL script for review.
#
# NOTE: **--idempotent does not work here.** EF throws
#   NotSupportedException: Generating idempotent scripts for migrations is not
#   currently supported for SQLite
# and writes no file at all. This block used to say `--idempotent`, and it had
# never once worked — measured, not assumed.
dotnet ef migrations script \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o migrations.sql

# Just the delta between two migrations. This form *is* supported on SQLite.
dotnet ef migrations script AddMoveOrigin AddScoreRuns \
  --project src/Gewu.Infrastructure \
  --startup-project src/Gewu.Api \
  -o delta.sql
```

Rule: never edit a migration that has already been merged to `main` — add a new one instead.

### Frontend

```bash
npm install
npm start           # ng serve (with the dev proxy → :5145)
npm run build
npm test            # Vitest, watch mode
npm run test:ci     # Vitest, single run — this is what CI runs
npm run lint        # ng lint + scripts/check-styles.mjs
```

`npm run lint` also runs `check-styles.mjs`, which asserts the board-skin token sets match, that no stylesheet hardcodes a suit path, and that the card table's fan formula has no percentage-valued variable in a `transform`. It pins those by **filename**, so moving a stylesheet will break it — correctly: the invariants must not silently stop being checked.

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
