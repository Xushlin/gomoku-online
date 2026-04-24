## MODIFIED Requirements

### Requirement: Game-ended CDK Dialog 由 `gameEnded` signal 驱动

RoomPage SHALL `effect(() => { ... })` 监听 `hub.gameEnded()`,当其从 null 变 non-null 时:

- 打开 `GameEndedDialog`(`src/app/pages/rooms/room-page/dialogs/game-ended-dialog.ts`)
- Dialog 数据 = `{ result, winnerUserId, endReason, roomId }` + 当前用户 id(供 dialog 计算"你赢了/你输了/平局"视角并支持回放跳转)
- Dialog 内容:
  - Title:
    - `result === 'Draw'` → `game.ended.title-draw`
    - `result === 'BlackWin' && mySide === 'black'` → `game.ended.title-win`
    - `result === 'WhiteWin' && mySide === 'white'` → `game.ended.title-win`
    - 否则 → `game.ended.title-lose`
  - Reason:`game.ended.reason-connected-5` / `.reason-resigned` / `.reason-timeout`(注意 `Connected5` 在 Draw 时也是该 reason,但 title 已经是 draw,不再强调 reason)
  - 按钮:**主按钮** `game.ended.back-to-lobby` → `router.navigateByUrl('/home')`;**新次按钮** `game.ended.view-replay` → `router.navigateByUrl('/replay/<roomId>')`(本次新增);**收尾按钮** `game.ended.dismiss` 关弹窗留在只读 RoomPage
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
- **WHEN** 弹窗中点 `dismiss` 按钮
- **THEN** dialog 关闭;RoomPage 仍在 `/rooms/:id`;棋盘只读(因为 `status === 'Finished'`)

#### Scenario: 跳转回放
- **WHEN** 弹窗中点 `view-replay` 按钮
- **THEN** `router.navigateByUrl('/replay/<currentRoomId>')` 被调一次;dialog 关闭;新页面是 ReplayPage

---

### Requirement: `RoomsApiService` 增加 `resign(roomId)` 方法

`src/app/core/api/rooms-api.service.ts` SHALL 增加:

```ts
abstract resign(roomId: string): Observable<GameEndedDto>;
abstract getReplay(roomId: string): Observable<GameReplayDto>;
```

`resign` —— Default 实现 POST `/api/rooms/{id}/resign`(无 body)。返回体按 `GameEndedDto` 形状解析。调用方 RoomPage 在收到响应后**不**立即打开 dialog —— 让 hub 的 `GameEnded` 事件作为唯一打开路径,避免重复;响应值只用于调试 / 失败时 toast。

`getReplay`(本次新增)—— Default 实现 `GET /api/rooms/{id}/replay`。返回体按 `GameReplayDto` 形状解析。404 / 409 / 401 由调用方(`ReplayPage`)按 HTTP 状态码分支处理(见 `web-replay` capability)。

#### Scenario: 方法路径正确
- **WHEN** 调 `rooms.resign('r-1')`
- **THEN** 实际发出 `POST /api/rooms/r-1/resign`,无请求 body(或空对象 `{}`)

#### Scenario: 响应形状
- **WHEN** 后端回 200 + `{ result, winnerUserId, endedAt, endReason }`
- **THEN** Observable emit 该对象;调用方可选择性读取但不触发 UI(让 hub 事件驱动 dialog)

#### Scenario: getReplay 路径与编码
- **WHEN** 调 `rooms.getReplay('abc 123')`
- **THEN** 实际发出 `GET /api/rooms/abc%20123/replay`

#### Scenario: getReplay 错误透传
- **WHEN** 后端回 409
- **THEN** Observable error emit `HttpErrorResponse`,`.status === 409`;不被 service 内部捕获

---

### Requirement: 房间侧栏 —— 信息 + 回合倒计时 + 辞局 + 离开按钮

`src/app/pages/rooms/room-page/sidebar/sidebar.ts` SHALL 渲染:

- 房间名 `state.name` + 房主 `state.host.username`(`game.room.*` i18n)。**房主用户名 SHALL 是 `routerLink` 链接到 `/users/<host.id>`,使用 `.username-link` class + `(click)="$event.stopPropagation()"`**(本次新增)。
- 黑方座位 `state.black?.username` / 白方座位 `state.white?.username`,每个座位显示是否在线(未实现在线探测则显示 `username` 字面量)。**座位上的 username SHALL 同样是 `/users/<id>` 链接**(本次新增,空座位文案不变)。
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

#### Scenario: 用户名是链接
- **WHEN** 侧栏渲染 host=alice、black=alice、white=bob
- **THEN** "alice" 与 "bob" 文本均为 `<a>`,`href` 解析到 `/users/<id>`;有 `username-link` class

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
- 聊天消息列表 MUST 按 `sentAt` 升序,自动滚到底;渲染 `senderUsername: content` 格式。**`senderUsername` SHALL 渲染为 `<a [routerLink]="['/users', message.senderUserId]" class="username-link">` 链接,`(click)="$event.stopPropagation()"`**(本次新增);Username 文本仍走 Angular 插值默认转义,严禁 `innerHTML` / `bypassSecurityTrust*`。

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
- **THEN** DOM 中渲染字面文本(在 `<a>` 内),不执行脚本(Angular 插值默认转义;MUST NOT 使用 `innerHTML` / `bypassSecurityTrust*`)

#### Scenario: 用户名链接跳转资料页
- **WHEN** 用户点某条聊天的发送者用户名
- **THEN** navigate 到 `/users/<senderUserId>`
