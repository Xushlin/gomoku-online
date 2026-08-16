## MODIFIED Requirements

### Requirement: `GameHubService` —— 抽象类 DI token,Signals 导出状态,Observable 导出瞬时事件

前端 SHALL 在 `src/app/core/realtime/game-hub.service.ts` 定义 abstract class `GameHubService` 作为 DI token,由 `DefaultGameHubService` 实现,通过 `{ provide: GameHubService, useClass: DefaultGameHubService }` 全局注册(`app.config.ts`)。

API 契约:

- `readonly state: Signal<RoomState | null>`
- `readonly connectionStatus: Signal<'disconnected' | 'connecting' | 'connected' | 'reconnecting'>`
- `readonly gameEnded: Signal<GameEndedDto | null>`
- `readonly urged$: Observable<UrgeDto>`
- `readonly roomDissolved$: Observable<{ roomId: string }>`
- `joinRoom(roomId: string): Promise<void>`
- `leaveRoom(roomId: string): Promise<void>`
- `joinSpectatorGroup(roomId: string): Promise<void>`
- `makeMove(roomId: string, row: number, col: number): Promise<void>`
- `movePiece(roomId: string, fromRow: number, fromCol: number, row: number, col: number): Promise<void>`
- `sendChat(roomId: string, content: string, channel: 'Room' | 'Spectator'): Promise<void>`
- `urge(roomId: string): Promise<void>`
- `applySnapshot(state: RoomState): void` —— REST rehydration path 用

`movePiece` 对应服务端的 `MovePiece` hub 方法,用于**走子类**棋种(象棋);`makeMove` 对应 `MakeMove`,用于**落子类**棋种(五子棋、一字棋)。

它们是两个方法而不是「`makeMove` 加两个可选参数」,原因是 **SignalR 不套用 C# 的可选参数默认值**:一个 3 参调用打到 5 参方法上会被直接拒绝(`InvalidDataException`),已发布的客户端会在下一步棋当场坏掉。这件事是 `AiSmoke` 抓到的 —— Domain、Application、Api 三层单元测试全绿,因为它们都不经过 SignalR 的参数绑定。**这个形状 MUST NOT 被「简化」成一个方法。**

组件 MUST 通过 `inject(GameHubService)` 消费,MUST NOT 直接 `inject(DefaultGameHubService)`。所有命令返回 `Promise<void>` —— 服务端结果通过 server→client 事件到达,而不是 RPC 返回值。命令失败时,`HubException` 的消息 MUST 透传给 caller,caller 可根据消息做翻译映射。

#### Scenario: 抽象类 DI 可替换
- **WHEN** 测试用 `TestBed.configureTestingModule({ providers: [{ provide: GameHubService, useValue: stub }] })`
- **THEN** 组件通过 `inject(GameHubService)` 得到 stub,不需要修改组件代码

#### Scenario: state 在 RoomState 事件后更新
- **WHEN** 服务端发出 `RoomState` 事件(payload 为完整 `RoomStateDto`)
- **THEN** `state()` signal MUST 返回完全等价的新对象(整体替换,不增量合并)

#### Scenario: state 在 MoveMade 事件后增量更新
- **WHEN** 服务端发出 `MoveMade` 事件(`MoveDto { ply, row, col, stone, playedAt }`)
- **THEN** `state()?.game?.moves` MUST 追加该 Move(按 `ply` 排序);`state()?.game?.currentTurn` MUST 翻转;`state()?.game?.turnStartedAt` MUST 更新为新值(如事件或随后 `RoomState` 提供的)

#### Scenario: gameEnded signal 在 GameEnded 事件后为 non-null
- **WHEN** 服务端发出 `GameEnded`
- **THEN** `gameEnded()` 返回事件 payload;保持非 null 直到 `leaveRoom` 被调用

#### Scenario: urged$ 只触发被叫方
- **WHEN** 服务端调用 `Clients.User(urgedUserId).SendAsync("UrgeReceived", ...)`
- **THEN** 仅该用户的 hub 连接 emits `urged$` 下一个值;其它订阅者不 emit

#### Scenario: movePiece 调的是 MovePiece
- **WHEN** 调用 `movePiece(roomId, 9, 0, 8, 0)`
- **THEN** 底层 `connection.invoke` 收到 `('MovePiece', roomId, 9, 0, 8, 0)` —— 5 个参数,MUST NOT 走 `MakeMove`

#### Scenario: 落子类棋种不受影响
- **WHEN** 调用 `makeMove(roomId, 7, 7)`
- **THEN** 底层 `connection.invoke` 收到 `('MakeMove', roomId, 7, 7)`,与本变更之前完全一致

