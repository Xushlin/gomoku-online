# Tasks — add-doudizhu-visibility

## 1. 接缝

- [x] 1.1 `IPerSeatViewRules.ViewFor(MatchState, int?)` —— 单独一个接口,四个既有棋种一行不动
- [x] 1.2 `DoudizhuSeatView` + `DoudizhuRules.ViewFor`
- [x] 1.3 `DoudizhuTable` 补 `Kitty` 与 `CurrentCards` —— 重建时本来就在手上

## 2. 内核

- [x] 2.1 `RoomView(IncludeSpectatorChat, Seat, SeatView)` + 四个工厂
- [x] 2.2 `RoomStateDto.Seats` / `GameSnapshotDto.SeatView`
- [x] 2.3 `GetRoomRoleQuery` 返回 `RoomMembership(Role, Seat)`,构造器强制两者一致
- [x] 2.4 视图子群改三类:座位 / 围观者 / 观察者
- [x] 2.5 `LeaveRoom` 的座位上界**从注册表算**,不是手写常量
- [x] 2.6 广播逐份投影(`SeatCount + 2` 次)

## 3. 断言

- [x] 3.1 逐张比对:没有一个座位看得到别人的任何一张
- [x] 3.2 围观者 / 越界座位号 → 空手牌(反面控制)
- [x] 3.3 底牌:叫分阶段 `null`,定完地主公开,地主手上 20 张
- [x] 3.4 张数、阶段、桌面对所有人公开
- [x] 3.5 纯函数;两个不同座位的字符串必须不同
- [x] 3.6 DTO 层:三份快照互不相同;没有隐藏信息的棋种 `SeatView` 是 `null`;Waiting 房不抛
- [x] 3.7 `Seats` 含 2 号座位;两座位房间仍列出两个座位

## 4. 验证

- [x] 4.1 `dotnet test Gewu.slnx` —— **1293** 全绿(Domain 872 / Application 296 / Infrastructure 125)
- [x] 4.2 真 HTTP:三个玩家 + 一个围观者,四份不同的视图
- [x] 4.3 变异三处,全红
- [x] 4.4 `openspec validate --strict` 通过

## 5. 实现记录

### 实测:三家各 17 张,两两无交集,围观者 0 张

真 HTTP,一个真 `doudizhu` 房间,三个真账号 + 一个围观者,各自 `GET /api/rooms/{id}`:

```
seat0  hand=ABDFINQRSUYimopvz  len=17  counts=[17,17,17]  kitty=None  phase=Bidding
seat1  hand=GJKOPTWXcdejklruy  len=17  counts=[17,17,17]  kitty=None  phase=Bidding
seat2  hand=CLVZabfghnqstwx@#  len=17  counts=[17,17,17]  kitty=None  phase=Bidding
watch  hand=                   len= 0  counts=[17,17,17]  kitty=None  phase=Bidding
seats=[0, 1, 2]   spectators=1
```

两两交集 **0 / 0 / 0**,三者并集 **51** 张 —— 剩下 3 张在底牌里,叫分阶段**谁都看不到**。
`seats=[0,1,2]`:三座位房间里 2 号座位第一次出现在线上。

### 断言的是"看不到别人的",不是"看得到自己的"

一个把三家手牌都塞进视图的实现,在「我看得到我的 17 张」那条断言下**是绿的**。所以核心那条是
**逐张比对**:对每个座位,拿另两家的每一张去它的视图里找,一张都不许命中。

同样的理由,还有一条反面控制:座位号越界(`-1` / `3`)MUST 也是空手牌 —— 一个坏座位号
MUST NOT 变成"看别人的牌"。

### 三处变异,全红

| 变异 | 结果 |
| --- | --- |
| `ViewFor` 忽略座位、永远给 0 号的手牌 | Domain 3 红 + Application 3 红 |
| `RoomView` 永远按"没有座位"投影 | Application 2 红 |
| `ToState` 把 `SeatView` 写成 `null` | Application 3 红 |

三段路各自可断:规则算对了、`RoomView` 没带上、`ToState` 没写进 DTO —— 任何一段断掉,症状都是
"客户端看不到自己的牌",看起来像同一个 bug。所以这三处分两层验:规则那层在 Domain,
投影那层在 Application。

### 分群从两类变三类,而这是必须的

`fix-spectator-chat-leak` 立下的规矩是分群 MUST **互斥且穷尽**。座位群一出现,坐着的人就不能再
留在 `non-spectators` 里 —— 否则他会收到两份快照(一份带手牌、一份不带),而**看到哪一份由到达
顺序决定**。所以那个群改名 `observers`,含义收窄成"在房间里、没坐座位、也没围观",三类连接各进
恰好一个。

**没有用 `Clients.User(...)`**:它会打到那个用户的**全部连接**,包括他开在另一个房间的标签页。
一个催促弹错标签无所谓;一份房间快照盖掉另一个房间的状态不行。

**也没有为"没有隐藏信息的棋种"开一条快路。** 两座位棋种从两次发送变成四次(两个座位 + 观察者 +
围观者),四份内容完全相同。开快路会是两条代码路径,而这整套 `RoomView` 机制存在的全部理由就是
不给任何 handler 一次忘记裁剪的机会。

### 那个"退掉全部座位群"的上界不是手写的

第一版写了 `internal const int MaxSeats = 4`,注释里还写着"真出现座位更多的棋种时这个数字要跟着涨"。
**那句话本身就是它该被删掉的理由**:一个复述结构性事实的手写值是判断,而判断会悄悄过期 ——
忘记涨没有任何报错,症状是那个座位的人离开房间之后**还在收快照**。改成
`_rules.All.Max(r => r.SeatCount)`,与 `enforce-ai-availability` 让校验去读 `IGameAiRegistry`
而不是加一个手写布尔是同一条。

### 一个恢复操作悄悄回滚了两天前的代码

变异测试的还原步骤里,我用 python 写 `/tmp/rm.bak`、再用 bash `cp` 回来 —— 而**这两个 `/tmp`
不是同一个目录**:python 解析成 `D:\tmp\rm.bak`,msys bash 的 `/tmp` 是另一个位置,里面躺着一个
**两天前**的同名文件。于是"还原"把 `RoomMapping.cs` 换成了两天前的版本,顺手撤掉了
`generalize-match-contract` 的两处改动。

发现它的不是那次 `dotnet test`——**那一次只打印了 Domain 一行**,因为另两个项目根本没编过。
是紧接着单独跑 Application 时的编译错误暴露的。**"输出里没有失败"与"三个项目都跑了"不是同一件事。**
修法是从 `git checkout HEAD --` 取回文件、再重新打上本变更的两处补丁。

### 明确没有覆盖到的一件事

**广播的扇出本身没有端到端测试。** 已验的是:投影按座位裁剪(单元 + 真 HTTP)、分群函数对三类身份
穷尽(一个 `switch` 加 `_` 兜底)、座位上界从注册表取。**没验的是**"三条真 SignalR 连接各自只收到
自己那一份" —— 那需要一个 Api 层集成测试项目(`Gewu.Api.Tests`),而这个仓库还没有。
写下来而不是含糊过去:`ViewGroupName` 写错一个字符,今天没有任何测试会红。
