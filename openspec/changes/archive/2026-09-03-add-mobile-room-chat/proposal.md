# 手机端的房间聊天

## 为什么

房间里能落子、能认输、能催促,不能说话。服务端的聊天从第一天就在:hub `SendChat(roomId, content, channel)`、推送 `ChatMessage`、历史在 `RoomStateDto.chatMessages` 里随房间一起下发。

**三件在动工前量过的事**(`test/room_social_probe_test.dart`):

1. `ChatChannel` 是 C# 枚举,而 hub 的 `PayloadSerializerOptions` 注册了 `JsonStringEnumConverter` —— 实测**字符串 `"Room"` 能绑**,整数 `0` 也能。这个客户端用字符串,和它解析 `GameResult` / `RoomStatus` 的做法一致。**这件事必须量而不是读注册代码**:SignalR 拒绝一个类型不对的参数是在绑定层,先于任何 filter,低于日志级别,两端都看不见。
2. 房间快照里**确实带着** `chatMessages`,所以「刚进房间就能看到前面说了什么」不需要第二个接口。
3. 房间频道的推送发给**房间组**,而进那个组的方法是 `JoinRoom` —— 不是 `JoinSpectatorGroup`。探针第一版漏了这一步,量出来的结果长得**和一个服务端 bug 一模一样**。

## 做什么

- `ChatMessage` 模型 + `ChatChannel` 按**名字**解析(不认识的取值保留原文,不塌成默认值)。
- `Room` 解析 `chatMessages`,所以打开房间就有历史。
- 订阅 hub 的 `ChatMessage`,推送进一个可监听的列表;**新消息 MUST 追加到历史后面**,而不是替换它 —— 推送里只有**一条**。
- 发送走 `SendChat(roomId, content, 'Room')`。
- 界面:对局页上一个可开合的聊天面板(375 px 上棋盘占满,所以是底部弹出而不是并排)。

### 只有房间频道

`game.chat.tab-room` / `tab-spectator` 两个键都在,而这一笔**只做房间频道**:围观还没落地(`add-mobile-spectate`),而围观频道**只有围观者收得到、也只有围观者发得出**。在一个只有玩家到得了的屏幕上放一个围观页签,是一个**永远空的页签**,而空页签看起来像坏了。

### 合法性由服务端判

内容规则是 trim 后 1–500 字符。客户端 MAY 把输入框限到 500 作为**输入体验**,但 MUST NOT 自己判定「这条能不能发」—— 错误码来自服务端(`game.chat.max-length-error` / `game.errors.invalid-chat`)。**两份规则会分叉,而分叉的表现是输入框说可以、服务端说不行。**

## 不做

- 围观频道(等 `add-mobile-spectate`)。
- 分页 / 历史加载更多:服务端按 `(RoomId, SentAt)` 建了索引以备分页,但快照下发的那一批已经够开一局棋用了。**加载更多是一个真实需求出现之后再做的事**,现在做等于猜。
- 未读计数、@某人、表情。
