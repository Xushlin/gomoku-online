# Tasks — add-web-tictactoe-ai

> 目标是**能玩上**。人人对战大厅、找人、排行榜、`/home` 参数化全部不做 —— 见 design D5。
> 判定完成的标准不是「测试绿」，而是「在浏览器里真的把一局一字棋下完」（§6）。

## 1. 后端：DTO 说出棋种

- [x] 1.1 `RoomStateDto` / `RoomSummaryDto` 各加 `string GameKey`。只增字段，既有字段不动。
- [x] 1.2 `RoomMapping.ToState` / `ToSummary` 映 `room.GameKey`。不加新依赖。
- [x] 1.3 `CreateAiRoomRequest` 已有可选 `GameKey`（`add-tictactoe` 加的），确认 controller 已透传 —— 若已透传则本项零改动。
- [x] 1.4 测试：两个 DTO 在 gomoku / tictactoe 房间上分别带对键；既有 DTO 测试不因新字段失败。

## 2. 前端：类型与 API

- [x] 2.1 `room.model.ts`：`RoomState` / `RoomSummary` 各加 `readonly gameKey: string`。
- [x] 2.2 `rooms-api.service.ts`：`createAiRoom(name, difficulty, humanSide?, gameKey?)`。省略时 body 里**不出现**该键 —— 与 `humanSide` 当初的处理一致。
- [x] 2.3 测试：带 / 不带 `gameKey` 时 POST body 的形状。

## 3. 前端：棋盘尺寸参数化

- [x] 3.1 `board.ts`：删掉 `const BOARD_SIZE = 15`，改为 `rows` / `cols` 两个 `input`，默认 15。
- [x] 3.2 `board.html`：grid 列数改为内联 style 绑定。**不能用 `grid-cols-{n}` 这类 Tailwind 类名** —— 编译期不可知，Tailwind 不会生成。
- [x] 3.3 越界走子静默忽略（尺寸失配时该画错，不该白屏）。
- [x] 3.4 `room-page`：由 `state.gameKey` 经 `GameCatalogService.byKey()` 取 `board`，传给 `<app-board>`；解析不出退回 15×15。`Board` 保持不认识 `gameKey`。
- [x] 3.5 **确认了，而且修掉了，没有记录成缺口。** 回放页确实复用同一个 `Board`，而且它自己拼一个 `RoomState` 喂进去 —— 编译直接报缺 `gameKey`，把问题顶到面前。填字面量 `'gomoku'` 能过编译但是在撒谎（一字棋回放会画成 15×15），而「刚赢一局 → 点查看回放」是主路径不是边角。所以给 `GameReplayDto` 也加了 `GameKey`，约 10 行。浏览器实测回放页 9 格。
- [x] 3.6 测试：默认 225 格；`[rows]="3" [cols]="3"` 得 9 格；越界走子不抛错。

## 4. 前端：manifest 与路由

- [x] 4.1 `game-manifest.ts` 加 `board?: { rows, cols }`。
- [x] 4.2 `gomoku/manifest.ts` 加 `board: { rows: 15, cols: 15 }`。
- [x] 4.3 `tictactoe/manifest.ts`：`status: 'available'`、`launchRoute: '/g/tictactoe'`、`board: { rows: 3, cols: 3 }`。
- [x] 4.4 `app.routes.ts` 加 `g/tictactoe`，`canMatch: [authGuard]` + `loadComponent`。
- [x] 4.5 测试：注册表里 tictactoe 是 available 且有 board；所有 available 的 match 游戏都有正整数 board。

## 5. 前端：人机页

