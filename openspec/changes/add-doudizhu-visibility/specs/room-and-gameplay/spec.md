# room-and-gameplay 的规格变化

## MODIFIED Requirements

### Requirement: `GameSnapshotDto` 扩展 TurnStartedAt / TurnTimeoutSeconds / EndReason

`GameSnapshotDto.CurrentSeat` SHALL 是**座位号**(`int`)。它此前是 `Stone CurrentTurn`,经 `SeatWire` 换算 —— 而那让三座位房间在**两个不同玩家的回合**都报同一个 `White`(实测)。倒计时 UI 要显示"在等谁",
而一个分不出两个人的字段答不了这个问题。

`GameSnapshotDto.SeatView`(`string?`)SHALL 携带**这个看客能看到的那一份棋种私有状态**,由规则序列化(`IPerSeatViewRules`),对本层完全不透明。棋种没有隐藏信息、或对局尚未开始时 MUST 是 `null`。

**同一局的不同座位拿到的是不同的字符串**,所以广播 MUST 按座位分别投影 —— 见 `IRoomNotifier` 那条要求。

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

### Requirement: 房间 DTO 携带棋种键

`RoomStateDto` 与 `RoomSummaryDto` SHALL 各带一个非空的 `GameKey` 字段,取自 `Room.GameKey`。

这不是装饰性字段,而是客户端**画不出棋盘就得靠它**:玩家进入 `/rooms/{id}` 有四条路 ——
从建房页跳转、刷新页面、点收藏链接、从"我的对局"进入 —— 只有第一条路上客户端知道棋种
(是它自己刚选的)。另外三条它手上只有一个房间 id,而没有本字段时 DTO 里没有任何东西能
区分 3×3 与 15×15。所以"棋种从路由参数带过来"这条捷径只在四条路里的一条上成立。

映射 MUST NOT 因此获得新依赖:`Room.GameKey` 已经存在且已填好,`ToState` / `ToSummary`
就是把它映出来。

本变更 MUST NOT 在 DTO 里下发盘面尺寸(`Rows` / `Cols`)—— 那需要把 `IGameRulesRegistry`
穿过九处 `ToState` / `ToSummary` 调用点。见 `add-web-tictactoe-ai` design D1:客户端从自己的
游戏注册表解析尺寸,该重复此刻比它的替代方案便宜,且 `generalize-match-contract` 反正要
重写这两个 DTO,届时再改为服务端下发。

`RoomStateDto.Seats`(`IReadOnlyList<RoomSeatDto>`,每项是座位号 + 座位上的人)SHALL 列出**全部**座位。

`Black` / `White` 保留,它们是 0 号与 1 号的**派生读法**,不是第二个真源,所以不会漂;但它们对三座位房间**不完整** —— 实测:三座位房间里 2 号座位上的人在任何字段里都不出现。`Seats` 是它的修法。

**这个字段在 `generalize-match-contract` 里被刻意推迟过**,理由是那时没有读者,而「交付一个没人读的字段」正是 `add-match-setup` 刚踩过的坑。现在读者有了:客户端要画三家的牌,就得知道谁坐哪。

#### Scenario: 五子棋房间
- **WHEN** 读取任意 `gomoku` 房间的状态或摘要
- **THEN** `GameKey == "gomoku"`

#### Scenario: 一字棋房间
- **WHEN** 读取任意 `tictactoe` 房间的状态或摘要
- **THEN** `GameKey == "tictactoe"`

#### Scenario: 只增字段,不改既有字段
- **WHEN** 比对本变更前后的 DTO
- **THEN** 既有字段的名称、类型、顺序语义 MUST NOT 改变 —— 已发布客户端反序列化行为不变

### Requirement: SignalR 服务端事件由 `IRoomNotifier` 抽象触发

Application 层 SHALL 定义 `IRoomNotifier` 契约,至少含:

