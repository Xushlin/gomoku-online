## Context

`Gomoku.Domain` 是 Clean 架构最内层,整个后端的命脉。本次要从零搭出棋盘/落子/判胜三件事。决定一旦落地,会被上层(Application 的 `MakeMoveCommand`、SignalR Hub 的推送、AI 的搜索树)反复调用,**判胜函数尤其会进入 AI 搜索的热路径**,所以性能不能随手写。

当前状态:`Gomoku.Domain.csproj` 只引用 `net10.0` 基类库,无任何第三方包。铁律:Domain 层永远不引外部 NuGet。

## Goals / Non-Goals

**Goals**

- 定义一组**不可变的**值对象(`Position`、`Move`、`GameResult`)和一个**可变的**聚合实体(`Board`),边界清楚。
- 落子和判胜的合法性校验**在 Domain 层一次性把关**,上层无需重复。
- 判胜在"下一步棋"的场景下是 **O(1)** (只检查刚落子的那颗周围四个方向),而不是 O(n²) 全盘扫描。
- 测试覆盖五连的全部四个方向与所有边界,包括刚到 5 子、超过 5 子(长连)的判定。
- 输出的类型与 API 表面是稳定的 —— 下个变更 (`add-application-core`) 直接 `using`,不需要再调整。

**Non-Goals**

- 禁手规则(三三 / 四四 / 长连禁手) —— 用一个可插拔接口或独立变更承接,本次不做。
- 计时、认输、悔棋、平局协商。
- 棋手身份、对局归属 —— `Move` 只知道 `Stone` 的颜色,不知道是谁下的。
- 棋谱序列化(PGN/SGF 之类) —— 留给 `add-game-persistence`。
- 并发安全 —— `Board` 按约定在单线程下使用(一次对局一个实例,Application 层用 handler 串行处理)。不加锁。

## Decisions

### D1. `Position` / `Move` / `GameResult` 用 C# `readonly record struct`

- **为什么**:值对象的语义就是"值相等 + 不可变"。`record struct` 自带结构相等 + `with` 表达式 + 零堆分配,判胜热路径每步会产生很多 `Position`,值类型可避免 GC 压力。`readonly` 防意外变更。
- **备选**:`class` + 重写 `Equals`/`GetHashCode` —— 代码多、易漏、堆分配。否决。
- **约束**:构造函数里做范围校验(0 ≤ Row,Col ≤ 14);非法值抛 `InvalidMoveException`。

### D2. `Board` 内部存储:一维 `Stone[225]`

- **为什么**:15×15=225 的一维数组比二维 `Stone[15,15]` 访问更快,CPU 缓存更友好;判胜里沿方向走步也是 +1 / +15 / +14 / +16 四个 stride,算法写起来更直接。
- **备选**:`Dictionary<Position, Stone>` —— 可读但慢、分配多,否决;`Stone[,]` —— 稍慢且没有 `Span` 友好度,否决。
- **索引公式**:`index = row * 15 + col`。封装成私有方法,外部只见 `Position`。

### D3. 判胜:**增量判定**,只检查刚落子的那颗

- **为什么**:`Board.PlaceStone` 每次被调用时只多了一颗新子,五连要么包含它要么不包含它 —— 不包含就意味着上一手已经赢了,矛盾。所以每步只需以新子为中心,沿 4 个方向(水平、竖直、两对角)两边延伸,数同色连续子数,任一方向 ≥ 5 即判胜。**最坏 4 × 2 × 4 = 32 次数组读**,O(1)。
- 返回值:`GameResult`(`BlackWin` / `WhiteWin` / `Ongoing`)。平局判定是另一条路径 —— 棋盘满且没人赢,返回 `Draw`。
- **备选**:全盘扫描 —— 每步 O(n²),AI 搜索一层几万个节点时性能不可接受。否决。
- **长连策略**:≥ 5 即赢(基础规则)。这样写的好处是禁手规则未来作为"外部校验器"嵌入时可以只管"不许下",不必改判胜逻辑。

### D4. `Board.PlaceStone(Move)` 签名返回 `GameResult`

