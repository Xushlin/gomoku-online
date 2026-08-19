# Tasks — pass-state-to-fallback

- [x] 1 `ITimeoutFallbackRules.MoveOnTimeout(MatchState state, int seat)`
- [x] 2 `Room.TimeOutCurrentTurn` 传 `Game.State()`(它已经这么给 `Apply` 了)
- [x] 3 两个测试替身的签名
- [x] 4 一条断言:兜底真的看得到 `state`(记下它,并验历史内容)
- [x] 5 `dotnet test Gewu.slnx` 全绿 —— 1207(Domain 797 / Application 286 / Infrastructure 124)
- [x] 6 无迁移;前端零改动;四个现有棋种零改动(都不实现这个接口)
- [x] 7 `openspec validate --strict` 通过,且 MODIFIED 的标题**去 live spec 里核对过存在**

## 实现记录

### 这是 `pass-setup-to-rules` 隔壁那个没被看的接缝

`generalize-turn-flow` (#86) 加了 `MoveOnTimeout`,签名收历史 —— 那时 `MatchState` 还不存在。
紧接着的 `pass-setup-to-rules` (#87) 把 `Apply` 改成收 `MatchState`,**理由正是"规则读不到设置"**,
而它没有回头看几十行之外那个刚加的、有一模一样问题的接缝。

**这与 `enforce-ai-availability` 记下的是同一个形状**:`enforce-human-vs-human` 修好了规则夹具,
没去看**隔七行**的 AI 夹具,于是同一个缺陷第三次出现。这次是隔一个变更、隔几十行。

写下这条不是自责:它说明**"我刚修过这个类型的问题"是一个应该去搜一遍同类的信号**,而不是一个
可以安心的理由。具体的搜法也很直接 —— 改完 `Apply` 之后 grep 一遍
`IReadOnlyList<PlayedMove>` 在 `Games/Abstractions/` 下还剩几处,就会看见它。

### 发现它的方式

不是任何自动检查,是**要动手写斗地主的兜底了**才发现"首出时要出手上最小的一张,而手牌在
发牌里"。与 `pass-setup-to-rules` 被发现的方式一模一样(要写规则了才去看手牌怎么读)。

**两次都是"开始用它"才发现接缝不够用**,而两次的接缝都有测试、都全绿。测试证明的是它做了它
声称做的事,不是它够不够用。
