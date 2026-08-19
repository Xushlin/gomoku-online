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

**第 2、3 步里的「玩家」SHALL 由 `Room.IsPlayer` 判定 —— 即"占着任何一个座位",MUST NOT 列举
座位号。** 这一条是围观频道那条规则的**写入侧**,而实现此前写的是 `BlackPlayerId || WhitePlayerId`:
三座位棋种里 2 号座位上的玩家因此 `isPlayer == false`,**发得进围观频道**。

`fix-spectator-chat-leak` 的结论写着「写入侧一直是强制的,漏的是三条读取路径」—— 那句话对两座位
成立、对三座位不成立。**一个结论可以在它成立的世界里被记录下来,然后世界变了而记录没变。**

#### Scenario: 玩家发房间聊天
- **WHEN** 玩家 `Alice` 调 `PostChatMessage(aliceId, "Alice", "good luck", ChatChannel.Room, now)`
- **THEN** `Room.ChatMessages` 新增一条 `Content == "good luck"`、`Channel == Room`;返回的 `ChatMessage.SentAt == now`

#### Scenario: 围观者发围观频道
- **WHEN** 围观者 `Carol` 调 `PostChatMessage(carolId, "Carol", "白方要赢了", ChatChannel.Spectator, now)`
- **THEN** 新增一条 `Channel == Spectator` 的消息

#### Scenario: 玩家尝试发围观频道
- **WHEN** 玩家 `Alice` 调 `PostChatMessage(aliceId, "Alice", "hmm", ChatChannel.Spectator, now)`
- **THEN** 抛 `PlayerCannotPostSpectatorChannelException`

#### Scenario: 三座位房间的最后一个座位同样发不进围观频道
- **WHEN** 一个三座位棋种的房间里,2 号座位上的玩家往 `ChatChannel.Spectator` 发消息
- **THEN** 抛 `PlayerCannotPostSpectatorChannelException`

#### Scenario: 三座位房间的最后一个座位发得了房间频道
- **WHEN** 同一个玩家往 `ChatChannel.Room` 发消息
- **THEN** 消息入列 —— 上一条不是"这个座位什么都发不了"

#### Scenario: 非成员发消息
- **WHEN** 不在房间的 `Eve` 调 `PostChatMessage`
- **THEN** 抛 `NotInRoomException`

#### Scenario: 空内容
- **WHEN** `rawContent` 为 `null` / 空 / 纯空白
- **THEN** 抛 `InvalidChatContentException`

#### Scenario: 内容超长
- **WHEN** trim 后长度 > 500
- **THEN** 抛 `InvalidChatContentException`

### Requirement: 围观频道在**读取侧**也必须被强制,不能靠客户端自觉

「`Spectator` 频道仅围观者可见」这条规则 SHALL 在**每一条读取路径**上由服务端强制。

具体三条,每一条都 MUST 独立成立:

1. **REST 快照** —— `GetRoomStateQuery` MUST 带发起者(`ViewerId`),`ToState` MUST 收一个**必需的** `RoomView` 参数并据此裁剪。参数 MUST NOT 有默认值:一个默认值会让「忘了表态」与「故意给全部」在代码里长得一模一样。
2. **`RoomState` 广播** —— MUST **每个座位一份,外加观察者一份、围观者一份**:只有围观者那份含围观频道。`IRoomNotifier.RoomStateChangedAsync` MUST 收原料(聚合)而不是成品 DTO,由实现方逐份投影 —— 让每个 handler 各自投影,就等于给每个 handler 一次忘掉裁剪的机会。

   **它此前是「分两份」。** `add-doudizhu-visibility` 之后不行了:斗地主的手牌只有一个座位能看,所以坐着的人不能再共用一份快照。而一旦座位群出现,坐着的人就 MUST NOT 再留在「非围观者」群里 ——否则他会收到两份(一份带手牌、一份不带),**看到哪一份由到达顺序决定**。分群因此是三类:某个座位 / 围观者 / 观察者(在房间里、没坐座位、也没围观),仍然**互斥且穷尽**。
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
- **THEN** 每个座位收到的那份 MUST NOT 含围观频道消息,围观者收到的那份 MUST 含;房间里每一个连接 MUST 恰好收到一份(分组互斥且穷尽)

