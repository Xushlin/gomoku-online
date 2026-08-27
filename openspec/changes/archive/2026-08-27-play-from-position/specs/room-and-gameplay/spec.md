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

### Requirement: 选定的设置**下发**,发牌的设置**不下发**

一局棋的设置有两个落点,而它们的可见性**相反** —— 一份 SHALL 下发,另一份 MUST NOT 下发:

- `Room.ChosenSetup` —— 建房时由调用方选定的那一份,SHALL 出现在房间状态里;
- `Game.Setup` —— 规则从种子发出来的那一份,MUST NOT 出现在任何 DTO 上。

**担保来自「它是哪个字段」,而不是来自一次判断。** 前者只由 `Room.CreateFromPosition` 写,而那个方法只收 `IPositionalStartRules`;那种设置按定义来自**公开的**资料(客户端递的是一个古谱线路 id,而那条线路的起始局面**匿名就能读到**)。所以不下发它保护不了任何东西 —— 只是让客户端画不出那块残局,而**画不出的表现是把残局那几手棋叠在一副标准开局上**,即一个看起来完全正常的错盘面。

下发它是必须的,而不是方便:**等待中的房间也要给** —— 房主坐在自己刚摆的残局房里,看到的必须是那一局。

#### Scenario: 发牌的棋种不通过这个字段漏牌
- **WHEN** 一个 `IDealtGameRules` 房间的状态被投影给任何座位或围观者
- **THEN** 「选定的设置」MUST 是空;而这一局**确实有**一份发出来的设置(否则该断言恒真)

#### Scenario: 选定式的房间在开局前就带着局面
- **WHEN** 一个 `IPositionalStartRules` 房间还在 Waiting
- **THEN** 房间状态 MUST 已经带着那份选定的设置

#### Scenario: 带 setup 名字的 DTO 成员是一份恰好的名单
- **WHEN** 反射走查 DTO 命名空间
- **THEN** 名字含 `setup` 的公开成员 MUST **恰好**是那份豁免名单;且 `GameSnapshotDto` 上 MUST 一个都没有

### Requirement: 从选定局面开的房间要**找得到**

一个第二个人找不到的房间等于没开。残局在服务端是一个独立的棋种键,而它 MUST NOT 因此变成一个独立的大厅:开一间残局房**必须先选一则残局**,所以那个大厅的「创建房间」按钮无从下手。

因此:一个游戏的 manifest SHALL 能声明**伴生棋种键**,而它的大厅 SHALL 把那些键的房间一并列出。判据 SHALL 取自 manifest,而 MUST NOT 是一句写在大厅里的棋种键比较 —— 后者在第二个这样的棋种落地时不会红。

没有声明伴生键的游戏 MUST 只发一次房间列表请求。**一个每次轮询都多打一次、结果永远是空数组的端点,只会在网络面板里露面**,而没有任何断言会红。

#### Scenario: 象棋大厅列出残局房
- **WHEN** 打开象棋大厅
- **THEN** 请求的棋种键 MUST 恰好是 `xiangqi` 与残局那个键,两份列表合成一份

#### Scenario: 没有伴生键的游戏只问一次
- **WHEN** 打开任何没有声明伴生键的游戏大厅
- **THEN** 房间列表请求 MUST 恰好一次

#### Scenario: 请求数从 manifest 推,而两支都在样本里
- **WHEN** 走查注册表里每一个可玩的游戏
- **THEN** 请求的键 MUST 等于「自己的键 + 声明的伴生键」;且有伴生键的与没有的 MUST 都出现在样本里

#### Scenario: 伴生键的房间行不是一块空白
- **WHEN** 大厅列出一间伴生键的房间
- **THEN** 它的纹章 MUST 与主棋种的一致 —— 一个查不到的键会画出空数组,而那**不抛、不报、不红,只是不见**
