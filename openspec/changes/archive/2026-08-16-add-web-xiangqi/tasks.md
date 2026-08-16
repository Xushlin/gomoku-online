# Tasks — add-web-xiangqi

## 1. 盘面模型（前端）

- [x] 1.1 `src/app/games/xiangqi/position.ts`：`XiangqiPieceType`、`XiangqiPiece`、`INITIAL_POSITION`（32 枚子）、`positionAfter(moves)`。
- [x] 1.2 方位与服务端 `XiangqiBoard.Initial()` 对齐：红方（`Stone.Black`）第 5–9 行，黑方（`Stone.White`）第 0–4 行。
- [x] 1.3 缺 `fromRow`/`fromCol` 的着法静默跳过。
- [x] 1.4 `position.spec.ts`：普查（32 子 / 每方子力清单）、左右镜像对称、红方在下、吃子后原位为空、无起点着法被忽略。

## 2. 棋盘组件

- [x] 2.1 `src/app/games/xiangqi/board/xiangqi-board.ts` + `.html`：10×9 交叉点，每点一个 `<button>`。
- [x] 2.2 输入 `state` / `mySide` / `submitting` / `readonly`，输出 `(pieceMove)`。不注入任何服务。
- [x] 2.3 两步交互：选子 → 落点；再点同一枚取消；点另一枚己方子改选；`Escape` 取消。
- [x] 2.4 `Stone.Black` 渲染为红、`Stone.White` 渲染为黑；字形按方区分（帥仕相傌俥炮兵 / 將士象馬車砲卒）。
- [x] 2.5 禁用条件：`readonly` / `submitting` / 观众 / 非 `Playing` / 非本方回合。
- [x] 2.6 无障碍：每点 `aria-label`（行列 + 棋子名或「空」），选中态用 `aria-pressed`。
- [x] 2.7 CSS（`global.css`）：`.xq-board` 9∶10 宽高比、交叉点格线、楚河汉界、两侧九宫斜线，全部走主题变量。
- [x] 2.8 `xiangqi-board.spec.ts`：90 个按钮 / 32 枚子、红先手且 `帥` 在 (9,4)、两步 emit、取消、改选、拿不起对方子、非本方回合全禁用、观众只读。

**格线画法与 `.board-grid` 不同,这是有意的。** 五子棋的格线是层叠 repeating-gradient;象棋的河界缺口与九宫斜线**无法**用重复渐变表达 —— 「这七条竖线在河边断掉」只能由一条 path 说清楚。SVG 覆盖层按半格内缩,让 path 的整数坐标正好落在按钮中心。

## 3. Hub

- [x] 3.1 `GameHubService` 抽象类加 `movePiece(roomId, fromRow, fromCol, row, col)`。
- [x] 3.2 `DefaultGameHubService` 实现：`invoke('MovePiece', roomId, fromRow, fromCol, row, col)`。
- [x] 3.3 `game-hub.service.spec.ts`：断言 invoke 收到 5 个参数且方法名是 `MovePiece`；`makeMove` 行为不变。

## 4. 房间页

- [x] 4.1 `isXiangqi` computed（来自 `state().gameKey`）；模板 `@if` 在两个棋盘间选。
- [x] 4.2 `handlePieceMove` → `hub.movePiece`，错误经 `hubErrorToKey`，并发错误 rehydrate。
- [x] 4.3 被拒绝后保留选中态。
- [x] 4.4 `room-page.spec.ts`：象棋房间渲染象棋盘、五子棋房间不变、走子调 `movePiece` 不调 `makeMove`、未知棋种退回 `<app-board>`。

## 5. 回放页

- [x] 5.1 同样的 `@if` 分支，`[readonly]="true"`。
- [x] 5.2 `replay-page.spec.ts`：象棋回放渲染象棋盘且全禁用；scrubber 回退时被吃的子重新出现；五子棋回放不变。

## 6. 入口页与路由

- [x] 6.1 `src/app/games/xiangqi/ai-game/ai-game.ts` + `.html`：难度三档 + 执红/执黑，默认 Medium + 执红。
- [x] 6.2 `createAiRoom(name, difficulty, humanSide, 'xiangqi')` → `/rooms/:id`；房间名客户端生成。
- [x] 6.3 文案：不计分；说明 AI 是限深搜索。**不得**出现任何「不可战胜 / 必和」措辞 —— 由一条读**已发布文案**（而非模板）的测试守着,因为写下那句话的诱惑发生在 locale 文件里。
- [x] 6.4 `app.routes.ts` 加 `g/xiangqi`（`loadComponent` + `authGuard`）。
- [x] 6.5 `ai-game.spec.ts`：默认值、开局跳转参数、失败可重试、提交中防重复。
- [x] 6.6 新增 `app.routes.spec.ts`：所有 `g/*` 路由都懒加载且带 `authGuard`,且每个声明了 `launchRoute` 的 manifest 都有对应路由。此前没有任何测试覆盖路由表 —— 「忘了懒加载」不会让任何组件测试变红,只会把一个棋种拖进初始包。

## 7. 目录与 i18n

