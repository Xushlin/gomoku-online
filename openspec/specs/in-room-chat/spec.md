# In-Room Chat

## Purpose

房间内的聊天与催促功能:两个频道(`Room` 所有人可见 / `Spectator` 仅围观者可见)、内容校验(trim 后 1–500 字符)、发送者用户名 snapshot、以及玩家催促对手的冷却机制(仅 Playing 状态、仅玩家、仅对手回合、30 秒冷却)。

SignalR 事件:`ChatMessage`(按频道广播)与 `UrgeReceived`(仅被催方)。持久化:`ChatMessages` 表 `(RoomId, SentAt)` 索引便于分页;催促事件 **不入库**,仅推送。

实现位于 `backend/src/Gewu.Domain/Rooms/`(`Room.PostChatMessage` / `Room.UrgeOpponent` 领域方法、`ChatMessage` 子实体、`ChatChannel` 枚举)、`backend/src/Gewu.Application/Features/Rooms/SendChatMessage` 与 `UrgeOpponent`(CQRS handlers)、`backend/src/Gewu.Api/Hubs/`(SignalR 路由)。
## Requirements
### Requirement: `ChatChannel` 枚举区分房间频道与围观频道

系统 SHALL 定义 `enum ChatChannel { Room=0, Spectator=1 }`。`Room` 频道对房间内所有人(玩家 + 围观者)可见;`Spectator` 频道**仅围观者**可见(玩家看不到围观者吐槽)。

#### Scenario: 枚举值存在且稳定
- **WHEN** 审阅 `ChatChannel.cs`
- **THEN** 存在 `Room = 0` 与 `Spectator = 1` 两个值,且其底层数值 MUST 保持不变以避免库迁移

---

### Requirement: `ChatMessage` 子实体承载一条已发送消息

`ChatMessage` MUST 包含:

- `Id: Guid`
- `RoomId: RoomId`
- `SenderUserId: UserId`
- `SenderUsername: string`(snapshot 时刻的发送者用户名,用户改名后老消息保留旧名)
- `Content: string`(trim 后非空,长度 ≤ 500)
- `Channel: ChatChannel`
- `SentAt: DateTime`(UTC)

构造仅由 `Room.PostChatMessage(...)` 内部发生(`internal` 构造函数),`ChatMessages` 集合对外只读。

#### Scenario: 字段只读且完整
- **WHEN** 读取 `Room.ChatMessages` 中任一元素
- **THEN** 上述七个字段可读,外部无 setter

---

### Requirement: `Room.PostChatMessage` 领域方法校验权限与频道并入列

系统 SHALL 提供 `Room.PostChatMessage(UserId senderId, string senderUsername, string rawContent, ChatChannel channel, DateTime now)`。按序校验:

1. **内容规范化**:`content = rawContent.Trim()`。若 `content` 为空 / 长度 > 500 → MUST 抛 `InvalidChatContentException`
2. **成员关系**:若 `senderId` 既非玩家、也不在 `Spectators` → MUST 抛 `NotInRoomException`
3. **频道权限**:
   - `channel == Room`:玩家 / 围观者均可发
   - `channel == Spectator`:**仅围观者**可发;玩家尝试 → MUST 抛 `PlayerCannotPostSpectatorChannelException`
4. 构造新的 `ChatMessage` 并 append 到内部列表;返回该 `ChatMessage`。

#### Scenario: 玩家发房间聊天
- **WHEN** 玩家 `Alice` 调 `PostChatMessage(aliceId, "Alice", "good luck", ChatChannel.Room, now)`
- **THEN** `Room.ChatMessages` 新增一条 `Content == "good luck"`、`Channel == Room`;返回的 `ChatMessage.SentAt == now`

#### Scenario: 围观者发围观频道
- **WHEN** 围观者 `Carol` 调 `PostChatMessage(carolId, "Carol", "白方要赢了", ChatChannel.Spectator, now)`
- **THEN** 新增一条 `Channel == Spectator` 的消息

#### Scenario: 玩家尝试发围观频道
- **WHEN** 玩家 `Alice` 调 `PostChatMessage(aliceId, "Alice", "hmm", ChatChannel.Spectator, now)`
- **THEN** 抛 `PlayerCannotPostSpectatorChannelException`

#### Scenario: 非成员发消息
- **WHEN** 不在房间的 `Eve` 调 `PostChatMessage`
- **THEN** 抛 `NotInRoomException`

#### Scenario: 空内容
- **WHEN** `rawContent` 为 `null` / 空 / 纯空白
- **THEN** 抛 `InvalidChatContentException`

#### Scenario: 内容超长
- **WHEN** trim 后长度 > 500
- **THEN** 抛 `InvalidChatContentException`

---

