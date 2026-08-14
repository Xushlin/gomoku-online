## RENAMED Requirements

标题里的「15×15」现在是错的 —— 同一个组件要画 3×3。改名走 RENAMED,应用顺序
RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: `Board` 组件 —— 15×15 CSS grid,点击调 hub.makeMove,非本方回合禁用
- TO: ### Requirement: `Board` 组件 —— 尺寸由输入决定的 CSS grid,点击调 hub.makeMove,非本方回合禁用

## MODIFIED Requirements

### Requirement: `Board` 组件 —— 尺寸由输入决定的 CSS grid,点击调 hub.makeMove,非本方回合禁用

`src/app/pages/rooms/room-page/board/board.ts` SHALL 渲染 `rows` × `cols` 的按钮网格,每格是 `<button type="button">`,代表 `Stone`(`'Empty' | 'Black' | 'White'`):

- `rows: InputSignal<number>` / `cols: InputSignal<number>`,默认 **15**。此前是文件内常量 `BOARD_SIZE = 15` —— 那是后端 `Board(15, 15, 5)` 的前端翻版,而后端已在 `add-game-rules-registry` 把它变成了参数。默认值保留 15,使既有五子棋调用点(房间页、回放页)MUST NOT 需要修改。
- `Board` MUST 保持纯展示组件:它 MUST NOT 注入 `GameCatalogService`、MUST NOT 认识 `gameKey`。尺寸由容器算好传进来 —— 这是"容器组件取数据、展示组件纯渲染"这条既有约定的直接应用。
- 网格使用 CSS grid(`grid-template-columns: repeat(var(--board-cols), 1fr); aspect-square; max-width: ~600px`),列数由内联 style 绑定,MUST NOT 依赖 Tailwind 的静态类名 —— `grid-cols-3` / `grid-cols-15` 之类的类名在编译期不可知。颜色全部来自主题变量(棋盘背景 `var(--color-surface)`、格线 `var(--color-border)`、黑子 `var(--color-text)`、白子 `var(--color-bg)` 加 `var(--color-border)` 细边)。
- 每个 `<button>` MUST 有 `[attr.aria-label]="'game.board.cell-aria-label' | transloco : { row: r+1, col: c+1 }"`。
- 每个 `<button>` `disabled` 当且仅当以下任一为真:
  - `state()?.status !== 'Playing'`(未开始 / 已结束)
  - `!myTurn()` 且当前格已为 Empty
  - 当前格已非 Empty(占用)
  - `submittingMove()` 为 true(上一个点击还在飞)
  - `mySide() === 'spectator'`(观众总只读)
- 点击 empty 格 → 设 `submittingMove.set(true)` → `await hub.makeMove(roomId, row, col)`:
  - 成功:等 `MoveMade` 事件流入 state;清 `submittingMove`。
  - 失败(`HubException`):显示翻译 toast(按消息文本模糊匹配到 `game.errors.not-your-turn` / `.invalid-move` / `.concurrent-move-refetched` / `.generic`);对并发错误或无法识别错误额外做一次 `roomsApi.getById() → applySnapshot`;清 `submittingMove`。
- 落子历史里超出 `rows` / `cols` 的坐标 MUST 被静默忽略而不是抛错 —— 尺寸失配时棋盘该画得不对,而不是白屏。
- 最后一步落子 MUST 有一个可见的视觉高亮(例如 2px 外环使用 `var(--color-primary)`),读屏时通过 `game.board.last-move-label` aria-describedby 附加说明。

Board 组件 MUST 可通过输入参数在只读模式下复用(为 `add-web-replay-and-profile` 的回放页留接口):

- `readonly: InputSignal<boolean>`(默认 false)—— 为 true 时所有格永远 disabled,不附加 click handler。

#### Scenario: 对方回合点击被忽略
- **WHEN** `myTurn() === false`,用户点一个 Empty 格
- **THEN** MUST NOT 发 `hub.makeMove`;`submittingMove` 保持 false;按钮本身 `disabled`(所以事件也不会触发)

#### Scenario: 本方回合正常落子
- **WHEN** `myTurn() === true` 且 `state.status === 'Playing'`,用户点 `(7,7)`
- **THEN** `hub.makeMove(roomId, 7, 7)` 被调一次;`submittingMove` 翻 true;`MoveMade` 事件到达后 state 反映新落子,`submittingMove` 清 false,高亮移到 `(7,7)`

#### Scenario: 观众只读
- **WHEN** `mySide() === 'spectator'`
- **THEN** 全部格子 `disabled`;点击不触发任何事件

#### Scenario: 已结束不可落子
- **WHEN** `state.status === 'Finished'`
- **THEN** 所有按钮 `disabled`;最后一步高亮仍可见

#### Scenario: 失败时回滚 + rehydrate
- **WHEN** `hub.makeMove` 抛 `HubException`(例如"concurrent")
- **THEN** UI 不把该格渲染为已落子;翻译 toast 显示;`roomsApi.getById` 被调一次以同步服务端真实状态

#### Scenario: readonly 模式
- **WHEN** `<app-board [readonly]="true" [state]="..." />`
- **THEN** 所有按钮 `disabled`;`hub.makeMove` 永不被调用

#### Scenario: 缺省仍是 15×15
- **WHEN** `<app-board [state]="..." />` 不传 `rows` / `cols`
- **THEN** 渲染 225 个按钮 —— 既有五子棋调用点行为不变

#### Scenario: 3×3
- **WHEN** `<app-board [rows]="3" [cols]="3" [state]="..." />`
- **THEN** 渲染 9 个按钮,CSS grid 列数为 3

#### Scenario: 越界落子历史不导致崩溃
- **WHEN** `rows`/`cols` 为 3,而 `state` 的走子历史里含 `(7,7)`
- **THEN** 组件正常渲染 9 格,该步被忽略,MUST NOT 抛错
