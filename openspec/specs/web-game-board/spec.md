# web-game-board Specification

## Purpose
TBD - created by archiving change add-web-game-board. Update Purpose after archive.
## Requirements
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
- `sendChat(roomId: string, content: string, channel: 'Room' | 'Spectator'): Promise<void>`
- `urge(roomId: string): Promise<void>`
- `applySnapshot(state: RoomState): void` —— REST rehydration path 用

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

---

### Requirement: Hub 连接使用 `/hubs/gomoku` + 查询串 JWT + `AuthService.accessToken()` 工厂

`DefaultGameHubService` SHALL 使用 `HubConnectionBuilder`:

- URL: `'/hubs/gomoku'`
- `accessTokenFactory: () => authService.accessToken() ?? ''` —— 工厂被 SignalR 在每次 connect / auto-reconnect 调用,读当前 `AuthService.accessToken()` signal 的值,保证 token 刷新后的自动重连用最新 token
- `withAutomaticReconnect([0, 2000, 5000, 10000, 30000])` —— 共 5 次重连尝试,时间 0s / 2s / 5s / 10s / 30s
- `configureLogging(LogLevel.Warning)` —— 生产日志级别;调试时可在 dev 环境覆盖

连接 MUST **懒启动**:构造 service 时不连接;只有在首次 `joinRoom()` / `joinSpectatorGroup()` / `makeMove()` / `sendChat()` / `urge()` 被调用时才 `connection.start()`。大厅页 / 其它路由 MUST NOT 触发握手。

`accessToken()` 为 null 时 MUST NOT 触发 connect —— caller(RoomPage)在 `auth.isAuthenticated() === false` 的情况下 MUST NOT 调任何 hub 方法。

#### Scenario: 路过大厅不建立连接
- **WHEN** 登录用户打开 `/home`,在大厅页停留 5 分钟,不进入任何房间
- **THEN** MUST NOT 有任何 WebSocket 握手发往 `/hubs/gomoku`

#### Scenario: 首次 joinRoom 建立连接
- **WHEN** RoomPage `ngOnInit` 调 `hub.joinRoom('r-1')`
- **THEN** MUST 向 `/hubs/gomoku?access_token=<JWT>` 发起一次 WebSocket 握手(auto-reconnect 期间的重试不计入"首次")

#### Scenario: 同连接跨房复用
- **WHEN** 用户从 `/rooms/a` 导航到 `/rooms/b`,期间未重新启动 app
- **THEN** MUST NOT 关闭连接后重建;service MUST 在一次 `leaveRoom('a') → joinRoom('b')` 后继续用同一 `HubConnection`

#### Scenario: token 刷新后自动重连用新 token
- **WHEN** `accessToken` signal 从 `'oldToken'` 被更新到 `'newToken'`(auth interceptor 的 refresh 路径);随后服务端以 token 过期为由关闭连接;auto-reconnect 触发
- **THEN** SignalR 再次握手时的查询串 `?access_token=` MUST = `'newToken'`

---

### Requirement: 重连协议 —— REST snapshot 是权威恢复源

`DefaultGameHubService` SHALL 订阅 SignalR `HubConnection.onreconnecting` / `onreconnected` / `onclose` 回调,并按下面映射 `connectionStatus`:

- `onreconnecting(err)` → `connectionStatus.set('reconnecting')`
- `onreconnected(id)` → `connectionStatus.set('connected')`
- `onclose(err)` 无 err(正常关闭,如 `connection.stop()`)→ `connectionStatus.set('disconnected')`
- `onclose(err)` 有 err(重连耗尽后关闭)→ `connectionStatus.set('disconnected')`

`RoomPage` —— **不是** service —— SHALL 负责重连后的 rehydration 编排:

