# xiangqi Specification Delta

## RENAMED Requirements

标题本身是一个会过期的判断,而它已经过期了 —— 象棋现在**有**对手,也计分。留着旧标题改内容
比改标题更糟:那正是 `enforce-human-vs-human` 付过账的形状,一条把过期事实钉成正确的断言。

应用顺序是 RENAMED → REMOVED → MODIFIED → ADDED,所以下面 MODIFIED 用的是新标题。

- FROM: ### Requirement: 象棋今天不计分，因为它还没有对手
- TO: ### Requirement: 象棋开放人人对战并计分

## MODIFIED Requirements

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