- [x] 5.1 `games/tictactoe/ai-game/ai-game.ts` + `.html` —— 难度三档（默认 Medium）+ 先后手（默认黑）+ 开始按钮。
- [x] 5.2 房间名客户端生成，满足 3–50 字符。**不加房间名输入框**，也**不放宽后端校验**。
- [x] 5.3 页面写明两件事：Hard 档打不赢（一字棋是已解游戏），本棋种不计 ELO。开局前说，不让玩家输三局自己猜。
- [x] 5.4 失败要有真 UI：可翻译错误横幅 + 可重试；提交中按钮 disabled 防重复。
- [x] 5.5 组件 < 200 LOC；深色模式与 375px 宽度都要正常（用 CSS 变量，不写死颜色）。
- [x] 5.6 i18n：两份 locale 加 `tictactoe.*`，键集合必须一致。模板零硬编码文案。
- [x] 5.7 测试：默认值；开始时 `createAiRoom` 的四个入参；失败时不跳转且横幅出现；提交中不发第二个请求。

## 6. 真的玩一局（这一节是验收标准，不是可选项）

- [x] 6.1 起 dev server，登录，`/games` → 一字棋卡片可点。
- [x] 6.2 选 Hard + 黑，开始，确认跳进房间且棋盘是 **3×3 九格**。
- [x] 6.3 下完四局。Easy 赢两局（`XXX.O.O..`、`XXXO...O.`），Hard 输一局（它连反对角 `(0,2)(1,1)(2,0)`），另有两局因为我在 DOM 里翻查超过 60 秒被**超时判负** —— 那两局意外把 tasks §6.2 点名「最容易漏」的超时路径在真实全栈上验了一遍。
  - **发现一处必须现在修的问题**：结束弹窗显示「You won! **Five in a row.**」—— 三连的一字棋被告知「五子连珠」。这正是我在 `add-tictactoe` 审计里判为「装饰性、可推迟」的 `GameEndReason.Connected5`，而它**玩家直接看得见**。那个判断错了。改可见文案为中性措辞（zh「连成一线。」/ en「A line was completed.」）：具体措辞对一个棋种对、对另一个错，中性措辞对两个都对。**枚举名与翻译键不动** —— 改它们要牵动五份文件、规范、以及持久化的整数，那是 `generalize-match-contract` 的账。
- [x] 6.4 刷新页面，确认棋盘**仍是 3×3**（这条验的是 §1 的 `gameKey`，不是路由参数）。
- [x] 6.5 确认自己的 rating 没变（不计分），且五子棋大厅 `/home` 里没有出现一字棋房间。
- [x] 6.6 375px 宽度 + 深色模式各看一眼。
- [x] 6.7 零报错 —— 但第一次读到三条（两个 `NG0950`、一个 500）。它们 stack 里的 vite 哈希是 `?v=665727bb`，而当时构建是 `?v=be40ee14`，即旧构建残留在控制台缓冲里的陈旧条目；网络面板也找不到对应的失败请求。开一个**新标签页**（缓冲为空）重载并完整下一局，控制台全程为空。

## 7. 已知缺口（记录，不在本变更修）

- [x] 7.1 没做的，以及一件本打算记录却发现必须修的：

| 缺口 | 为什么 |
| --- | --- |
| 一字棋没有人人对战 | 需要大厅。`/home` 在五份 web spec 里是规范路径，参数化它是关于**五子棋** UX 的决定，不该藏在「让我玩上一字棋」里一起过审。留给中国象棋（它必须要人人对战）。 |
| `/home` 里没有一字棋入口 | 同上 —— `/home` 就是五子棋大厅。一字棋只从 `/games` 目录进。 |
| 一字棋没有排行榜 | 它不计分。完美对弈必和、Hard 不可战胜，排行榜会是一列空的。`add-per-game-rating` 之后才有意义。 |
| ~~回放页棋盘尺寸~~ | **已修**，见 §3.5。 |
| `GameEndReason.Connected5` 的**枚举名** | 仍叫这个。改名要动五份文件、规范、持久化整数，随 `generalize-match-contract` 走。**但它的可见文案已经改了** —— 见 §6.3。 |

## 8. Ship

- [x] 8.1 `dotnet build` + `dotnet test` 全绿。
- [x] 8.2 `npm run lint` + `npm test -- --run` 全绿。
- [x] 8.3 `openspec validate add-web-tictactoe-ai --strict`。
- [x] 8.4 PR 描述链接本变更，写明 §6 的实测结果与 §7 的缺口。
