# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

Multi-platform online Gomoku (五子棋) game. Players register, create/join rooms, play real-time matches (via SignalR) with room chat, spectator chat, and urge-opponent shortcuts. ELO-based ranking with special icons for the top three. Also supports human-vs-AI with multiple difficulties, plus game-record storage and replay.

## Current phase

Backend MVP and web client v1 are both feature-complete. Detail:

- [x] 4-layer Clean Architecture solution skeleton (`backend/Gomoku.slnx`)
- [x] OpenSpec initialized (`openspec/config.yaml`); each shipped change is archived under `openspec/changes/archive/<date>-<name>/`
- [x] **Backend MVP** — auth, rooms, gameplay, AI (Easy / Medium / Hard, with side-picker), ELO, replay, presence, observability, rate limiting. Live specs in `openspec/specs/`.
- [x] **Web client v1** (`frontend-web/`) — Angular 21, Tailwind v4, Material/CDK, Transloco (`zh-CN` + `en`). Auth pages, lobby, real-time game board, replay player, public profiles, find-player search, AI room creation with side-picker, sound effects (Wood + Chiptune packs), board skins (Wood + Classic), themes (Material + System) × dark/light, presence dots.
- [x] GitHub Actions CI runs on every push and PR (`backend` + `web` jobs in parallel).

Not yet done:

- `frontend-desktop/` — empty. Phase 2: Electron wrap of the Angular app.
- `frontend-mobile/` — empty. Phase 3: Flutter + Material 3.

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

- **`Gomoku.Domain`** — entities, value objects, domain events. Zero outward dependencies.
- **`Gomoku.Application`** — use cases (MediatR handlers), DTOs, interfaces for infrastructure concerns. Depends on `Domain` only.
- **`Gomoku.Infrastructure`** — EF Core, persistence, external adapters. Implements `Application` interfaces.
- **`Gomoku.Api`** — ASP.NET host, HTTP endpoints, SignalR hubs, DI composition root.

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

`Gomoku.Domain.Tests` and `Gomoku.Application.Tests` exist. If an Api-level integration test project is added, name it `Gomoku.Api.Tests` and register it in `Gomoku.slnx`. The test csprojs declare `Xunit` as a global using — don't add `using Xunit;` in test files.

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
dotnet build Gomoku.slnx                              # build all
dotnet test  Gomoku.slnx                              # run all tests
dotnet run   --project src/Gomoku.Api                 # http://localhost:5145, https://localhost:7082

dotnet test tests/Gomoku.Domain.Tests                 # single project
dotnet test tests/Gomoku.Domain.Tests \
  --filter "FullyQualifiedName~WinDetectionTests.Diagonal"   # single test
```

### EF Core migrations

Install once: `dotnet tool install --global dotnet-ef`. Run from `backend/`:

```bash
# Add a migration (name in PascalCase, describes the intent)
dotnet ef migrations add AddUserAndRoom \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api \
  --output-dir Persistence/Migrations

# Apply to the configured DB
dotnet ef database update \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api

# Roll back the last migration (only before it has been merged / pushed)
dotnet ef migrations remove \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api

# Generate an idempotent SQL script for review / production apply
dotnet ef migrations script --idempotent \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api \
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
