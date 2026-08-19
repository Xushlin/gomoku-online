# Tasks — add-match-setup

## 1. Domain

- [x] 1.1 `IDealtGameRules : IGameRules { string CreateSetup(int seed); }`
- [x] 1.2 `Game.Setup: string?`,构造时写入,之后只读
- [x] 1.3 `Room.JoinAsPlayer(userId, now, rules, string? setup)` —— 必填可空,无默认值
- [x] 1.4 开局那一刻校验 `setup` 与 `rules is IDealtGameRules` 一致,两个方向都抛 `MissingGameSetupException`(码 `missing-game-setup`)
- [x] 1.5 坐不满时**不**校验(三人棋种前两次入座)

## 2. Application

- [x] 2.1 `MatchSetup.For(rules, seeds)` —— 一处实现,两个 handler 用
- [x] 2.2 `JoinRoomCommandHandler` / `CreateAiRoomCommandHandler` 各接一行
- [x] 2.3 不需要设置的棋种 MUST NOT 调 `ISeedProvider.NextSeed()` —— 由 `FakeSeeds.Calls` 断言

## 3. Infrastructure

- [x] 3.1 `Games.Setup` 的 EF 映射(可空 TEXT,刻意不加长度上限、刻意不 `IsRequired`)
- [x] 3.2 迁移 `AddGameSetup` —— **核对过** EF 生成的版本,这一次它是对的
- [x] 3.3 迁移里写下"这个 `Down` 为什么不需要守卫"的理由

## 4. 断言

- [x] 4.1 反射断言:`Gewu.Application.Common.DTOs` 下任何类型都不得有名字含 `Setup` 的成员,**带正控制**
- [x] 4.2 遍历 `BuiltInGameRules.All` 断言现有棋种都不实现 `IDealtGameRules`
- [x] 4.3 `Game.Setup` 的落库往返 + 直接问 `pragma_table_info` 确认那一列真的可空
- [x] 4.4 42 处 `JoinAsPlayer` 调用点(24 个文件)补 `setup: null`

## 5. 验证

- [x] 5.1 `dotnet test Gewu.slnx` 全绿 —— 1186(Domain 779 / Application 283 / Infrastructure 124)
- [x] 5.2 前端零改动
- [x] 5.3 变异测试 8 条
- [x] 5.4 `openspec validate --strict` 通过

## 6. 实现记录

### 内核收的是**字符串**,不是种子 —— 三个理由,一个是测试

`JoinAsPlayer` 本来可以收一个 `int seed` 让规则在 Domain 内部发牌。不那样做:

1. 内核完全不必知道"有一个随机源"。它存一个不透明字符串,由规则造、由规则读。
2. 熵的来源**已经有了**:`ISeedProvider`(俄罗斯方块为同一件事建的,连"为什么与 `IAiRandomProvider` 分开"的理由都写在它上面)。它在 Application 层,那正是依赖方向允许拿到随机源的地方。
3. **测试因此可复现。** 斗地主的整局测试将来传 `DoudizhuDeal.FromSeed(20260819).Encode()`,而不是"发了什么算什么"。一个不能重跑的失败测试是模糊测试,不是回归测试 —— 而这一条是决定性的:如果种子在 Domain 内部生成,那局测试就只能读它自己拿到的牌。

### 两个方向都抛,而第二个方向更容易被认为多余

「要设置却没给」显然要抛。「不要设置却给了」也抛,理由是:那个调用方拿着一个错误的心智模型,而那份设置会被存下来**再也没人读** —— 一个永远不会被观察到的错误状态,是最贵的那种。

校验发生在**开局那一刻**,不是每一次入座。否则三人棋种的前两次入座都得携带一份最终会被丢掉的设置,而那份设置的存在会误导下一个读代码的人。这一条单独有一条测试(坐不满时传 `null` 不抛)。

### EF 这一次生成对了 —— 而这句话是核对出来的

`AddColumn<string>(nullable: true)`,没有 `defaultValue`,`Down` 直接删列。**这是本仓库第一个可以直接采用的迁移。**

前面四次各自错在不同的地方:`AddRoomGameKey` 的 `defaultValue: ""`、`DropUserRatingColumns` 的 `defaultValue: 0`、`AddRoomSeats` 的 drop-before-create、`RenameMoveStoneToSeat` 与 `RemapGameResultValues` 的值位移隐形。所以**核对本身仍然是必要的那一步** —— 变的只是这一次的结论。三条断言把核对固定下来(其他列不变、新列可空、回滚干净)。

`Down` 没有守卫,而**这个理由写进了迁移**:`Setup` 的唯一读者是需要它的那个棋种的规则,而回滚到本迁移之前意味着那个棋种在这个构建里还不存在,所以不可能有非 `NULL` 的行需要保护。下一个人只会看到"这个 `Down` 没有守卫",而无从知道那是核对过的结论还是漏掉的。**并且这条理由本身有一条断言**:此刻没有任何内置棋种会产生非 `NULL` 的 `Setup`;斗地主落地那天它会红,而那正是该重新想一遍这个 `Down` 的时刻。

### 那条反射断言带正控制

`No_dto_exposes_anything_named_setup` 在"一个类型都没扫到"时同样会通过,而那种通过什么都没证明。所以旁边有一条 `The_dto_namespace_is_actually_populated`。

这个仓库刚在 `add-game-sounds` 里付过这个账:一次 `tsc` 探针因为编译了零个文件而"通过",是**正控制的通过**暴露了它。

### 一次走错的路:internal 的东西不该为了测试打开

第一版的 `MatchSetupTests` 直接调 `MatchSetup.For` —— 而它是 `internal`,`Gewu.Application.Tests` 看不见。当时的选项是给 `Gewu.Application.csproj` 加 `InternalsVisibleTo`。

没那么做:为了测一个两行的辅助函数把整个 Application 的内部打开给测试程序集,是把测试的便利换成了封装。改成走 `JoinRoomCommandHandler`,与 `GameEloApplier` 一直以来的测法一致,而且这样测到的是**真实路径** —— 包括"handler 真的把那个字符串传给了聚合"这一段,直接调 helper 是测不到的。

### 变异结果

```
RED  Room 不再拒绝「要设置却没给」
RED  Room 不再拒绝「不要设置却给了」
RED  Game 收下设置却不存
RED  MatchSetup 永远不造设置
RED  MatchSetup 无条件取一个种子
RED  EF 把 Setup 配成必填
RED  迁移把列配成非空 + 空串默认值(AddRoomGameKey 犯过的那个错)
RED  DTO 上出现一个 Setup 成员
```

### 记在案:32 位种子对隐藏信息的棋种偏窄

`DoudizhuDeal.FromSeed(int)` 的种子空间是 2³²。一个改过的客户端知道算法、又看得见自己的 17 张牌,可以枚举 40 亿个种子找出与自己手牌相符的那一个,从而算出另两家的牌 —— 一台笔记本上这是**可行的**。

不是本变更引入的,也不是本变更能修的。但本变更的形状让将来能修:**存的是 `Encode()` 的结果,不是种子**,所以换一个更宽的或非种子式的洗牌只动 `DoudizhuDeal` 内部,已存的对局一个字节都不用改。留给 `add-doudizhu`。
