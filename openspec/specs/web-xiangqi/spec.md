# web-xiangqi Specification

## Purpose
TBD - created by archiving change add-web-xiangqi. Update Purpose after archive.
## Requirements
### Requirement: 客户端持有象棋初始摆子，并由 `from → to` 历史推导盘面

`src/app/games/xiangqi/position.ts` SHALL 导出一份 32 枚子的初始布局，以及一个 `positionAfter(moves)` 纯函数：从初始布局出发，按 `MoveDto` 的 `fromRow`/`fromCol` → `row`/`col` 逐步应用，终点覆盖（被吃的子消失）。

这份布局是一份**刻意的复制** —— 服务端的 `XiangqiBoard.Initial()` 是同一份事实。接受它的理由见 design D1：失配的表现是第零步就画错整块盘（最容易被发现的失效形态），且服务端会拒绝任何在错盘面上「看起来合法」的着法。它同时不是服务端状态而是这个游戏的规则，与「棋盘是 10×9」同类。

方位 MUST 与服务端一致：红方（`Stone.Black`）占第 5–9 行，第 9 行是它的底线；黑方（`Stone.White`）占第 0–4 行。

缺 `fromRow`/`fromCol` 的着法（落子类棋种的形状）MUST 被静默忽略，MUST NOT 抛错 —— 一个畸形或跨棋种的历史应该画出一块可能不对的盘，而不是白屏。

#### Scenario: 初始布局的普查
- **WHEN** 检视初始布局
- **THEN** 共 32 枚子；每方各 1 将帥、2 士仕、2 象相、2 馬傌、2 車俥、2 砲炮、5 卒兵

#### Scenario: 左右镜像对称
- **WHEN** 把初始布局沿第 4 列镜像
- **THEN** 得到的布局与原布局相同 —— 这条抓的是「打错一个坐标」这个真实失效模式

#### Scenario: 红方在下
- **WHEN** 检视初始布局中所有 `Stone.Black` 的子
- **THEN** 它们的行号全部 ≥ 5；所有 `Stone.White` 的子行号全部 ≤ 4

#### Scenario: 吃子后原位为空
- **WHEN** 对初始布局应用一步把某子走到对方子所在的交叉点
- **THEN** 起点变空，终点是走过去的那枚子，被吃的子不再出现在盘上

#### Scenario: 没有起点的着法被忽略
- **WHEN** 历史里含一条 `fromRow`/`fromCol` 为 `null` 的着法
- **THEN** 该步被跳过，其余步正常应用，MUST NOT 抛错

### Requirement: `XiangqiBoard` 组件 —— 10×9 交叉点棋盘，两步选子落子

`src/app/games/xiangqi/board/xiangqi-board.ts` SHALL 提供一个独立展示组件，渲染 10 行 × 9 列的交叉点，每个交叉点是一个 `<button type="button">`。

它 MUST NOT 是 `Board` 的参数化：`Board` 渲染「每格有没有子」并且一步落子，象棋渲染「哪个子在哪个交叉点」并且两步走子（见 design D4）。两个组件各自独立，`Board` 在本变更中 MUST NOT 被修改。

组件保持**纯展示**：MUST NOT 注入 `GameCatalogService`、MUST NOT 注入 `GameHubService`、MUST NOT 认识路由。走子意图通过 `(pieceMove)` output 交给容器。

视觉 MUST 全部走主题变量，MUST NOT 硬编码颜色 —— 与既有 `.board-grid` 同一约定。棋盘 MUST 画出楚河汉界与两侧九宫斜线；宽高比 MUST 为 9∶10（既有 `.board-grid` 的 `aspect-ratio: 1/1` 对象棋是错的）。

#### Scenario: 渲染 90 个交叉点
- **WHEN** 组件以初始局面渲染
- **THEN** 存在 90 个 `<button>`，其中 32 个含棋子

