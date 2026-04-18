# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

Multi-platform online Gomoku (五子棋) game. Players register, create/join rooms, play real-time matches (via SignalR) with room chat, spectator chat, and urge-opponent shortcuts. ELO-based ranking with special icons for the top three. Also supports human-vs-AI with multiple difficulties, plus game-record storage and replay.

## Current phase

Project is in initial scaffolding. Completed:
- [x] 4-layer Clean Architecture solution skeleton (`backend/Gomoku.slnx`)
- [x] OpenSpec initialized (`openspec/config.yaml`)

Not yet done:
- [ ] First OpenSpec change: `add-domain-core` — no proposal exists yet
- `Program.cs` still holds the .NET template `WeatherForecast` sample; `Application` / `Domain` / `Infrastructure` projects contain only placeholder `Class1.cs`
- `frontend-web/`, `frontend-desktop/`, `frontend-mobile/` directories are empty — framework decisions made (see below) but nothing scaffolded

Expect tasks to be about *adding* things from near-zero, not modifying existing code.

## Workflow — OpenSpec is mandatory

**Never write implementation code without an approved OpenSpec proposal.** This is a hard rule, not a preference.

1. **Propose** — new feature → create a change in `openspec/changes/<change-name>/` (`proposal.md` + `tasks.md` + `specs/`). Use `/opsx:propose` or `/openspec-propose`.
2. **Review** — the user reads the proposal and requests edits. Wait for explicit approval before touching code.
3. **Implement** — once approved, work through `tasks.md` item by item, checking off as you go. Use `/opsx:apply`.
4. **Archive** — when done, `openspec archive <change-name>` moves specs from `changes/` to `specs/`.

Project-wide agent rules belong in `openspec/AGENTS.md` (to be created). `openspec/changes/archive/` and `openspec/specs/` are currently empty.

## Tech stack

### Backend (`backend/`) — .NET 10

- ASP.NET Core Web API, target `net10.0` on every project (nullable + implicit usings enabled)
- **MediatR** for CQRS — every write is a `Command`, every read is a `Query`, one handler per file
- **EF Core** — SQLite for early dev, SQL Server when scaling
- **SignalR** for real-time play and chat
- **FluentValidation** for input validation, **Serilog** for logging, **JWT** for auth
- Tests: **xUnit** + **FluentAssertions**

Solution file is `.slnx` (XML), not `.sln`. `dotnet` CLI handles it transparently; older tooling may not.

### Web (`frontend-web/`) — phase 1

- **Angular 21** + TypeScript strict mode
- **Tailwind CSS** + **Angular Material**
- `@microsoft/signalr` client
- State: **Angular Signals** first, NgRx only for genuinely complex flows
- Tests: **Vitest** (not Karma/Jasmine) — use Angular 21's Vitest builder or `@analogjs/vitest-angular`

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

None of these project references are wired yet — preserve the direction when adding them. **Never** have `Api` reference `Domain` directly; **never** put DB access outside `Infrastructure`.

### DDD aggregates

- **Room aggregate** (root: `Room`) contains `Game` + `Chat`. All mutations to game state and chat go through `Room`.
- **User aggregate** (root: `User`) is independent.
- Value objects (no identity, immutable): `Move`, `BoardPosition`, `Score`.
- Domain events (e.g. `MoveMade`, `GameEnded`) published via MediatR.

### CQRS conventions

- Commands: `CreateRoomCommand`, `MakeMoveCommand`, ...
- Queries: `GetRoomListQuery`, `GetLeaderboardQuery`, ...
- One handler per file, name matches the command/query.
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

`Gomoku.Domain.Tests` and `Gomoku.Application.Tests` exist. If an Api-level integration test project is added, name it `Gomoku.Api.Tests` and register it in `Gomoku.slnx`. The test csprojs have `Xunit` as a global using — don't add `using Xunit;` in test files.

## Frontend conventions (Angular)

- Filenames: **kebab-case**. Classes: PascalCase.
- Use **standalone components** (Angular 17+ style) — don't create new NgModules unless unavoidable.
- Prefer **Signals** over `BehaviorSubject` for local state.
- When Tailwind class strings get long, extract via `@apply` into a custom utility.
- All HTTP lives in `services/api/`. Components must not call `HttpClient` directly.

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

# Roll back the last migration (before it's been shared / pushed)
dotnet ef migrations remove \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api

# Generate an idempotent SQL script for review / prod apply
dotnet ef migrations script --idempotent \
  --project src/Gomoku.Infrastructure \
  --startup-project src/Gomoku.Api \
  -o migrations.sql
