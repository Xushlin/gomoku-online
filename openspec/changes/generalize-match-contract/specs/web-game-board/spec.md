# web-game-board 的规格变化

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
- `sayWord(roomId: string, word: string): Promise<void>` —— 文本类棋种(成语接龙)
- `reconnect(): Promise<void>`
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
- **WHEN** 服务端发出 `MoveMade` 事件(`MoveDto { ply, row, col, seat, playedAt }`)
- **THEN** `state()?.game?.moves` MUST 追加该 Move(按 `ply` 排序);`state()?.game?.turnStartedAt` MUST 更新;而 `state()?.game?.currentSeat` MUST **保持不变**

  **本场景此前要求 `currentTurn` MUST 翻转,而那是一个两座位假设。** 实现写的是
  `move.stone === 'Black' ? 'White' : 'Black'`,三座位棋种下它是错的;客户端也算不出来 ——
  它不知道房间有几个座位(DTO 没有座位表,`GET /api/games` 没有 `seatCount`)。

  它不需要算:权威状态先到(见 `room-and-gameplay` 的到达顺序要求),所以 `currentSeat`
  在这个 handler 跑之前就已经是对的,而 `lastAppliedPly` 会让它直接返回。**删掉一个猜测的
  论证必须自带那个顺序的证据**,证据在 `AiSmoke` 里,对着真连接量。

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

### Requirement: 房间侧栏 —— 信息 + 回合倒计时 + 辞局 + 离开按钮

`src/app/pages/rooms/room-page/sidebar/sidebar.ts` SHALL 渲染:

- 房间名 `state.name` + 房主 `state.host.username`(`game.room.*` i18n)。**房主用户名 SHALL 是 `routerLink` 链接到 `/users/<host.id>`,使用 `.username-link` class + `(click)="$event.stopPropagation()"`**。
- 黑方座位 `state.black?.username` / 白方座位 `state.white?.username`,每个座位显示是否在线(未实现在线探测则显示 `username` 字面量)。**座位上的 username SHALL 同样是 `/users/<id>` 链接**(空座位文案不变)。
- 当前状态徽章(`Waiting / Playing / Finished`)
- 当前回合指示:`state.game.currentSeat === FIRST_SEAT ? game.turn.black-turn : game.turn.white-turn`;若 `mySide()` 对应的座位等于 `currentSeat`,额外突出 `game.turn.your-turn`
- **回合倒计时**:
  - 计算 `deadline = state.game.turnStartedAt + state.game.turnTimeoutSeconds`
  - 显示剩余时间 `M:SS`,驱动源是 RoomPage 的 1 Hz `now` signal
  - 剩余 ≤ 10s 时用 `text-danger` 强调
  - 剩余 ≤ 0s 时显示 `0:00`,后端轮询最多 5s 内会发 `GameEnded`
- 玩家专用按钮(`mySide() !== 'spectator'` 时渲染):
  - **辞局**:需二次确认(CDK Dialog, `ResignConfirmDialog`);确认后 `rooms.resign(id)` REST;无论成功失败,后续 `GameEnded` 事件负责打开结束弹窗(见下一条 Requirement)
  - **离开房间** —— `RoomPage.handleLeave()` SHALL 分两条路径:
    - **当前用户是 host 且 `state.status === 'Waiting'`**(自己开的空房间)→ 调 `rooms.dissolve(id)` REST(`DELETE /api/rooms/:id`)。后端的 `Room.Leave` invariant 拒绝这种情况(`HostCannotLeaveWaitingRoomException`),所以前端必须走 dissolve 端点。Dissolve 成功后,后端发出 `RoomDissolved` SignalR 事件 —— 同房间所有连接(包括发起者本人)由既有的 `roomDissolved$` 订阅触发 navigate `/home`,所以即便不显式 navigate 也会到大厅。
    - **其它情况**(玩家在 Playing / Finished 房间;或观众;或非 host)→ 调 `rooms.leave(id)` REST(`POST /api/rooms/:id/leave`)。
  - 两条路径在前端 success 回调里都 `router.navigateByUrl('/home')`。网络错误 → generic error toast,不导航。
