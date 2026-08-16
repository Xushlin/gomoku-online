# xiangqi Specification

## Purpose
TBD - created by archiving change add-xiangqi. Update Purpose after archive.
## Requirements
### Requirement: `XiangqiRules` 实现 `IGameRules`，自带盘面表示

`Gewu.Domain` SHALL 定义 `XiangqiRules : IGameRules`，`GameKey == "xiangqi"`，`Rows == 10`，`Cols == 9`。

它 MUST NOT 实现 `INInARowRules` —— 象棋没有「连几子」，也不用 `Board`。棋子表示
（`XiangqiPiece` / `XiangqiBoard`）MUST 内部于规则:聚合根看不到它,这正是
`generalize-match-domain` 抽象的目的。

`Apply` MUST 从走子历史重建局面 —— 与 `NInARowRules` 同一纪律,盘面不冗余存盘。

#### Scenario: 注册进注册表
- **WHEN** 以 `"xiangqi"` 查 `IGameRulesRegistry`
- **THEN** 解析出 `XiangqiRules`，`Rows == 10`、`Cols == 9`

#### Scenario: 不是连 N 子棋种
- **WHEN** 检查 `XiangqiRules` 的类型
- **THEN** 它 MUST NOT 可赋值给 `INInARowRules`

#### Scenario: 聚合根不需要改
- **WHEN** 用 `XiangqiRules` 走一整局
- **THEN** `Room` / `Game` / `Move` MUST 无需任何改动 —— 走子经 `MoveIntent.Slide` 记录，起点落库

### Requirement: 本棋种中 `Stone.Black` 是红方

在 `XiangqiRules` 下，`Stone.Black` SHALL 表示**红方**，`Stone.White` 表示**黑方**。

理由是先手:`Game` 初始化 `CurrentTurn = Stone.Black`,而象棋红先。`Stone` 在 Domain 里的含义
本就是「先手方 / 后手方」,红黑是**显示层**怎么画它 —— 与 `BlackPlayerId` / `WhitePlayerId`
就是两个座位是同一件事。

红方 MUST 位于第 5–9 行(第 9 行是其底线),黑方位于第 0–4 行。楚河汉界在第 4 行与第 5 行之间。

#### Scenario: 红方先走
- **WHEN** 一局新棋开始
- **THEN** `Game.CurrentTurn == Stone.Black`,而按本棋种的读法那是红方

#### Scenario: 黑方不能先走
- **WHEN** `Stone.White` 一方尝试走第一步
- **THEN** 聚合根抛 `NotYourTurnException`

### Requirement: 每种棋子按其走法移动

`Apply` MUST 按下列走法校验，非法一律抛 `InvalidMoveException`：

- **将 / 帅**：上下左右一步，且**不得出九宫**（红 `rows 7–9 × cols 3–5`，黑 `rows 0–2 × cols 3–5`）。
- **士 / 仕**：斜走一步，且不得出九宫。
- **象 / 相**：田字（`|dr| == 2 && |dc| == 2`），**不得过河**，且**塞象眼**（田字中心有子）时不可走。
- **马**：日字（`(2,1)` 或 `(1,2)`），**蹩马腿**（长边方向的相邻格有子）时不可走。
- **车**：直线任意步，**中间不得有子**。
- **炮**：走法同车；**吃子时中间必须恰有一个子**（炮架），**不吃子时中间不得有子**。
- **兵 / 卒**：向前一步；**过河后**方可左右各一步；**永不后退**。

目标格 MUST NOT 是己方棋子。走子形状 MUST 是 `from → to`：`MoveIntent.From` 为 `null` 时
MUST 抛 `InvalidMoveException` —— 象棋是走子类棋种，没有「落子」。

#### Scenario: 马被蹩腿
- **WHEN** 马从 `(9,1)` 走向 `(7,2)`，而 `(8,1)` 有子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 象不过河
- **WHEN** 红方象试图走到第 4 行或更上
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 炮隔子吃
- **WHEN** 炮与目标敌子之间恰有一个子
- **THEN** 走法合法，敌子被吃