### Requirement: 错误处理 —— `HubException` 消息到翻译键的映射

RoomPage / Board / ChatPanel SHALL 把从 hub 命令 promise 抛出的 `HubException` 处理为用户可见的翻译文案。映射规则(按消息字段包含的关键字,case-insensitive):

- 包含 `"not your turn"` → `game.errors.not-your-turn`
- 包含 `"in check"` → `game.errors.self-check`
- 包含 `"invalid move"` / `"occupied"` / `"out of bounds"` / `"cannot move from"` / `"there is no piece at"` / `"does not belong to"` / `"must change the piece"` / `"origin square"` / `"outside the"` → `game.errors.invalid-move`
- 包含 `"concurrent"` 或 `"DbUpdateConcurrency"` → `game.errors.concurrent-move-refetched`(并**必须**跟进一次 `roomsApi.getById → applySnapshot`)
- 包含 `"too frequent"`(urge 冷却)→ `game.errors.urge-cooldown`
- 其它未识别 → `game.errors.generic`

网络层错误(Promise rejection 不是 `HubException`,而是 connection 已断)→ `game.errors.network`。

这种字符串匹配承认脆弱但**是当前后端没有结构化错误码的最小痛苦**方案;design.md 记录了后续添加 typed error code 的跟进项。

**中国象棋 抬高了这条的赌注,所以后半段的关键字是必须的而不是顺手加的。** 五子棋的棋盘只允许点空格,`invalid-move` 因此几乎不可达,一条没被映射的消息不花什么代价。象棋的棋盘**刻意不懂规则**(否则就是第二份真源),所以被拒绝是玩家了解棋子怎么走的常规途径 —— 而它原本落在「Something went wrong. Please try again.」上,读起来像应用坏了,不像一步棋被拒。

自将/照面 SHALL 有**自己**的文案而不是并进 `invalid-move`:它是象棋里最常见的一种拒绝,而「这步不合法」不告诉玩家他漏看了什么。

#### Scenario: 并发错误走 rehydration
- **WHEN** `hub.makeMove` reject,消息包含 `'concurrent'`
- **THEN** 显示 `game.errors.concurrent-move-refetched` 翻译 toast;`roomsApi.getById(id)` 被调一次;state 被 `applySnapshot` 替换

#### Scenario: 未识别错误走 generic
- **WHEN** `HubException` 消息是 `"something weird"`
- **THEN** toast 显示 `game.errors.generic` 翻译

#### Scenario: 象棋的走法拒绝读得懂
- **WHEN** `HubException` 消息是 `"A General cannot move from (9, 4) to (7, 4)."`
- **THEN** toast 显示 `game.errors.invalid-move`,MUST NOT 显示 `game.errors.generic`

#### Scenario: 自将有自己的说法
- **WHEN** `HubException` 消息含 `"leave your general in check"`
- **THEN** toast 显示 `game.errors.self-check`

### Requirement: i18n —— `game.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `game.*` 键集合,包含但不限于:

- `game.room.{name-label, host-label, seat-black, seat-white, status-waiting, status-playing, status-finished}`
- `game.board.{cell-aria-label, last-move-label}`(cell-aria-label 带 `{{row}}` / `{{col}}` 插值占位符)
- `game.turn.{your-turn, opponent-turn, black-turn, white-turn, countdown-label}`
- `game.actions.{resign, resign-confirm-title, resign-confirm-body, resign-confirm-ok, leave, urge}`
- `game.chat.{title, tab-room, tab-spectator, send, placeholder, empty, max-length-error, forbidden-error}`
- `game.urge.{toast, button-disabled-own-turn, button-disabled-cooldown}`
- `game.ended.{title-win, title-lose, title-draw, reason-connected-5, reason-resigned, reason-timeout, back-to-lobby, dismiss}`
- `game.errors.{generic, network, not-your-turn, invalid-move, self-check, concurrent-move-refetched, urge-cooldown}`
- `game.connection.{reconnecting, disconnected, retry, connected}`

键集合 MUST 两份 JSON 完全相等;已有 flattener parity check 持续 0 drift。

模板 MUST 零硬编码 CJK / 长英文显示字符串;按 scaffold / auth / lobby 已立规则。

#### Scenario: parity
- **WHEN** 对比 `en.json` 与 `zh-CN.json` flatten 后的 key 集合
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/rooms/room-page/**/*.html` 下搜索 CJK 字符或 ≥3 字母英文显示字符串
- **THEN** 0 匹配(Brand / test-id / 技术字符串豁免)
