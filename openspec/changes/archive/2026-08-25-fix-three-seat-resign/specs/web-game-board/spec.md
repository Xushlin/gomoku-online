## MODIFIED Requirements

### Requirement: 棋盘底下的操作条 —— 现在怎么样,以及我能做什么

`src/app/pages/rooms/room-page/action-bar/action-bar.ts` SHALL 渲染在**棋盘那一列的最后**,
承担三样从侧栏搬来的东西:回合指示、回合倒计时、玩家按钮组。

**位置的判据是 y 坐标,不是 `position` 属性。** 量到的:375 px 下棋盘 311×311 顶在
y=100,操作条顶在 **427**、按钮在 **488**(各 44 px 高),最下沿 532 —— 全在第一屏之内,
而它们在侧栏里时是 638–781。所以这一版
MUST NOT 吸底(`position: sticky`):吸底要付 `env(safe-area-inset-bottom)`、要盖住内容,
而且**牌桌自己有一排出牌按钮**,两条操作条上下叠着就得让人想一下哪个是出牌。

**牌桌那一排不搬。** `card-table` 的出牌 / 不要 / 提示贴着你的手牌,而选牌状态就在那里;
操作条拿的是**房间级**动作。这条差别 SHALL 写在这里,否则下一个人会把两者合并,然后
发现出牌按钮离手牌 400 px 远。

**触达尺寸:** 操作条里每个按钮 SHALL ≥ 44 px 高。搬之前它们是 **30 px** —— WCAG 2.2
SC 2.5.8(AA)的底线是 24×24,所以旧尺寸**合规**;44 是 SC 2.5.5(AAA)与各家移动端指南
的数,而这是一个用手指点的棋盘页面。

**视觉:** 用既有角色 `panel`(渐变 + 硬边 + `--shadow-raised`)。MUST NOT 为它新增角色
utility 或 token —— 「厚重」在这套 token 里已经有说法了。

**i18n:** 零新增键 —— `game.turn.*` / `game.actions.*` 都已存在。

内容:

- 当前回合指示:两座位棋种读作 `game.turn.black-turn` / `white-turn`,座位数大于二的棋种
  MUST 说座位号 —— 「白方走棋」在一个没有白方的棋种里是错的(`add-web-doudizhu` 修的)。
  若 `mySide()` 对应的座位等于 `currentSeat`,额外突出 `game.turn.your-turn`
- **回合倒计时**:
  - 计算 `deadline = state.game.turnStartedAt + state.game.turnTimeoutSeconds`
  - 显示剩余时间 `M:SS`,驱动源是 RoomPage 的 1 Hz `now` signal
  - 剩余 ≤ 10s 时用 `text-danger` 强调
  - 剩余 ≤ 0s 时显示 `0:00`,后端轮询最多 5s 内会发 `GameEnded`
- 玩家按钮组(`mySide() !== 'spectator'` 时渲染),横排一行,375 px 下换行。
  **认输只在 `seatCount == 2` 时渲染** —— 领域层对三座位的认输是硬拒绝(见
  `room-and-gameplay` 的 `Room.Resign`),而一个点了必定 409 的按钮比没有按钮更糟。
  判据 MUST 是 `=== 2`,MUST NOT 是「不大于 2」:后者在描述符还没到达时会说「可以认输」。
  - **辞局**:需二次确认(CDK Dialog, `ResignConfirmDialog`);确认后 `rooms.resign(id)` REST;无论成功失败,后续 `GameEnded` 事件负责打开结束弹窗(见 `Game-ended CDK Dialog 由 gameEnded signal 驱动` 那一条)
  - **离开房间** —— `RoomPage.handleLeave()` SHALL 分两条路径:
    - **当前用户是 host 且 `state.status === 'Waiting'`**(自己开的空房间)→ 调 `rooms.dissolve(id)` REST(`DELETE /api/rooms/:id`)。后端的 `Room.Leave` invariant 拒绝这种情况(`HostCannotLeaveWaitingRoomException`),所以前端必须走 dissolve 端点。Dissolve 成功后,后端发出 `RoomDissolved` SignalR 事件 —— 同房间所有连接(包括发起者本人)由既有的 `roomDissolved$` 订阅触发 navigate `/home`,所以即便不显式 navigate 也会到大厅。
    - **其它情况**(玩家在 Playing / Finished 房间;或观众;或非 host)→ 调 `rooms.leave(id)` REST(`POST /api/rooms/:id/leave`)。
  - 两条路径在前端 success 回调里都 `router.navigateByUrl('/home')`。网络错误 → generic error toast,不导航。
- 观众专用:不显示辞局 / 离开;可能有"停止观战"按钮(调 REST `POST /api/rooms/:id/spectate` 的反向 `DELETE`;如果 spec 没有 DELETE endpoint,则不提供此按钮)
  —— 观众仍然看得到回合指示与倒计时:那是「现在怎么样」,不是「我能做什么」。

#### Scenario: 我方回合突出
- **WHEN** `mySide() === 'black'` 且 `state.game.currentSeat === 0`
- **THEN** 操作条 MUST 同时显示 `game.turn.black-turn` 与 `game.turn.your-turn`

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

#### Scenario: 操作条在第一屏
- **WHEN** 375 px 宽的房间页渲染一局进行中的棋
- **THEN** 操作条紧跟在棋盘之后 —— 判据是它与棋盘的相对位置,MUST NOT 是「它是不是吸底的」

#### Scenario: 侧栏不再画这三样
- **WHEN** 渲染一局进行中的棋
- **THEN** 倒计时与三个按钮出现在**操作条里**、且**不**出现在侧栏里。**两半都要断言** ——
  只断言侧栏没有,那么一个把它们整个删掉的实现同样是绿的

#### Scenario: 每个按钮都够大
- **WHEN** 走查操作条里的全部 `button`
- **THEN** 至少有三个(**前置条件:样本非空**),且每一个都带 44 px 的最小高度

#### Scenario: 三座位不给认输,两座位给
- **WHEN** 分别在 `seatCount == 2` 与 `seatCount == 3` 的进行中对局里渲染操作条
- **THEN** 前者有认输按钮,后者没有;**两头都要断言** —— 只断言后者没有的话,一个把按钮
  整个删掉的实现同样是绿的。而离开与催促在两种情况下都在
