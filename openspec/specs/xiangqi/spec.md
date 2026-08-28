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

结果 MUST 是 `GameResult.Decided`,且 `MoveApplication.WinnerSeat` MUST 是**走子方的座位号**,
由聚合根写入 `GameEndReason.Decided` 与对应的 `WinnerUserId`。

此前这里写的是 `GameResult.BlackWin` / `WhiteWin`,由 `side == Stone.Black ? BlackWin : WhiteWin`
算出 —— 那个颜色恒等于 `side`,即规则把自己的入参重新说了一遍。

#### Scenario: 将死
- **WHEN** 一步将军之后对方无任何合法走法
- **THEN** `Apply` 返回 `(Decided, WinnerSeat: 走子方座位)`

#### Scenario: 困毙同样判负
- **WHEN** 对方未被将军但没有任何合法走法
- **THEN** `Apply` 返回 `(Decided, WinnerSeat: 走子方座位)` —— MUST NOT 是和棋

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

### Requirement: 象棋开放人人对战并计分

`XiangqiRules` SHALL 声明 `SupportsHumanVsHuman == true` 与 `IsRated == true`。

**两个值的性质不同,所以理由也不同。**

`SupportsHumanVsHuman` 是**推论**。`enforce-human-vs-human` 给这个字段定的含义是「平台是否提供
人人对战入口」,而判据是行为不是意图:只要 `POST /api/rooms` 接受这个棋种,入口就**确实**存在。
大厅泛化之后 `/g/xiangqi/lobby` 是一个真实可用的页面,象棋走的是同一个 `Room` 聚合、同一套建房
与加入,所以声明只能跟上。反过来也一样 —— 声明与行为不一致时,不一致的是行为。

`IsRated` 是**判断**,而这正是本要求上一版预告过的那个决定:「获得对手入口之后翻
`SupportsHumanVsHuman`,而计不计分是那时一个**独立的、需要理由的决定**」。理由写在这里:
象棋此前不计分的**唯一**依据是「没有对手池,阶梯量不出棋力」,而开放人人对战正好消灭了那条依据。
剩下的形状与五子棋逐项相同 —— 有真人对手池、也有 AI,而机器人对局计分是 `ai-opponent` D7 的
反套利规则,不是漏洞。

不变量 `IsRated ⇒ SupportsHumanVsHuman` 仍然成立(true ⇒ true),并且仍然由遍历注册表的测试强制。

**一字棋 MUST NOT 跟着翻。** 3×3 是已解棋,双方不犯错必平;而且它不计分的依据是「唯一对手是
机器人」,开了真人房那条依据会失效、需要重新论证。它因此仍然是注册表里 `SupportsHumanVsHuman
== false` 的那一个,而这不只是保守 —— 那条「放行与拒绝两种结果都 MUST 出现过」的遍历断言靠它
才不会退化成只走一边的空转。

#### Scenario: 不变量成立
- **WHEN** 遍历注册表检查每个棋种
- **THEN** 每个 `IsRated == true` 的棋种 MUST 同时 `SupportsHumanVsHuman == true`

#### Scenario: 象棋开得出真人房
- **WHEN** `POST /api/rooms` 送 `{ name, gameKey: "xiangqi" }`
- **THEN** HTTP 201

#### Scenario: 象棋对局结算 ELO
- **WHEN** 一局真人象棋结束
- **THEN** 双方各得到 / 更新一行 `UserGameStats(userId, "xiangqi")`,ELO 按既有公式结算

#### Scenario: 象棋有阶梯页
- **WHEN** 打开 `/g/xiangqi/lobby`
- **THEN** 渲染排行榜卡片 —— 它按 `descriptor.isRated` 渲染,MUST NOT 需要任何新代码

#### Scenario: 注册表里仍然两类都有
- **WHEN** 遍历注册表统计 `SupportsHumanVsHuman` 的真假两类
- **THEN** 两类 MUST 都非空 —— 一字棋是 false 的那一个

### Requirement: 象棋的走子逻辑由标准开局与残局两个棋种**共用一份**

`XiangqiRules`(从标准开局)与 `XiangqiEndgameRules`(从选定局面)SHALL 共用**同一份**走子合法性与胜负判定,而 MUST NOT 各持一份副本。

**复制品会漂,而漂的表现是同一步棋在两个房间里一个合法一个不合法** —— 那种不一致没有任何断言会红,除非有人正好同时在两种房间里试同一步。

`XiangqiRules` 的**对外行为 MUST 不变**:它仍然从标准开局重放历史。既有的象棋测试因此是这次抽取「没有改行为」的可执行形式,而它们 MUST 一条不改地通过。

#### Scenario: 两个棋种对同一步棋的判断一致
- **WHEN** 给定同一个局面与同一步棋,分别经两个棋种判断
- **THEN** 结果 MUST 相同 —— 而这条断言 MUST 用**残局与标准开局都能到达**的局面,否则它在单一路径上恒真