#### Scenario: 三类连接各进恰好一个视图群
- **WHEN** 一个连接 `JoinRoom`
- **THEN** 它 MUST 被放进「它那个座位的群」/「围观者群」/「观察者群」之一,且 MUST NOT 同时在两个里 ——身份取自聚合(座位号与身份来自**同一次**查询,分开问会有它们不一致的可能)

#### Scenario: 玩家不能把自己塞进围观子群
- **WHEN** 这局的玩家调 `JoinSpectatorGroup`
- **THEN** 服务端 MUST NOT 把它加进围观子群,该玩家 MUST NOT 收到任何围观频道的实时消息

#### Scenario: 还没围观的人也看不到围观频道
- **WHEN** 一个既非玩家也非围观者的登录用户读房间状态
- **THEN** MUST NOT 含围观频道 —— 判据是「是不是围观者」,不是「不是玩家」。两者对这个人给出不同答案,而取前者才能让 REST 与广播分组一致

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
3. 取 `senderId` 的座位号,若等于 `Game.CurrentTurn`(即**轮到自己**却催别人) → MUST 抛 `NotOpponentsTurnException`
4. 若 `LastUrgeAt != null` 且 `(now - LastUrgeAt).TotalSeconds < cooldownSeconds` → MUST 抛 `UrgeTooFrequentException`(Api 层映射 HTTP 429)
5. 否则更新 `Room.LastUrgeAt = now`、`Room.LastUrgeByUserId = senderId`,返回"催促结果"DTO,包含被催方 `UserId`(供 `IRoomNotifier` 定向推送)。

**被催方 SHALL 是"该走棋的那个人"(`PlayerAt(Game.CurrentTurn)`),MUST NOT 是"另一个座位"。**

两座位下这两句话**完全等价** —— 第 3 步已经保证发起人不是当前回合,所以"该走棋的人"就是对手;
五子棋 / 象棋 / 一字棋 / 成语接龙一行行为都不变。三座位下只有前者仍然唯一:原式
`senderSeat == 0 ? WhitePlayerId : BlackPlayerId` 会**永远催 0 号座位**,而 2 号座位上的人
永远催不到。催促这件事本来就只在"等某一个具体的人"时才有意义。

#### Scenario: 对手该下时催
- **WHEN** 轮到 1 号座位,0 号座位的玩家调 `UrgeOpponent(seat0, now)`,`LastUrgeAt == null`
- **THEN** 返回结果,被催方是 1 号座位上的玩家,`Room.LastUrgeAt == now`,`Room.LastUrgeByUserId == seat0`

#### Scenario: 三座位下催的是该走棋的那个人
- **WHEN** 一个三座位棋种的房间里 `Game.CurrentTurn == 2`,0 号座位的玩家调 `UrgeOpponent`
- **THEN** 被催方 MUST 是 **2 号**座位上的玩家,MUST NOT 是 1 号

#### Scenario: 冷却期内再催
- **WHEN** 上次催促在 10 秒前,再次调 `UrgeOpponent`
- **THEN** 抛 `UrgeTooFrequentException`

#### Scenario: 轮到自己时催
- **WHEN** `Game.CurrentTurn` 就是发起人的座位
- **THEN** 抛 `NotOpponentsTurnException`

#### Scenario: 围观者催促
- **WHEN** 围观者调 `UrgeOpponent`
- **THEN** 抛 `NotAPlayerException`

#### Scenario: 非 Playing 状态
- **WHEN** `Status == Waiting` 或 `Finished`
- **THEN** 抛 `RoomNotInPlayException`

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

### Requirement: 聊天在 375 px 下不横向溢出,包括最长的一条消息

聊天面板 SHALL 在 375 px 宽度下不产生横向滚动,且该断言 MUST 在**面板里有一条服务端上限长度
(500 字符)的无断点消息**时验证。