- **为什么**:落子和判胜是同一个原子动作(放子 + 立即判),分开调用容易漏。让 `PlaceStone` 负责原子化:校验 → 放子 → 判胜 → 返回结果。调用方拿 `GameResult` 就知道该不该推进对局。
- **备选**:`void PlaceStone` + 另有 `CheckWinner()` —— 调用方可能忘记第二步。否决。

### D5. `Clone` 做**深拷贝**

- **为什么**:AI 搜索会在不同分支上反复"试走"。必须要求 `Clone()` 返回一块独立的棋盘,对它的改动不影响原棋盘。因为内部就是一个一维数组,`Array.Copy` 一次即可,成本极低。
- **备选**:暴露只读视图 —— 对 AI 不够用(AI 要实际试走)。否决。

### D6. 错误用异常 `InvalidMoveException`,不用 `Result<T>` / `bool` 返回

- **为什么**:非法落子在 Domain 层就是"程序错误或用户恶意" —— 客户端理应先校验,SignalR 层也会再校验一次。抛到 Domain 才触发 `InvalidMoveException`,Application 层统一捕获,转成 HTTP 409 / SignalR 错误。用异常承载是最 C# 的写法,也让 Domain API 保持简单。
- 异常信息要**带上原因和上下文**(例如 `"Position (15, 3) is out of board bounds [0..14]"`),方便排查与前端错误消息展示。
- 不引入 `Result<T>` 模式 —— 对这个规模是过度设计,上层会变啰嗦。

### D7. `GameResult` 用枚举而非子类 / 判别联合

- 只有四种终局状态、无额外负载数据,枚举最简单。若未来要附加"胜者 id""胜利线的坐标列表"等,再升级为 `readonly record struct` 包装。

### D8. 测试框架:现有 `xUnit` + 新增 `FluentAssertions`

- **为什么**:断言可读性是测试维护成本的关键变量。`board.Winner.Should().Be(Stone.Black)` 比 `Assert.Equal(Stone.Black, board.Winner)` 好得多。FluentAssertions 是 MIT 许可、无运行时副作用,只在测试工程引用,不会污染 Domain。
- 本变更顺带把 `FluentAssertions`(最新稳定版)加到 `Gomoku.Domain.Tests.csproj`。

## Risks / Trade-offs

- **长连即赢不符合所有玩家习惯** → 把判胜阈值做成 `private const int WinLength = 5`,未来禁手变更里可以加可注入的 `IWinCondition`。本次不抽象,避免过早设计。
- **增量判胜只对"通过 `PlaceStone` 进入"的棋盘成立** → 如果未来引入"从棋谱还原 `Board`"的场景(例如观战者中途加入需要回放),需要提供一个一次性的全盘扫描 API。在 specs 里把这个假设写死为前置条件,后续变更显式放宽。
- **`record struct` 的值拷贝在热路径可能产生隐性成本** → `Position` 就 2 个 `int`,8 字节,常见架构下通过寄存器传递,不会比 `class` 慢。已在决策中权衡过,保留决策。
- **`InvalidMoveException` 在 AI 搜索中如果被频繁触发会很贵**(异常开销)→ AI 在调用 `PlaceStone` 前自己已经只生成合法走法集合,不会踩异常路径。这是 AI 层的约定,不是 Domain 的问题,但要在 spec 的"使用约定"里写明:**AI 与 Application 层应在调用前校验,避免用异常控制流程**。
- **15 × 15 的 `Stone` 数组初始化**:默认值 `Stone.Empty` 必须是枚举的 `0` 值 —— 这是一个显式约束,枚举定义时写清楚,测试里覆盖。

## Migration Plan

零影响:没有正在运行的代码、没有数据、没有 API 契约要兼容。按 tasks.md 顺序提 PR 即可,PR 合入后 `Gomoku.Application`(下个变更)可以开始引用。

## Open Questions

- 禁手规则做成独立变更还是做成 `add-domain-core` v2?**倾向独立变更**(职责单一、好审查)。本次不决策,只确保 `Board` 的 API 允许未来**在 `PlaceStone` 之外**插入前置校验器(比如传入 `IWinCondition` 接口或 `IMoveValidator`);具体插点等那个变更来定。
