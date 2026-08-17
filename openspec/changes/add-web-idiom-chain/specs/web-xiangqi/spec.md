# web-xiangqi Specification Delta

## MODIFIED Requirements

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
