# room-and-gameplay Specification Delta

## MODIFIED Requirements

### Requirement: SignalR Hub `GomokuHub` 路由实时操作,但不写入业务逻辑

系统 SHALL 在 `/hubs/gomoku` 暴露 `GomokuHub`(`[Authorize]`)。Hub 客户端方法:

- `JoinRoom(Guid roomId)` —— 把当前 connection 加入 SignalR group `room:{roomId}`;若调用方已是该房间的玩家或围观者(聚合成员已由 REST 建立),则**额外**加入 `room:{roomId}:spectators` 子群(仅围观者)。不会修改 `Room` 聚合。
- `LeaveRoom(Guid roomId)` —— 从上述 group 中移除。不会修改聚合。
- `MakeMove(Guid roomId, int row, int col)` —— **落子类**棋种(五子棋 / 一字棋)。派 `MakeMoveCommand`。
- `MovePiece(Guid roomId, int fromRow, int fromCol, int row, int col)` —— **走子类**棋种(中国象棋)。
- `SayWord(Guid roomId, string word)` —— **文本类**棋种(成语接龙)。
- `SendChat(Guid roomId, string content, ChatChannel channel)` —— 派 `SendChatMessageCommand`(规则见 `in-room-chat` spec)。
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

#### Scenario: 未登录连接被拒
- **WHEN** 不带有效 JWT 的客户端尝试连接 `/hubs/gomoku`
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

## ADDED Requirements

### Requirement: `MakeMoveCommand` 携带它收到的那一种载荷

`MakeMoveCommand` SHALL 带 `int? Row`、`int? Col`、`int? FromRow`、`int? FromCol`、`string? Text`,与 `MoveIntent` / `Move` 在 `generalize-match-payload` 之后的形状一致。

Handler MUST 依据载荷选出**恰好一个** `MoveIntent` 工厂(`Place` / `Slide` / `Say`),MUST NOT 自己再实现一遍"恰好一种载荷"——那条不变量由 `MoveIntent` 的构造器强制,handler 拼错了会当场抛。

`MakeMoveCommandValidator` SHALL:

- 坐标**存在时**非负。上界仍属于棋种,理由不变:校验器跑在解析房间之前,那时还不知道这是哪一种棋。
- 文本**存在时**非空白。

这是"位置或文本"这个形状在本仓库的**第三处**编码(值对象、持久化实体、命令),而这是一次**有取舍的选择**,不是疏漏:更整洁的做法是让命令直接带 `MoveIntent`,那样编码就只剩一处。不这么做的具体理由是 `Position` 的构造器拒绝负坐标 —— 把 intent 的构造上移到 Hub,会把负坐标的拒绝从 `MakeMoveCommandValidator`(**400 + 点名字段**)挪到命令还不存在的时候抛出。那条错误路径被 `web-game-board` 与 `add-hub-error-codes` 两处钉着。改它是一个说得通的变更,在一个功能变更里顺手改掉不是。

#### Scenario: 落子类
- **WHEN** 命令带 `Row` / `Col`,不带 `FromRow` 也不带 `Text`
- **THEN** handler 用 `MoveIntent.Place`

#### Scenario: 走子类
- **WHEN** 命令另带 `FromRow` / `FromCol`
- **THEN** handler 用 `MoveIntent.Slide`

#### Scenario: 文本类
- **WHEN** 命令带 `Text`,四个坐标为 `null`
- **THEN** handler 用 `MoveIntent.Say`

#### Scenario: 负坐标仍是 400
- **WHEN** 命令带 `Row = -1`
- **THEN** 校验失败 —— 这条错误路径与本变更之前完全一致

#### Scenario: 空白文本被拒
- **WHEN** 命令带 `Text = "   "`
- **THEN** 校验失败

#### Scenario: 缺坐标的落子不被校验器放行成文本
- **WHEN** 命令四个坐标与 `Text` 全为 `null`
- **THEN** 请求 MUST 失败 —— 由 `MoveIntent` 的构造器兜住,handler MUST NOT 悄悄补一个默认值
