# room-and-gameplay Specification Delta

## RENAMED Requirements

标题里的「第二位玩家」现在不对了 —— 三座位棋种下开局的是第三位。archive 的应用顺序是
RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: `Room.JoinAsPlayer` 让第二位玩家加入并启动对局
- TO: ### Requirement: `Room.JoinAsPlayer` 让玩家入座,坐满才开局

## MODIFIED Requirements

### Requirement: `Room` 聚合根承载玩家、围观者、对局、状态与元数据

系统 SHALL 定义 `Room` 作为聚合根,字段包含:

- `Id: RoomId`
- `Name: string`(3–50 字符,非空白)
- `HostUserId: UserId`(创建者)
- **`Seats: IReadOnlyList<RoomSeat>`** —— 座位集合,按 `Index` 升序。`RoomSeat` 是 `(RoomId, Index, UserId)`,主键就是前两者。**空座位 MUST NOT 存行。**
- `Status: RoomStatus`(`Waiting` / `Playing` / `Finished`)
- `CreatedAt: DateTime`(UTC)
- `LastUrgeAt: DateTime?` / `LastUrgeByUserId: UserId?`
- `Game: Game?`(子实体;`Status == Waiting` 时为 `null`,`Playing`/`Finished` 时存在)
- `Spectators: IReadOnlyCollection<UserId>`(只读;内部私有集合)
- `ChatMessages: IReadOnlyCollection<ChatMessage>`(只读)

`BlackPlayerId` / `WhitePlayerId` **仍然存在,但已是派生读法**:分别是 0 号与 1 号座位上的玩家(1 号空时为 `null`)。它们 MUST NOT 是存储 —— 座位只有一份,存在 `Seats` 里。**这不是镜像**:镜像是两份能各自漂移的存储,派生只有一份。

保留这两个名字的理由与 `Stone` 下沉时相同:**两人棋种的"黑方"就是 0 号座位**,那句话仍然成立,而 87 处调用点读的正是这句话。**座位数不等于 2 的棋种 MUST NOT 使用这两个名字**,用 `PlayerAt(index)`。

系统 SHALL 另外提供:

- `SeatOf(UserId) → int?` —— 这人坐几号;不是玩家则 `null`。落子、催促、出牌三条路径 MUST 共用它,MUST NOT 各写一遍座位判定:座位变多之后,漏一个座位的表现是"某个座位的人被当成不是玩家",而漏的概率随座位数涨。
- `PlayerAt(int) → UserId?` —— 几号座位上是谁。

所有字段外部 MUST NOT 直接修改;变更仅通过领域方法。

#### Scenario: 字段可读
- **WHEN** 访问 `Room` 的任意上述属性
- **THEN** 返回相应类型的当前值

#### Scenario: 座位按号升序
- **WHEN** 读 `Room.Seats`
- **THEN** `Index` 升序 —— EF 物化不保证顺序,而轮转与 `PlayerAt` 都按座位号说话

#### Scenario: 两人棋种的派生读法与此前等价
- **WHEN** 一个两人棋种的房间坐满
- **THEN** `BlackPlayerId == PlayerAt(0)`;`WhitePlayerId == PlayerAt(1)`

#### Scenario: `Spectators` 与 `ChatMessages` 只读
- **WHEN** 外部把 `Room.Spectators` / `Room.ChatMessages` 强转为可变集合并 `Add`
- **THEN** 该修改 MUST NOT 影响 `Room` 内部状态

#### Scenario: 每一条取房间的路径都带回座位
- **WHEN** 仓库的任一读取路径返回 `Room`
- **THEN** 该 `Room` 的 `Seats` 非空

  座位是聚合的一部分,不是可选的附加数据。漏一处 `Include` 的表现不是"少个字段",而是那条路径整个抛 —— `BlackPlayerId` 会在空集合上 `Single()`。这条 MUST 有测试:**变异验证发现,五处 `Include` 全删掉之后整个 Infrastructure 套件仍然是绿的**,也就是说当时没有任何测试加载过一个房间再读它的座位。

---

### Requirement: `Room.JoinAsPlayer` 让玩家入座,坐满才开局

系统 SHALL 提供 `Room.JoinAsPlayer(UserId userId, DateTime now, IGameRules rules)`。调用后:

- 若 `Status != Waiting`:MUST 抛 `RoomNotWaitingException`
- 若 `SeatOf(userId) != null`(已经坐着,含创建者):MUST 抛 `AlreadyInRoomException`
- 若 `userId ∈ Spectators`:MUST 先从围观者集合移除,再入座
- 若 `Seats.Count >= rules.SeatCount`:MUST 抛 `RoomFullException`
- 否则:在**下一个空座位号**入座;**当且仅当**坐满(`Seats.Count == rules.SeatCount`)时 `Status = Playing` 且 `Game = new Game(currentTurn: 0, startedAt: now)`

**座位数由 `rules` 给,MUST NOT 存在 `Room` 上。** 存一份就是规则事实的第二份副本,而它错了的表现是"房间永远开不了局"或"少一个人就开局了" —— 两者都不会有人立刻发现。`PlayMove` 早就是收规则的形状,这里只是把同一个惯例用在同一个地方。

#### Scenario: 两人棋种第二位玩家加入即开局
- **WHEN** 房间处于 `Waiting`,`SeatCount == 2`,调用 `JoinAsPlayer(bobId, now, rules)`
- **THEN** `PlayerAt(1) == bobId`;`Status == Playing`;`Game != null` 且 `Game.CurrentTurn == 0`;`Game.StartedAt == now`

#### Scenario: 三人棋种坐第二个人时仍然等待
- **WHEN** `SeatCount == 3`,房间里已有 host,第二个人 `JoinAsPlayer`
- **THEN** `Seats.Count == 2`;**`Status` 仍为 `Waiting`**;`Game == null`

#### Scenario: 三人棋种坐满第三个人才开局
- **WHEN** 承上,第三个人 `JoinAsPlayer`
- **THEN** `Seats` 的 `Index` 为 `0, 1, 2`;`Status == Playing`;`Game.CurrentTurn == 0`

#### Scenario: 非等待状态
- **WHEN** `Status` 为 `Playing` 或 `Finished`,调用 `JoinAsPlayer`
- **THEN** 抛 `RoomNotWaitingException`

#### Scenario: 已经坐着的人重复加入
- **WHEN** 任一已入座的用户(含创建者)再调 `JoinAsPlayer`
- **THEN** 抛 `AlreadyInRoomException`,消息点明他已坐在几号

#### Scenario: 围观者升级为玩家
- **WHEN** 用户先进入围观者集合,随后调 `JoinAsPlayer`
- **THEN** 该用户从 `Spectators` 移除,在下一个空座位入座

#### Scenario: 座位坐满后再有人要坐
- **WHEN** 座位已满
- **THEN** 抛 `RoomNotWaitingException` 或 `RoomFullException` —— 坐满即开局,所以先撞上状态检查;两者都表示"坐不进去"

## ADDED Requirements

### Requirement: `AddRoomSeats` 迁移把两列搬进座位表,两个方向都手写

`AddRoomSeats` SHALL 建 `RoomSeats` 表,**先建表、再回填、最后才删两列** —— 这个顺序是必须的。

EF 生成的版本 MUST NOT 直接采用,它有两处错而两处都不报错:

1. 它把两列**先删再建表**,回填无从下手,存量房间的座位全丢。EF 自己提示了 "may result in the loss of data",而生成的代码对此什么都没做。
2. 它的 `Down` 用 `defaultValue: Guid.Empty` 把 `BlackPlayerId` 加回来 —— 每个房间的黑方变成空 GUID。同 `AddRoomGameKey` 的 `defaultValue: ""` 与 `DropUserRatingColumns` 的 `defaultValue: 0`,一模一样的形状。

回填 MUST 只为非空的 `WhitePlayerId` 建 1 号座位:空座位不存行,而 `UserId` 列非空。

`Down` MUST 把数据从座位表搬回两列,然后才删表。若某房间没有 0 号座位,该 `UPDATE` 会写 `NULL` 进非空列而失败 —— **那是想要的**:大声坏掉,而不是留下一个空 GUID 的黑方。

#### Scenario: 两个玩家变成 0 号与 1 号座位
- **WHEN** 存量房间有 `BlackPlayerId` 与 `WhitePlayerId`,跑迁移
- **THEN** `RoomSeats` 有该房间的两行,`Index` 为 0 与 1,`UserId` 与原值一致

#### Scenario: 等人的房间只有 0 号座位
- **WHEN** 存量房间 `WhitePlayerId IS NULL`,跑迁移
- **THEN** 只有一行 `Index = 0`;MUST NOT 出现 `UserId` 为 NULL 的行

#### Scenario: 回滚把玩家搬回去而不是留下空 GUID
- **WHEN** 迁移后回滚
- **THEN** `BlackPlayerId` / `WhitePlayerId` 与迁移前逐项相同