### Requirement: 围观频道在**读取侧**也必须被强制,不能靠客户端自觉

「`Spectator` 频道仅围观者可见」这条规则 SHALL 在**每一条读取路径**上由服务端强制。

具体三条,每一条都 MUST 独立成立:

1. **REST 快照** —— `GetRoomStateQuery` MUST 带发起者(`ViewerId`),`ToState` MUST 收一个**必需的** `RoomView` 参数并据此裁剪。参数 MUST NOT 有默认值:一个默认值会让「忘了表态」与「故意给全部」在代码里长得一模一样。
2. **`RoomState` 广播** —— MUST 分两份:非围观者子群那份不含围观频道,围观者子群那份含。`IRoomNotifier.RoomStateChangedAsync` MUST 收原料(聚合)而不是成品 DTO,由实现方投影两份 —— 让每个 handler 各自投影,就等于给每个 handler 一次忘掉裁剪的机会。
3. **实时 `ChatMessage` 事件** —— 按频道分群推送是必要的但**不充分**:入群本身 MUST 校验(见 `room-and-gameplay` 的 `JoinRoom` / `JoinSpectatorGroup`)。

客户端隐藏围观 Tab MUST NOT 被当作实现手段。它是展示;数据不该到那里。

**这三条此前全部不成立**,而写入侧一直是强制的 —— 于是规则看起来在工作。屏幕上也看不出来:`ChatPanel` 用 `@if (isSpectator())` 藏了围观 Tab,玩家 UI 干净,而对手围观区的全文早就在他的客户端里。

**一条只做对了一半的机制,读那一半的代码看不出来。** 第三条的分群是对的,校验是缺的;先只读代码会把它判成正确的。

#### Scenario: 玩家的 REST 快照不含围观频道
- **WHEN** 玩家 `GET /api/rooms/{id}`
- **THEN** `chatMessages` 里 MUST NOT 出现 `Channel == Spectator` 的消息;房间频道的消息 MUST 照常返回

#### Scenario: 围观者的 REST 快照两个频道都有
- **WHEN** 围观者 `GET /api/rooms/{id}`
- **THEN** 两个频道的消息都返回,包括其它围观者发的

#### Scenario: 玩家收到的广播不含围观频道
- **WHEN** 房间状态变化触发 `RoomState` 广播
- **THEN** 玩家收到的那份 MUST NOT 含围观频道消息,围观者收到的那份 MUST 含 —— 两者 MUST 都真的收到一份(分组互斥且穷尽)

#### Scenario: 玩家不能把自己塞进围观子群
- **WHEN** 这局的玩家调 `JoinSpectatorGroup`
- **THEN** 服务端 MUST NOT 把它加进围观子群,该玩家 MUST NOT 收到任何围观频道的实时消息

#### Scenario: 还没围观的人也看不到围观频道
- **WHEN** 一个既非玩家也非围观者的登录用户读房间状态
- **THEN** MUST NOT 含围观频道 —— 判据是「是不是围观者」,不是「不是玩家」。两者对这个人给出不同答案,而取前者才能让 REST 与广播分组一致

---

### Requirement: 聊天消息通过 `IRoomNotifier` 按频道分发

Handler `SendChatMessageCommand` 在 `SaveChangesAsync` 之后 MUST 调 `IRoomNotifier.ChatMessagePostedAsync(roomId, channel, dto)`。SignalR 实现 MUST:

- `channel == Room`:广播到 `room:{roomId}` group
- `channel == Spectator`:只广播到 `room:{roomId}:spectators` 子群

客户端事件名统一为 `ChatMessage`,payload 的 `channel` 字段告诉前端放在哪个面板。

#### Scenario: 房间频道广播到所有人
- **WHEN** Alice 发 `Room` 频道消息
- **THEN** `Clients.Group("room:{roomId}").SendAsync("ChatMessage", payload)` 被调用一次

#### Scenario: 围观频道仅发给围观者
- **WHEN** Carol(围观者)发 `Spectator` 频道消息
- **THEN** `Clients.Group("room:{roomId}:spectators").SendAsync("ChatMessage", payload)` 被调用;玩家所在的主 group **不**接收

---

### Requirement: `SendChatMessageCommand` Validator 对入参做基础校验

Application 层 SHALL 提供 `AbstractValidator<SendChatMessageCommand>`,至少:

- `Content` 非空,trim 后长度 1–500
- `Channel` 是 `ChatChannel` 的合法枚举值
- `RoomId` 非空

Validator 失败时 `ValidationBehavior` 抛 `ValidationException`,最终 HTTP 400 + `ProblemDetails.errors`。Domain 的 `InvalidChatContentException` 仍在 Handler 调用 `Room.PostChatMessage` 时兜底 —— 但一般 validator 已拦下。

