# add-doudizhu-visibility

## Why

斗地主的手牌**只有一个座位能看**。今天房间快照对所有玩家是同一份,所以:

- 一个客户端**看不到自己的牌**(`Game.Setup` 是服务端侧的,不上任何 DTO —— 那是刻意的,
  `GameSetupStaysServerSideTests` 用反射钉着它);
- 三座位房间里 **2 号座位在 `RoomStateDto` 的任何字段里都不出现**(只有 `Black` / `White`);
- 底牌在定下地主之后**是公开的**,而它今天藏在 `Setup` 里,谁都看不到。

也就是说:斗地主的规则、传输、超时兜底都通了(`add-doudizhu` + `generalize-match-contract`),
而**它仍然是不可玩的** —— 不是因为没有 UI,是因为没有一条路能把"你手上这 17 张"送到你面前。

`RoomView` 今天只有一个维度(「围观频道给不给」)。手牌需要第二个维度:**这份快照是给哪个座位的**。

## What Changes

**内核侧(与棋种无关):**

- `RoomView` 从 `(IncludeSpectatorChat)` 变成 `(IncludeSpectatorChat, Seat, SeatView)`:
  这份快照给谁看、他坐哪、以及**他能看到的那一份私有切片**。
  `RoomView.For(room, viewer, rules)` 一次算出三者。
- `RoomStateDto.Seats: IReadOnlyList<RoomSeatDto>` —— 座位号 + 座位上的人。
  **这个字段现在有读者了**:客户端要画三个人的牌背,就得知道谁坐哪。
  `generalize-match-contract` 刻意没加它,理由是那时没有读者。
- `GameSnapshotDto.SeatView: string?` —— **对内核完全不透明**的、按座位裁剪过的 JSON。
- 广播从「两个群各一份」变成「**每个座位一份 + 观察者一份 + 围观者一份**」。
  今天 `non-spectators` 群里既有坐着的人也有没坐的人,而他们现在要收到**不同的**载荷。

**棋种侧:**

- `IPerSeatViewRules.ViewFor(MatchState state, int? seat)` —— 只有需要隐藏信息的棋种实现它。
  五子棋 / 一字棋 / 中国象棋 / 成语接龙**一行不动**,理由与 `IDealtGameRules` 分出来时相同:
  留在基接口上,四个棋种就得各写一个骗人的实现,而**骗人的实现是下一个人删不掉的东西**。
- `DoudizhuRules` 实现它:自己那 17(或 20)张、另两家的**张数**、桌面上的一手、地主是谁、
  底分、定完地主之后的底牌、连续过牌数。

## 三个决定,各自的理由

**一、`SeatView` 是不透明 JSON,而这不是"又想加个 JSON 载荷"。**
`generalize-match-payload` 拒绝过 JSON 走子列,理由是「为一个不存在的需求付钱」。这里的需求存在
且形状不同:**内核 MUST NOT 知道什么是牌**,而每个棋种要送的东西天生不一样。这个仓库已经有先例
—— 闯关那条线的 `LayoutJson` / `SolutionJson` 就是不透明的、按棋种解析的 JSON,由客户端自己解。

**二、按座位分群,而不是 `Clients.User(...)`。**
`Clients.User` 会打到那个用户的**全部连接**,包括他在另一个房间的那个标签页 —— 一个催促弹错标签
无所谓,一份房间快照盖掉另一个房间的状态不行。所以每个座位一个群:
`room:{id}:seat:{n}`。

**三、`non-spectators` 改名 `observers`,而这是必须的、不是整理。**
`fix-spectator-chat-leak` 立下的规矩是分群 MUST **互斥且穷尽**。座位群一旦出现,坐着的人就不能
再留在 `non-spectators` 里 —— 否则他会收到两份(一份带手牌、一份不带),而**看到哪一份由到达顺序
决定**。改名把"在房间里、没坐座位、也没围观"这件事说出来,三类连接各进恰好一个群。

**四、所有棋种都走同一条路。** 没有隐藏信息的棋种,每个座位的投影是**同一份内容**,所以两座位
棋种从两次发送变成四次(两个座位 + 观察者 + 围观者)。**没有为它开一条"没有私有状态就走老路"的
分支**:那会是两条代码路径,而这个仓库反复记下的教训是——每多一条路,就多给每个 handler 一次
忘记裁剪的机会。代价是同一份 payload 多发两次,而那是进程内的扇出。

## Impact

- Affected specs: `room-and-gameplay`、`in-room-chat`(围观频道那条读取侧要求)、
  `game-rules-registry`、`doudizhu`
- Affected code: `RoomView`、`RoomMapping`、`RoomDtos`、`SignalRRoomNotifier`、`MatchHub`、
  `GetRoomRoleQuery`(要返回座位号,不只是"是玩家")、`IGameRules` 抽象、`DoudizhuRules`
- 前端:**零改动**(新字段无人读,直到 `add-web-doudizhu`)。这一次那不是缺陷:
  `Seats` 与 `SeatView` 的读者在**服务端就有断言**,而 UI 是下一个变更。
- 无迁移 —— 手牌一直在 `Game.Setup` 里,这次只是让**该看到的人**看得到。