- `RoomStateChangedAsync(Room, IReadOnlyDictionary<Guid, string>, int)` —— 收**聚合**而不是 DTO,自己**逐份**投影:**每个座位一份**,外加观察者一份、围观者一份。座位那几份各含该座位的私有状态(`GameSnapshotDto.SeatView`)。

  投影次数从 2 变成 `SeatCount + 2`,而**没有为「没有隐藏信息的棋种」开一条快路**:那会是两条代码路径,而这整套 `RoomView` 机制存在的全部理由就是不给任何 handler 一次忘记裁剪的机会。代价是同一份 payload 多发几次,进程内扇出。
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

#### Scenario: 每个座位收到自己的那一份
- **WHEN** 一个有隐藏信息的棋种触发 `RoomState` 广播
- **THEN** 每个座位群各收到一份、且**内容互不相同**;观察者群与围观者群各收到一份、都不含任何座位的私有状态

#### Scenario: 落子成功后的事件顺序
- **WHEN** `MakeMoveCommand` 成功持久化
- **THEN** Handler 按顺序调 `RoomStateChangedAsync`,然后 `MoveMadeAsync`;若对局结束,再调 `GameEndedAsync`

#### Scenario: 到达顺序在真连接上被量到
- **WHEN** 一个真 SignalR 客户端同时订阅 `RoomState` 与 `MoveMade`,然后走一步棋
- **THEN** 第一个提到该 `ply` 的帧 MUST 是 `RoomState`;这条 MUST 由 `AiSmoke` 在 CI 里跑

#### Scenario: 事务失败时不发事件
- **WHEN** `SaveChangesAsync` 抛 `DbUpdateConcurrencyException`
- **THEN** Handler MUST NOT 调 `IRoomNotifier` 的任何方法

### Requirement: SignalR Hub `MatchHub` 路由实时操作,但不写入业务逻辑

系统 SHALL 在 `/hubs/match` 暴露 `MatchHub`(`[Authorize]`)。Hub 客户端方法:

- `JoinRoom(Guid roomId)` —— 把当前 connection 加入 SignalR group `room:{roomId}`,并按**聚合里的身份**额外加入恰好一个子群:围观者进 `room:{roomId}:spectators`,其余(玩家,以及进了房但还没围观的连接)进 `room:{roomId}:non-spectators`。身份由 `GetRoomRoleQuery` 从聚合解析,MUST NOT 采信客户端自报。不会修改 `Room` 聚合。

  两个子群 MUST **互斥且穷尽**。互斥不成立会让某个连接收到两份 `RoomState`、由到达顺序决定它看到什么;不穷尽会让某个连接一份都收不到。子群名叫「非围观者」而不是「玩家」正是为了穷尽 —— 按「玩家」分会把还没围观的旁观连接漏在缝里。

  **此前这里的实现与本条不符**:`JoinRoom` 只加 `room:{roomId}`,而围观子群靠客户端自己调 `JoinSpectatorGroup`,那个方法**不做任何校验**。于是这局的玩家调一次就进了围观子群,实时收到全部围观频道消息。实测过。
- `LeaveRoom(Guid roomId)` —— 从上述 group 中移除。不会修改聚合。
- `MakeMove(Guid roomId, int row, int col)` —— **落子类**棋种(五子棋 / 一字棋)。派 `MakeMoveCommand`。
- `MovePiece(Guid roomId, int fromRow, int fromCol, int row, int col)` —— **走子类**棋种(中国象棋)。
- `SayWord(Guid roomId, string word)` —— **文本类**棋种(成语接龙)。
- `SendChat(Guid roomId, string content, ChatChannel channel)` —— 派 `SendChatMessageCommand`(规则见 `in-room-chat` spec)。
- `JoinSpectatorGroup(Guid roomId)` —— 幂等地把连接放进围观子群,**身份由服务端查聚合确认**。对非围观者 MUST 静默无操作:「我还不是围观者」不是错误,把它变成异常只会让客户端多一条要处理的分支。它保留的用途是重连,以及 `JoinRoom` 之后才 `POST /spectate` 的顺序。
- `Urge(Guid roomId)` —— 派 `UrgeOpponentCommand`。

