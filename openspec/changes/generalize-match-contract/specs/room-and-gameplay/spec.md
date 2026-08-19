# room-and-gameplay 的规格变化

## MODIFIED Requirements

### Requirement: `Move` 子实体记录每一步的上下文

`Move` MUST 包含:`Id: Guid`、`GameId: Guid`、`Ply: int (1-based)`、**`Seat: int`(座位号)**、`PlayedAt: DateTime`(UTC),外加**恰好一种载荷**(见下一条 Requirement)。数据库持久化:`(GameId, Ply)` 唯一。

出手方 MUST 记为座位号而 MUST NOT 记为 `Stone`。**持久化格式不变**:两座位棋种的历史数据里,原先的 `Black`/`White` 底层值就是 `1`/`2`,而座位号是 `0`/`1` —— 因此本次 MUST 附带一次值迁移,或以映射保持读写一致,二者选一并写明。MUST NOT 出现"看起来对、但历史局重放后出手方错位"的第三种做法。

线上 DTO SHALL 同样说座位:`MoveDto.seat: int`。**那笔带触发条件的债已经到期并还掉了。**

上一版这里写的是「`MoveDto.stone` 仍为 `'Black' | 'White'`,由 Api 边界从座位 0/1 映射……第一个 `SeatCount != 2` 的棋种落地那天,DTO 加座位字段、映射删除」。斗地主落地,而那个映射
(`SeatWire.ToStone(seat) = seat === 0 ? Black : White`)对三座位房间**给出错的答案**,不只是不完整:
实测三手 `bid:0` 的 `stone` 是 `Black / White / White` —— 两个农民在走子记录里重合。

`SeatWire` MUST 被删除,MUST NOT 以任何形式在契约边界上重建。棋色是**显示层**对座位的读法
(五子棋读 0 号为黑,象棋读 0 号为红),而显示层是它成立的唯一一层。

#### Scenario: Ply 从 1 起严格递增
- **WHEN** 在同一局依次走 3 步
- **THEN** 三个 `Move` 的 `Ply` 分别为 1、2、3

#### Scenario: 线上载荷说座位
- **WHEN** 任一路径产生 `MoveDto`(REST 快照、`MoveMade` 事件、回放)
- **THEN** 它 MUST 携带 `seat`,MUST NOT 携带棋色;三座位房间里三个座位 MUST 得到三个不同的值

#### Scenario: 历史对局重放后出手方不变
- **WHEN** 取一局改动前存下的两座位对局,按新代码重放
- **THEN** 每一步的出手方与改动前一致 —— 这条 MUST 有测试,因为"错位一位"在棋盘上表现为整局颜色反转,而在计分上表现为赢家错人

### Requirement: `GameSnapshotDto` 扩展 TurnStartedAt / TurnTimeoutSeconds / EndReason

`GameSnapshotDto.CurrentSeat` SHALL 是**座位号**(`int`)。它此前是 `Stone CurrentTurn`,经 `SeatWire` 换算 —— 而那让三座位房间在**两个不同玩家的回合**都报同一个 `White`(实测)。倒计时 UI 要显示"在等谁",
而一个分不出两个人的字段答不了这个问题。

`GameSnapshotDto` MUST 追加三个字段(纯追加,向后兼容):

- `DateTime TurnStartedAt` —— 当前回合起始时间,等价于 `Moves.OrderBy(Ply).LastOrDefault()?.PlayedAt ?? Game.StartedAt`
- `int TurnTimeoutSeconds` —— 由 `GameOptions.TurnTimeoutSeconds` 传入的阈值(不同房间相同,为前端倒计时 UI 提供)
- `GameEndReason? EndReason` —— 与 `Game.Result` 同时为 null 或同时非 null

`GameEndedDto` MUST 追加字段 `GameEndReason EndReason`(非 nullable,结束事件时必有)。

`RoomMapping.ToState` MUST 在入参里接受 `turnTimeoutSeconds` 参数,并计算 `TurnStartedAt`。

#### Scenario: 进行中 DTO
- **WHEN** 对 Playing 房间构造 `GameSnapshotDto`
- **THEN** `TurnStartedAt` 是最后一步 `PlayedAt`(或 `StartedAt` 如无 Moves);`TurnTimeoutSeconds > 0`;`EndReason == null`

#### Scenario: 结束 DTO
- **WHEN** 对 Finished 房间构造 `GameSnapshotDto`
- **THEN** `EndReason` 取对应值(`Decided` / `Resigned` / `TurnTimeout`)—— `Connected5` 早在 `generalize-match-domain` 改名为 `Decided`,本条正文此前一直没跟上

#### Scenario: GameEndedDto 总含 EndReason
- **WHEN** 任一路径触发 `GameEndedAsync` 广播
- **THEN** payload `GameEndedDto.EndReason` 非 null 且匹配实际原因

### Requirement: SignalR 服务端事件由 `IRoomNotifier` 抽象触发

Application 层 SHALL 定义 `IRoomNotifier` 契约,至少含:

- `RoomStateChangedAsync(Room, IReadOnlyDictionary<Guid, string>, int)` —— 收**聚合**而不是 DTO,自己投影「非围观者」与「围观者」两份视图(见 `fix-spectator-chat-leak`)。本条此前一直写着 `(RoomId, RoomStateDto)`
- `PlayerJoinedAsync(RoomId, UserSummaryDto)` / `PlayerLeftAsync(RoomId, UserSummaryDto)`
- `SpectatorJoinedAsync(RoomId, UserSummaryDto)` / `SpectatorLeftAsync(RoomId, UserSummaryDto)`
- `MoveMadeAsync(RoomId, MoveDto)`
- `GameEndedAsync(RoomId, GameEndedDto)`
- `ChatMessagePostedAsync(RoomId, ChatChannel, ChatMessageDto)`
- `OpponentUrgedAsync(RoomId, UserId urgedUser, UrgeDto payload)`

Handler MUST 在 `SaveChangesAsync` **之后** 调用 `IRoomNotifier`,且 MUST NOT 在事务内调用(避免"事件发了但事务回滚"的不一致)。Api 层实现 `SignalRRoomNotifier : IRoomNotifier`,用 `IHubContext<MatchHub>` 把事件发到对应 SignalR group。

**这个顺序现在有客户端依赖它,所以它 MUST 在线上被量到,而不只是在 handler 里被读到。**
Web 客户端的 `MoveMade` 处理器**不再自己推算下一手是谁** —— 它此前算的是
`move.stone === 'Black' ? 'White' : 'Black'`,一个两座位假设。删掉那个推算的理由正是这条顺序:
权威的 `currentSeat` 先到。**一个"因为顺序如此所以可以删代码"的论证必须自带那个顺序的证据。**

#### Scenario: 落子成功后的事件顺序
- **WHEN** `MakeMoveCommand` 成功持久化
- **THEN** Handler 按顺序调 `RoomStateChangedAsync`,然后 `MoveMadeAsync`;若对局结束,再调 `GameEndedAsync`

#### Scenario: 到达顺序在真连接上被量到
- **WHEN** 一个真 SignalR 客户端同时订阅 `RoomState` 与 `MoveMade`,然后走一步棋
- **THEN** 第一个提到该 `ply` 的帧 MUST 是 `RoomState`;这条 MUST 由 `AiSmoke` 在 CI 里跑

#### Scenario: 事务失败时不发事件
- **WHEN** `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`
- **THEN** Handler MUST NOT 调 `IRoomNotifier` 的任何方法