- `effect(() => hub.connectionStatus())` 观察,从 `'reconnecting'` 过渡到 `'connected'` 时触发 rehydration 序列:
  1. `await hub.joinRoom(currentRoomId)` —— 重新加入 group
  2. 若 rehydration 前已知当前用户是 spectator(通过 `hub.state()` 计算的 `mySide === 'spectator'` 或 REST snapshot),`await hub.joinSpectatorGroup(currentRoomId)`
  3. `roomsApi.getById(currentRoomId)` → 成功后 `hub.applySnapshot(res)`
  4. 失败(404 —— 房间已被解散):navigate to `/home`

在 `connectionStatus === 'reconnecting'` 期间,RoomPage MUST 在页面顶部渲染一条可见 banner(i18n 键 `game.connection.reconnecting`)。`disconnected` 时 banner 显示 `game.connection.disconnected` + 一个 `game.connection.retry` 按钮,点击调 service 的 `reconnect()` 方法重启连接并重新 rehydrate。

重连期间 `state()` MUST NOT 被清为 null —— UI 继续显示最后已知的状态,仅加一层"正在重连"的 banner;rehydration 成功后 state 被 `applySnapshot` 覆盖。

#### Scenario: 短暂断线自动恢复
- **WHEN** 用户 wifi 中断 5 秒后恢复;auto-reconnect 在第二次尝试成功
- **THEN** banner MUST 在 reconnecting 期间可见;成功后 MUST 调 `joinRoom` + `rooms.getById` 做 rehydration;随后 banner MUST 消失;state 最终反映 rehydration 后的完整最新状态

#### Scenario: 重连用尽后显示 Retry
- **WHEN** 所有 5 次 auto-reconnect 全部失败
- **THEN** `connectionStatus() === 'disconnected'`;页面显示翻译后的断线 banner + Retry 按钮;点击 Retry MUST 重新 `connection.start()`(实现可以是新建 HubConnection 或 `start()` 现有实例,任选其一)

#### Scenario: 重连期间状态不被清空
- **WHEN** 从 `connected` → `reconnecting`
- **THEN** `state()` MUST 保持之前的值(不变成 null);玩家继续看到棋盘(冻结),直到 reconnected + rehydrated

#### Scenario: 重连时房间已被解散
- **WHEN** reconnected 后 rehydration 的 `GET /api/rooms/:id` 回 404
- **THEN** RoomPage MUST navigate 到 `/home`;`state` 被清;不留残余连接订阅

---

### Requirement: `RoomPage` 替换 `/rooms/:id` placeholder

`app.routes.ts` 的 `/rooms/:id` 路由 SHALL:

- 依然是 lazy (`loadComponent`)
- 依然带 `canMatch: [authGuard]`
- 目标组件从 `RoomPlaceholder` 切到 `RoomPage`

`src/app/pages/rooms/room-placeholder/` 目录连同其组件与测试 MUST 被删除。`src/app/pages/rooms/room-page/` 新增承载真实对局 UI。

`RoomPage` `ngOnInit` 序列:

1. 读路由参数 `id`。
2. `auth.isAuthenticated() === false` → 按 guard 不可能到此,防御性 navigate `/login`。
3. `roomsApi.getById(id)` —— 成功 → `hub.applySnapshot(res)`;404 → 显示翻译的 not-found 文案 + back-to-lobby 链接;其它错误 → 翻译 generic error banner + retry 按钮。
4. `await hub.joinRoom(id)` —— 进 hub group。
5. 若 `mySide() === 'spectator'`,`await hub.joinSpectatorGroup(id)`。
6. 启动 1 Hz `now` 信号驱动的回合倒计时。
7. 订阅 `hub.urged$` / `hub.roomDissolved$` / `hub.gameEnded` 副作用(见下)。

`ngOnDestroy` 序列:

1. 停倒计时 `clearInterval`。
2. `hub.leaveRoom(id)` —— 只退 group,**不** REST leave(不因关闭 tab 放弃玩家位)。
3. 解绑副作用订阅。

#### Scenario: placeholder 被完全替换
- **WHEN** 构建完成
- **THEN** `src/app/pages/rooms/room-placeholder/` 目录不存在;`/rooms/:id` 路由的 `loadComponent` 解析到 `RoomPage`

