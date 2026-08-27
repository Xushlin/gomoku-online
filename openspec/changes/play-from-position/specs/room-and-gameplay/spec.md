## ADDED Requirements

### Requirement: 设置可以由**调用方选定**,而不只是由规则从种子生成

平台 SHALL 有两种、且**并列**的设置来源:

- `IDealtGameRules` —— 设置由**规则**从种子**生成**(发牌);
- `IPositionalStartRules`(本变更新增)—— 设置由**调用方选定**,而规则负责**校验**它。

`Room` 在开局那一刻的判断 SHALL 从「是不是 `IDealtGameRules`」改成「是不是这两者之一」,而 **MUST NOT 改成「设置是可选的」**。两个方向的抛 MUST 都保留:

- 说要设置却没给 → 抛;
- 说不要设置却给了 → 抛。

**理由是那两个检查各自都在防一个真实的错误心智模型**,而把设置改成可选会同时删掉它们。既有代码里第二个方向的注释写着:「一个把设置传给不需要设置的棋种的调用方,拿着一个错误的心智模型,而那份设置会被存下来再也没人读」。

`IPositionalStartRules` 的校验 MUST 在**存下来之前**发生 —— 一份存进去才发现不合法的设置,表现是这一局谁都动不了,而那要等到几十秒后超时才暴露(与 `IFirstSeatRules` 越界那条同一个理由)。

#### Scenario: 选定式棋种没给设置就抛
- **WHEN** 一个 `IPositionalStartRules` 棋种坐满,而没有设置
- **THEN** MUST 抛,与 `IDealtGameRules` 缺设置时同一个异常语义

#### Scenario: 不要设置的棋种给了设置仍然抛
- **WHEN** 一个两者都不是的棋种坐满,而调用方给了设置
- **THEN** MUST 抛 —— 新增的分支 MUST NOT 让这个方向变成放行

#### Scenario: 设置不合法时不开局
- **WHEN** 一个 `IPositionalStartRules` 棋种收到一份校验不通过的设置
- **THEN** MUST 抛并报出**为什么**不合法;房间 MUST 留在 Waiting,`Game` MUST NOT 被创建

#### Scenario: 两种来源都要在样本里
- **WHEN** 测试遍历内置棋种注册表
- **THEN** `IDealtGameRules` 与 `IPositionalStartRules` MUST 各至少有一个实现,否则「两种来源」这条在单一种类上恒真

### Requirement: 从选定局面开局的房间不计分

`IsRated` 为真的棋种 MUST NOT 同时是 `IPositionalStartRules`。

理由不是工期:一则残局**按构造就不公平** —— 有一方是赢定的,那是谱主设计它的方式。给这样的局面算 ELO,是在给一个已知结局的局面发分。

这条与既有的 `IsRated ⇒ SeatCount == 2` 并列,而 SHALL 由**同一条遍历注册表的测试**守着 —— 一条写在文档里的约定不会在有人加第八个棋种时红。

#### Scenario: 注册表走查
- **WHEN** 遍历内置棋种
- **THEN** 任何 `IPositionalStartRules` 的棋种 MUST 是不计分的

#### Scenario: 不计分的理由不止一种,而测试要说清楚
- **WHEN** 断言不计分棋种的集合
- **THEN** 该断言 MUST 是**恰好**的集合,且 MUST 为每一个成员写下它不计分的**理由** —— 一字棋是「必和」,残局是「开局就不公平」,而两条理由不同这件事正是「恰好」在第二个同类出现时该问的问题
