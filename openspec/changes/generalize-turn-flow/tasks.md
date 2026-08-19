# Tasks — generalize-turn-flow

## 1. 规则决定下一手

- [x] 1.1 `MoveApplication` 多一个 `int? NextSeat`,`null` 表示按环轮转
- [x] 1.2 构造器强制:结束了的对局 `NextSeat` MUST 为 `null`;负数不是座位
- [x] 1.3 `Game.RecordMove` 收 `nextSeat`,`CurrentTurn = nextSeat ?? (seat + 1) % seatCount`
- [x] 1.4 工厂 `MoveApplication.OngoingWithTurn(nextSeat)`
- [x] 1.5 `NInARowRules.Apply` 的三分支改用工厂(顺带把 `Draw` 说清楚)

## 2. 超时兜底

- [x] 2.1 `ITimeoutFallbackRules : IGameRules { MoveIntent MoveOnTimeout(history, seat); }`
- [x] 2.2 `Room` 抽出私有 `ApplyMove(seat, intent, now, rules)`,`PlayMove` 与兜底共用
- [x] 2.3 `Room.TimeOutCurrentTurn(now, seconds, rules)` 分两条路
- [x] 2.4 `TurnTimeoutOutcome`,恰好携带"走了一步 / 结束了"之一,构造时强制
- [x] 2.5 没有兜底的路仍然要求恰好两个座位

## 3. Application

- [x] 3.1 `TurnTimeoutCommandHandler` 解析规则(未知键 → 与落子路径一致地当作损坏记录)
- [x] 3.2 两条广播路径:`Played` → `MoveMade`;`Ended` → `GameEnded`;兜底那一步结束对局时两个都发
- [x] 3.3 一步棋不结束对局就不动评分 —— 与 `MakeMoveCommandHandler` 同一条

## 4. 断言

- [x] 4.1 轮转 / 覆盖 / 覆盖回自己,三条
- [x] 4.2 `MoveApplication` 的两条新非法组合
- [x] 4.3 兜底走一步、兜底赢了、兜底非法被拒、三座位无兜底仍拒
- [x] 4.4 **未到阈值时 MUST NOT 问兜底**(`FallbackCalls == 0`)
- [x] 4.5 `ApplyCalls == 1` —— 兜底那一步真的过了 `Apply`
- [x] 4.6 遍历 `BuiltInGameRules.All` 断言现有棋种都不实现 `ITimeoutFallbackRules`
- [x] 4.7 handler 的两条广播路径各一条

## 5. 验证

- [x] 5.1 `dotnet test Gewu.slnx` 全绿 —— 1204(Domain 794 / Application 286 / Infrastructure 124)
- [x] 5.2 前端零改动;**无迁移**
- [x] 5.3 变异测试 10 条
- [x] 5.4 `openspec validate --strict` 通过

## 6. 实现记录

### `NextSeat` 为 `null` 表示轮转 —— 与「参数不给默认值」不矛盾,而判据要说清

`add-match-setup` 刚刚刻意**不给** `setup` 参数默认值,理由是"默认值会让'忘了传'和'故意不传'长得一样"。这里却给了 `null` 一个默认语义。两者的判据是同一个:**忘了会不会有人发现。**

- 忘了传 `setup` → 一局没有牌的棋,要到第一次出牌才炸,离开局已过去几十秒。**必须当场抛。**
- 忘了给 `NextSeat` → **下一手轮到错的人**,在那个棋种的第一条测试里就会红。响得刺耳。

而且 `null` 在这里有**真实含义**,不是"没填":四个现有棋种的每一手、以及斗地主出牌阶段的每一手,答案确实都是"按环轮转"。让五个实现每次都算一遍内核已经知道的事,是重复而不是明确。

变异测试把这条钉住了两个方向:忽略 `NextSeat`(一律轮转)会红,一律听规则的(没指定就不动)也会红。

### 兜底那一步**必须过 `Apply`** —— 这是本变更最要紧的一条

不是"直接往 `Game` 里塞一条 `Move`"。两个理由,第二个更要紧:

1. 规则给出的兜底动作也可能非法(实现出错)。
2. **它可能结束对局** —— 牌类里替人出掉最后一手牌,那一手就赢了。

所以 `Room` 里抽出私有的 `ApplyMove(seat, intent, now, rules)`,`PlayMove` 与兜底共用。两条路径各写一遍,会让本仓库已经立下的「`Apply` 是走子合法性与胜负判定的**唯一**入口」变成两个入口。

