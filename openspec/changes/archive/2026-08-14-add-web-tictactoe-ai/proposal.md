## Why

一字棋的后端在 `add-tictactoe` 里已经完全可玩 —— 建房、Hard bot 走子、认输、回放都通过了真实 HTTP 冒烟。但目录页上它仍是「即将上线」，因为没有任何页面能打开它。后端能玩而玩家玩不到，是这两个变更之间唯一剩下的东西。

本变更只补**最小可玩路径**：人机对战。人人对战的大厅、找人、排行榜都不做 —— 一字棋不计分，排行榜本来就没内容；而大厅要不要参数化是一个会动到已发布五子棋 UX 的决定，不该被「我想玩上」这件事裹带着一起做。

## Scope

`/g/tictactoe` = 选难度 + 选先后手 → 建 AI 房 → 直接进 `/rooms/:id` 下棋。

**不做**：一字棋的房间列表 / 创建人人房间 / 找人 / 排行榜卡片、`/home` 的任何改动、大厅参数化。

## What Changes

### 后端：DTO 得说出自己是哪个棋种（一个字段）

`RoomStateDto` 与 `RoomSummaryDto` 增加 `GameKey`。

这不是可选的顺手改动，而是本变更的**前置阻塞**：玩家直接打开 `/rooms/:id`（刷新页面、收藏链接、从「我的对局」点进来）时，客户端手上只有房间 id，而现在的 DTO 里没有任何字段说明这是 3×3 还是 15×15。棋盘只能猜，而猜错就是画错。

`Room.GameKey` 早就存在，`ToState` / `ToSummary` 不需要任何新依赖 —— 就是把已有的字段映出来。

### 前端：棋盘尺寸变成输入

`board.ts` 里 `const BOARD_SIZE = 15` 是后端 `Board(15, 15, 5)` 的前端翻版。改为 `rows` / `cols` 两个 `input`，默认 15 —— `Board` 保持纯展示组件，尺寸由容器给它。

`room-page` 用 `state.gameKey` 经 `GameCatalogService` 查出尺寸传进去。

### 前端：`GameManifest` 声明对战棋种的盘面

`board?: { rows: number; cols: number }`，只对 `category: 'match'` 有意义。

**这里有一份刻意的重复**：盘面尺寸同时存在于后端 `NInARowRules` 与前端 manifest。见 design D1 —— 结论是这份重复此刻比它的替代方案便宜，且失配的症状是立刻可见的（棋盘画错格数），不是无声的。

### 前端：`/g/tictactoe` 一个懒加载页面

难度（Easy / Medium / Hard）+ 先后手（黑 / 白）→ `POST /api/rooms/ai` 带 `gameKey: 'tictactoe'` → `navigateByUrl('/rooms/' + id)`。

房间名不让玩家填 —— 人机局的房间名对任何人都不可见（它不进任何大厅列表），要求玩家为一局自己跟机器下的棋起名字是纯粹的摩擦。客户端生成一个满足后端 3–50 字符校验的名字。

### 前端：manifest 翻转 + i18n

`tictactoe/manifest.ts` 的 `status: 'planned'` → `'available'`，加 `launchRoute: '/g/tictactoe'` 与 `board`。这是 `add-platform-catalog` 承诺的「状态翻转只动自己的 manifest」第二次兑现。

`games.tictactoe.*` 已存在；新增本页自己的 `tictactoe.*` 键，两份 locale 都加。

## Impact

- **Affected specs:** `room-and-gameplay`(MODIFIED ×2 — 两个 DTO)、`platform-catalog`(MODIFIED ×2 — manifest 形状 + 注册表)、`web-game-board`(MODIFIED ×1)、`web-i18n`(MODIFIED ×1)，新增 capability `web-tictactoe`。
- **Affected code:** `Gewu.Application/Common/DTOs/RoomDtos.cs`、`Common/Mapping/RoomMapping.cs`、`frontend-web/src/app/games/{game-manifest,tictactoe}`、`pages/rooms/room-page/{room-page,board}`、`core/api/{rooms-api.service,models/room.model}`、`app.routes.ts`、两份 i18n JSON。
- **无迁移、无破坏性 wire 变更**：DTO 只增字段，`createAiRoom` 的 `gameKey` 参数可选且缺省 `gomoku`。五子棋的既有路径一行不改。
- **Out of scope:** 大厅参数化（`/home` 保持为五子棋大厅）、一字棋的人人对战入口、per-game 排行榜。三者各有各的驱动变更。
