## MODIFIED Requirements

### Requirement: 房间页按棋种决定棋盘尺寸

`RoomPage` SHALL 由 `state.gameKey` 经 `GameCapabilitiesService.of()` 解析出服务端声明的 `rows` / `cols`,并传给 `<app-board>`。

尺寸的真源是后端的 `IGameRules`,`GET /api/games` 把它下发。此前这一步查的是前端 `GameManifest.board` —— 一份服务端数据的客户端副本 —— 而它的正当性在 `add-web-xiangqi` 之后不再成立(见 platform-catalog)。

解析不出时(未知棋种、或描述符尚未到达)MUST 退回 15×15 而不是报错 —— 一个没更新的客户端遇到新棋种应该画出一块可能不对的棋盘,而不是白屏;服务端照样挡越界落子。

**但页面 MUST NOT 用那个退回值去画一块它其实知道尺寸的棋盘。** 描述符是异步到达的,所以房间页的加载态 MUST 覆盖到 `GameCapabilitiesService.loaded()` 为止:玩家看到的是骨架屏多停一会儿,而不是一块 15×15 跳成 3×3。

`RoomPage` 是容器组件,所以查能力这件事归它;`Board` MUST 保持不认识 `gameKey`。

#### Scenario: 一字棋房间画 3×3
- **WHEN** 打开一个 `gameKey === 'tictactoe'` 的房间,服务端描述符已到达
- **THEN** 棋盘渲染 9 格

#### Scenario: 五子棋房间画 15×15
- **WHEN** 打开一个 `gameKey === 'gomoku'` 的房间
- **THEN** 棋盘渲染 225 格

#### Scenario: 直接进入房间也能画对
- **WHEN** 直接访问 `/rooms/{一字棋房间id}`(刷新 / 收藏链接,没有经过 `/g/tictactoe`)
- **THEN** 棋盘仍渲染 9 格 —— 尺寸来自 DTO 的 `gameKey` 加服务端描述符,不来自路由来源

#### Scenario: 未知棋种退回缺省
- **WHEN** 房间的 `gameKey` 在服务端描述符里不存在
- **THEN** 棋盘渲染 15×15,页面 MUST NOT 崩溃

#### Scenario: 描述符没到就不画
- **WHEN** 房间数据已到达但 `GameCapabilitiesService.loaded()` 仍为 false
- **THEN** 页面停在加载态,MUST NOT 渲染棋盘 —— 不让玩家看见一块尺寸会跳的盘
