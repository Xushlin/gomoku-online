## MODIFIED Requirements

### Requirement: 房间侧栏 —— 信息 + 回合倒计时 + 辞局 + 离开按钮

`src/app/pages/rooms/room-page/sidebar/sidebar.ts` SHALL 渲染:

- 房间名 `state.name` + 房主 `state.host.username`(`game.room.*` i18n)。**房主用户名 SHALL 是 `routerLink` 链接到 `/users/<host.id>`,使用 `.username-link` class + `(click)="$event.stopPropagation()"`**。
- 黑方座位 `state.black?.username` / 白方座位 `state.white?.username`,每个座位显示是否在线(未实现在线探测则显示 `username` 字面量)。**座位上的 username SHALL 同样是 `/users/<id>` 链接**(空座位文案不变)。
- 当前状态徽章(`Waiting / Playing / Finished`)
- 当前回合指示:`state.game.currentTurn === 'Black' ? game.turn.black-turn : game.turn.white-turn`;若 `mySide()` 与 `currentTurn` 对应,额外突出 `game.turn.your-turn`
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
- **WHEN** `mySide() === 'black'` 且 `state.game.currentTurn === 'Black'`
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

---

### Requirement: `RoomsApiService` 增加 `resign(roomId)` 方法

`src/app/core/api/rooms-api.service.ts` SHALL 增加:

```ts
abstract resign(roomId: string): Observable<GameEndedDto>;
abstract getReplay(roomId: string): Observable<GameReplayDto>;
abstract dissolve(roomId: string): Observable<void>;
```

`resign` —— Default 实现 POST `/api/rooms/{id}/resign`(无 body)。返回体按 `GameEndedDto` 形状解析。调用方 RoomPage 在收到响应后**不**立即打开 dialog —— 让 hub 的 `GameEnded` 事件作为唯一打开路径,避免重复;响应值只用于调试 / 失败时 toast。

`getReplay` —— Default 实现 `GET /api/rooms/{id}/replay`。返回体按 `GameReplayDto` 形状解析。404 / 409 / 401 由调用方(`ReplayPage`)按 HTTP 状态码分支处理(见 `web-replay` capability)。

`dissolve` —— Default 实现 `DELETE /api/rooms/{id}`。仅在调用方判定"当前用户是 host 且房间 status === 'Waiting'"时调用(后端只对这种组合允许 dissolve;Playing / Finished 状态走辞局 / 超时路径)。成功响应 204 No Content;后端会向房间内所有 SignalR 连接发出 `RoomDissolved` 事件,调用方除了显式 navigate `/home` 外,也可以依赖既有的 `roomDissolved$` 订阅自动跳转。

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

#### Scenario: dissolve 路径与方法
- **WHEN** 调 `rooms.dissolve('r-1')`
- **THEN** 实际发出 `DELETE /api/rooms/r-1`,无请求 body
