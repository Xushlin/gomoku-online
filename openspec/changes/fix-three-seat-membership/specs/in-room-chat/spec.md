# in-room-chat 的规格变化

## MODIFIED Requirements

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
