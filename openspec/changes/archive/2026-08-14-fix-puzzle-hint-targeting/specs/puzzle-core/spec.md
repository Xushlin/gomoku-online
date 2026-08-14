## MODIFIED Requirements

### Requirement: `hint` 由服务端揭示并计费

`POST /api/puzzle-attempts/{id}/hint` SHALL 由 `IPuzzleRules.Hint` 依据 `SolutionJson`、`LayoutJson` 与**客户端上报的盘面状态**决定要揭示的片段,返回该片段,并递增 `HintsUsed`。

请求体 MAY 携带一份对平台**不透明**的 `stateJson` —— 与 `check` / `submit` 的载荷同一性质,平台不理解其内容,由各游戏的规则自行解析。缺省或无法解析时,规则 SHALL 退化到一个合理的默认揭示,MUST NOT 返回错误。

上报的盘面状态 MUST NOT 参与计分。`HintsUsed` 仍由服务端在每次调用时递增,是唯一算数的那个数字;客户端上报的只是"我这边哪些格有字、光标在哪",它决定的是**揭哪一格**,而不是**花了几次**。

采信这份上报不构成计分漏洞:客户端报告的是自己可见的盘面,不是答案;答案始终只在服务端,响应也始终只有一格。客户端确实可以借此**指定**要揭哪一格 —— 那是特性而非漏洞,原型本来就让玩家点着某格要提示,而且每次照样扣一颗星。

响应 MUST 只包含被揭示的那一个片段,MUST NOT 包含答案的其余部分。

#### Scenario: 提示只揭示一个片段
- **WHEN** 对一个 4 字成语关卡调用 `hint` 一次
- **THEN** 响应只含一个位置及其字,`HintsUsed` 为 1

#### Scenario: 上报状态影响揭哪一格,不影响计数
- **WHEN** 两次调用 `hint`,分别携带不同的 `stateJson`
- **THEN** 两次可能揭示不同的格子,但 `HintsUsed` 依次为 1、2 —— 计数只由调用次数决定

#### Scenario: 缺省请求体仍可用
- **WHEN** 调用 `hint` 且不带请求体
- **THEN** 仍返回一个被揭示的片段并递增 `HintsUsed`,MUST NOT 返回 4xx

#### Scenario: 畸形状态不致报错
- **WHEN** `stateJson` 不是合法 JSON
- **THEN** 规则退化到默认揭示,请求正常完成
