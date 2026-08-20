# web-game-board 的规格变化

## MODIFIED Requirements

### Requirement: 房间侧栏 —— 信息 + 回合倒计时 + 辞局 + 离开按钮

`src/app/pages/rooms/room-page/sidebar/sidebar.ts` SHALL 渲染:

- 房间名 `state.name` + 房主 `state.host.username`(`game.room.*` i18n)。**房主用户名 SHALL 是 `routerLink` 链接到 `/users/<host.id>`,使用 `.username-link` class + `(click)="$event.stopPropagation()"`**。
- **座位名单,而判据是这个棋种的座位数,不是坐了几个人。** 判据 SHALL 是
  `GET /api/games` 给的 `seatCount`,MUST NOT 是 `state.seats.length` —— 后者是
  「有几个座位**被坐上了**」,于是一个**等待中**的三座位房间会被当成两座位房间渲染。
  (那正是本要求此前的写法留下的缺陷,在浏览器里量到:一个两人在座的斗地主房间,
  侧栏原文是 `Black: … White: …`。)
  - `seatCount == 2`:渲染「黑方 / 白方」两个座位(象棋读作红 / 黑)。**颜色留着**,
    因为你正看着一张摆着黑白子的棋盘,而「谁是黑方」是座位号给不出的信息。
  - `seatCount > 2`:按座位号逐个渲染,**含空座位**。
  - 两支的 username SHALL 都是 `/users/<id>` 链接。
  - 描述符尚未到达时 MUST NOT 猜:`RoomPage` 的 loading 状态里本来就含
    `!capabilities.loaded()`,所以整页是骨架屏。

  **大厅行的答案与这里不同,而两个都对。** 大厅 MUST NOT 说颜色(`fix-lobby-seats`):
  它是跨棋种的列表,而 `board-seats.ts` 的文档写着那套读法只有棋盘家族可以调用。
  侧栏在一个具体棋种的房间里,那个房间要么有棋盘要么没有。**同一个问题,两个层次,
  两个答案。**
- 当前状态徽章(`Waiting / Playing / Finished`)
- 当前回合指示:两座位棋种读作 `game.turn.black-turn` / `white-turn`,座位数大于二的棋种
  MUST 说座位号 —— 「白方走棋」在一个没有白方的棋种里是错的(`add-web-doudizhu` 修的)。
  若 `mySide()` 对应的座位等于 `currentSeat`,额外突出 `game.turn.your-turn`
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

#### Scenario: 等待中的三座位房间不说颜色
- **WHEN** 一个 `seatCount == 3` 的房间只坐了两个人
- **THEN** 侧栏按座位号渲染,MUST NOT 出现「黑方」/「白方」

#### Scenario: 两座位棋种仍然说颜色
- **WHEN** 一个 `seatCount == 2` 的房间
- **THEN** 侧栏说「黑方 / 白方」—— 棋盘上就是黑白子