#### Scenario: 进入房间时初始化流程
- **WHEN** 登录用户从大厅点 Join 导航到 `/rooms/abc-123`
- **THEN** 可观察到的请求序列:`GET /api/rooms/abc-123`(200),随后 WebSocket 握手 + `JoinRoom('abc-123')` 调用。后续页面只靠 hub 推送增量,MUST NOT 再做任何 `GET /api/rooms/abc-123` 轮询(rehydration 是例外,只在重连成功后)

#### Scenario: 关闭 tab 不退出玩家位
- **WHEN** 用户关闭 tab
- **THEN** MUST 发出 hub `LeaveRoom` 调用;MUST NOT 发出 `POST /api/rooms/:id/leave`(玩家在服务端仍然持位)

---

### Requirement: `Board` 组件 —— 15×15 CSS grid,点击调 hub.makeMove,非本方回合禁用

`src/app/pages/rooms/room-page/board/board.ts` SHALL 渲染 15×15 的按钮网格,每格是 `<button type="button">`,代表 `Stone`(`'Empty' | 'Black' | 'White'`):

- 网格使用 CSS grid(`grid-template-columns: repeat(15, 1fr); aspect-square; max-width: ~600px`)。颜色全部来自主题变量(棋盘背景 `var(--color-surface)`、格线 `var(--color-border)`、黑子 `var(--color-text)`、白子 `var(--color-bg)` 加 `var(--color-border)` 细边)。
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
- **THEN** 所有 225 个按钮 `disabled`;点击不触发任何事件

#### Scenario: 已结束不可落子
- **WHEN** `state.status === 'Finished'`
- **THEN** 所有按钮 `disabled`;最后一步高亮仍可见

#### Scenario: 失败时回滚 + rehydrate
- **WHEN** `hub.makeMove` 抛 `HubException`(例如"concurrent")
- **THEN** UI 不把该格渲染为已落子;翻译 toast 显示;`roomsApi.getById` 被调一次以同步服务端真实状态

#### Scenario: readonly 模式
- **WHEN** `<app-board [readonly]="true" [state]="..." />`
- **THEN** 所有按钮 `disabled`;`hub.makeMove` 永不被调用

---

### Requirement: 房间侧栏 —— 信息 + 回合倒计时 + 辞局 + 离开按钮

`src/app/pages/rooms/room-page/sidebar/sidebar.ts` SHALL 渲染:

- 房间名 `state.name` + 房主 `state.host.username`(`game.room.*` i18n)
- 黑方座位 `state.black?.username` / 白方座位 `state.white?.username`,每个座位显示是否在线(未实现在线探测则显示 `username` 字面量)
- 当前状态徽章(`Waiting / Playing / Finished`)
- 当前回合指示:`state.game.currentTurn === 'Black' ? game.turn.black-turn : game.turn.white-turn`;若 `mySide()` 与 `currentTurn` 对应,额外突出 `game.turn.your-turn`
- **回合倒计时**:
  - 计算 `deadline = state.game.turnStartedAt + state.game.turnTimeoutSeconds`
  - 显示剩余时间 `M:SS`,驱动源是 RoomPage 的 1 Hz `now` signal
  - 剩余 ≤ 10s 时用 `text-danger` 强调
  - 剩余 ≤ 0s 时显示 `0:00`,后端轮询最多 5s 内会发 `GameEnded`
- 玩家专用按钮(`mySide() !== 'spectator'` 时渲染):
  - **辞局**:需二次确认(CDK Dialog, `ResignConfirmDialog`);确认后 `rooms.resign(id)` REST;无论成功失败,后续 `GameEnded` 事件负责打开结束弹窗(见下一条 Requirement)
  - **离开房间**:直接 `rooms.leave(id)` REST,成功后 `router.navigateByUrl('/home')`;网络错误 → generic error toast,不导航
- 观众专用:不显示辞局 / 离开;可能有"停止观战"按钮(调 REST `POST /api/rooms/:id/spectate` 的反向 `DELETE`;如果 spec 没有 DELETE endpoint,则不提供此按钮)

