## MODIFIED Requirements

### Requirement: 侧栏在座位多于两个时说座位号

「轮到谁」的文字 SHALL 在斗地主里说**座位号**(`game.turn.seat-turn`),而不是「黑方 / 白方」。

**这是在浏览器里发现的**:一局斗地主轮到 2 号座位时,写的是「白方走棋」—— 而那一桌上
没有白方。

**本条原来的判据已经漂了两次,而两次都不是本变更造成的 —— 这里把它对齐到代码。**

1. 原文写着「判据是 `seats.length`,MUST NOT 去问棋种注册表要 `seatCount`」。那句话在
   `publish-seat-count` 里就被推翻了:`seats` 只含**在座的**座位,所以 `seats.length`
   是「坐了几个人」,而一个**等待中**的三座位房间会被当成两座位房间渲染。侧栏与操作条
   今天读的都是描述符给的 `seatCount`,而**这条要求仍然禁止那么做** —— 一条与已发布代码
   相反的 live 要求,`validate --strict` 看不见。
2. 而 `seatCount` 也不再是**这个问题**的判据:说不说座位号,取决于这个棋种有没有声明
   席位名(见 `web-game-board` 那两条)。斗地主的答案不变 —— 它不声明,所以它说座位号 ——
   **变的是它为什么**。

#### Scenario: 斗地主说座位号
- **WHEN** 一个斗地主房间且 `currentSeat === 2`
- **THEN** 文字 MUST 是「轮到 3 号座位」,MUST NOT 出现「白方」

#### Scenario: 等待中的三座位房间也说座位号
- **WHEN** 一个三座位房间只坐了两个人
- **THEN** 文字仍 MUST 说座位号 —— 判据 MUST NOT 是 `seats.length`

#### Scenario: 五子棋一字不变
- **WHEN** 一个五子棋房间
- **THEN** 文字仍是「黑方 / 白方走棋」

