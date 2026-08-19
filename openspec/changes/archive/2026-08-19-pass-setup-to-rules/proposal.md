## Why

**`add-match-setup` 落地了一个没有读者的字段。**

它给 `Game` 加了 `Setup`,由 `IDealtGameRules.CreateSetup` 造、经 `Room.JoinAsPlayer` 存下来 —— 而 `IGameRules.Apply` 的签名是 `(history, intent, seat)`,**规则拿不到它**。今天 `src` 里唯一提到 `.Setup` 的地方是一句注释。

那次变更的 spec 把这件事写成「由规则读(**将来的** `Apply`)」,所以它是被声明为延后的,不是被忘掉的。但延后的结果是同一个:**一个存下来再也没人读的值** —— 而那正是 `add-match-setup` 自己为「不需要设置的棋种却收到设置」加守卫时给出的理由。守卫防住了那一半,而另一半是它自己造成的。

`add-doudizhu` 一行都写不了,直到这条通路存在:出牌要校验"你手上确实有这几张牌",而手牌只在发牌里。

## What Changes

`IGameRules.Apply` 的前两个参数合成一个:

```
public readonly record struct MatchState(string? Setup, IReadOnlyList<PlayedMove> History);

MoveApplication Apply(MatchState state, MoveIntent intent, int seat);
```

`Room` 传 `Game.State()`。四个现有棋种的实现各改一行(`history` → `state.History`),**行为零变化**。

## 为什么是一个记录,而不是加第四个参数

`Apply(history, setup, intent, seat)` 有四个参数,其中两个是**这局到目前为止的状态**,两个是**这一步**。四个平铺的参数要求读代码的人记住顺序,而 `Apply(state, intent, seat)` 按它们实际的用法分了组。

**这不是为将来的扩展付钱** —— 那条理由本仓库拒绝过(`generalize-match-payload` 不加 JSON 载荷列,因为"一个成语是一个标量")。这里的理由是**可读性**:`state` 是一个有名字的东西("规则知道的关于这局的一切"),而 `(history, setup)` 是两个碰巧相邻的参数。顺带的好处是将来加字段不churn 调用点,但那是结果,不是论据。

## `Setup` 仍然不出服务端

本变更**不动**那条反射断言:DTO 命名空间下仍然不得有名字含 `Setup` 的成员。规则读得到它,客户端读不到 —— 与成语纵横「答案不出服务端」同一条:*客户端算不出来的东西,客户端就骗不了*。

## Impact

- Domain:`MatchState`(新类型)、`IGameRules.Apply`(签名)、`Game.State()`(新方法)、`Room.ApplyMove`(一行)、三个规则实现各一行。
- 测试替身:约十个 `IGameRules` 实现各一行。
- Application / Infrastructure / 前端:**零改动**。
- **无迁移** —— 列早就在了,只是没人读。
