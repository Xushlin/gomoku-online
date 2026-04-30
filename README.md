# Gomoku Online

Real-time multiplayer Gomoku (五子棋) — play another human across a SignalR
connection, or take on a built-in AI in three difficulties. Web client is
Angular 21 + Tailwind, backend is .NET 10 with Clean Architecture and CQRS.

## Status

| Layer              | State                                                    |
| ------------------ | -------------------------------------------------------- |
| Backend (`backend/`)         | **MVP done** — auth, rooms, gameplay, AI, ELO, replay, presence, observability, rate limiting |
| Web (`frontend-web/`)        | **Feature-complete v1** — auth pages, lobby, real-time game board, replay, profiles, AI picker, sound, presence |
| Desktop (`frontend-desktop/`) | empty — Electron wrap pending                           |
| Mobile (`frontend-mobile/`)   | empty — Flutter pending                                  |

## What's in the box (web)

- **Real-time play** — JWT auth, SignalR hub at `/hubs/gomoku`, urge / chat / spectate / resign / dissolve flows.
- **AI opponent** — Easy / Medium / Hard, pick Black or White when you create the room.
- **Replay player** — `/replay/:id` reuses the live `Board` component in read-only mode with a scrubber (▶ play / ⏪ prev / ⏩ next / slider / 0.5× / 1× / 2× speed).
- **Public profile** — `/users/:id` with rating + W-L-D + paginated games, online presence dot.
- **Find player** — debounced lobby card with prefix search + click-through to profile.
- **Themes & skins** — Material / System theme × light / dark, two board skins (Wood / Classic), two sound packs (Wood / Chiptune), all switchable from the header.
- **i18n** — Simplified Chinese + English from the start, every string runs through Transloco; adding a third locale is one file + one register line.
- **Modern Angular** — standalone components, Signals everywhere, abstract-class-as-DI-token services, lazy routes, Vitest tests.

## Quick start

```cmd
:: Windows: double-click start-dev.cmd, or:
start-dev.cmd
```

Opens two windows — backend on `http://localhost:5145`, Angular dev server on
`http://localhost:4200`, then auto-launches the browser when the frontend is up.
A small Angular dev proxy forwards `/api/*` and `/hubs/*` from `:4200` to the
backend so relative paths just work.

### Run pieces by hand

Backend (.NET 10 SDK required):

```bash
cd backend
dotnet restore Gomoku.slnx
dotnet run --project src/Gomoku.Api --launch-profile http
```

Web (Node 20+ recommended):

```bash
cd frontend-web
npm install
npm start
```

### Tests

```bash
# Backend — domain + application unit tests (~390 tests)
cd backend && dotnet test Gomoku.slnx

# Web — Vitest (~178 tests)
cd frontend-web && npm test
```

## Project layout

```
backend/
  Gomoku.slnx                       (XML solution file — net10.0 across all projects)
  src/
    Gomoku.Domain/                  (pure entities, value objects, aggregates; zero outward deps)
    Gomoku.Application/             (MediatR handlers, DTOs, infrastructure interfaces)
    Gomoku.Infrastructure/          (EF Core, persistence; implements Application interfaces)
    Gomoku.Api/                     (ASP.NET host, controllers, SignalR hubs, DI composition root)
  tests/
    Gomoku.Domain.Tests/
    Gomoku.Application.Tests/

frontend-web/
  src/app/
    app.{config,routes}.ts          (root config + lazy routes)
    core/                           (cross-cutting services — auth, api, i18n, theme, sound, realtime)
    pages/                          (auth, lobby, rooms, users, replay)
    shell/                          (Header + Shell container)
  public/i18n/{en,zh-CN}.json
  proxy.conf.json                   (dev proxy → :5145)

openspec/
  config.yaml
  specs/                            (live capability specs — single source of truth)
  changes/
    archive/                        (every shipped change kept for audit trail)

start-dev.cmd                       (Windows one-click launcher)
```

## OpenSpec workflow

Every feature on this repo went through a proposal → spec delta → tasks →
implementation → archive cycle. Live specs in `openspec/specs/` document
behaviour at the requirement level (each behaviour has explicit `WHEN/THEN`
scenarios). Archived changes in `openspec/changes/archive/` give the full
"why + how" trail going back to project zero.

Useful CLI:

```bash
openspec list               # active changes
openspec validate <name>    # before archive
openspec archive <name>     # promote change → live specs
```

See [`CLAUDE.md`](CLAUDE.md) for the contributor playbook (architecture
constraints, commit/PR style, code review expectations).

## Backend architecture

Clean Architecture with strict layer dependencies (`Domain ← Application ←
Infrastructure / Api`). MediatR for CQRS — every write is a `Command`, every
read is a `Query`, one handler per file. SignalR hubs route messages only;
they dispatch to MediatR and push results back, no business logic.

Persistence is SQLite for local dev (`backend/src/Gomoku.Api/gomoku.db`,
auto-migrated on first run); SQL Server-ready when needed.

JWT auth with HS256 — dev-only key in `appsettings.Development.json`. Production
**must** override via `GOMOKU_JWT__SIGNINGKEY` env var; the app refuses to
start in `Production` with an empty key.

CORS, rate limiting, structured logging (Serilog), exception → ProblemDetails
middleware are all wired.

## Web architecture

Angular 21 standalone components only — no NgModules. Signals first, NgRx
deliberately avoided. Tailwind v4 with a tokens layer (`@theme` block in
`tailwind.css` + `[data-theme="..."]` cascading runtime values in
`tokens.css`); every visual property comes from a token utility, no hex /
`bg-gray-*` literals.

Three pluggable preference services share the same shape — `ThemeService`,
`BoardSkinService`, `SoundService`. Each is an abstract DI token + default
impl with a `register(name, tokens)` registry, persisted to localStorage,
applied via `<html data-*>` attributes. Adding a new theme / board skin /
sound pack costs one TS file + one `register` call; no component touches.

Vitest for unit tests, no Karma. Templates use control-flow blocks (`@if /
@for / @switch / @let`), no structural directives. Strict TypeScript.

## What's next

Roughly in priority order — see `openspec/changes/archive/` for fine-grained
history and `openspec/specs/` for current behaviour:

- GitHub Actions CI (build + lint + test on PR)
- Electron desktop wrap (`frontend-desktop/`)
- Flutter mobile client (`frontend-mobile/`)
- Volume slider / additional sound packs / additional board skins
- Browser push notifications

## License

Not yet picked. If you want to use any of this, open an issue and ask.
