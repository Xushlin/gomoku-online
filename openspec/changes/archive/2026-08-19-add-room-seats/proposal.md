## Why

`generalize-match-seats` 让内核说座位号,但**房间只有两个座位字段** —— `BlackPlayerId` 非空、`WhitePlayerId` 可空。上一个变更留下的那条断言就钉着这件事:三座位规则下 `CurrentTurn` 会走到 `2`,而 2 号座位没人坐。

触发条件已到:斗地主 `SeatCount == 3`。

## What Changes

**`RoomSeat(RoomId, Index, UserId)`,一张 `RoomSeats` 表,主键 `(RoomId, Index)`。** `Room.Seats` 按 `Index` 排序,配 `SeatOf(userId)` 与 `PlayerAt(index)`。

### `JoinAsPlayer` 收 `IGameRules`,而不是把座位数存进房间

房间要知道"满没满"才能开局。两条路:

- 在 `Room` 上存一个 `SeatCount`(建房时从规则抄过来);
- 让 `JoinAsPlayer(userId, now, rules)` 收规则 —— 与 `PlayMove(userId, intent, now, rules)` 完全一致。

**选后者。** 存下来就是规则事实的第二份副本,而它错了的表现是"房间永远开不了局"或者"少一个人就开局了" —— 两者都不会有人立刻发现。而 `PlayMove` 早就是这个形状,所以这不是新惯例,是把同一个惯例用在同一个地方。

由此多出一个状态:座位没坐满时 `JoinAsPlayer` **留在 `Waiting`**。两人棋种下这与今天逐步等价(第二个人一坐满就开局)。

### `BlackPlayerId` / `WhitePlayerId` 变成**派生**读法,不再是字段

87 处引用里绝大多数是内存里读一下"黑方是谁"。把它们改成 `Seats[0]` / `Seats[1]` 的派生属性,这些调用点一行不用动 —— 而这**不是镜像**:镜像是两份可以各自漂移的存储,派生只有一份。

这与 `Stone` 的处理是同一条:名字留着,含义降到它真正成立的那一层。**两人棋种的"黑方"就是 0 号座位**;牌类棋种 MUST NOT 用这两个名字,用 `PlayerAt`。

三处 LINQ-to-SQL 必须真改(派生属性翻不成 SQL),而其中一处**改完更简单了**:`UserRepository` 找"当前回合是 bot 的房间",现在两次 JOIN `Users`、两个分支各写一遍 —— 换成 `RoomSeats` 之后是一次 JOIN + `s.Index == g.CurrentTurn`。那两个分支正是三座位下要加第三个的形状。

### 迁移:expand → contract,两个

`add-per-game-rating` 立下的形状:

1. **`AddRoomSeats`** —— 建表 + 从两列回填,**列一个不碰**。纯新增,树保持绿。
2. **`DropRoomSeatColumns`** —— 读者搬完之后再落。

`Down` 手写。EF 生成的版本会把两列以 `defaultValue` 加回来,那正是 `AddRoomGameKey` 的 `defaultValue: ""` 与 `DropUserRatingColumns` 的 `defaultValue: 0` 同一个错:**房间的黑方会变成空 GUID**。带回滚测试。

顺序由迁移时间戳保证 —— **未来压缩迁移不得颠倒这两个**。

## 线上格式仍不变

`black` / `white` 仍从座位 0/1 投影。第三个座位在 DTO 里还看不见,而现在没有任何三座位棋种注册,所以这不是能被观察到的缺失。

**触发条件:`add-doudizhu`。** 那个变更要在 DTO 上加座位数组,并且要处理"围观者与每个座位看到的东西不同"——那是另一件事,不塞进这里。

## 被否掉的

- **再加一个可空的第三列**:写死的上限披着通用化的外衣,四人局又要加一列。
- **座位存成 JSON**:查不动。`generalize-match-payload` 为同一条理由拒过一次 —— 三处 LINQ 都要按"这人是不是这房间的玩家"过滤。
- **在 `Room` 上存 `SeatCount`**:见上。

## Impact

- `Gewu.Domain/Rooms/`(`Room` + 新 `RoomSeat`)、3 处 LINQ、`RoomConfiguration`、两个迁移。
- **前端零改动,线上格式零改动。**
- 受影响 spec:`room-and-gameplay`。