所有文案走 `| transloco`,零硬编码。

#### Scenario: 我方回合突出
- **WHEN** `mySide() === 'black'` 且 `state.game.currentTurn === 'Black'`
- **THEN** 侧栏 MUST 同时显示 `game.turn.black-turn` 与 `game.turn.your-turn`

#### Scenario: 倒计时低于阈值强调
- **WHEN** `turnRemainingMs() <= 10_000`
- **THEN** 倒计时文本 MUST 带 `text-danger` class(视觉上红色调,取自主题 token)

#### Scenario: 辞局二次确认
- **WHEN** 点辞局按钮
- **THEN** MUST 先打开 CDK Dialog;只有确认按钮点击后才发 `POST /api/rooms/:id/resign`

#### Scenario: 离开房间 → 大厅
- **WHEN** 点离开 + 后端回 204
- **THEN** `router.navigateByUrl('/home')` 被调;hub `LeaveRoom` 也在 ngOnDestroy 路径自动发出

---

### Requirement: `ChatPanel` —— 双通道不对称可见性

`src/app/pages/rooms/room-page/chat/chat-panel.ts` SHALL:

- 接收 `state().chatMessages` + 后续 `ChatMessage` 事件合并作为消息源
- 通道拆分(按 `message.channel`):
  - Room 频道(`channel === 'Room'`):玩家 + 观众**都可见**
  - Spectator 频道(`channel === 'Spectator'`):**只有观众可见**
- 视觉呈现:
  - 玩家只看到一个 Room tab(不显示 Spectator tab)
  - 观众看到两个 tab:Room / Spectator
- 发送:
  - 当前激活 tab 决定发送 `channel` 参数
  - 输入框有 `Validators.maxLength(500)`(匹配后端);超长时按钮禁用,提示 `game.chat.max-length-error`
  - 空白 / 只含空格的输入 disabled 提交(trim 后 length 0)
  - 点发送 → `hub.sendChat(roomId, content.trim(), channel)`;成功后清空输入框;失败(403 如玩家试图发 Spectator —— 理论上按钮不存在)→ `game.chat.forbidden-error` 翻译 banner
- 聊天消息列表 MUST 按 `sentAt` 升序,自动滚到底;渲染 `senderUsername: content` 格式,Username 用 text 插值自动转义

#### Scenario: 玩家只看 Room tab
- **WHEN** `mySide() !== 'spectator'`
- **THEN** `ChatPanel` DOM 中 MUST 仅有一个 tab 按钮(Room);不存在 Spectator tab 的 DOM

#### Scenario: 观众看两个 tab
- **WHEN** `mySide() === 'spectator'`
- **THEN** MUST 存在两个 tab 按钮(Room / Spectator);Room tab 默认激活

#### Scenario: 发送带当前 channel
- **WHEN** 观众在 Spectator tab 输入 "hello" 点发送
- **THEN** `hub.sendChat(roomId, 'hello', 'Spectator')` 被调一次

#### Scenario: 500 字符超限
- **WHEN** 输入长度 501
- **THEN** 发送按钮 `disabled`;输入框下方显示 `game.chat.max-length-error`

#### Scenario: 消息用户名防 XSS
- **WHEN** 某消息 `senderUsername` 为 `<img onerror=alert(1)>`
- **THEN** DOM 中渲染字面文本,不执行脚本(Angular 插值默认转义;MUST NOT 使用 `innerHTML` / `bypassSecurityTrust*`)

---

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
- **WHEN** `mySide() === 'black'` 且 `currentTurn === 'White'`
- **THEN** Urge 按钮启用;点击后发 `hub.urge(roomId)`

#### Scenario: 本方回合禁用
- **WHEN** `currentTurn === 'Black'` 且 `mySide() === 'black'`
- **THEN** Urge 按钮 `disabled`

#### Scenario: 冷却中禁用
- **WHEN** 上次成功 urge 后 20 秒
- **THEN** Urge 按钮 `disabled`,tooltip/aria-label 解释剩余冷却

