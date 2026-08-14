# web-tictactoe Specification

## Purpose
TBD - created by archiving change add-web-tictactoe-ai. Update Purpose after archive.
## Requirements
### Requirement: `/g/tictactoe` 是一字棋的人机对战入口

`app.routes.ts` SHALL 新增路由 `g/tictactoe`,带 `canMatch: [authGuard]`,并通过 `loadComponent` 懒加载 —— 与既有根路由契约一致,MUST NOT 使用 `component:` 直接引用。

未登录用户访问 MUST 被 `authGuard` 重定向到 `/login?returnUrl=/g/tictactoe`。

本路由的加入 MUST NOT 改变任何既有路由、guard、重定向目标或落地页 —— `/home` 仍是登录后的落地页与五子棋大厅。本变更**刻意不做**一字棋的大厅、人人对战入口与排行榜卡片(见 design D5)。

#### Scenario: 懒加载
- **WHEN** 检视 `app.routes.ts` 中的 `g/tictactoe` 条目
- **THEN** 它 MUST 使用 `loadComponent: () => import(...)`

#### Scenario: 未登录被拦
- **WHEN** 未登录用户访问 `/g/tictactoe`
- **THEN** 重定向到 `/login?returnUrl=/g/tictactoe`

#### Scenario: 既有路由不受影响
- **WHEN** 比对本变更前后的路由表
- **THEN** 除新增一条 `g/tictactoe` 外,MUST NOT 有其它条目被增删改;`/home` 仍是 `''` 的重定向目标

### Requirement: 一字棋人机页 —— 选难度与先后手,一步开局

`src/app/games/tictactoe/ai-game/ai-game.ts` SHALL 提供一个懒加载的独立组件,让玩家选择:

- **难度**:`Easy` / `Medium` / `Hard`,默认 `Medium`。
- **先后手**:黑(先手)/ 白(后手),默认黑。

点「开始」→ `RoomsApiService.createAiRoom(name, difficulty, humanSide, 'tictactoe')` → 成功后 `navigateByUrl('/rooms/' + id)`。

**房间名由客户端生成,MUST NOT 让玩家填。** 理由见 design D3:人机房间不进任何大厅列表
(`GetRoomListQuery` 按棋种过滤,而一字棋没有大厅),这个名字对任何人都不可见;
要求玩家为一局自己跟机器下的棋起名字是纯摩擦。生成的名字 MUST 满足后端 3–50 字符校验。
后端的 `Name` 校验 MUST NOT 为此放宽 —— 那会同时放宽人人房间,而那里名字是有意义的。

页面 MUST 说明 Hard 档的性质:一字棋是已解游戏,Hard 档穷举整棵博弈树,玩家打不赢它,最好结果是和棋。把这件事写在选择难度的地方,而不是让玩家输三局之后自己猜。

页面 MUST 说明本棋种**不计 ELO**(`IGameRules.IsRated == false`)—— 玩家有权在开局前知道这局不算分。

失败路径 MUST 有真实 UI:请求失败显示可翻译的错误横幅并允许重试,MUST NOT 静默失败或只留一个 loading 态。

#### Scenario: 默认值
- **WHEN** 打开 `/g/tictactoe`
- **THEN** 难度选中 `Medium`,先后手选中黑

#### Scenario: 开局跳转
- **WHEN** 选 `Hard` + 白,点开始,后端返回房间 id `X`
- **THEN** `createAiRoom` 被调一次且 `gameKey === 'tictactoe'`、`humanSide === 'White'`、`difficulty === 'Hard'`;随后 `navigateByUrl('/rooms/X')`

#### Scenario: 玩家不填房间名
- **WHEN** 检视页面的表单
- **THEN** MUST NOT 存在房间名输入框;送给后端的 `name` 长度 MUST 在 [3..50]

#### Scenario: 建房失败可重试
- **WHEN** `createAiRoom` 返回错误
- **THEN** 显示可翻译的错误横幅,按钮回到可点状态,MUST NOT 跳转

#### Scenario: 提交中防重复
- **WHEN** 请求在飞
- **THEN** 开始按钮 `disabled`,MUST NOT 发出第二个请求

### Requirement: 房间页按棋种决定棋盘尺寸

`RoomPage` SHALL 由 `state.gameKey` 经 `GameCatalogService.byKey()` 解析出 `board`,并把 `rows` / `cols` 传给 `<app-board>`。

解析不出时(未知棋种、或该 manifest 没声明 `board`)MUST 退回 15×15 而不是报错 —— 一个没更新的客户端遇到新棋种应该画出一块可能不对的棋盘,而不是白屏。

