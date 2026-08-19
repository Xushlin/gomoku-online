# Tasks — generalize-match-contract

## 1. 契约

- [x] 1.1 `MoveDto.Stone` → `Seat`(`int`)
- [x] 1.2 `GameSnapshotDto.CurrentTurn` → `CurrentSeat`(`int`)
- [x] 1.3 五处调用点直接传座位号(`RoomMapping` ×2、回放、落子、超时)
- [x] 1.4 **`SeatWire` 删除**

## 2. 前端

- [x] 2.1 `MoveDto.seat` / `GameSnapshot.currentSeat`
- [x] 2.2 新建 `games/board-seats.ts` —— `seatStone` / `seatOfSide`,**显示层**的读法
- [x] 2.3 三个棋盘 + 房间页 + 侧栏的 `myTurn` 改比座位号
- [x] 2.4 棋盘按座位上色;侧栏与词链的模板改读座位
- [x] 2.5 回放页:`currentSeat` 在那里不参与任何判断(三个棋盘都显式传 `mySide='spectator'`)
- [x] 2.6 **删掉 hub 里那个两座位推算**,理由见 §5

## 3. smoke

- [x] 3.1 DTO 镜像改说座位
- [x] 3.2 新增一条**到达顺序**断言 —— `RoomState` 先于 `MoveMade`
- [x] 3.3 `currentSeat == 0` / `seat == 0` / `seat == 1` 三条替换掉原来的颜色断言

## 4. 验证

- [x] 4.1 `dotnet test Gewu.slnx` —— 1277 全绿
- [x] 4.2 `ng test --no-watch` —— **745** 全绿;`npm run lint` 通过
- [x] 4.3 `AiSmoke` 对真服务器 **36 条全绿**,退出码 0
- [x] 4.4 变异四次,见 §5
- [x] 4.5 `openspec validate --strict` 通过

## 5. 实现记录

### 契约对三座位房间给的是**错的答案**,不是不完整的答案

```
tick1  currentTurn=White   ply=1 stone=Black text=bid:0     ← 0 号座位
tick2  currentTurn=White   ply=2 stone=White text=bid:0     ← 1 号座位
tick3  result=Draw         ply=3 stone=White text=bid:0     ← 2 号座位,与 1 号同一个标签
```

真 HTTP、三个真账号、三次超时兜底。`SeatWire.ToStone(seat)` 是 `seat == 0 ? Black : White`,
于是**两个农民在走子记录里重合**,而 `currentTurn` 在两个不同玩家的回合都报 `White` ——
倒计时 UI 说不出在等谁。

`SeatWire` 自己的文档写了触发条件(「第一个 `SeatCount != 2` 的棋种落地那天」),这次是**它到期**,
不是有人临时起意重构。

### 删掉一个猜测,而不是把它一般化

Hub 的 `MoveMade` 处理器此前自己算下一手:`move.stone === 'Black' ? 'White' : 'Black'`。
三座位下它是错的,而客户端**也算不出来** —— 它不知道房间有几个座位(DTO 没有座位表,
`GET /api/games` 没有 `seatCount`)。

它不需要算:`MakeMoveCommandHandler` 先 `await RoomStateChangedAsync` 再 `await MoveMadeAsync`,
同一个 group、同一条连接,所以权威的 `currentSeat` 早就到了,`lastAppliedPly` 会让这个处理器直接返回。

**而"因为顺序如此所以可以删代码"这个论证必须自带那个顺序的证据。** 那条顺序在 spec 里躺了很久
(「Handler 按顺序调 `RoomStateChangedAsync`,然后 `MoveMadeAsync`」),但**从来没有人在线上量过它**,
也从来没有客户端依赖它。现在有了,所以 `AiSmoke` 记录两个事件的到达次序并断言
「第一个提到 ply 1 的帧是 `state:1`」。实测通过;把服务端那两个 `await` 调个顺序,它变红:

```
✗ the authoritative RoomState for a move arrives before its MoveMade (first was move:1)
=== SUMMARY: 35 passed, 1 failed ===   退出码 1
```

### 变异抓到一条**什么都没验**的覆盖

把 `seatStone` 改成永远返回 `'Black'` —— **744 条前端测试全绿**。也就是说这次改动的全部意义
(线上说座位、显示层读颜色)一条断言都没有。唯一沾到颜色的那条测试只查了 0 号座位,而它本来是在测越界。

补了一条:一手 `seat: 0` 与一手 `seat: 1` 必须画成一黑一白,且**各自不是对方**。再跑同一个变异:1 红。

另外三次变异都直接红,不需要补:`seatOfSide` 左右互换 → **19 红**;服务端广播顺序对调 → smoke 红;
`SeatWire` 删除本身由编译器强制(五个调用点)。

### 规格里那份抄本第四次过期,这次把它删了

`web-game-board` 的「RoomState 类型完整化」把 `MoveDto` / `GameSnapshot` / `GameEndedDto` 三个接口的
源码**整段抄在 requirement 里**,而它自己的正文写着:

> 一条把源码整段抄进来的 requirement,会在每一次那段源码变化时静静过期

这次它又过期了(第四次)。所以这次不是更新抄本,而是**换掉那条要求的形状**:点出哪些类型必须存在、
哪些**决定**必须成立(座位不是棋色、颜色住显示层、`GameResult` 不含颜色、`row`/`col` 可空),
逐字段的形状交给 TypeScript —— 那是编译器的活。

顺带修掉两处**先前就在漂**的规格文本,因为 MODIFIED 是整体替换,照抄就是把错的又签一次名:

- `IRoomNotifier.RoomStateChangedAsync(RoomId, RoomStateDto)` —— 自 `fix-spectator-chat-leak` 起它收**聚合**;
- `GameHubService` 的 API 清单缺 `sayWord` 与 `reconnect`;
- `EndReason` 的取值里写着 `Connected5`,而 `generalize-match-domain` 早改成了 `Decided`。

### 一个方法上的选择:提取而不是重打

四条 MODIFIED 要求正文很长,而 MODIFIED 是整体替换。**手打一遍长正文,就是给自己一次悄悄改掉
无关句子的机会**(`add-doudizhu` 归档时刚遇到过一次真的:旧正文差点盖掉新签名)。所以这次的 delta
是脚本**从 live spec 里提取要求正文、再打补丁**生成的:每一处补丁的锚点不存在就断言失败,
其余每一行按字节相同。

### 刻意没做的两件事

- **`RoomStateDto.Seats`。** 三座位房间里 2 号座位在任何字段里都不出现 —— 但这个变更**没有它的读者**:
  今天有 UI 的四个棋种都是两座位,`Black` / `White` 对它们仍然为真。加一个没人读的字段正是
  `add-match-setup` 刚踩过的坑(`Setup` 交付时没有任何读者,而"声明过的延后"与"疏漏"结果一样)。
  它属于 `add-doudizhu-visibility`,那里"谁的手牌"第一次需要说出座位。
- **`Black` / `White` 不删。** 它们是 `_seats` 的投影,不是第二个真源,所以不会漂;对三座位房间它们
  **不完整**,但不像 `stone` 那样**说错**。删它们要动 24 处前端,而那 24 处正是 `add-web-doudizhu`
  要改的地方。