#### Scenario: 不修改既有棋盘组件
- **WHEN** 比对本变更的 diff
- **THEN** `src/app/pages/rooms/room-page/board/board.ts` 与 `board.html` MUST NOT 出现在其中

### Requirement: `Stone.Black` 在象棋中显示为红方

`XiangqiBoard` SHALL 把 `Stone.Black` 的棋子渲染成**红色**、`Stone.White` 渲染成**黑色**。

这不是笔误。`Game` 从 `Stone.Black` 开局而象棋红先，所以本棋种里 `Stone.Black` 就是红方 —— 这正是 `add-xiangqi` 让 Domain 一行不改的原因，代价全部落在显示层，而这里就是那一层。

棋子字形 MUST 按方区分：红方 `帥 仕 相 傌 俥 炮 兵`，黑方 `將 士 象 馬 車 砲 卒`。

**本条存在的目的是防止有人把它「修正」回去。**

#### Scenario: 先手方是红的
- **WHEN** 渲染初始局面
- **THEN** `Stone.Black` 的子带红色样式类，且第 9 行正中的字是 `帥`

#### Scenario: 后手方是黑的
- **WHEN** 渲染初始局面
- **THEN** `Stone.White` 的子带黑色样式类，且第 0 行正中的字是 `將`

### Requirement: 两步交互 —— 选子、落点，且第一步可以反悔

`XiangqiBoard` SHALL 维护一个本地的「已选中起点」状态：

- 点自己的子 → 选中它并高亮。
- 已有选中时点一个非自己子的交叉点 → emit `(pieceMove)`，载荷为 `{ from, to }`。
- 点**同一枚**已选中的子 → 取消选择，MUST NOT emit。
- 点**另一枚**自己的子 → 改选它，MUST NOT emit（那既不是吃自己，也不该发一个必然失败的请求）。
- `Escape` → 取消选择。

组件 MUST NOT 判定着法是否合法（design D2）。它只做两件不需要规则的事：只能拿起自己的子，以及非本方回合时整块盘只读。

以下任一为真时全盘 `disabled`：`readonly`、`submitting`、`mySide() === 'spectator'`、`state.status !== 'Playing'`、非本方回合。

每个交叉点 MUST 有可翻译的 `aria-label`，含行列与该点的棋子（或「空」）；选中的子 MUST 以 `aria-pressed` 表达，MUST NOT 只靠颜色。

#### Scenario: 选子再落点
- **WHEN** 红方回合，点 `(9,0)` 的俥，再点 `(8,0)`
- **THEN** `(pieceMove)` emit 一次，载荷为 `{ from: {row:9,col:0}, to: {row:8,col:0} }`

#### Scenario: 再点一次取消
- **WHEN** 点 `(9,0)` 后再点 `(9,0)`
- **THEN** MUST NOT emit；选中态清空

#### Scenario: 改选另一枚子
- **WHEN** 点 `(9,0)` 后点 `(9,1)`（也是自己的子）
- **THEN** MUST NOT emit；选中态变为 `(9,1)`

#### Scenario: 拿不起对方的子
- **WHEN** 红方回合，点一枚黑子且当前无选中
- **THEN** MUST NOT emit，MUST NOT 进入选中态

#### Scenario: 非本方回合只读
- **WHEN** `currentTurn` 是对方
- **THEN** 全部 90 个按钮 `disabled`；点击不触发任何事件

#### Scenario: 观众只读
- **WHEN** `mySide() === 'spectator'`
- **THEN** 全部按钮 `disabled`

#### Scenario: 组件不判定合法性
- **WHEN** 检索组件源码
- **THEN** MUST NOT 存在任何棋子走法规则（马走日、象飞田、炮翻山、九宫、河界限制等）

### Requirement: `RoomPage` 按棋种选择棋盘渲染器，并把走子交给 `movePiece`

