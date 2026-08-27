# play-from-position

从一则古谱残局开始,两个人对弈。

## Why

用户要的三件事里,这是第二件。**它比看起来便宜,而便宜的理由是平台早就为发牌铺好了同一条路。**

### 已经有的(量过的,不是推的)

```csharp
public readonly record struct MatchState(string? Setup, IReadOnlyList<PlayedMove> History);
```

- `Game.Setup` 是随本局存下、对内核**不透明**的一段设置,由规则读;
- `Room` 在坐满那一刻:`needsSetup = rules is IDealtGameRules` → 要设置;
  然后 `firstSeat = (rules as IFirstSeatRules)?.FirstSeat(new MatchState(setup, []))` ——
  **先手座位由设置推出**;`Game` 本来就收调用方给的 `firstSeat`。
- 斗地主与挖坑把这两条都跑通了。

所以「这一局从哪个局面开始」与「谁先走」在内核层面**都已经有位置**。而残局需要的正是这两样:1634 局里 **7 局是黑先走**。

### 唯一必须动的规则代码

`XiangqiRules.Replay(history)` 从 `XiangqiBoard.Initial()` 起,**一个字都不读 `state.Setup`**。这是整件事的全部技术难点。

而 `LegalMoves(history, side)` 只有 `XiangqiAi` 一个调用者 —— **人人对弈不经过它**,所以那个只收历史的签名在这个变更里不挡路(它是「和机器对弈」那件事的账,见 Non-goals)。

### 而内核有一条不变量挡在这里,它是这个提案的核心

`Room` 用**类型**判断要不要设置,并且**两个方向都抛**:

- `IDealtGameRules` 却没给设置 → 抛;
- 不是 `IDealtGameRules` 却给了设置 → 抛,理由写在代码里:「一个把设置传给不需要设置的棋种的调用方,拿着一个错误的心智模型」。

**象棋「有时要、有时不要」会破坏它。** 三条路,而前两条要拒:

1. **让象棋实现 `IDealtGameRules`,`CreateSetup(seed)` 返回标准开局。** 拒:那个接口自己的文档写着「**骗人的实现是下一个人删不掉的东西**」,而一个忽略种子的 `CreateSetup` 正是这种实现。
2. **把设置改成可选。** 拒:那会同时删掉上面两个方向的检查,而它们各自都在防一个真实的错误心智模型。
3. **残局是一个独立的棋种键 `xiangqi-endgame`。** 取这条。

### 为什么第三条是对的,而不只是省事

- **内核的不变量一个字不改** —— 新键**总是**要设置,老键**从不**要,两个方向的检查都还在;
- **「不计分」因此是诚实的**:残局不是一局公平的棋 —— 有一方按构造就是赢的,给它算 ELO 是在给一个已知结局的局面发分;
- 它兑现的正是平台自己的承诺:**加一个棋种是注册表里加一行**。

**代价说清楚:** 走子合法性**必须共用**,不能复制一份 —— 复制品会和 `XiangqiRules` 各自漂,而漂的表现是「同一步棋在两个房间里一个合法一个不合法」。

### 新的接缝,而它与发牌是**并列**的两种

- `IDealtGameRules`:设置由**规则**从种子**生成**;
- `IPositionalStartRules`(新):设置由**调用方选定**,而规则负责**校验**它。

两者都保持「设置存在 ⇔ 棋种说要」,所以 `Room` 的判断从「是不是 `IDealtGameRules`」变成「是不是这两者之一」——**不是变成可选**。

### 局面从哪来:客户端报**线路 id**,不报盘面

建房时带的是一个古谱线路 id,服务端去 `XiangqiManualLines` 取那条的起始局面与先走方。**客户端 MUST NOT 递一个盘面** —— 那等于让客户端定义棋局,而那是一个不需要开的口子。

## What Changes

- 新接口 `IPositionalStartRules`:`ValidateSetup(string setup)`,由 `Room` 在开局那一刻调用。
- `Room` 的 `needsSetup` 改成「`IDealtGameRules` 或 `IPositionalStartRules`」,**两个方向的抛都保留**。
- 象棋的走子逻辑抽成共享的一份,由 `XiangqiRules`(标准开局)与新的 `XiangqiEndgameRules`(从设置开局)共用;后者实现 `IPositionalStartRules` + `IFirstSeatRules`,`IsRated => false`。
- `XiangqiRules` 自己**不变** —— 它仍然从标准开局重放,所以既有的一千多条象棋测试是这次「共享没有改行为」的可执行形式。
- 建房命令多一个**可选**的古谱线路 id;给了它就必须是 `xiangqi-endgame`,反之亦然(两个方向都校验)。
- 前端:房间页与大厅按「象棋族」而不是单一键挑棋盘;从学习页多一个「摆此局对弈」的入口。

## 会红的、而且是**故意**的

`The_unrated_games_are_tictactoe_doudizhu_and_wakeng` 断言的是不计分棋种的**恰好**集合。加 `xiangqi-endgame` 会让它红 —— 这正是本仓库写「恰好」而不是「至少」的理由:**第二个同类出现的那一刻,该有人来问这两个需求是不是同一件事。**

答案是:不是。一字棋不计分是因为它必和;残局不计分是因为**开局就不公平**。两条理由都要写进那条测试。

## Non-goals

- **和机器对弈**:`IBoardGameAi.SelectMove` 只收走子历史(六个实现),从残局出发 AI 会按标准开局重建棋盘。那是它自己的变更。
- **任何「你解对了」的判定**:领域里没有重复局面 / 长将 / 长捉规则。**而这条在人人对弈里有一个具体后果,必须写在界面上**:一则「和棋」题下成和局时,平台**认不出来** —— 这一局只会以将死、认输或超时结束。两个人可以自己商量,但**平台不会宣布和棋**。
- ELO、排行榜、战绩:残局房不计分,所以它不进任何一条阶梯。