#### Scenario: 既有象棋测试原样通过
- **WHEN** 运行既有的 `XiangqiRules` 测试
- **THEN** 全部 MUST 通过,且断言 MUST 一条未改

### Requirement: `XiangqiEndgameRules` 从设置里读起始局面与先走方

设置 SHALL 编码**起始局面**与**先走方**,而两者都 MUST 来自古谱线路自己的列,MUST NOT 由客户端提供。

- 走子从该起始局面重放,而 MUST NOT 从标准开局重放;
- `FirstSeat(state)` SHALL 返回设置里的先走方 —— 实测 1634 局残局里 **7 局是黑先走**,所以它是数据,不是「红先」这条约定。

校验 MUST 至少覆盖:盘面串长度、恰好一帅一将、将/士在各自九宫内、相/象在各自的点上、先走方落在 `[0, SeatCount)`。**校验不通过 MUST 抛并说明哪一条不满足**,而 MUST NOT 静默退回标准开局 —— 那会让一个坏设置表现成「这局怎么是开局」。

#### Scenario: 残局从它自己的局面开始
- **WHEN** 用一则 4 子残局的设置开局
- **THEN** 第一步的合法性 MUST 按那 4 个子判断;一步在标准开局下合法、在该残局下非法的走子 MUST 被拒

#### Scenario: 黑先走的残局由黑先动
- **WHEN** 用一则先走方为黑的残局开局
- **THEN** `Game.CurrentTurn` MUST 是黑的座位;红方此时走子 MUST 被拒

#### Scenario: 坏设置不静默退回开局
- **WHEN** 设置的盘面串少一个字符,或缺一个将
- **THEN** MUST 抛并点名;MUST NOT 开出一局标准开局的棋

### Requirement: 建房时用**线路 id** 指定残局,而不是盘面

创建房间 SHALL 接受一个**可选**的古谱线路 id;服务端据它从库里取起始局面与先走方。请求 MUST NOT 携带盘面串 —— 那等于让客户端定义棋局。

两个方向都 MUST 校验:给了线路 id 而棋种不是残局棋种 → 拒;是残局棋种而没给线路 id → 拒。

线路 id 不存在时 MUST 是一个**清楚的拒绝**,而 MUST NOT 落成「开一局标准开局的棋」。

#### Scenario: 指定线路开房
- **WHEN** 以残局棋种 + 一个存在的线路 id 建房并坐满
- **THEN** 对局从该线路的起始局面开始,先走方与该线路一致

#### Scenario: 线路 id 与棋种不匹配
- **WHEN** 以标准象棋建房却带了线路 id,或以残局棋种建房却没带
- **THEN** MUST 拒绝,两个方向都要有断言

#### Scenario: 不存在的线路
- **WHEN** 带一个不存在的线路 id
- **THEN** MUST 拒绝并说明;MUST NOT 开局

### Requirement: 界面 MUST 说清楚平台**认不出和棋**

残局房的界面 SHALL 写明:这一局只会以**将死、认输或超时**结束,而平台 **MUST NOT** 宣布和棋。

**结论不变,而它此前的理由已经不成立了 —— 这两件事要分开写。** 原文的理由是「领域里没有
重复局面 / 长将 / 长捉规则」。`limit-repeated-checks` 落地之后,领域里**有**重复局面计数,也有
一条长将规则;那句理由从此是错的,而**结论仍然对**。

现在的理由:那条规则只用来**拒绝一步棋**,不用来**宣布**一个结果。而**「和棋」题的解就是把局面
走成和** —— 判和还需要两样东西,一样是规则(长捉 / 长拦,以及「重复到第几次算和」这个与
「第几次不许再走」不同的判据),另一样是一个决定。两个人可以自己商量,但平台看不出来。
**不写这句话,玩家会以为是程序坏了**,而那种误解和真的坏了在界面上完全一样。

**一个正确的结论可以在支撑它的前提变假之后继续正确,而那正是它看不见的原因** —— 界面上什么都
没坏。所以这条要求 MUST 保留它现在的理由文本:下一次有人来问「和棋做不做」时,读到的必须是
剩下的那两样东西,而不是一句已经被推翻的「领域里没有长将规则」。

这条与「界面不出现任何『你解对了』」是**同一条约束的两半**:平台不判对错,也不判和。

#### Scenario: 残局房写明这一点
- **WHEN** 进入一个残局房
- **THEN** 界面 MUST 有一处说明「平台不判和棋」;而 MUST NOT 出现任何宣布和棋或判定解法正确的文案

### Requirement: 同一个将军最多重复三次

一步**将军**的着法,若它走出来的局面此前已经由**同一方**走出过 3 次,SHALL 被拒绝,并抛出带
`repeated-check` 码的 `InvalidMoveException`。上限 `3` MUST 是一个具名常量,而不是散落的字面量。