`RoomPage` 是容器组件,所以查注册表这件事归它;`Board` MUST 保持不认识 `gameKey`。

#### Scenario: 一字棋房间画 3×3
- **WHEN** 打开一个 `gameKey === 'tictactoe'` 的房间
- **THEN** 棋盘渲染 9 格

#### Scenario: 五子棋房间画 15×15
- **WHEN** 打开一个 `gameKey === 'gomoku'` 的房间
- **THEN** 棋盘渲染 225 格

#### Scenario: 直接进入房间也能画对
- **WHEN** 直接访问 `/rooms/{一字棋房间id}`(刷新 / 收藏链接,没有经过 `/g/tictactoe`)
- **THEN** 棋盘仍渲染 9 格 —— 尺寸来自 DTO 的 `gameKey`,不来自路由来源

#### Scenario: 未知棋种退回缺省
- **WHEN** 房间的 `gameKey` 在前端注册表中不存在
- **THEN** 棋盘渲染 15×15,页面 MUST NOT 崩溃

### Requirement: `RoomsApiService.createAiRoom` 接受棋种键

`RoomsApiService` 的抽象方法 SHALL 变为 `createAiRoom(name, difficulty, humanSide?, gameKey?)`,`gameKey` 可选。

省略时 MUST NOT 在请求体里出现该字段 —— 由后端缺省为 `gomoku`。这保持既有调用点(大厅的 AI 对战对话框)一行不改,与 `humanSide` 当初的处理方式一致。

`RoomState` / `RoomSummary` 的客户端类型 SHALL 增加 `readonly gameKey: string`,对齐后端 DTO。

#### Scenario: 带棋种
- **WHEN** `createAiRoom('n', 'Hard', 'Black', 'tictactoe')`
- **THEN** POST body 含 `gameKey: 'tictactoe'`

#### Scenario: 不带棋种
- **WHEN** `createAiRoom('n', 'Medium')`
- **THEN** POST body MUST NOT 含 `gameKey` 键,也 MUST NOT 含 `humanSide` 键

### Requirement: i18n —— 一字棋页面的键在两份 locale 中齐备

`public/i18n/zh-CN.json` 与 `public/i18n/en.json` SHALL 各增加 `tictactoe.*` 子树,覆盖:标题与说明、难度三档的名称、先后手两项、开始按钮、提交中状态、错误横幅、Hard 档不可战胜的提示、不计分的提示。

两份文件的键集合 MUST 完全一致 —— 缺键在运行时表现为屏幕上出现原始键名。

模板 MUST NOT 硬编码任何中英文展示字符串。

#### Scenario: 键集合一致
- **WHEN** 比较两份 locale 文件中 `tictactoe.*` 的键集合
- **THEN** 两者相等

#### Scenario: 模板无硬编码
- **WHEN** 检视 `ai-game.html`
- **THEN** 所有面向用户的文案 MUST 经 `| transloco`

### Requirement: 胜负原因的展示文案不得绑定某一个棋种

`game.ended.reason-connected-5` 的**翻译文案** SHALL 对所有「连成一线者胜」的棋种都成立,MUST NOT 写明具体连子数。

这条约束是在浏览器里实测发现的,不是推演出来的:一字棋赢一局后,结束弹窗显示
「You won! **Five in a row.**」/「五子连珠。」—— 三连的棋被告知连了五子。

修的是**文案**而不是枚举名与翻译键(`GameEndReason.Connected5` / `reason-connected-5` 一律不动):
改键要牵动五份前端文件、四份规范,以及一个已经以整数持久化的枚举,那是
`generalize-match-contract` 的账。而文案是玩家唯一看得见的部分。

选中性措辞而非按棋种分支,是因为具体措辞对一个棋种对、对另一个错;中性措辞对两个都对。
未来若要恢复按棋种的具体措辞(五子棋的「五子连珠」确实更有味道),那需要让结束原因
知道棋种,应与 `generalize-match-contract` 一同进行。

**这一条存在的目的是防止有人把它「改回」更好听的写法。**

#### Scenario: 文案不含具体连子数
- **WHEN** 读取两份 locale 的 `game.ended.reason-connected-5`
- **THEN** 文案 MUST NOT 出现「五」/「5」/「five」等特指五子连珠的字样

#### Scenario: 一字棋获胜的提示读得通
- **WHEN** 一局一字棋以三连结束,结束弹窗显示
- **THEN** 原因文案对三连成立(如「连成一线。」/「A line was completed.」)

#### Scenario: 枚举与键未被改动
- **WHEN** 比对本变更的 diff
- **THEN** `GameEndReason` 的成员名与 `game.ended.reason-connected-5` 这个键 MUST NOT 被修改

