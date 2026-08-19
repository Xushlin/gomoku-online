## Why

`ITimeoutFallbackRules.MoveOnTimeout(IReadOnlyList<PlayedMove> history, int seat)` **看不到对局设置**,而斗地主的兜底需要它:首出时要出"手上最小的一张单牌",而手牌在发牌里。

## 这是 `pass-setup-to-rules` 隔壁那个没被看的接缝

`generalize-turn-flow` (#86) 加了 `MoveOnTimeout`,签名收的是历史。下一个变更 `pass-setup-to-rules` (#87) 把 `Apply` 改成收 `MatchState`,**理由正是"规则读不到设置"** —— 而它没有回头看几十行之外那个刚加的、有一模一样问题的接缝。

**这与 `enforce-ai-availability` 记下的那件事是同一个形状**:`enforce-human-vs-human` 修好了规则夹具,没有去看**隔七行**的 AI 夹具,于是同一个缺陷第三次出现。这次是隔一个变更、隔几十行。

写下这条不是自责:它说明**"我刚修过这个类型的问题"是一个应该去搜一遍同类的信号**,而不是一个可以安心的理由。

## What Changes

```
MoveIntent MoveOnTimeout(MatchState state, int seat);
```

`Room.TimeOutCurrentTurn` 传 `Game.State()`(它已经这么给 `Apply` 了)。三处引用,行为零变化 —— 今天没有任何实现读 `state.Setup`。

## Impact

- Domain:接口一行、`Room` 一行。
- 测试:一个替身 + 两处断言。
- **无迁移;前端零改动;四个现有棋种零改动**(它们都不实现这个接口)。
