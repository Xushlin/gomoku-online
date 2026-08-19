# Tasks — fix-three-seat-membership

## 1. 一个判定,一处实现

- [x] 1.1 `Room.IsPlayer` → `SeatOf(userId) is not null`
- [x] 1.2 `Room.Leave` 的守卫改调 `IsPlayer`
- [x] 1.3 `Room.JoinAsSpectator` 的守卫改调 `IsPlayer`
- [x] 1.4 `Room.PostChatMessage` 的 `isPlayer` 改调 `IsPlayer`(围观频道的**写入侧**)
- [x] 1.5 `LeaveRoomCommandHandler.wasPlayer` 改调 `room.IsPlayer`
- [x] 1.6 `RoomMapping.CollectUserIds` 遍历 `Room.Seats`
- [x] 1.7 `Room.UrgeOpponent` 的被催方 → `PlayerAt(Game.CurrentTurn)`

## 2. 顺手删的死代码

- [x] 2.1 三处 `var state = room.ToState(...)` —— 算完就扔,没有编译警告

## 3. 断言

- [x] 3.1 2 号座位:`IsPlayer` / 能离开 / 不能围观 / 发不进围观频道 / 发得了房间频道
- [x] 3.2 **反面控制**:真外人仍然离不开、发不了、而且围观得成
- [x] 3.3 催促:三座位下催到 2 号;两座位下用真五子棋房间验零改动
- [x] 3.4 `CollectUserIds` 含 2 号座位,且仍含 host 与围观者
- [x] 3.5 `LeaveRoomCommandHandler` 对 2 号座位发 `PlayerLeftAsync`

## 4. 验证

- [x] 4.1 `dotnet test Gewu.slnx` —— **1277** 全绿(Domain 863 / Application 289 / Infrastructure 125)
- [x] 4.2 变异:七处调用点逐个换回两座位写法,**七处全红**
- [x] 4.3 真 HTTP 复测那两个实测出来的数字
- [x] 4.4 `openspec validate --strict` 通过,四个 MODIFIED 标题去 live spec 核对过存在

## 5. 实现记录

### 一个事实,七份手写副本

「这个人是不是本房间的玩家」在代码里有七份实现,每一份都是 `BlackPlayerId || WhitePlayerId`:

| 位置 | 错的后果 |
| --- | --- |
| `Room.IsPlayer` | `RoomView.For` 的判据之一 |
| `Room.Leave` | 2 号座位离不开自己在的房间(实测 **404**) |
| `Room.JoinAsSpectator` | 2 号座位**围观成功**(实测 **204**),于是拿到围观视角 |
| `Room.PostChatMessage` | 2 号座位**发得进围观频道** |
| `Room.UrgeOpponent` | 永远催 0 号;2 号永远催不到 |
| `LeaveRoomCommandHandler` | 他离开时**两个事件一个都不发** |
| `RoomMapping.CollectUserIds` | 他的用户名查不到 → `<unknown>` |

`SeatOf` 的文档说它存在正是因为"三处需要'这人是第几号'的地方各写了一遍同样的 if/else"。
**那次收敬只做了一半**:"他是几号"进了一处,而**紧邻的**"他是不是玩家"仍散在七处。
两者是同一个事实的两种问法。

### 实测的那两个数字,以及它们为什么重要

三个真账号、一个真 `doudizhu` 房间、真 HTTP:

```
seat2 spectate: 204   →   409   (PlayerCannotSpectateException)
seat2 leave   : 404   →   204
outsider leave: 404   →   404   ← 反面控制:这个洞没有被顺手挖大
outsider spect: 204   →   204
```

围观那条是要紧的:围观成功之后 `IsSpectator == true`,`RoomView.For` 给他围观视角,`ToState`
把围观频道全部内容发给一个**正坐在牌桌上的人**。`fix-spectator-chat-leak` 花了一整个变更
把这件事的三条读取路径堵上,而三座位棋种从**写入侧**又把它打开了。

那次修复的结论里写着「写入侧一直是强制的」。那句话在两座位的世界里是真的。
**一个结论可以在它成立的世界里被记录下来,然后世界变了而记录没变** ——
这与 `enforce-human-vs-human` 记的"结论对 web UI 成立、对 API 不成立"是同一个形状。

### 1266 条测试一条都没红

七处全错,而全套测试全绿:三座位的这几条路此前**没有任何测试走过**。斗地主是三天前才有的,
它自己的测试打的是牌,不是房间关系。所以这次先量、再改、再变异 —— 七处逐个换回旧写法,
七处全红,包括那条"两座位零行为改动"的催促。

### 催促是唯一一处**行为**的一般化

其余六处是判定的修正(意图一直在,只是数不到 2 号座位);催促不同 —— 三座位下"对手"根本没有
唯一解。取"该走棋的那个人",因为:

- 两座位下与原式**完全等价**(第 3 步守卫已保证发起人不是当前回合),所以四个既有棋种零改动;
- 三座位下它仍然唯一,而"催促"这件事本来就只在"等某一个具体的人"时才有意义。

判别用的测试局面必须是"该走棋的人既不是 0 号、也不是发起人"(叫两轮不叫把出手权推到 2 号),
否则新旧两式答案相同、那条测试什么都没证。

### 三行死代码,没有编译警告

`JoinAsSpectator` / `LeaveAsSpectator` / `LeaveRoom` 三个 handler 各有一行
`var state = room.ToState(usernames, ..., RoomView.For(room, request.UserId));`,算完就扔 ——
`fix-spectator-chat-leak` 之后 `RoomStateChangedAsync` 收的是聚合,自己投影两份视图。

C# 不会为此报警(CS0219 只管常量初始化),所以它们活了下来。**而它们长得像一处安全判定**:
`RoomView.For(room, viewer)` 是这个仓库里"这份快照给谁看"的唯一表达,下一个读到它的人会以为
那一行承重。删掉。

顺带一条记录上的更正,而它比那三行代码本身有意思。

`fix-spectator-chat-leak` 的提交信里写着:「编译器随后点出全部九个调用点,
其中三处的投影只服务于广播 —— **那三行自己消失了**」。去看那个 commit（`f6bc36d`）的 diff:
那三行**没有消失**。它们被认真地补上了新参数 —— `+ var state = room.ToState(usernames, ..., RoomView.For(room, request.UserId));`
—— 而下一行的 `RoomStateChangedAsync` 同时改成了收聚合,于是 `state` 当场变成死的。

**一个必需参数能让编译器列出每一个调用点,但"让它编过"不等于"想清每一个调用点是干什么的"。**
那个机制本身是有效的（没有任何一处惄惄地把全部消息发出去）;而它同时把三处本该删掉的
调用点机械地保下来了,还给它们穿上了一件看起来像安全判定的衣服。
**"我记得删了"与"它不在那儿了"不是同一件事**,而两天后的我就是那个"下一个人"。
