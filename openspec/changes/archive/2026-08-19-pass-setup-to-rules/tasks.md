# Tasks — pass-setup-to-rules

## 1. 接缝

- [x] 1.1 `MatchState(string? Setup, IReadOnlyList<PlayedMove> History)`
- [x] 1.2 `IGameRules.Apply(MatchState state, MoveIntent intent, int seat)`
- [x] 1.3 `Game.State()` 组装它 —— 两个字段都是 `Game` 的东西,交出去应该是一个完整的答案
- [x] 1.4 `Room.ApplyMove` 传 `Game.State()`

## 2. 四个现有棋种

- [x] 2.1 `NInARowRules` / `XiangqiRules` / `IdiomChainRules` 各改签名 + `history` → `state.History`
- [x] 2.2 行为零变化 —— 1204 条测试无一条断言需要改动

## 3. 测试替身与调用点

- [x] 3.1 十个 `IGameRules` 替身的签名
- [x] 3.2 26 处直接 `Apply(...)` 的调用点包成 `new MatchState(null, X)`
- [x] 3.3 **不加隐式转换。** 一个 `IReadOnlyList<PlayedMove> → MatchState` 的隐式转换会让 26 处调用点一行不改就编译过 —— 而那正是问题:它让"这一局有没有设置"这个问题在调用点消失

## 4. 断言

- [x] 4.1 设置真的到得了 `Apply`(探针记下 `state.Setup`,走真 `Room` 的 `PlayMove`)
- [x] 4.2 对称的一半:不需要设置的棋种在 `Apply` 里看到 `null`,不是 `""`
- [x] 4.3 两条都断言 `ApplyCalls` —— 免得"没看到"与"没被调"混为一谈
- [x] 4.4 反射断言不变:DTO 里仍然不得有名字含 `Setup` 的成员

## 5. 验证

- [x] 5.1 `dotnet test Gewu.slnx` 全绿 —— 1206(Domain 796 / Application 286 / Infrastructure 124)
- [x] 5.2 前端零改动;**无迁移**(列早就在了,只是没人读)
- [x] 5.3 变异测试 3 条
- [x] 5.4 `openspec validate --strict` 通过

## 6. 实现记录

### 这是我上一个变更留下的洞

`add-match-setup` 落地了 `Game.Setup`:由 `CreateSetup` 造、经 `JoinAsPlayer` 存下来。而
`IGameRules.Apply` 的签名是 `(history, intent, seat)` —— **规则拿不到它**。合并之后,`src` 里
唯一提到 `.Setup` 的地方是一句注释。

那次的 spec 把这件事写成「由规则读(**将来的** `Apply`)」,所以它是被声明为延后的,不是被
忘掉的。**但延后的结果和忘掉是同一个**:一个存下来再也没人读的值 —— 而那正是那次变更自己
为「不需要设置的棋种却收到设置」加守卫时给出的理由。守卫防住了那一半,另一半是它自己造成的。

发现它的方式是要动手写 `add-doudizhu` 了才去看"规则怎么读手牌",而不是任何自动检查。
**一个字段有没有读者,编译器不问,测试也不问** —— `Setup` 的写路径当时是有测试的,而"有人读它"
不在那些测试的范围里。

### 一个记录,而不是第四个参数

`Apply(history, setup, intent, seat)` 有四个参数,其中两个是这局的状态、两个是这一步。
`Apply(state, intent, seat)` 按它们实际的用法分组。

**理由是可读性,不是可扩展性。** 后者本仓库拒绝过(`generalize-match-payload` 不加 JSON 载荷列,
因为"一个成语是一个标量")。"将来加字段不 churn 调用点"是这个形状的**结果**,不是选它的论据 ——
把结果当论据,下次就会为一个不存在的需求造一个容器。

### 刻意不加隐式转换

`IReadOnlyList<PlayedMove> → MatchState` 的隐式转换会让 26 处测试调用点一行不改就编译通过,
省掉这次全部的机械改动。

没那么做:那个便利的代价是**"这一局有没有设置"这个问题在调用点消失**。一个隐式转换出来的
`MatchState` 永远 `Setup == null`,而它看起来与一个真的没有设置的局面一模一样 —— 于是斗地主的
某个测试哪天忘了给设置,会得到"规则说你手上没有这张牌",而不是一个编译错误。

26 行的机械改动换一个编译期问题,是这个仓库反复做的那个交易。

### 变异结果

```
RED  Room 把设置藏起来不给规则(Apply 只收历史)
RED  Game.State 把空设置换成空字符串
RED  Game.State 不带历史
```

第二条值得单独说:把 `Setup ?? ""` 塞进去会红,因为那条"不需要设置的棋种在 `Apply` 里看到
`null`"的断言在盯着。**空字符串与 `null` 在这里不是同一件事** —— 规则里 `state.Setup is null`
是"这个棋种没有设置",而 `""` 会让它变成"设置是空的",两者要走的分支不同。
