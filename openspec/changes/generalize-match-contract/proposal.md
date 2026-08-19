# generalize-match-contract

## Why

线上契约里"这一步是谁走的"和"该谁走"是 `Stone`(`'Black' | 'White'`),而 `SeatWire.ToStone(seat)`
是 `seat == 0 ? Black : White` —— **2 号座位被说成 1 号**。

**实测,不是推断。** 三个真账号、一个真 `doudizhu` 房间、三次超时兜底(`bid:0`),真 HTTP:

```
tick1  currentTurn=White   ply=1 stone=Black text=bid:0     ← 0 号座位
tick2  currentTurn=White   ply=2 stone=White text=bid:0     ← 1 号座位
tick3  result=Draw         ply=3 stone=White text=bid:0     ← 2 号座位,与 1 号同一个标签
```

`currentTurn` 在**两个不同玩家的回合**都报 `White`;三手棋的 `stone` 是 `Black / White / White`。
客户端因此**分不出三个人**:走子记录里两个农民重合,倒计时不知道在等谁。这不是"少一个字段",
是这份契约对三座位房间**给出了错的答案**。

`SeatWire` 自己的文档写了触发条件:「第一个 `SeatCount != 2` 的棋种落地那天,DTO 加座位字段,
本类删除」。它落地了。

## What Changes

- `MoveDto.Stone`(`Stone`)→ `MoveDto.Seat`(`int`)。`MoveMade` 事件用的就是这个 DTO,所以
  实时通道跟着变。
- `GameSnapshotDto.CurrentTurn`(`Stone`)→ `CurrentSeat`(`int`)。
- `GameReplayDto` 的走子列表同样(它用的也是 `MoveDto`)。
- **`SeatWire` 删除。**
- 前端 16 处读 `stone` / `currentTurn` 的地方改读座位,并在**显示层**把座位映射成颜色 ——
  五子棋 `0 → 黑`,象棋 `0 → 红`。那个映射本来就该在显示层:`add-xiangqi` 定下的规矩是
  「`Stone` 一直是"先手 / 后手",红黑是显示层的读法」,而 `SeatWire` 把它写进了**契约**。

不含 —— 而且是**刻意**不含:

- `RoomStateDto.Seats`。三座位房间里 2 号座位在任何字段里都不出现,但这个变更**没有它的读者**:
  今天有 UI 的四个棋种都是两个座位,`Black` / `White` 对它们仍然为真。加一个没人读的字段正是
  `add-match-setup` 刚踩过的坑(`Setup` 交付时没有任何读者,而"声明过的延后"与"疏漏"结果一样)。
  它属于 `add-doudizhu-visibility` —— 那里"谁的手牌"第一次需要说出座位。
- `RoomStateDto.Black` / `White`。它们是 `_seats` 的**投影**,不是第二个真源,所以不会漂;
  对三座位房间它们**不完整**,但不像 `stone` 那样**说错**。删它们要动 24 处前端,而那 24 处
  正是 `add-web-doudizhu` 要改的地方。

## Impact

- Affected specs: `room-and-gameplay`(`Move` 的 DTO、`GameSnapshotDto`)、`game-replay`、
  `web-game-board`
- Affected code: `RoomDtos.cs`、`RoomMapping.cs`、`GetGameReplayQueryHandler`、
  `MakeMoveCommandHandler`、`TurnTimeoutCommandHandler`、删 `SeatWire.cs`;
  前端 `room-state` 类型、棋盘组件、房间页、回放页
- **破坏性**:线上字段改名。没有已部署的客户端,前端与后端在同一个提交里改。
- 无迁移 —— `Moves.Seat` 列自 `RenameMoveStoneToSeat` 起就已经是座位号,这个变更只是让**线上**
  也这么说。