`RoomPage` SHALL 依据 `state().gameKey` 在 `<app-board>`、`<app-xiangqi-board>` 与 `<app-chain-board>` 之间选择，并把 `(pieceMove)` 接到 `hub.movePiece(roomId, from.row, from.col, to.row, to.col)`、把 `(wordSay)` 接到 `hub.sayWord(roomId, word)`。

选择方式是容器模板里的 `@if`，MUST NOT 引入棋盘组件注册表。

**本条此前的理由说「这个分支已知只有两个，且没有第三个在路上（对战族只剩 成语接龙，它没有网格盘面）」，并附了一句「若真出现第三种形状，那时再抽同样便宜」。第三种形状到了，那句话被检验了，成立：**多一条 `@else if` 是六行，两侧绑定仍然类型安全；注册表要换成动态组件、并放弃对 `(wordSay)` 的编译期检查。所以结论不变，而它现在是量过的，不是预测的。

失败路径 MUST 与既有落子一致：`HubException` 经 `hubErrorToKey` 映射成可翻译提示；并发错误额外 `getById → applySnapshot`。被拒绝后选中态 MUST 保留 —— 玩家多半想换个落点，而不是重新找那枚子。

未知棋种 MUST 退回 `<app-board>`，MUST NOT 白屏。**声明为无盘面的棋种走 `<app-chain-board>`** ——「没有盘面」与「不认识这个键」在这里也是两件事：前者有确定的渲染器，后者才退回缺省棋盘。

#### Scenario: 象棋房间画象棋盘
- **WHEN** 打开一个 `gameKey === 'xiangqi'` 的房间
- **THEN** 渲染 `<app-xiangqi-board>`，MUST NOT 渲染 `<app-board>`

#### Scenario: 五子棋房间不受影响
- **WHEN** 打开一个 `gameKey === 'gomoku'` 的房间
- **THEN** 渲染 `<app-board>` 且为 15×15

#### Scenario: 走子调 movePiece 而不是 makeMove
- **WHEN** 在象棋房间完成一次选子落点
- **THEN** `hub.movePiece` 被调一次且参数为 `(roomId, 9, 0, 8, 0)`；`hub.makeMove` MUST NOT 被调用

#### Scenario: 服务端拒绝后保留选中
- **WHEN** `movePiece` 抛 `HubException`
- **THEN** 显示可翻译提示；起点仍处于选中态

#### Scenario: 未知棋种退回缺省棋盘
- **WHEN** 房间的 `gameKey` 在前端注册表中不存在
- **THEN** 渲染 `<app-board>`，页面 MUST NOT 崩溃

#### Scenario: 成语接龙房间画词链
- **WHEN** 打开一个 `gameKey === 'idiom-chain'` 的房间
- **THEN** 渲染 `<app-chain-board>`，MUST NOT 渲染 `<app-board>` 或 `<app-xiangqi-board>`

#### Scenario: 说词调 sayWord
- **WHEN** 在成语接龙房间提交一个词
- **THEN** `hub.sayWord(roomId, word)` 被调一次；`hub.makeMove` 与 `hub.movePiece` MUST NOT 被调用

#### Scenario: 无盘面的棋种不落到缺省棋盘
- **WHEN** 描述符声明 `rows: null, cols: null`
- **THEN** MUST NOT 渲染 15×15 的 `<app-board>`

### Requirement: `/g/xiangqi` 是象棋的人机对战入口

`app.routes.ts` SHALL 新增路由 `g/xiangqi`，带 `canMatch: [authGuard]`，并通过 `loadComponent` 懒加载。

页面让玩家选难度（`Easy` / `Medium` / `Hard`，默认 `Medium`）与执红/执黑（默认执红，即 `Black`），点开始 → `RoomsApiService.createAiRoom(name, difficulty, humanSide, 'xiangqi')` → `navigateByUrl('/rooms/' + id)`。房间名由客户端生成，MUST NOT 让玩家填 —— 与一字棋同一理由（人机房间不进任何大厅列表，这个名字对任何人都不可见）。

