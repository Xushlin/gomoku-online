## MODIFIED Requirements

### Requirement: 按棋种复用共享棋盘组件的只读模式,回放页不自己写渲染

`ReplayPage` SHALL 通过传 `[readonly]="true"` 给**共享**棋盘组件来实现只读渲染;MUST NOT 在 `pages/replay/` 下复制粘贴任何 board 实现。

共享组件按棋种解析:`gameKey === 'xiangqi'` 用 `<app-xiangqi-board>`,其余(含未知棋种)用 `<app-board>`。选择方式与 `RoomPage` 一致 —— 容器模板里的 `@if`,MUST NOT 引入棋盘组件注册表。

本条原本写作「不引入第二个棋盘渲染层」。那个说法写于平台只有一种盘面形状的时候,而象棋的盘面**不是**五子棋盘的参数化(交叉点上的子 vs 格子里的子、两步走子 vs 一步落子)。约束的**意图**不变:回放页一行渲染代码都不自己写。变的是「共享组件」从单数变成了按棋种解析的两个。

`<app-board>` 的 `rows` / `cols` SHALL 与房间页同源:`GameCapabilitiesService.of(gameKey)`,即 `GET /api/games` 下发的服务端声明,MUST NOT 来自前端清单。描述符未到达时页面停在加载态,理由与房间页相同 —— 见 web-tictactoe「房间页按棋种决定棋盘尺寸」。

`boardState` `computed` SHALL 合成 `RoomState` 形状(synthesised partial)使棋盘组件自然消费 —— `status: 'Finished'` 触发落子按钮永远 disabled,所以 readonly 边界由两层共同保证(`[readonly]` 输入 + `status !== 'Playing'`)。

象棋回放 MUST 从 `MoveDto` 的 `fromRow`/`fromCol` → `row`/`col` 逐步推导盘面(与房间页同一个纯函数),MUST NOT 另写一份推导。

#### Scenario: 落子按钮永远禁用
- **WHEN** ReplayPage 渲染任意 currentPly
- **THEN** 棋盘的全部按钮都 `disabled`;点击不触发任何事件

#### Scenario: 最后一步高亮跟着 scrubber
- **WHEN** `currentPly` 从 5 移到 7
- **THEN** 棋盘的 last-move 高亮自动从第 5 步落点移到第 7 步落点(因为 `boardState` 重新合成了 `moves.slice`)

#### Scenario: 象棋回放画象棋盘
- **WHEN** 回放一局 `gameKey === 'xiangqi'` 的对局
- **THEN** 渲染 `<app-xiangqi-board>` 且为只读;MUST NOT 渲染 15×15 的 `<app-board>`

#### Scenario: 象棋回放的盘面随 scrubber 回溯
- **WHEN** `currentPly` 从 7 退回 3
- **THEN** 盘面等于「初始摆子 + 前 3 步」,被吃的子重新出现在盘上

#### Scenario: 五子棋回放不受影响
- **WHEN** 回放一局五子棋
- **THEN** 渲染 `<app-board>`,行为与本变更之前完全一致

#### Scenario: 一字棋回放画 3×3
- **WHEN** 回放一局 `gameKey === 'tictactoe'` 的对局,服务端描述符已到达
- **THEN** 棋盘渲染 9 格
