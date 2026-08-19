# game-rules-registry Specification Delta

## MODIFIED Requirements

### Requirement: `IGameRules` 描述一个棋种的盘面属性

`Gewu.Domain` SHALL 定义 `IGameRules`:

- `GameKey` —— 棋种键,与房间的 `GameKey`、游戏注册表中的 key 一致。
- `SeatCount` —— 本棋种需要几个座位。**本次新增**,现有实现全部为 2。
- `SupportsHumanVsHuman` —— 本棋种是否存在人类对手池(平台是否提供人人对战入口)。
- `IsRated` —— 本棋种的对局是否结算 ELO。
- `Apply(history, intent, seat)` —— 判定并应用一手,出手方以**座位号**给出。

**本条此前还把 `Rows` / `Cols` / `WinLength` / `CreateBoard()` / `IsInBounds(Position)` 列在 `IGameRules` 名下,那是过期的。** `generalize-match-domain` 把 `WinLength` / `CreateBoard` 下沉到 `INInARowRules`,`generalize-match-payload` 又把 `Rows` / `Cols` 下沉到 `IBoardGameRules`(理由是成语接龙没有棋盘,而把 `0,0` 当"不适用"是本内核明令禁止的),但这条 requirement 一直没跟上。顺手改正,不是本变更的新决定 —— 与 `enable-xiangqi-human-play` 顺手删掉 `board: { rows, cols }` 是同一类修正。

层级因此是:`IGameRules`(所有棋种)→ `IBoardGameRules`(有棋盘的,带 `Rows` / `Cols`)→ `INInARowRules`(连 N 子的,带 `WinLength` / `CreateBoard`)。

实现 MUST 是无状态的:同一个实例会被并发的多个房间共享,MUST NOT 持有任何对局状态。

`Domain` MUST NOT 因此获得任何外部依赖 —— 规则由调用方传入聚合,注册表住在 `Infrastructure`。

**不变量一:`IsRated` 为 `true` 时 `SupportsHumanVsHuman` MUST 也为 `true`。** 一个只能跟机器人下的棋种不存在有意义的评分:机器人对局是计分的(见 `ai-opponent` 的反套利约束),所以它的阶梯排出来的是"谁刷弱档刷得多",而不是棋力。

**不变量二:`IsRated` 为 `true` 时 `SeatCount` MUST 等于 2。** 现有 ELO 是两人制的,三人及以上的评分需要单独设计。这条与不变量一同源同形:把"三人局暂不计分"钉成**结构性约束**,而不是留在注释里的 TODO —— 后者这个仓库付过账(`add-game-capabilities` 就是把一个手工维护的布尔约束成结构性事实的那次)。三人评分真设计出来那天,动作是**改这条不变量**,而不是希望有人记得回来翻某个布尔。

两条不变量 MUST 在构造器中校验,违反时抛 `ArgumentException` —— 在构造处失败,而不是等到某个 handler 算出一个没人该看的分数。两条都 MUST 由遍历注册表的测试强制,不能只写在文档里。

`SupportsHumanVsHuman` 是**结构性事实**,`IsRated` 是**判断**,`SeatCount` 是**棋种形状**(与 `Rows` / `Cols` 同类,不是平台能力)。这个区分决定了下面那条阈值怎么数:本接口承载的**平台能力**声明仍是两个(`SupportsHumanVsHuman`、`IsRated`),`SeatCount` 不计入。超过三个时 SHOULD 抽成独立的 `GameCapabilities` 类型,使 `IGameRules` 回到只描述棋种本身。

平台 MUST NOT 增加 `SupportsAi` 之类的声明 —— 该问题由 `IGameAiRegistry.For(gameKey)` 是否解析出工厂回答,再加一个字段就是第二份真源。

#### Scenario: 五子棋的棋种属性
- **WHEN** 读取 `gomoku` 规则
- **THEN** `SeatCount == 2`;作为 `INInARowRules` 时 `Rows == 15`、`Cols == 15`、`WinLength == 5`

#### Scenario: 现有棋种一律两个座位
- **WHEN** 遍历注册表中每一个 `IGameRules`
- **THEN** 每一个 `SeatCount == 2` —— 本变更行为零变化,这条是它的可执行形式

#### Scenario: 规则可安全共享
- **WHEN** 两个房间同时用同一个规则实例落子
- **THEN** 两局互不影响 —— 规则实例上 MUST NOT 出现任何随对局变化的字段

#### Scenario: 五子棋计分
- **WHEN** 读取 `gomoku` 规则
- **THEN** `IsRated == true` 且 `SupportsHumanVsHuman == true` 且 `SeatCount == 2`

#### Scenario: 一字棋无人类对手池,因此不计分
- **WHEN** 读取 `tictactoe` 规则
- **THEN** `SupportsHumanVsHuman == false`,因此 `IsRated == false`

#### Scenario: 两条不变量都被测试强制
- **WHEN** 遍历注册表中每一个 `IGameRules`
- **THEN** 每一个满足 `IsRated == false || SupportsHumanVsHuman == true`,且满足 `IsRated == false || SeatCount == 2`;构造一个违反任一式的规则实例 MUST 失败

#### Scenario: 拒绝一个三座位却要计分的规则
- **WHEN** 构造一个 `SeatCount == 3` 且 `IsRated == true` 的规则实例
- **THEN** 抛 `ArgumentException`,消息点明是哪条不变量

#### Scenario: 是否支持人机由 AI 注册表回答
- **WHEN** 检视 `IGameRules` 的成员
- **THEN** MUST NOT 存在 `SupportsAi` 或同义字段;"该棋种有没有 AI" MUST 由 `IGameAiRegistry.For` 解析
