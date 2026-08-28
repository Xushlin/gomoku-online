## MODIFIED Requirements

### Requirement: `XiangqiRules` 对外枚举合法着法

`XiangqiRules` SHALL 提供 `IReadOnlyList<MoveIntent> LegalMoves(IReadOnlyList<PlayedMove> history, Stone side)`,
返回该方在该局面下的**全部合法着法**。判据 SHALL 是「`Apply` 会不会接受它」,而 MUST NOT 是
一份列举出来的排除项清单 —— 后者每加一条规则就少一项,而**少了的那一项不会有任何断言发现**。
当前会被排除的是:自将 / 照面,以及**超出上限的重复将军**(见 `xiangqi`「同一个将军最多重复三次」)。

它此前是私有的(`HasAnyLegalMove` 内部用)。对外暴露是因为 AI 需要它,而让 AI 自己再实现一遍
走法枚举,就是第二份真源 —— 两份迟早不一致,而不一致的表现是 **AI 走出规则会拒绝的棋**。

**长将上限让这条从「已经成立」变成「需要一份共用实现」。** 自将与照面是**局面**的性质,
`LegalMoves` 本来就在算;重复将军是**历史**的性质,而 `LegalMoves` 恰好收得到历史。所以
「这一步是不是一次被禁的重复将军」SHALL 由 `Apply` 与 `LegalMoves` **共用同一份判断**,
MUST NOT 各写一遍。

搜索**内部**那份按盘面枚举的入口(`LegalMovesOnBoard`)收不到历史,因此深层节点不懂重复 ——
这是**棋力**问题而不是合法性问题:真正走出去的只有根节点那一步,而根节点走的是本入口。
这一点写下来,是因为「AI 内部有一份不懂长将的枚举」读起来像个缺陷,而它是刻意的。

一方的**每一条**着法都被这条上限挡住时,`LegalMoves` SHALL 返回空,而 AI 的报错文案
MUST NOT 断言「棋早该结束了」—— 那句话在长将上限存在之后就不再是唯一的解释。收场的是
回合超时(`TurnTimeoutWorker` 判走不了的一方负),见 `xiangqi` 那条要求。

#### Scenario: 开局有合法着法
- **WHEN** 对开局局面枚举红方着法
- **THEN** 返回非空,且每一条都能被 `Apply` 接受

#### Scenario: 枚举与判负一致
- **WHEN** 某方无合法着法
- **THEN** `LegalMoves` 返回空,且 `Apply` 在对方走完后判该方负

#### Scenario: 被禁的重复将军不出现在枚举里
- **WHEN** 一个局面下某方的某一步是**第四次**送出同一个将军
- **THEN** `LegalMoves` MUST NOT 包含那一步,而 MUST 仍然包含其余着法 ——
  两半都要断言:少了后一半,一个返回空表的实现也能通过