- 观众专用:不显示辞局 / 离开;可能有"停止观战"按钮(调 REST `POST /api/rooms/:id/spectate` 的反向 `DELETE`;如果 spec 没有 DELETE endpoint,则不提供此按钮)

所有文案走 `| transloco`,零硬编码。

#### Scenario: 我方回合突出
- **WHEN** `mySide() === 'black'` 且 `state.game.currentSeat === 0`
- **THEN** 侧栏 MUST 同时显示 `game.turn.black-turn` 与 `game.turn.your-turn`

#### Scenario: 倒计时低于阈值强调
- **WHEN** `turnRemainingMs() <= 10_000`
- **THEN** 倒计时文本 MUST 带 `text-danger` class(视觉上红色调,取自主题 token)

#### Scenario: 辞局二次确认
- **WHEN** 点辞局按钮
- **THEN** MUST 先打开 CDK Dialog;只有确认按钮点击后才发 `POST /api/rooms/:id/resign`

#### Scenario: 离开房间(非 host-Waiting)→ 大厅
- **WHEN** 玩家在 Playing 房间点离开 + 后端回 204
- **THEN** `rooms.leave(id)` 被调一次;成功后 `router.navigateByUrl('/home')` 被调;hub `LeaveRoom` 也在 ngOnDestroy 路径自动发出

#### Scenario: host 离开自己的 Waiting 房间走 dissolve
- **WHEN** 当前用户 = `state.host.id` 且 `state.status === 'Waiting'`,点离开按钮
- **THEN** `rooms.dissolve(id)` 被调一次(DELETE),`rooms.leave` MUST NOT 被调;成功后 `router.navigateByUrl('/home')` 被调

#### Scenario: 用户名是链接
- **WHEN** 侧栏渲染 host=alice、black=alice、white=bob
- **THEN** "alice" 与 "bob" 文本均为 `<a>`,`href` 解析到 `/users/<id>`;有 `username-link` class

### Requirement: `UrgeOpponent` 按钮 —— 服务端 30s 冷却客户端镜像

RoomPage 的"催促对手"按钮 SHALL:

- 仅对玩家可见(`mySide() !== 'spectator'`);观众无此按钮
- `disabled` 当以下任一为真:
  - `myTurn() === true`(后端会抛 `NotOpponentsTurnException`,客户端提前防)
  - `urgeCooldownUntil > now()`(镜像 30s 冷却)
  - `state.status !== 'Playing'`
  - hub 正在重连 / 断线
- 点击成功 → `urgeCooldownUntil = Date.now() + 30_000`;禁用状态至冷却结束
- 点击失败 429 `UrgeTooFrequentException` → 同步冷却(`urgeCooldownUntil` 设为刚才估计值或从响应的 `Retry-After` 头读);显示翻译 toast `game.urge.button-disabled-cooldown`
- 点击失败其它 → generic toast

Urge 被叫方:hub `urged$` emit → RoomPage 显示顶部 toast(i18n `game.urge.toast`)~ 4 秒后自动消失。Toast 使用 `var(--color-*)` 着色,不用 Material snackbar。

#### Scenario: 对方回合可用,本方回合禁用
- **WHEN** `mySide() === 'black'` 且 `currentSeat === 1`
- **THEN** Urge 按钮启用;点击后发 `hub.urge(roomId)`

#### Scenario: 本方回合禁用
- **WHEN** `currentSeat === 0` 且 `mySide() === 'black'`
- **THEN** Urge 按钮 `disabled`

#### Scenario: 冷却中禁用
- **WHEN** 上次成功 urge 后 20 秒
- **THEN** Urge 按钮 `disabled`,tooltip/aria-label 解释剩余冷却