页面 MUST 说明本棋种**不计 ELO**。

页面 MUST NOT 声称任何难度「不可战胜」或「必和」。象棋不可能穷举，`add-xiangqi-ai` 因此明确拒绝了这类断言 —— **一个验不了的断言比没有断言更糟**，把它印在选难度的地方就是把它变成对玩家的承诺。页面该说的是它实际是什么：限深搜索，深一档看得远一档。

本变更 MUST NOT 新增象棋的大厅、人人对战入口或排行榜卡片：`SupportsHumanVsHuman === false`、`IsRated === false`，那两处入口分别指向服务端会拒绝的操作和一个永远空的榜。

失败路径 MUST 有真实 UI：请求失败显示可翻译错误横幅并允许重试。

#### Scenario: 懒加载
- **WHEN** 检视 `app.routes.ts` 中的 `g/xiangqi` 条目
- **THEN** 它 MUST 使用 `loadComponent: () => import(...)`

#### Scenario: 未登录被拦
- **WHEN** 未登录用户访问 `/g/xiangqi`
- **THEN** 重定向到 `/login?returnUrl=/g/xiangqi`

#### Scenario: 开局跳转
- **WHEN** 选 `Hard` + 执黑，点开始，后端返回房间 id `X`
- **THEN** `createAiRoom` 被调一次且 `gameKey === 'xiangqi'`、`humanSide === 'White'`、`difficulty === 'Hard'`；随后 `navigateByUrl('/rooms/X')`

#### Scenario: 不承诺不可战胜
- **WHEN** 读取页面在两份 locale 中的全部文案
- **THEN** MUST NOT 出现「不可战胜」「打不赢」「必和」「unbeatable」「cannot be beaten」这类断言

#### Scenario: 建房失败可重试
- **WHEN** `createAiRoom` 返回错误
- **THEN** 显示可翻译错误横幅，按钮回到可点状态，MUST NOT 跳转

### Requirement: 象棋 manifest 从「即将上线」翻到「可玩」

`src/app/games/xiangqi/manifest.ts` SHALL 把 `status` 改为 `'available'`，并加上 `launchRoute: '/g/xiangqi'` 与 `board: { rows: 10, cols: 9 }`。

目录页 MUST NOT 为它渲染排行榜入口 —— 它不计分。这条已由 platform-catalog 的既有约束覆盖，此处 MUST NOT 为象棋开任何例外。

#### Scenario: 目录页可点进
- **WHEN** 打开 `/games`
- **THEN** 象棋卡片可交互，指向 `/g/xiangqi`

#### Scenario: 象棋没有排行榜入口
- **WHEN** 检视目录页的象棋卡片
- **THEN** MUST NOT 存在排行榜链接

### Requirement: i18n —— `xiangqi.*` 键在两份 locale 中齐备

`public/i18n/zh-CN.json` 与 `public/i18n/en.json` SHALL 各增加 `xiangqi.*` 子树，覆盖：入口页标题与说明、三档难度、执红/执黑、开始按钮、提交中状态、错误横幅、不计分提示、AI 性质说明、棋盘的无障碍文案（含 14 个棋子名与「空」）、楚河汉界。

两份文件的键集合 MUST 完全一致 —— 缺键在运行时表现为屏幕上出现原始键名。

模板 MUST NOT 硬编码任何中英文展示字符串。棋子**字形**（`帥` / `將` 等）是绘制在棋盘上的图形而非文案，MUST 留在组件中；它们的**读法**（读屏用）MUST 走 i18n。

#### Scenario: 键集合一致
- **WHEN** 比较两份 locale 文件中 `xiangqi.*` 的键集合
- **THEN** 两者相等

#### Scenario: 模板无硬编码
- **WHEN** 检视入口页与棋盘的模板
- **THEN** 所有面向用户的**文案** MUST 经 `| transloco`

