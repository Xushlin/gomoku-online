# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

Multi-platform online Gomoku (五子棋) game. Players register, create/join rooms, play real-time matches (via SignalR) with room chat, spectator chat, and urge-opponent shortcuts. ELO-based ranking with special icons for the top three. Also supports human-vs-AI with multiple difficulties, plus game-record storage and replay.

## Current phase

Project is in initial scaffolding. Completed:
- [x] 4-layer Clean Architecture solution skeleton (`backend/Gomoku.slnx`)
- [x] OpenSpec initialized (`openspec/config.yaml`)
- [x] Backend MVP (auth, rooms, gameplay, AI, ELO, replay, presence, observability, rate limiting) — see `openspec/specs/` and `openspec/changes/archive/`
- [x] `frontend-web/` Angular 21 scaffold (Tailwind v4, Material/CDK, Transloco zh-CN+en, ThemeService, LanguageService, Vitest) — `add-web-scaffold`. No business pages yet.

Not yet done:
- `frontend-desktop/`, `frontend-mobile/` directories are empty — frontend-web business pages (`add-web-auth-pages` / `-lobby` / `-game-board` / `-replay-and-profile`) come next.

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
- **Tailwind CSS** + **Angular Material** + **`@angular/cdk`**(overlays / dialogs / a11y)
- **Transloco** for runtime i18n (initial locales: `zh-CN`, `en`;扩展靠添 JSON + 注册一条)
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

### 命名 & 结构

- Filenames: **kebab-case**. Classes: PascalCase.
- Use **standalone components** (Angular 17+ style) — don't create new NgModules unless unavoidable.
- Prefer **Signals** over `BehaviorSubject` for local state.
- When Tailwind class strings get long, extract via `@apply` into a custom utility.
- All HTTP lives in `services/api/`. Components must not call `HttpClient` directly.

### 设计 & UX(硬规则)

- **Dark mode 从第 1 天起必须可工作**。方案:`ThemeService`(Signal)在 `<html>` 上 toggle `dark` class;颜色全走 CSS 变量(`--color-bg`、`--color-primary` 等),Tailwind 用 `dark:` variant 辅助;绝不在组件里写 `bg-gray-900` / `text-white` 这种硬编码暗色。
- **响应式(mobile-first)**。每个路由 MUST 在 **375px** 宽度下可用,再逐步 `sm: / md: / lg: / xl:` 扩展到 1440px+。Tailwind 默认断点够用;不许为单屏幕分辨率专门写 CSS。
- **现代化 UX**:CSS 过渡优于 JS 动画、`focus-visible` 环可见、骨架屏避免布局抖动、键盘可达每个交互元素、尊重 `prefers-reduced-motion`、loading / empty / error 三态都要有 UI,不许 "loading…" 纯文字糊弄。

### 性能

- **懒加载(Lazy loading)**是强制的:根壳(shell + login)外的每个路由都用 `loadComponent` / `loadChildren`。单 lazy chunk 控制在 **gzip 后 < 200 KB**,超了拆。
- `<img loading="lazy">` 默认,只有首屏 above-the-fold 的例外。
- SignalR 客户端装在服务里,**首次订阅时才连**,不在 app bootstrap 就握手 —— 未登录 / 不上对局页时不必要的连接是浪费。

### 架构 —— SOLID,易扩展

- **单一职责**:一个组件只干一件事。Container(拿数据 / 分发事件)与 Presentational(纯渲染输入)分层,**不混**。
- **依赖倒置**:Service 有可能有替代实现(mock 测试 / 未来换 API 客户端 / 换状态后端)时,用**抽象类作为 DI token**,inject by token 不 by concrete。
- **开闭**:新增主题 / 新增 locale / 新增对局难度 MUST 是"加一条配置或一个文件"级的改动,不改现有代码。
- 组合优于继承。横切行为走 directive / pipe,不走 base class。
- 组件 < 200 LOC。超出就抽 service 或 store。

### 对话框 & 覆盖层

- 对话框 / 浮层 / Popover MUST 基于 **Angular CDK**(`@angular/cdk/dialog` 或 `@angular/cdk/overlay`)。Material 的 `MatDialog` 也行(它包了 CDK);**不许**手写 `<div>` + `*ngIf` 土制模态 —— focus trap / ESC / backdrop / a11y attrs 都要,CDK 免费给。

### i18n —— 国际化

- 用 **Transloco**(或 Angular i18n + ICU 运行时切换)。
- 首发 locale:`zh-CN`(简体)+ `en`。文件在 `src/assets/i18n/<locale>.json`,扁平 key / 点分路径(`room.join.button` 之类)。
- 模板里**禁止硬编码**中文或英文;一律 `{{ 'key.path' | translate }}`。date / number 走 Angular 的 `formatDate` / `formatNumber` 带 locale 参数。
- 语言切换由 `LanguageService`(Signal)承载,持久化到 `localStorage`;初值 = `localStorage` → `navigator.language` → `en` 回退链。
- 加新 locale = 新增 `i18n/<locale>.json` + `LanguageService.supported` 注册一行,**不改**其它代码。

### 主题切换(Material / System / 更多)

- 主题以**注册表**形式:`ThemeService.register(name, tokens)`,`tokens` 是一组 CSS 变量值(`--color-primary`、`--color-surface`、`--radius-card`、`--shadow-elevated` 等)。切换主题 = 在 `<html>` 设 `data-theme="<name>"` + 持久化到 `localStorage`。
- 首发两套:`material`(Angular Material 默认配色 + Material 圆角 / 阴影)、`system`(Apple / Fluent-ish 简洁风,更小圆角、更少阴影)。
- **Dark/Light 是主题的正交维度**:每个主题都有明暗两套 token 集合,`ThemeService` 的两个 signal(`themeName` / `isDark`)独立切换。
- 组件样式 MUST 引用 CSS 变量,不直接写色值;"这个按钮用主题蓝"= `var(--color-primary)`,不是 `#2962FF`。
- 加新主题 = 新增一份 tokens 文件 + `ThemeService.register(...)` 一行,**不改**任何组件。

### 前端测试

- Vitest 覆盖:有逻辑的 service / store、有条件分支的组件、i18n 管道、`ThemeService` / `LanguageService` 等横切 service。纯展示组件可跳。
- 对话框、路由守卫、SignalR 订阅等有副作用的路径,用 TestBed + `ComponentHarness` 写集成测试。

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