抽出来的边界正好落在**前三步与后三步**之间:身份与回合校验是 `PlayMove` 独有的(兜底的座位由 `CurrentTurn` 给出,没有一个"调用者"需要被核对身份),而规则、记录、结束是共用的。

变异测试专门有一条:让兜底绕过 `Apply` 直接 `RecordMove` —— 那会让"兜底赢了对局就结束"和"非法兜底被拒"两条一起红。

### 那条限制没有被放宽,只是有了一个正当的出口

`generalize-match-outcome` 让 `TimeOutCurrentTurn` 在座位数不为 2 时抛,正是为了让"三座位的超时意味着什么"必须被回答。本变更没有删掉那个守卫:**一个三座位棋种若不提供兜底,仍然在超时那一刻大声坏掉。**

这一点单独有一条测试。它防的是"加了兜底接缝之后有人以为守卫可以拿掉了"。

### 一条要求写在实现身上,而它**不是**防死循环的护栏

一个可以合法地无限重复的兜底(牌类里"永远过牌")会把 worker 变成一个永不结束的自动对局。要求写在接口上:实现 MUST 保证推进(斗地主的形式是"能过就过,**不能过时出最小的一手**",而牌只会变少)。

**但它不是资源风险**,理由值得记下来:每一次兜底都要等满一个超时周期(worker 从最后一手的 `PlayedAt` 重算 `lastActivity`),所以最坏是每个周期一步 —— 慢、可见、不会自旋。所以这里**没有**发明一个"连续兜底次数上限":那个数字会是凭空的,而它要防的东西并不存在。

### 未到阈值时 MUST NOT 问兜底

这一条容易漏:先算超时、再问兜底,顺序反了的表现是**替一个还在思考的人出手**。而 worker 每 1500 ms 轮询一次,顺序反了就等于把每个房间的每一步都交给系统代打。

单独一条断言(`FallbackCalls == 0`),而不是靠"反正会抛"—— 抛与不抛跟"有没有问过兜底"是两件事。

### handler 的两条广播路径

兜底走出的一步在**线上与真人走的一步没有区别**,这是刻意的:客户端不需要区分"他走的"与"系统替他走的",而房间状态广播本来就带着新的 `CurrentTurn`。所以 `Played` 那条路的广播序列与 `MakeMoveCommandHandler` **逐条相同**。

顺带补上一处此前没有的东西:超时 handler 以前**不解析规则**(它不需要),现在需要了,于是未知棋种键的处理与落子路径对齐 —— 那是一条损坏的房间记录,不是一次非法超时。

### 顺手订正的两处 spec 问题

**一、我给一条不存在的 requirement 写了 MODIFIED,而 strict 校验通过了。** 我把它命名成
「`TurnTimeoutCommand` 由后台 worker 周期性派发,超时则结束对局」,而 live spec 里那条叫
「`TurnTimeoutCommand` 是 worker 内部命令」。MODIFIED 按**名字**匹配,所以那会**新增**第二条
描述同一个 handler 的 requirement,而不是替换旧的 —— 正是这个仓库已经撞过三次的重复枚举。

是去 grep live spec 里的名字发现的,不是校验器告诉我的。**`openspec validate --strict` 校验
spec 的形状,不校验 spec 的真假**,而"这条 requirement 存在吗"属于后者。

**二、那条 requirement 本身是过期的。** 它写着
`GameEloApplier.ApplyAsync(room, outcome.Result, _users, ct)` —— `generalize-match-outcome`
两个 PR 前就把那个签名改了(从聚合读结果与赢家,并少一个参数)。改对名字之后正好一并订正。

### 变异结果

```
RED  Game 忽略规则指定的下一手,一律轮转
RED  Game 一律听规则的(没指定就不动)
RED  MoveApplication 不再拒绝「结束了还有下一手」
RED  MoveApplication 不再拒绝负数的下一手
RED  超时不再走兜底,一律判负
RED  兜底那一步绕过 Apply,直接塞进 Game
RED  超时未到也去问兜底
RED  TurnTimeoutOutcome 不再强制「恰好一半」
RED  兜底走了一步也广播 GameEnded
RED  判负那条路也广播 MoveMade
```

两条"方向相反"的变异都变红,是这次最值得记的一点:把 `NextSeat` 忽略掉会红,把它当成必填
(没指定就不动)也会红。**一个有默认语义的字段,两个方向都得钉住** —— 只钉一个方向的话,
"默认值被误当成必填"这种错会静静通过。