- [x] 7.1 `manifest.ts`：`status: 'available'`、`launchRoute`、`board: { rows: 10, cols: 9 }`。
- [x] 7.2 `zh-CN.json` / `en.json` 增加 `xiangqi.*` 子树（各 30 键）。
- [x] 7.3 目录页测试：象棋卡片可交互；排行榜入口由 platform-catalog 既有的「未计分棋种无排行榜」约束覆盖,**未为象棋开任何例外**;浏览器实测确认卡片上没有排行榜链接。
- [x] 7.4 i18n 一致性由既有 `i18n-parity.spec.ts` 自动覆盖（键集合零差集）。

## 8. 验证

- [x] 8.1 `npm run lint` 全绿；`npm run test:ci` **424 passed**（本变更前 361）。
- [x] 8.2 `npm run build` 成功。懒加载 chunk：`room-page` 6.44 kB gzipped、`replay-page` 3.03 kB、两个 `ai-game` 各 1.54 kB —— 全部远低于 200 kB 上限。
- [x] 8.3 浏览器实测：`/games` → 象棋 → 建 Medium 人机房 → 走子（炮二平五）→ AI 应手 → 吃子（炮打卒,31 子）→ 认输 → 结束弹窗 → 回放。
- [x] 8.4 实测覆盖 zh-CN 与 en、375px、深色模式、三套棋盘皮肤。
- [x] 8.5 实测非法着法路径：帥 走斜线被拒 → 提示「That move isn't allowed.」→ **选中态仍在** → 换个落点能走。
- [x] 8.6 `openspec validate add-web-xiangqi --strict` 通过。

### 8.7 实测中发现并修掉的两件事（都不是推演出来的）

**① 非法着法落在「Something went wrong. Please try again.」上。** `hubErrorToKey` 的关键字表是按五子棋写的,象棋的拒绝措辞一条都不匹配。这在五子棋里无所谓 —— 它的棋盘只让你点空格,`invalid-move` 几乎不可达。象棋的棋盘**刻意不懂规则**,所以被拒绝是玩家了解棋子怎么走的常规途径,而「应用出错了」是最坏的说法。已补上象棋的措辞,并给**自将/照面**单独一条文案:它是象棋里最常见的一种拒绝,而「这步不合法」不告诉玩家他漏看了什么。

**② 棋子颜色不是主题变量,只是带 fallback 的字面量。** 我写的是 `var(--xq-red, #b3261e)`,但没有任何皮肤定义 `--xq-*`,所以它们实际上是常量,不随皮肤或深浅色变化。已把 `pieces: { bg, red, black }` 加进 `BoardSkinTokens` 并在三套皮肤里各自定值 —— 加一套皮肤就必须声明它们,这是**编译期**保证,不是约定(改完立刻有一个测试 fixture 编译失败,证明机制生效)。

约束写进了 token 的文档注释:皮肤可以自选**深浅**,不能自选**色相** —— 红方不红的象棋盘在任何主题下都是坏的。

## 9. 归档前必答

- [x] **9.1 前端初始布局与后端 `XiangqiBoard.Initial()` 是否逐子一致。**

  逐项比对通过:底线序 `車馬象士將士象馬車`(镜像对称)、炮在第 2/7 行的 1、7 列、兵卒在第 3/6 行的偶数列、红方(`Stone.Black`)底线为第 9 行。

  更有力的是一条**经验**证据:浏览器里客户端把 (7,1) 标为「红炮」并据此发出 `MovePiece(7,1 → 7,4)`,服务端接受了它,随后多步同样成立。两边若不一致,服务端会拒绝这些「在错盘面上看起来合法」的着法 —— 这正是 design D1 里列的第二张安全网,现在它被实际走过一遍。

- [x] **9.2 实际净变更行数。**

  预估「超过 400 行」,实际 **frontend-web 1906 行新增 / 25 行删除**(含 openspec 共 2469 行)。差得很远,拆开看:

  | | 行数 |
  | --- | --- |
  | 测试 | ~800 |
  | i18n(两份 locale,30+1 键) | ~74 |
  | CSS | ~141 |
  | 实现代码 | ~550 |
  | openspec 文档 | ~560 |

  也就是说**实现本身约 550 行**,其余是测试、翻译、样式与文档。预估错在只想着实现:一块新棋盘的 CSS 与两份 locale 加起来就超过了我猜的整个变更。

  仍然不认为它该拆:只有棋盘没有入口谁也打不开,只有入口没有棋盘点进去是画错的盘。

## 10. 遗留（不在本变更内）

- **初始包体积超预算**:`ng build` 报 `bundle initial exceeded maximum budget`(500 kB 预算 / 535 kB 实际)。**这条在本变更之前就存在**(532 kB),本变更加了约 2.6 kB(一条路由 + 全局 CSS)。它是 warning 不是 error,CI 仍绿,但没人在缩它。
- **`hubErrorToKey` 是对服务端英文散文的模糊匹配** —— 等于把领域异常的措辞抄了第二份,没有任何机制保持同步。本变更抬高了它的赌注(见 8.7 ①),正确的修法是 hub 契约上的结构化错误码,那是错误处理的横切改动,单独一笔。
- **自将文案未在浏览器里复现**:构造一个送将局面需要很多步。映射由一条对着 `XiangqiRules.Apply` **逐字**抄来的服务端消息写的单元测试覆盖,浏览器只验到了 `invalid-move` 那条路径。