#### Scenario: 被叫方 toast
- **WHEN** 对手 urge 了我
- **THEN** RoomPage 页顶 MUST 出现 toast 显示 `game.urge.toast` 翻译文本,大约 4 秒后消失

---

### Requirement: Game-ended CDK Dialog 由 `gameEnded` signal 驱动

RoomPage SHALL `effect(() => { ... })` 监听 `hub.gameEnded()`,当其从 null 变 non-null 时:

- 打开 `GameEndedDialog`(`src/app/pages/rooms/room-page/dialogs/game-ended-dialog.ts`)
- Dialog 数据 = `{ result, winnerUserId, endReason }` + 当前用户 id(供 dialog 计算"你赢了/你输了/平局"视角)
- Dialog 内容:
  - Title:
    - `result === 'Draw'` → `game.ended.title-draw`
    - `result === 'BlackWin' && mySide === 'black'` → `game.ended.title-win`
    - `result === 'WhiteWin' && mySide === 'white'` → `game.ended.title-win`
    - 否则 → `game.ended.title-lose`
  - Reason:`game.ended.reason-connected-5` / `.reason-resigned` / `.reason-timeout`(注意 `Connected5` 在 Draw 时也是该 reason,但 title 已经是 draw,不再强调 reason)
  - 按钮:主按钮 `game.ended.back-to-lobby` → `router.navigateByUrl('/home')`;次按钮 `game.ended.dismiss` 关弹窗留在只读 RoomPage
- Dialog 打开期间棋盘仍显示最终局面(不在它前面遮挡整个视图);背后的 RoomPage 仍响应 Resize / Leave 按钮
- 离开房间(`leaveRoom` 被调、navigate 走)时 `hub.gameEnded` signal 被清(下一次进入房间重新开始)

#### Scenario: Gameover 自动弹框
- **WHEN** `hub.gameEnded()` 从 null 变为 `{ result: 'BlackWin', ..., endReason: 'Connected5' }` 且我是黑方
- **THEN** CDK Dialog 自动打开,title = `game.ended.title-win` 翻译文案,reason = `game.ended.reason-connected-5`

#### Scenario: 平局
- **WHEN** `result === 'Draw'`
- **THEN** title = `game.ended.title-draw`;reason 部分可显示 `game.ended.reason-connected-5`(后端用此 reason 表示"棋盘满了")或单独的 draw 说明文案(implementation choice,翻译键已备好)

#### Scenario: 返回大厅按钮
- **WHEN** 弹窗中点主按钮
- **THEN** `router.navigateByUrl('/home')` 被调;dialog 关闭;RoomPage 被销毁

#### Scenario: 关闭保留只读视图
- **WHEN** 弹窗中点次按钮(Dismiss)
- **THEN** dialog 关闭;RoomPage 仍在 `/rooms/:id`;棋盘只读(因为 `status === 'Finished'`)

---

### Requirement: `RoomsApiService` 增加 `resign(roomId)` 方法

`src/app/core/api/rooms-api.service.ts` SHALL 增加:

```ts
abstract resign(roomId: string): Observable<GameEndedDto>;
```

Default 实现 POST `/api/rooms/{id}/resign`(无 body)。返回体按 `GameEndedDto` 形状解析。调用方 RoomPage 在收到响应后**不**立即打开 dialog —— 让 hub 的 `GameEnded` 事件作为唯一打开路径,避免重复;响应值只用于调试 / 失败时 toast。

#### Scenario: 方法路径正确
- **WHEN** 调 `rooms.resign('r-1')`
- **THEN** 实际发出 `POST /api/rooms/r-1/resign`,无请求 body(或空对象 `{}`)

#### Scenario: 响应形状
- **WHEN** 后端回 200 + `{ result, winnerUserId, endedAt, endReason }`
- **THEN** Observable emit 该对象;调用方可选择性读取但不触发 UI(让 hub 事件驱动 dialog)

---

### Requirement: RoomState 类型完整化 —— scaffold 留下的 `unknown` 被完整类型替换

