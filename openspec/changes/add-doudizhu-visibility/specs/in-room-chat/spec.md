# in-room-chat 的规格变化

## MODIFIED Requirements

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

