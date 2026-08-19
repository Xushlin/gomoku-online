# fix-three-seat-membership

## Why

「这个人是不是本房间的玩家」这个判定,在聚合与 Application 层一共有**七份手写副本**,每一份都写成
`BlackPlayerId || WhitePlayerId` —— 只认 0 号与 1 号座位。斗地主(三个座位)落地之后它们全错。

**实测,不是推断。** 三个真账号、一个真 `doudizhu` 房间、真 HTTP:

| 请求(以 2 号座位的身份) | 结果 | 该是什么 |
| --- | --- | --- |
| `POST /api/rooms/{id}/leave` | **404** | 204 —— 他离不开自己在的房间 |
| `POST /api/rooms/{id}/spectate` | **204** | 拒绝 —— 一个占着座位的玩家成了围观者 |

第二条是要紧的那条:围观之后他 `IsSpectator == true`,于是 `RoomView.For` 给他**围观视角**,
`ToState` 把围观频道全部内容发给他,而他同时坐在牌桌上。那正是 `fix-spectator-chat-leak`
建起来的不变量。同一次修复的结论还写着「写入侧一直是强制的」—— 对两座位成立,对三座位不成立:
`PostChatMessage` 里的 `isPlayer` 同样只认 0/1,所以 **2 号座位发得进围观频道**。

再加两处不出声的:`RoomMapping.CollectUserIds` 不收 2 号座位的 id(那个人的用户名会查不到,
显示为 `<unknown>`),`LeaveRoomCommandHandler` 的 `wasPlayer` / `wasSpectator` 对他**双双为 false**,
于是他离开时**两个事件一个都不发**,房间里没人知道第三个人走了。

**1266 条既有测试一条都没红**,因为三座位的这几条路此前没有任何测试走过。

`SeatOf` 的文档说它存在正是因为"三处需要'这人是第几号'的地方各写了一遍同样的 if/else"。
**收敬只做了一半**:"他是几号"进了一处,"他是不是玩家"仍散在七处 —— 而那是同一个事实的两种问法。

## What Changes

- `Room.IsPlayer(userId)` 改为 `SeatOf(userId) is not null` —— 覆盖全部座位。
- `Room.Leave`、`Room.JoinAsSpectator`、`Room.PostChatMessage`、`LeaveRoomCommandHandler` 四处
  各自的手写副本改为调 `IsPlayer`。
- `RoomMapping.CollectUserIds` 遍历 `Room.Seats`。
- `Room.UrgeOpponent` 的被催方改为 `PlayerAt(Game.CurrentTurn)` —— **两座位下零行为改动**
  (既有守卫已保证发起人不是当前回合,所以"该走棋的人"就是对手);三座位下原式永远催 0 号,
  且 2 号座位永远催不到。
- 顺手删掉三处 `var state = room.ToState(...)` 死代码:`fix-spectator-chat-leak` 之后
  `RoomStateChangedAsync` 收聚合、不收 DTO,这三行算完就扔。它们没有编译警告(CS0219 只管常量),
  而**一段看起来像安全判定、实际没有读者的代码,下一个人会当成承重墙**。

不含:DTO 的形状。三座位房间里 2 号座位在 `RoomStateDto` 的**任何字段里都不出现**,而
`SeatWire.ToStone(2) == White` 让他的回合与走子与 1 号座位无法区分 —— 那是
`generalize-match-contract` 的活,它自己在 `RoomStateDto` 的文档注释里已经被点名。

## Impact

- Affected specs: `room-and-gameplay`(`Room.Leave` / `JoinAsSpectator`)、`in-room-chat`
  (`PostChatMessage` / `UrgeOpponent`)
- Affected code: `Room.cs`、`RoomMapping.cs`、`LeaveRoomCommandHandler.cs`、
  两个 spectator handler(仅删死代码)
- 无迁移;前端零改动;两座位棋种零行为改动