`src/app/core/api/models/room.model.ts` SHALL 把 `add-web-lobby` change 遗留的 `RoomState.game: unknown | null` 与 `RoomState.chatMessages: readonly unknown[]` 完整化,对齐后端 DTO:

```ts
export type Stone = 'Empty' | 'Black' | 'White';
export type GameResult = 'Ongoing' | 'BlackWin' | 'WhiteWin' | 'Draw';
export type GameEndReason = 'Connected5' | 'Resigned' | 'TurnTimeout';
export type ChatChannel = 'Room' | 'Spectator';

export interface MoveDto {
  readonly ply: number;
  readonly row: number;
  readonly col: number;
  readonly stone: Stone;
  readonly playedAt: string;
}

export interface GameSnapshot {
  readonly id: string;
  readonly currentTurn: Stone;
  readonly startedAt: string;
  readonly endedAt: string | null;
  readonly result: GameResult | null;
  readonly winnerUserId: string | null;
  readonly endReason: GameEndReason | null;
  readonly turnStartedAt: string;
  readonly turnTimeoutSeconds: number;
  readonly moves: readonly MoveDto[];
}

export interface ChatMessage {
  readonly id: string;
  readonly senderUserId: string;
  readonly senderUsername: string;
  readonly content: string;
  readonly channel: ChatChannel;
  readonly sentAt: string;
}

export interface GameEndedDto {
  readonly result: GameResult;
  readonly winnerUserId: string | null;
  readonly endedAt: string;
  readonly endReason: GameEndReason;
}

export interface UrgeDto {
  readonly roomId: string;
  readonly urgerUserId: string;
  readonly urgedUserId: string;
  readonly sentAt: string;
}
```

`RoomState.game` SHALL 为 `GameSnapshot | null`;`RoomState.chatMessages` SHALL 为 `readonly ChatMessage[]`。

所有字段名 MUST 与后端 System.Text.Json camelCase + `JsonStringEnumConverter` 产生的 wire 名完全对齐。**枚举类型 MUST 是字符串字面量并联类型**,不是数字 enum —— 后端统一 PascalCase 字符串枚举。

#### Scenario: 类型编译通过
- **WHEN** 用更新后的 `RoomState` 解析 `GET /api/rooms/:id` 的真实响应(在开发环境)
- **THEN** 无 TypeScript 错误,字段名逐一对应

#### Scenario: 枚举是字符串不是数字
- **WHEN** 代码写 `state.game?.currentTurn === 'Black'`
- **THEN** 编译通过(字面量联合类型);写 `=== 1` 不通过

---

### Requirement: 错误处理 —— `HubException` 消息到翻译键的映射

RoomPage / Board / ChatPanel SHALL 把从 hub 命令 promise 抛出的 `HubException` 处理为用户可见的翻译文案。映射规则(按消息字段包含的关键字,case-insensitive):

- 包含 `"not your turn"` → `game.errors.not-your-turn`
- 包含 `"invalid move"` 或 `"occupied"` 或 `"out of bounds"` → `game.errors.invalid-move`
- 包含 `"concurrent"` 或 `"DbUpdateConcurrency"` → `game.errors.concurrent-move-refetched`(并**必须**跟进一次 `roomsApi.getById → applySnapshot`)
- 包含 `"too frequent"`(urge 冷却)→ `game.errors.urge-cooldown`
- 其它未识别 → `game.errors.generic`

网络层错误(Promise rejection 不是 `HubException`,而是 connection 已断)→ `game.errors.network`。

这种字符串匹配承认脆弱但**是当前后端没有结构化错误码的最小痛苦**方案;design.md 记录了后续添加 typed error code 的跟进项。

#### Scenario: 并发错误走 rehydration
- **WHEN** `hub.makeMove` reject,消息包含 `'concurrent'`
- **THEN** 显示 `game.errors.concurrent-move-refetched` 翻译 toast;`roomsApi.getById(id)` 被调一次;state 被 `applySnapshot` 替换

