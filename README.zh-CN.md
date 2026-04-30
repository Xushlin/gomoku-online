# Gomoku Online

[English](README.md) · **简体中文**

[![CI](https://github.com/Xushlin/gomoku-online/actions/workflows/ci.yml/badge.svg)](https://github.com/Xushlin/gomoku-online/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

实时多人五子棋。可以跟真人在 SignalR 上联机对战,也可以跟内置 AI(三档难度)单打。
Web 客户端是 Angular 21 + Tailwind,后端是 .NET 10 + Clean Architecture + CQRS。

## 状态

| 层               | 进度                                                       |
| ---------------- | ---------------------------------------------------------- |
| 后端 (`backend/`)            | **MVP 完成** — 鉴权、房间、对局、AI、ELO、回放、在线状态、可观测性、限流 |
| Web (`frontend-web/`)         | **v1 功能完整** — 鉴权页、大厅、实时对局、回放、个人主页、AI 选边、音效、在线徽章 |
| 桌面 (`frontend-desktop/`)    | 空 — 待 Electron 包壳                                       |
| 移动 (`frontend-mobile/`)     | 空 — 待 Flutter                                             |

## Web 端做了什么

- **实时对战** — JWT 鉴权,SignalR Hub 在 `/hubs/gomoku`,催促 / 聊天 / 观战 / 认输 / 解散全套流程。
- **AI 对手** — Easy / Medium / Hard,创建房间时可选执黑或执白。
- **回放播放器** — `/replay/:id` 复用实时对局的 `Board` 组件(只读模式),带步进控件(▶ 播放 / ⏪ 上一步 / ⏩ 下一步 / 进度条 / 0.5× / 1× / 2× 速度)。
- **公开主页** — `/users/:id` 展示积分、胜负平、分页对局列表,以及在线状态点。
- **找人** — 大厅卡片防抖 + 前缀搜索 + 一键跳到对方主页。
- **主题与皮肤** — Material / System 主题 × 浅 / 深色,两套棋盘皮肤(Wood / Classic),两套音效(Wood / Chiptune),全部 header 一键切换。
- **i18n** — 一开始就同时支持简体中文和英文,每个字符串走 Transloco;加第三种语言只要一个文件 + 一行 register。
- **现代 Angular** — Standalone 组件、全程 Signals、抽象类作为 DI token、Lazy 路由、Vitest 单测。

## 快速开始

```cmd
:: Windows:双击 start-dev.cmd,或者
start-dev.cmd
```

会开两个窗口 —— 后端在 `http://localhost:5145`,Angular dev server 在
`http://localhost:4200`,前端起来后自动打开浏览器。一个小的 dev 代理把
`/api/*` 和 `/hubs/*` 从 `:4200` 转发到后端,相对路径就能直接用。

### 单独跑各个部分

后端(需要 .NET 10 SDK):

```bash
cd backend
dotnet restore Gomoku.slnx
dotnet run --project src/Gomoku.Api --launch-profile http
```

Web(推荐 Node 20+):

```bash
cd frontend-web
npm install
npm start
```

### 测试

```bash
# 后端 — Domain + Application 单测(约 390 个)
cd backend && dotnet test Gomoku.slnx

# 前端 — Vitest(约 178 个)
cd frontend-web && npm test
```

## 项目结构

```
backend/
  Gomoku.slnx                       (XML 解决方案文件 — 所有项目目标 net10.0)
  src/
    Gomoku.Domain/                  (实体、值对象、聚合;零外部依赖)
    Gomoku.Application/             (MediatR handler、DTO、基础设施接口)
    Gomoku.Infrastructure/          (EF Core、持久化;实现 Application 的接口)
    Gomoku.Api/                     (ASP.NET 宿主、Controller、SignalR Hub、DI 装配点)
  tests/
    Gomoku.Domain.Tests/
    Gomoku.Application.Tests/

frontend-web/
  src/app/
    app.{config,routes}.ts          (根配置 + 懒加载路由)
    core/                           (横切服务 — auth、api、i18n、theme、sound、realtime)
    pages/                          (auth、lobby、rooms、users、replay)
    shell/                          (Header + Shell 容器)
  public/i18n/{en,zh-CN}.json
  proxy.conf.json                   (dev 代理 → :5145)

openspec/
  config.yaml
  specs/                            (现行能力规约 —— 行为单一事实源)
  changes/
    archive/                        (每个已交付的变更全部保留,审计足迹完整)

start-dev.cmd                       (Windows 一键启动脚本)
```

## OpenSpec 工作流

仓库里每个 feature 都走过 提议 → 规约增量 → 任务 → 实施 → 归档 这一套。
`openspec/specs/` 里是现行规约,把行为写到"requirement + WHEN / THEN scenario"粒度;
`openspec/changes/archive/` 留下了每次变更"为什么 + 怎么做"的完整轨迹,从项目第一天到现在都查得到。

常用命令:

```bash
openspec list               # 列出活跃 change
openspec validate <name>    # 归档前校验
openspec archive <name>     # 归档:把 change 的 spec delta 提到 live specs
```

贡献指南(架构约束、提交 / PR 规范、代码审查标准)看 [`CLAUDE.md`](CLAUDE.md)。

## 后端架构

Clean Architecture,严格分层(`Domain ← Application ← Infrastructure / Api`)。
MediatR 跑 CQRS — 每个写操作是一个 `Command`,每个读操作是一个 `Query`,
一文件一 handler。SignalR Hub 只做消息路由,把请求派发给 MediatR,再把结果推回去,
不写业务逻辑。

本地开发用 SQLite(`backend/src/Gomoku.Api/gomoku.db`,首次启动自动迁移);
需要扩展时切 SQL Server 即可。

JWT 鉴权用 HS256 —— `appsettings.Development.json` 里有 dev-only 密钥。
**生产环境** 必须用环境变量 `GOMOKU_JWT__SIGNINGKEY` 覆盖;Production 模式下密钥为空时直接拒启动。

CORS、限流、结构化日志(Serilog)、统一异常 → ProblemDetails 中间件全都布好了。

## Web 架构

只用 Angular 21 standalone 组件,不建 NgModule。Signals 优先,NgRx 故意没用。
Tailwind v4 + token 层(`tailwind.css` 里 `@theme` 块 + `tokens.css` 里
`[data-theme="..."]` 级联运行时值);视觉属性全部走 token utility,
不允许 hex 色值或 `bg-gray-*` 字面量。

三个可插拔的偏好服务都是同一种形态 —— `ThemeService`、`BoardSkinService`、`SoundService`。
每个都是抽象类作为 DI token + 默认实现,提供 `register(name, tokens)` 注册表,
状态持久化到 localStorage,通过 `<html data-*>` 属性应用样式。
加新主题 / 棋盘皮肤 / 音效包 = 一个 TS 文件 + 一行 register,组件零修改。

测试用 Vitest,没用 Karma。模板里用控制流块(`@if / @for / @switch / @let`),
不用结构化指令。TypeScript strict 模式。

## 接下来

按优先级粗排 —— 细节看 `openspec/changes/archive/`,现状看 `openspec/specs/`:

- Electron 桌面壳(`frontend-desktop/`)
- Flutter 移动客户端(`frontend-mobile/`)
- 音量条 / 更多音效包 / 更多棋盘皮肤
- 浏览器推送通知

## License

[MIT](LICENSE) — 随便用,保留版权声明就行。
