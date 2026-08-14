## ADDED Requirements

### Requirement: 提示优先揭示玩家指着的那一格

`IdiomCrosswordRules.Hint` SHALL 依据客户端上报的 `stateJson` 决定揭示哪一格,并按下列优先级取舍。`stateJson` 形如 `{ "filled": ["行,列", …], "selected": "行,列" }` —— 分别是客户端已填入字符的格子集合与当前光标位置,两者都是玩家看得见的东西,不含答案。

揭示顺序 SHALL 为:

1. `selected` 指向一个**存在且非预填**的格子 → 揭示它,**即使该格已有字**。玩家盯着一个填错的格子要提示,想解的正是那一格;客户端会先把错字块退回字盘再写入正确字。
2. 否则 → 阅读顺序上第一个**不在 `filled` 中**的格子。
3. `filled` 覆盖了全部格子 → 阅读顺序上第一个非预填格。满盘皆错正是"用正确字覆盖一格"最有用的时刻。

`selected` 若不指向真实格子、或指向预填格,SHALL 被忽略并退到第 2 条 —— 重开后残留的光标应当降级成一个合理的提示,而不是一个错误。

**本要求取代了原先的"按阅读顺序推进"。** 那条规则在实测中近乎必然浪费:玩家自上而下填,提示也自上而下揭,两者同向,所以等玩家卡住时,提示能够到的格子全是他已经解开的。第 5 关实测中,第一个有用的提示要点到第 14 次。

#### Scenario: 优先揭示选中格
- **WHEN** `selected` 为 `"6,2"`(存在、非预填),`filled` 含前 13 格
- **THEN** 揭示 `(6,2)`

#### Scenario: 选中格已有字也照揭
- **WHEN** `selected` 指向一个已在 `filled` 中的非预填格
- **THEN** 仍揭示该格

#### Scenario: 没有选中格时揭第一个未填格
- **WHEN** `selected` 缺省,`filled` 含阅读序前 13 个可揭示格
- **THEN** 揭示第 14 个 —— 即玩家真正还空着的那一格

#### Scenario: 回归 —— 已解开的格子不再被浪费
- **WHEN** 玩家已填好网格上半部,只剩底部三格,携带真实 `filled` 请求提示
- **THEN** 揭示的是底部那三格之一,MUST NOT 是任何一个已填格

#### Scenario: 无效的选中格被忽略
- **WHEN** `selected` 指向不存在的坐标或一个预填格
- **THEN** 退到第一个未填格,请求正常完成

#### Scenario: 满盘时覆盖第一格
- **WHEN** `filled` 覆盖全部格子
- **THEN** 揭示阅读顺序上第一个非预填格

## MODIFIED Requirements

### Requirement: `IdiomCrosswordRules` 实现四个操作

`Gewu.Domain` SHALL 提供注册在 `idiom-crossword` 下的 `IPuzzleRules` 实现:

- `Validate` —— 全部格子与答案一致才算通关。
- `CheckPartial` —— 校验**一个词槽**;正确时 MUST 附带该成语与其释义作为载荷(见 `puzzle-core` 的 `check` 要求)。
- `Hint` —— 依据客户端上报的盘面状态揭示一格,顺序见下一条要求。
- `Score` —— `cost = mistakes + hintsUsed`;`cost == 0` → 3 星,`cost <= 2` → 2 星,否则 1 星。

计分公式与原型一致。三个入参都由服务端产生,本实现 MUST NOT 引入任何其它信号。

#### Scenario: 全对才通关
- **WHEN** 提交的网格有任意一格与答案不符
- **THEN** `Validate` 判定未通关

#### Scenario: 答对一条成语时返回释义
- **WHEN** 某词槽被填满且与答案一致
- **THEN** `CheckPartial` 判定正确,载荷中含该成语的词与释义

#### Scenario: 答错一条成语时不返回释义
- **WHEN** 某词槽被填满但与答案不符
- **THEN** `CheckPartial` 判定错误,MUST NOT 返回载荷

#### Scenario: 计分与原型一致
- **WHEN** `(hintsUsed, mistakes)` 分别为 `(0,0)`、`(1,1)`、`(0,3)`
- **THEN** 星级分别为 3、2、1