这条要求存在的理由与 `web-idiom-chain` 那条同源,而它们防的是同一种脆弱:一条长内容只因为
`overflow-wrap: break-word` 才留在面板里,而那个 class 一直没有任何断言守着 —— 一次样式重写
会发出一个在 375 px 横向滚动的房间页。

**断言分两半,而且各自只证明一半。** 单元测试 MUST 断言渲染消息的元素带有一个会断长词的工具类;
它抓得住 class 被删掉。它 MUST NOT 被当成「样式表仍然定义了那条规则」的证明 —— jsdom 没有
布局引擎也没有样式表,`getComputedStyle` 读不到有效值而 `scrollWidth` 恒为 0。后半句只有浏览器
能给,而浏览器验证是证据不是守卫。

断言 MUST NOT 只认一个 class 名。`break-words` / `break-all` / `wrap-anywhere` 都能防住溢出,
选哪个取决于内容;只认一个会让一次合理的替换变成假失败,而认这一组仍然抓得住「彻底去掉换行」。

#### Scenario: 上限长度的无断点消息不撑破布局
- **WHEN** 面板里含一条 500 字符的无断点消息,视口宽 375 px
- **THEN** `document.documentElement.scrollWidth === clientWidth`

#### Scenario: 渲染消息的元素带有断词工具类
- **WHEN** 审阅渲染消息内容的元素
- **THEN** 它带有 `break-words` / `break-all` / `wrap-anywhere` 之一

### Requirement: 围观与围观评论是对战内核的能力,不属于任何一个棋种

围观机制 SHALL 对**每一个有真人房的对战棋种**成立,MUST NOT 出现任何按 `GameKey` 分支的围观逻辑。

这一条不新建任何东西 —— 它把一件已经为真但从未写明的事写下来,并给它一个可检验的形状。整套机制早就在内核里:`ChatChannel`、`Room.PostChatMessage` 的成员与频道校验、`Room.JoinAsSpectator`、`IRoomNotifier` 的 `room:{id}:spectators` 子群、`POST /api/rooms/{id}/spectate`、大厅 `Playing` 行上的围观按钮、`ChatPanel` 的不对称可见性。**它们全都不点名棋种**,所以一个棋种够不到围观的唯一可能原因是它没有真人房。

写明它的理由是:「围观对所有对战棋种可用」此前是一个**推断** —— 由"代码里没有棋种分支"推出来的。而这个仓库反复付过同一种账:`SupportsHumanVsHuman` 被声明、被发布、被当作承重事实使用,却没有任何机制维持它。**一条没有断言的正确结论,与一条没人检查的结论,长得一模一样。**

围观人数 MUST NOT 有上限。`JoinAsSpectator` MUST 幂等(同一用户重复围观不产生第二条记录),且玩家 MUST NOT 能围观自己所在的局。

#### Scenario: 多名观众同时评论
- **WHEN** 一局真人对局有 ≥ 2 名围观者,各自在 `Spectator` 频道发言
- **THEN** 每条都入列 `Room.ChatMessages`;每个围观者都收到全部这些消息

#### Scenario: 玩家看不到围观频道
- **WHEN** 围观者在 `Spectator` 频道发言
- **THEN** 两名玩家 MUST NOT 收到该消息,也 MUST NOT 在自己的聊天面板里看到它

#### Scenario: 围观者与玩家共用房间频道
- **WHEN** 任一方在 `Room` 频道发言
- **THEN** 玩家与围观者**都**收到

#### Scenario: 围观不分棋种
- **WHEN** 对每一个 `SupportsHumanVsHuman == true` 的棋种各开一局真人对局并围观
- **THEN** 每一局都能围观、都能在围观频道评论;代码中 MUST NOT 存在任何按 `GameKey` 区分围观行为的分支

#### Scenario: 重复围观幂等
- **WHEN** 同一用户对同一房间调两次 `POST /api/rooms/{id}/spectate`
- **THEN** 围观者集合中只有一条该用户的记录

#### Scenario: 玩家不能围观自己的局
- **WHEN** 房间中的玩家调 `spectate`
- **THEN** 抛 `PlayerCannotSpectateException`

