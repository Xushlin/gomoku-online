## ADDED Requirements

### Requirement: 聊天历史 SHALL 来自房间快照,而推送 SHALL 追加而不是替换

客户端 SHALL 从 `RoomStateDto.chatMessages` 读取进入房间时已有的消息,MUST NOT 为此调用
第二个接口。

服务端推送的 `ChatMessage` 每次只带**一条**消息。客户端 SHALL 把它**追加**到已有列表之后,
MUST NOT 用它替换整个列表 —— 后者的表现是「一发消息,前面的全没了」。

`ChatChannel` SHALL 按**名字**解析(`Room` / `Spectator`),不认识的取值 MUST NOT 塌成一个
默认频道 —— 一个没人认识的频道应该是可见的,不是被悄悄当成房间频道广播出去。

#### Scenario: 进房间就看得到之前的话
- **WHEN** 房间快照里有 3 条消息
- **THEN** 打开房间时这 3 条都在列表里

#### Scenario: 推送追加
- **WHEN** 列表里已有 3 条,服务端推来第 4 条
- **THEN** 列表变成 4 条,前 3 条不变

#### Scenario: 不认识的频道不当成房间频道
- **WHEN** 一条消息的 `channel` 是服务端将来新增的取值
- **THEN** 它 MUST NOT 被当作 `Room` 频道

---

### Requirement: 发送 SHALL 走 `SendChat`,频道以字符串给出,合法性 MUST NOT 由客户端判定

发送 SHALL 调用 hub 方法 `SendChat(roomId, content, channel)`,三个参数一个不多一个不少
—— SignalR 两个方向都不套用 C# 可选参数默认值,多一个少一个都在绑定层被拒,而那层的拒绝
低于日志级别,两端都看不见。

`channel` SHALL 以**字符串**给出(`'Room'`),与本客户端解析其他枚举的方式一致。

内容规则(trim 后 1–500 字符)由服务端判定。客户端 MAY 限制输入长度作为输入体验,但
MUST NOT 据此断定一条消息「能不能发」;被拒时 SHALL 显示服务端错误码对应的文案。

#### Scenario: 发送用字符串频道
- **WHEN** 玩家在房间频道发一条消息
- **THEN** 调用 `SendChat`,第三个参数是字符串 `'Room'`

#### Scenario: 空白内容不发
- **WHEN** 输入框里只有空白
- **THEN** MUST NOT 调用 `SendChat`(这不是判定合法性,是没有内容可发)

#### Scenario: 服务端拒绝时说服务端的理由
- **WHEN** 服务端以 `InvalidChatMessage` 拒绝
- **THEN** 屏幕上出现 `game.errors.invalid-chat`,MUST NOT 落到通用错误文案

---

### Requirement: 手机端的聊天 SHALL 只有房间频道,MUST NOT 显示一个到不了的围观频道

在围观能力落地之前,手机端 SHALL 只显示房间频道。

围观频道**只有围观者收得到、也只有围观者发得出**;在一个只有玩家到得了的屏幕上显示围观
页签,是一个**永远空的页签**,而一个永远空的页签看起来像坏了。

#### Scenario: 没有围观页签
- **WHEN** 玩家打开聊天面板
- **THEN** 界面上 MUST NOT 出现围观频道的入口