**判据是局面,不是走法。** 「将军」是**局面**的性质,不是那一步的性质 —— 所以同一个局面重复
出现 N 次,就等于同一个将军被送出 N 次,而实现 MUST NOT 为每一步额外记「这一步是不是将军」。
少了那份记录,少的是一份会和局面本身漂开的第二真源。

局面的身份 SHALL **就是盘面** —— 象棋里没有王车易位 / 吃过路兵那类额外状态,所以盘面 +
轮到谁就是局面的全部,而**「轮到谁」在这一处不必再算进去**:计数只在对手被将的局面上进行,
而**一个「对手被将」的盘面只可能由本方走出来** —— 对手走到那儿等于把自己的将留在被吃的
位置,那一步早被自将规则挡掉,进不了历史。

这不是省一个条件,是量出来的:实现里原本有一个「只数本方走出的局面」的判断,而**五条变异里
只有它活了下来** —— 没有任何断言能让它红。删掉它靠的是自将必被拒,**而那条规则自己有测试**。
一个靠有测试的规则支撑的删除,好过一个没有任何断言覆盖的分支。

计数 SHALL 覆盖整局历史,MUST NOT 只看最近一个循环。起始局面 SHALL NOT 计入 —— 它不是任何人
走出来的,所以一个开局就被将着的残局设置不算「这个将军被送过一次」。

**将死优先于上限。** 一步**将死**的着法 MUST 判胜,判定顺序因此是先判将死、后判上限。

而**这两条撞不上,所以那个分支没有任何测试能让它红,这一点必须写下来而不是假装有覆盖**:
局面相同 ⇒ 合法着法集合相同(象棋里局面就是盘面 + 轮到谁,没有别的状态)⇒ 若此刻是将死,
那么此前那次也是将死,棋在那时就该结束了 —— 所以一个将死的局面**不可能**有过往出现,
在任何上限值下都不可能。「既达到上限又是将死」是一个**构造不出来的**组合,而一条
构造不出来的 Scenario 是一条永远不会失败的断言。

那个分支因此是**纯防御**,保留的理由是让正确性不依赖上面那段论证。它**变成承重的那一天**
有名字:谁把计数的键从「这一个完整局面」换成更粗的东西(例如为长捉数「这一方将了几次」),
不可能就变成可能,而那时它是唯一挡着的东西。

**这条规则拒绝一步棋,MUST NOT 宣布和棋,也 MUST NOT 直接判负。** 用户要的就是「不能再走了」。
而它不是一条悬在空中的规则:一方若**只剩**这一步,他确实一步也走不了,而收场的是既有的回合
超时 —— `TurnTimeoutWorker` 判走不了的一方负,那正是传统的长将判负。这个机制早就在那里,
并且同一条推理在 `GameKeyValidation` 里已经用过一次。

规则 SHALL 落在标准开局与残局**共用的那一份**判定里,因此两个棋种都受限,而残局正是长将
最常出现的地方。

落子类棋种(五子棋 / 一字棋)MUST NOT 获得任何重复限制:那里的盘面单调增长,同一个局面
不可能第二次出现,而一条永不触发的规则是每个读代码的人都要付一次的税。

#### Scenario: 第三次同一个将军仍然允许
- **WHEN** 同一方第三次走出同一个将军局面
- **THEN** MUST 被接受,对局 MUST 仍是 `Ongoing`

#### Scenario: 第四次被拒
- **WHEN** 同一方第四次走出同一个将军局面
- **THEN** MUST 抛 `InvalidMoveException`,码为 `repeated-check`;这一步 MUST NOT 落盘
- **AND** 上一条与这一条 MUST 同时存在 —— 只钉一端的话,一个把上限写成 0 的实现和一个
  写成 99 的实现各能通过其中一条

#### Scenario: 不将军的重复不受限
- **WHEN** 同一方第四次走出同一个**不将军**的局面(双方各自往复)
- **THEN** MUST 被接受 —— 这条规则限制的是**长将**,不是重复本身。少了这条对照,
  一个把所有重复都拒掉的实现也能通过上面两条

#### Scenario: 将死仍然判胜
- **WHEN** 一步将军的着法让对方无着可走
- **THEN** MUST 判走子方胜,MUST NOT 抛
- **AND** 这一条 MUST NOT 被写成「既达到上限又是将死」—— 那个组合构造不出来(见上),
  而一条构造不出来的断言永远不会失败

#### Scenario: 残局房同样受限
- **WHEN** 在一个 `xiangqi-endgame` 房里第四次走出同一个将军局面
- **THEN** MUST 抛 `repeated-check` —— 判定与标准开局共用同一份,而这条断言是它的可执行形式

#### Scenario: 用来数的历史必须是真走出来的
- **WHEN** 写这条规则的测试
- **THEN** 历史 MUST 由逐步调用 `Apply` 累积而成,MUST NOT 手工拼一串 `PlayedMove` ——
  重放**不会**校验历史里的步,所以手拼的历史可以是一局不可能的棋,而那时断言测的是别的东西