#### Scenario: 未识别错误走 generic
- **WHEN** `HubException` 消息是 `"something weird"`
- **THEN** toast 显示 `game.errors.generic` 翻译

---

### Requirement: i18n —— `game.*` 翻译树同步扩充

`public/i18n/en.json` 与 `public/i18n/zh-CN.json` SHALL 同步新增 `game.*` 键集合,包含但不限于:

- `game.room.{name-label, host-label, seat-black, seat-white, status-waiting, status-playing, status-finished}`
- `game.board.{cell-aria-label, last-move-label}`(cell-aria-label 带 `{{row}}` / `{{col}}` 插值占位符)
- `game.turn.{your-turn, opponent-turn, black-turn, white-turn, countdown-label}`
- `game.actions.{resign, resign-confirm-title, resign-confirm-body, resign-confirm-ok, leave, urge}`
- `game.chat.{title, tab-room, tab-spectator, send, placeholder, empty, max-length-error, forbidden-error}`
- `game.urge.{toast, button-disabled-own-turn, button-disabled-cooldown}`
- `game.ended.{title-win, title-lose, title-draw, reason-connected-5, reason-resigned, reason-timeout, back-to-lobby, dismiss}`
- `game.errors.{generic, network, not-your-turn, invalid-move, concurrent-move-refetched, urge-cooldown}`
- `game.connection.{reconnecting, disconnected, retry, connected}`

键集合 MUST 两份 JSON 完全相等;已有 flattener parity check 持续 0 drift。

模板 MUST 零硬编码 CJK / 长英文显示字符串;按 scaffold / auth / lobby 已立规则。

#### Scenario: parity
- **WHEN** 对比 `en.json` 与 `zh-CN.json` flatten 后的 key 集合
- **THEN** 差集为空

#### Scenario: 模板零硬编码
- **WHEN** 在 `src/app/pages/rooms/room-page/**/*.html` 下搜索 CJK 字符或 ≥3 字母英文显示字符串
- **THEN** 0 匹配(Brand / test-id / 技术字符串豁免)

---

### Requirement: 颜色 / 组件 / 交互规则继承所有先前立下的约定

Room 页 SHALL 遵守 scaffold / auth / lobby 立下的全部横切规则,MUST NOT 引入任何绕过这些规则的图样:

- 所有颜色 MUST 来自 token utilities / CSS variables(`bg-bg`, `bg-surface`, `text-text`, `text-primary`, `border-border`, `text-danger`, `rounded-card`, `shadow-elevated`)。MUST NOT 出现硬编码色值、rgb/hsl 字面量、tailwind palette utility(`bg-gray-*` 等)。
- 对话框 / 覆盖层 MUST 基于 `@angular/cdk/dialog`(不是自制 `*ngIf` 模态)。
- 所有字符串走 `| transloco`。
- HttpClient 只在 `src/app/core/api/`;组件层 / 服务层除 `AuthService` + API services + `TranslocoHttpLoader` 外 MUST NOT 直接 inject(HttpClient)。
- 页面 / 组件 LOC:`RoomPage` < 250,`Board` < 150,`Sidebar` < 150,`ChatPanel` < 200,`GameEndedDialog` < 100,`ResignConfirmDialog` < 50。
- 375px 视口:棋盘 + 侧栏 + 聊天面板都可达;棋盘在窄屏下占满宽度,侧栏与聊天在下方堆叠;无横向滚动。

#### Scenario: 全局 grep 通过
- **WHEN** 在 `src/app/pages/rooms/room-page/` 与 `src/app/core/realtime/` 下跑和 lobby 一致的色值 / tailwind palette / CJK 三套 grep
- **THEN** 0 匹配(test-spec fixture 豁免,延续 scaffold / auth / lobby 约定)

#### Scenario: 375px 无横向滚动
- **WHEN** 在 375 × 667 视口进入 `/rooms/:id`
- **THEN** `document.documentElement.scrollWidth <= document.documentElement.clientWidth`;棋盘 + 侧栏 + 聊天 3 个区域可见可达(经过垂直滚动)