```

Rule: never edit a migration that has been merged to `main` — add a new one instead.

### Frontend (once Angular is scaffolded)

```bash
npm install
npm start           # ng serve
npm run build
npm test            # Vitest, watch mode
npm run test -- --run   # Vitest, single run (CI)
npm run lint
```

## Git — branches & commits

Use **GitHub Flow** plus **Conventional Commits**.

**Branches** — `main` is protected, always deployable. Branch off `main` for every change; PR back to `main`:

```
feat/<slug>      新功能         feat/room-chat
fix/<slug>       修 bug         fix/elo-draw-calc
refactor/<slug>  重构           refactor/move-validator
docs/<slug>      文档           docs/signalr-contract
chore/<slug>     构建 / 杂项    chore/upgrade-ef-core
test/<slug>      只加测试       test/win-detection-edge
```

Slugs are kebab-case. Tie to an OpenSpec change name when one exists (e.g. `feat/add-domain-core`).

**Commits** — [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<scope>): <subject>

[optional body — 说明 "为什么",不是 "做了什么"]

[optional footer — BREAKING CHANGE: ... / Refs: #123]
```

Types: `feat` `fix` `docs` `style` `refactor` `perf` `test` `build` `ci` `chore` `revert`. Scope is the module (`domain`, `api`, `web`, `infra`, etc.). Subject is imperative mood, ≤72 chars, no trailing period. Breaking changes get a `!` after the type/scope *and* a `BREAKING CHANGE:` footer.

Examples:

```
feat(domain): add five-in-a-row win detection
fix(api): return 409 instead of 500 when room is full
refactor(web)!: replace BehaviorSubject with Signals in game store
```

中英文皆可,但一条提交内只用一种语言。不要提交未通过的测试。

## Code review

All merges to `main` go through PR. Direct push is forbidden.

### PR 要求

- **关联 OpenSpec 变更**:PR 描述里写明对应的 `openspec/changes/<name>/`;若没有关联变更,说明为什么不需要(如纯文档、构建修复)。
- **体量**:单个 PR 尽量控制在 **400 行改动** 以内(不含自动生成的 migration / lock file)。超出就拆。
- **CI 必绿**:`dotnet build` + `dotnet test` + 前端 `npm test -- --run` + lint 全部通过才能请人审。
- **至少 1 个 approve** 才能合并;触碰架构、安全、数据库 schema 的 PR 建议 2 个 approve。
- 合并方式:**Squash merge**,commit message 用 Conventional Commits 格式并保留 PR 编号(`feat(domain): ... (#42)`)。

### 作者自检(提 PR 前)

- [ ] 分层没破:`Domain` 无外部依赖;`Application` 只依赖 `Domain`;DB 访问只在 `Infrastructure`;`Api` 不直接引用 `Domain`
- [ ] `Domain`/`Application` 里没有 `async void` / `.Result` / `.Wait()`
- [ ] SignalR Hub 只做路由,业务逻辑在 Handler 里
- [ ] 公共方法有 XML `<summary>`;接口加 `I` 前缀
- [ ] 单元测试覆盖:判胜 / ELO / 新增 Handler / 有逻辑的前端 service
- [ ] 没有提交密钥、连接串、`appsettings.*.json` 里的敏感值
- [ ] OpenSpec `tasks.md` 勾选到位,PR 描述里贴最新进度

### 审查者关注点(按优先级)

1. **正确性与业务逻辑** — 判胜规则、ELO 公式、禁手、超时、断线重连这类规则是否对;边界值是否测了。
2. **架构与依赖方向** — 有没有偷懒跨层调用;有没有把 DB / HTTP 细节泄漏到 `Application` 或 `Domain`。
3. **并发与异步** — 有没有死锁风险;`async` 用得对不对;SignalR group / connection 生命周期。
4. **安全** — 输入校验(FluentValidation)、鉴权/授权、SQL 注入、XSS、JWT 校验、围观者不能发落子命令。
5. **测试质量** — 不只看覆盖率,看有没有测 *行为* 而不是 *实现*;mock 有没有滥用。
6. **可读性** — 命名、函数长度、注释解释 *为什么* 而不是 *做了什么*。
7. **性能** — 只在有指标/profile 证据时提,别凭感觉要求优化。

### 评论礼仪

给评论加前缀,让作者知道哪些必改、哪些可选:

- `must:` — 必须改,否则不合并(正确性 / 安全 / 架构违规)
- `should:` — 强烈建议改,需要理由才能保留
- `nit:` — 小意见 / 风格,作者可自行决定
- `question:` — 纯提问,不是要求改动
- `praise:` — 写得好就说出来

作者可以拒绝 `should` / `nit`,但要简短说明。`must` 未解决前不要点 approve。

## Shell

Windows host, bash shell. Use Unix syntax (`/dev/null`, forward slashes) in commands, not `NUL` / backslashes.
