## MODIFIED Requirements

### Requirement: `IGameRules` 描述一个棋种的盘面属性

`Gewu.Domain` SHALL 定义 `IGameRules`:

- `GameKey` —— 棋种键,与房间的 `GameKey`、游戏注册表中的 key 一致。
- `Rows` / `Cols` / `WinLength` —— 盘面尺寸与连子长度。
- `SupportsHumanVsHuman` —— 本棋种是否存在人类对手池(平台是否提供人人对战入口)。
- `IsRated` —— 本棋种的对局是否结算 ELO。
- `CreateBoard()` —— 造一块该棋种的空棋盘。
- `IsInBounds(Position)` —— 该坐标是否在本棋种界内。

实现 MUST 是无状态的:同一个实例会被并发的多个房间共享,MUST NOT 持有任何对局状态。

`Domain` MUST NOT 因此获得任何外部依赖 —— 规则由调用方传入聚合,注册表住在 `Infrastructure`。

**不变量:`IsRated` 为 `true` 时 `SupportsHumanVsHuman` MUST 也为 `true`。** 一个只能跟机器人下的
棋种不存在有意义的评分:机器人对局是计分的(见 `ai-opponent` 的反套利约束),所以它的阶梯排出来的
是"谁刷弱档刷得多",而不是棋力。本不变量 MUST 由一条遍历注册表的测试强制,不能只写在文档里。

`SupportsHumanVsHuman` 是**结构性事实**,`IsRated` 是**判断**。这样分是因为判断会过期而不报错:
`IsRated` 原本的语义是"要不要给这个棋种算分",于是"一字棋将来有了人人对战要记得回来翻它"成了
一件依赖记性的事。改成受不变量约束之后,一字棋的 `IsRated` 只能是 `false` —— 不是谁的判断,
是被结构逼出来的;而它获得人人对战那天,翻 `SupportsHumanVsHuman` 会把评分从**禁止**变成**允许**,
开不开则是一个独立的、需要理由的决定。

平台 MUST NOT 增加 `SupportsAi` 之类的声明 —— 该问题由 `IGameAiRegistry.For(gameKey)` 是否解析出
工厂回答,再加一个字段就是第二份真源。

本接口承载的"平台能力"声明超过三个时,SHOULD 抽成独立的 `GameCapabilities` 类型,使 `IGameRules`
回到只描述盘面。

#### Scenario: 五子棋的盘面属性
- **WHEN** 读取 `gomoku` 规则
- **THEN** `Rows == 15`、`Cols == 15`、`WinLength == 5`

#### Scenario: 规则可安全共享
- **WHEN** 两个房间同时用同一个规则实例落子
- **THEN** 两局互不影响 —— 规则实例上 MUST NOT 出现任何随对局变化的字段

#### Scenario: 五子棋计分
- **WHEN** 读取 `gomoku` 规则
- **THEN** `IsRated == true` 且 `SupportsHumanVsHuman == true`

#### Scenario: 一字棋无人类对手池,因此不计分
- **WHEN** 读取 `tictactoe` 规则
- **THEN** `SupportsHumanVsHuman == false`,因此 `IsRated == false`

#### Scenario: 不变量被测试强制
- **WHEN** 遍历注册表中每一个 `IGameRules`
- **THEN** 每一个满足 `IsRated == false || SupportsHumanVsHuman == true`;构造一个违反此式的规则实例 MUST 失败

#### Scenario: 是否支持人机由 AI 注册表回答
- **WHEN** 检视 `IGameRules` 的成员
- **THEN** MUST NOT 存在 `SupportsAi` 或同义字段;"该棋种有没有 AI"MUST 由 `IGameAiRegistry.For` 解析

### Requirement: `NInARowRules` 以参数覆盖全部"连 N 子"棋种

`Gewu.Domain` SHALL 提供 `NInARowRules(gameKey, rows, cols, winLength, supportsHumanVsHuman = true, isRated = true)`,实现 `IGameRules`。

五子棋 SHALL 注册为 `NInARowRules("gomoku", 15, 15, 5)`。一字棋 SHALL 注册为 `NInARowRules("tictactoe", 3, 3, 3, supportsHumanVsHuman: false, isRated: false)` —— 判胜算法完全相同,只有三个数不同,因此 MUST NOT 为其另写一份实现。

两个布尔的默认值 SHALL 均为 `true` —— 一个棋种默认有人类对手池且算分,**不**是那一侧才需要在调用处写出理由。

构造器 MUST 校验不变量 `IsRated ⇒ SupportsHumanVsHuman`,违反时抛 `ArgumentException` —— 在构造处失败,而不是等到某个 handler 算出一个没人该看的分数。

#### Scenario: 同一实现服务不同尺寸
- **WHEN** 分别以 `(15,15,5)` 与 `(3,3,3)` 构造
- **THEN** 两者都是合法的 `IGameRules`,各自的 `IsInBounds` 与判胜按自己的参数工作

#### Scenario: 参数校验
- **WHEN** 以非正的 `rows` / `cols` / `winLength`,或 `winLength` 大于 `max(rows, cols)` 构造
- **THEN** 构造失败 —— 一个永远赢不了的棋种是配置错误,不是可玩的棋

#### Scenario: 一字棋不另写判胜
- **WHEN** 检视一字棋的规则实现
- **THEN** 它 MUST 是 `NInARowRules` 的一个实例,`Gewu.Domain` 中 MUST NOT 存在名为 `TicTacToeRules` 的独立判胜实现

#### Scenario: 拒绝一个没有人类对手却要计分的棋种
- **WHEN** 以 `supportsHumanVsHuman: false, isRated: true` 构造
- **THEN** 抛 `ArgumentException`

#### Scenario: 默认是有人类对手且计分
- **WHEN** 以 `NInARowRules("some-new-game", 3, 3, 3)` 构造
- **THEN** `SupportsHumanVsHuman == true` 且 `IsRated == true`