#### Scenario: 被叫方 toast
- **WHEN** 对手 urge 了我
- **THEN** RoomPage 页顶 MUST 出现 toast 显示 `game.urge.toast` 翻译文本,大约 4 秒后消失

### Requirement: RoomState 类型完整化 —— scaffold 留下的 `unknown` 被完整类型替换

`src/app/core/api/models/room.model.ts` SHALL 声明与后端 DTO 对齐的完整类型,覆盖
`Stone` / `GameResult` / `GameEndReason` / `ChatChannel` 四个字符串字面量联合,以及
`MoveDto` / `GameSnapshot` / `GameEndedDto` / `RoomState` 四个接口。
`RoomState.game` SHALL 为 `GameSnapshot | null`;`RoomState.chatMessages` SHALL 为 `readonly ChatMessage[]`。

所有字段名 MUST 与后端 System.Text.Json camelCase + `JsonStringEnumConverter` 产生的 wire 名完全对齐。
**枚举类型 MUST 是字符串字面量并联类型**,不是数字 enum。

**这条要求此前把那三个接口的源码整段抄在这里,而它自己的正文就写着「一条把源码整段抄进来的
requirement,会在每一次那段源码变化时静静过期」。** 它随后又过期了一次(本次改动),所以这次
不是更新那份抄本,而是**删掉它**:要求改为点出**哪些类型必须存在**与**哪些决定必须成立**,
逐字段的形状由 TypeScript 自己保证 —— 那是编译器的活,不是规格的活。

以下几条是**决定**,所以留在规格里:

- **`MoveDto.seat: number` 与 `GameSnapshot.currentSeat: number` MUST 是座位号,MUST NOT 是棋色。**
  服务端此前经 `SeatWire.ToStone(seat)` 换算,而那是 `seat === 0 ? Black : White` —— 于是
  **2 号座位被说成 1 号**。实测:三座位房间三手 `bid:0` 的 `stone` 是 `Black / White / White`,
  两个农民在走子记录里重合,`currentTurn` 在两个不同玩家的回合都报 `White`。
- **颜色是显示层对座位的读法**,住在 `games/board-seats.ts`(`seatStone`):五子棋读 0 号为黑,
  象棋读 0 号为红。同一个数字两种读法,而两种都只在显示层成立。这与后端 `BoardSeats` 是同一件事,
  与被删掉的 `SeatWire` 恰恰相反 —— 后者把这个读法写进了**契约**。
- **`GameResult` MUST NOT 含带颜色的取值。** 服务端合并了 `BlackWin` / `WhiteWin`,理由是那两个值
  与 `winnerUserId` 说的是同一件事。客户端因此 MUST 用 `winnerUserId` 判断"谁赢了",MUST NOT 拿
  结果值去跟自己的棋色比 —— 后者在座位数超过两个时无从下手。
- `row` / `col` 可空(无盘面棋种),`text` / `fromRow` / `fromCol` 存在,`GameEndReason` 里是
  `Decided` 而不是 `Connected5`。

#### Scenario: 类型编译通过
- **WHEN** 用更新后的 `RoomState` 解析 `GET /api/rooms/:id` 的真实响应(在开发环境)
- **THEN** 无 TypeScript 错误,字段名逐一对应

#### Scenario: 带颜色的结果值不再存在
- **WHEN** 代码写 `result === 'BlackWin'`
- **THEN** TypeScript MUST 报错 —— 该取值已不在联合类型里

#### Scenario: 棋色不再出现在走子载荷上
- **WHEN** 代码写 `move.stone` 或 `game.currentTurn`
- **THEN** TypeScript MUST 报错 —— 这两个字段已换成 `seat` / `currentSeat`

#### Scenario: 两个座位画成两种颜色
- **WHEN** 棋盘上有一手 `seat: 0` 与一手 `seat: 1`
- **THEN** 前者 MUST 渲染成黑子、后者 MUST 渲染成白子 —— 一条断言,因为把 `seatStone` 改成
  永远返回 `'Black'` 曾让**全部 744 条前端测试保持绿色**