#### Scenario: 炮无架不能吃
- **WHEN** 炮与目标敌子之间没有子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 兵不后退
- **WHEN** 兵试图朝本方底线方向走一步
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 兵过河才能横走
- **WHEN** 未过河的兵试图横走一步
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 落子形状被拒
- **WHEN** 以 `MoveIntent.Place(...)`（无起点）调 `Apply`
- **THEN** 抛 `InvalidMoveException`

### Requirement: 自将与将帅照面都是非法走子

一步棋走完后若**本方将帅被攻击**，该步 MUST 非法（抛 `InvalidMoveException`），包括：

- 走子之后本方将帅落入敌子攻击范围（送将 / 自将）；
- 走子之后两将**同列且中间无子**（将帅照面，俗称「飞将」）。

将帅照面 MUST 与「被攻击」同样处理 —— 它在实现上等价于「将帅之间沿该列可以直吃」。

#### Scenario: 送将非法
- **WHEN** 一步棋走完后本方将帅正被敌车照住
- **THEN** 抛 `InvalidMoveException`，且历史不增加

#### Scenario: 照面非法
- **WHEN** 一步棋走完后两将同列且中间无子
- **THEN** 抛 `InvalidMoveException`

#### Scenario: 解将是合法的
- **WHEN** 本方被将军，走一步挡住 / 吃掉将军的子
- **THEN** 走法合法

### Requirement: 将死与困毙都判负

一步棋走完后，若**对方没有任何合法走法**，本方 MUST 获胜：

- 对方被将军且无法解将（**将死**）；
- 对方未被将军但无子可动（**困毙**）。

象棋与国际象棋在这里不同:**困毙判负,不是和棋**。

结果 MUST 是 `GameResult.BlackWin` / `WhiteWin`,由聚合根写入 `GameEndReason.Decided`。

#### Scenario: 将死
- **WHEN** 一步将军之后对方无任何合法走法
- **THEN** `Apply` 返回走子方获胜

#### Scenario: 困毙同样判负
- **WHEN** 对方未被将军但没有任何合法走法
- **THEN** `Apply` 返回走子方获胜 —— MUST NOT 是和棋

#### Scenario: 仅仅将军不结束对局
- **WHEN** 一步将军但对方有解
- **THEN** `Apply` 返回 `Ongoing`

### Requirement: 象棋今天不计分，因为它还没有对手

`XiangqiRules` SHALL 声明 `SupportsHumanVsHuman == false` 与 `IsRated == false`。

这两个值是**结构性事实**而非判断:本变更只做规则,平台还没有任何进入象棋对局的入口
—— 既没有人人对战(要大厅泛化),也没有人机(要 `add-xiangqi-ai`)。

拆除条件与一字棋相同:获得对手入口之后翻 `SupportsHumanVsHuman`,而计不计分是那时一个
**独立的、需要理由的决定**。不变量 `IsRated ⇒ SupportsHumanVsHuman` 保证这件事不靠谁记得。

#### Scenario: 不变量成立
- **WHEN** 遍历注册表检查每个棋种
- **THEN** 每个 `IsRated == true` 的棋种 MUST 同时 `SupportsHumanVsHuman == true`

### Requirement: 内置棋种只有一份清单

`BuiltInGameRules` SHALL 暴露 `All`，含全部内置棋种实例。DI 注册与「遍历注册表」的不变量测试
MUST 都从它取。

**此前有两份清单**:`DependencyInjection` 里逐个 `AddSingleton`,以及测试里手写的
`AllBuiltInRules() => { Gomoku, TicTacToe }`。后者的注释写着「遍历注册表…将来加中国象棋
它自动被覆盖」——**那句话是假的**,数据源是手写的,象棋会静静绕过那个不变量测试。

这正是那条注释自己预言的失效方式,而它预言错了自己的机制。**一份清单**之后,
登记一个棋种只有一个地方。

#### Scenario: 新棋种自动进入不变量测试
- **WHEN** 向 `BuiltInGameRules.All` 添加一个棋种
- **THEN** 不变量测试 MUST 自动覆盖它，无需改测试

#### Scenario: DI 与测试看到同一份清单
- **WHEN** 比较注册表解析出的棋种与 `BuiltInGameRules.All`
- **THEN** 两者 MUST 一致