三条走子入口 MUST 是**三个方法**,MUST NOT 合并为一个带可选参数的方法。**SignalR 不套用 C# 的可选参数默认值**,参数个数是**双向精确匹配**:

| 调用 | 目标 | 服务端回 |
| --- | --- | --- |
| `SayWord` 1 个参数 | 2 参 | `InvalidDataException: Invocation provides 1 argument(s) but target expects 2.` |
| `SayWord` 3 个参数 | 2 参 | `InvalidDataException: Invocation provides 3 argument(s) but target expects 2.` |
| `MakeMove` 2 个参数 | 3 参 | `InvalidDataException: Invocation provides 2 argument(s) but target expects 3.` |

多一个参数与少一个参数都被拒。所以给既有方法加参数,**两个方向都断**:旧客户端少发一个会被拒,而新客户端也没法先发着等服务端升级。这一条是实测出来的,不是推断的 —— `generalize-match-domain` 由 `AiSmoke` 撞上,本变更用一条真实长轮询连接复测过。

领域合法性一律由 Handler 调 `Room.PlayMove` 决定;Hub 只把参数搬成一个 `MakeMoveCommand`。哪个棋种收哪种载荷由**规则**判(棋盘类规则收到文本会拒,反之亦然),Hub MUST NOT 知道这件事。

Hub 方法 MUST NOT 访问 `DbContext`、MUST NOT 直接发送 SignalR 消息(事件由 `IRoomNotifier` 在 Handler 完成后触发)。

视图子群 SHALL 是三类:`room:{id}:seat:{n}`(每个座位一个)、`room:{id}:spectators`、`room:{id}:observers`(在房间里、没坐座位、也没围观)。三类 MUST **互斥且穷尽** —— `JoinRoom` 按聚合身份把每个连接放进恰好一个。

**按座位分群,而 MUST NOT 用 `Clients.User(...)`**:后者会打到那个用户的**全部连接**,包括他开在另一个房间的标签页 —— 一个催促弹错标签无所谓,一份房间快照盖掉另一个房间的状态不行。

`observers` 群此前叫 `non-spectators`,里面既有坐着的人也有没坐的人。座位群出现之后那样不行:坐着的人会收到两份快照(一份带手牌、一份不带),**看到哪一份由到达顺序决定**。

`LeaveRoom` MUST 退掉**每一个**座位群,而它的上界 MUST 从注册表算(`All.Max(r => r.SeatCount)`),MUST NOT 是一个手写常量:手写值在座位更多的棋种落地那天要有人记得涨,而**忘记涨没有任何报错** ——症状是那个座位的人离开房间之后还在收快照。与 `enforce-ai-availability` 让校验去读注册表是同一条。

#### Scenario: 未登录连接被拒
- **WHEN** 不带有效 JWT 的客户端尝试连接 `/hubs/match`
- **THEN** 连接被 SignalR 中间件以 401 拒绝

#### Scenario: Hub 方法透传到 Handler
- **WHEN** 客户端调 `MakeMove(roomId, 7, 7)`
- **THEN** `MakeMoveCommand` 被 `ISender.Send` 派发,携带落点 `(7,7)`;Hub 方法本身不读写数据库,不调用 `Clients.*.SendAsync`

#### Scenario: 文本类走子透传
- **WHEN** 客户端调 `SayWord(roomId, "一心一意")`
- **THEN** `MakeMoveCommand` 被派发,`Text == "一心一意"` 且四个坐标为 `null`

#### Scenario: 三个方法各自独立
- **WHEN** 审阅 hub 的走子入口
- **THEN** MUST 存在三个独立方法,且没有任何一个带可选参数

#### Scenario: 参数个数不符一律被拒
- **WHEN** 客户端以 1 个或 3 个参数调 `SayWord`
- **THEN** 两种都被 SignalR 的参数绑定拒掉,不进入 Hub 方法体