#### Scenario: 空 content
- **WHEN** `SendChat(roomId, "", ChatChannel.Room)`
- **THEN** Hub 方法调用 `ISender.Send` 时 `ValidationBehavior` 抛 `ValidationException` → 客户端收到 400 + `errors["Content"]`

---

### Requirement: `Room.UrgeOpponent` 催促对手下棋,30 秒冷却

系统 SHALL 提供 `Room.UrgeOpponent(UserId senderId, DateTime now, int cooldownSeconds = 30)`。规则:

1. `Status != Playing` → MUST 抛 `RoomNotInPlayException`
2. `senderId` 不是玩家 → MUST 抛 `NotAPlayerException`
3. 推断 `senderId` 的棋色,若等于 `Game.CurrentTurn`(即**轮到自己**却催对手) → MUST 抛 `NotOpponentsTurnException`
4. 若 `LastUrgeAt != null` 且 `(now - LastUrgeAt).TotalSeconds < cooldownSeconds` → MUST 抛 `UrgeTooFrequentException`(Api 层映射 HTTP 429)
5. 否则更新 `Room.LastUrgeAt = now`、`Room.LastUrgeByUserId = senderId`,返回"催促结果"DTO,包含被催方 `UserId`(供 `IRoomNotifier` 定向推送)。

催促 **不** 写入 `ChatMessages`(非持久化),只产生 `UrgeReceived` 事件。

#### Scenario: 对手该下时催
- **WHEN** 轮到白方,黑方玩家调 `UrgeOpponent(blackId, now)`,`LastUrgeAt == null`
- **THEN** 返回结果,`Room.LastUrgeAt == now`,`Room.LastUrgeByUserId == blackId`

#### Scenario: 冷却期内再催
- **WHEN** 上次催促在 10 秒前,再次调 `UrgeOpponent`
- **THEN** 抛 `UrgeTooFrequentException`

#### Scenario: 轮到自己时催
- **WHEN** `CurrentTurn == Black`,黑方调 `UrgeOpponent(blackId, ...)`
- **THEN** 抛 `NotOpponentsTurnException`

#### Scenario: 围观者催促
- **WHEN** 围观者调 `UrgeOpponent`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: 非 Playing 状态
- **WHEN** `Status == Waiting` 或 `Finished`
- **THEN** 抛 `RoomNotInPlayException`

---

### Requirement: 催促事件仅推给被催玩家

`UrgeOpponentCommand` Handler 成功后 MUST 调 `IRoomNotifier.OpponentUrgedAsync(roomId, urgedUserId, payload)`。SignalR 实现 MUST 用 `IHubContext<MatchHub>.Clients.User(urgedUserId.ToString()).SendAsync("UrgeReceived", payload)` —— **只发给被催那一方**,不广播给房间。

`payload` 至少包含 `{ fromUserId, fromUsername, sentAt }`。

#### Scenario: 仅被催方收到
- **WHEN** 黑方成功催促白方
- **THEN** `Clients.User(whitePlayerId).SendAsync("UrgeReceived", ...)` 被调一次;`Clients.Group("room:{roomId}").SendAsync` 不被触发

### Requirement: 催促异常的 HTTP 映射

全局异常中间件 MUST 新增映射:

| 异常 | HTTP |
|---|---|
| `NotOpponentsTurnException` | 409 |
| `UrgeTooFrequentException` | 429 |
| `InvalidChatContentException` | 400 |
| `PlayerCannotPostSpectatorChannelException` | 403 |

#### Scenario: 冷却期内催促
- **WHEN** Hub `Urge` 方法触发 `UrgeTooFrequentException`
- **THEN** 客户端通过 Hub 的错误回传(或相应 REST 调用的)收到 429 + `ProblemDetails`

---

### Requirement: 聊天 `ChatMessages` 表持久化记录

Infrastructure SHALL 把 `ChatMessage` 映射到表 `ChatMessages`,列:`Id (PK)`、`RoomId (FK)`、`SenderUserId`、`SenderUsername (<=20 chars)`、`Content (<=500 chars)`、`Channel (int)`、`SentAt`。索引 `(RoomId, SentAt)` 用于未来"分页拉历史"。

`Room.Status == Finished` 后的房间会在终局 30 分钟后被清理作业删除 —— **本次不实现清理作业**,仅持久化结构就位。相关清理逻辑留给独立变更。

#### Scenario: 落库列齐全
- **WHEN** `SaveChangesAsync` 成功
- **THEN** `ChatMessages` 行包含全部七个字段;`Channel` 列写入枚举 int

#### Scenario: 用户名快照
- **WHEN** 发送消息时 `User.Username == "Alice"`,之后用户改名为 `Alicia`
- **THEN** 已存消息的 `SenderUsername` 仍为 `"Alice"`(历史不变)

