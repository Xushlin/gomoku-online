## MODIFIED Requirements

### Requirement: `IBoardGameAi` 是纯函数式 AI 决策接口

`IBoardGameAi` SHALL 定义为:

```
MoveIntent SelectMove(IReadOnlyList<PlayedMove> history, Stone myStone);
```

**签名从 `SelectMove(Board, Stone) → Position` 改成这样,理由与 `IGameRules.Apply` 完全相同**:
原签名有两条硬假设 —— 吃的是 `Board`(连 N 子专用的表示,它带着 `WinLength` 与 `PlaceStone`),
以及返回一个 `Position`(假设一步棋就是「落在某格」)。中国象棋两条都不满足。

**该接口此前的注释写着「它从来就没用到任何五子棋专属的东西」—— 那句话是错的。**
它是 `add-tictactoe` 把 `IGomokuAi` 改名时写下的,而一字棋证明不了这件事:它也是落子类、
也用 `Board`。**一字棋是缩小版五子棋,它验证不了泛用性。**

实现 MUST:

- 返回一个在该棋种下**合法**的着法;
- MUST NOT 修改入参 `history`;
- MUST NOT 读时钟 / 磁盘 / 网络 / 静态可变状态;
- 相同 `history` + 相同 `myStone` + 相同随机源 → 输出 MUST 可复现。

无合法着法时 MUST 抛 `InvalidOperationException` —— 调用方应在对局结束之后就不再问 AI。

#### Scenario: 落子类棋种返回无起点的着法
- **WHEN** 五子棋 AI 被要求走一步
- **THEN** 返回的 `MoveIntent.From` 为 `null`

#### Scenario: 走子类棋种返回带起点的着法
- **WHEN** 象棋 AI 被要求走一步
- **THEN** 返回的 `MoveIntent.From` 非 `null`,且该着法能被 `XiangqiRules.Apply` 接受

#### Scenario: 不修改历史
- **WHEN** AI 选完一步
- **THEN** 入参 `history` 的内容 MUST 与调用前一致

## ADDED Requirements

### Requirement: `IPlacementAi` 保住既有落子类 AI 的实现与测试

`Gewu.Domain` SHALL 定义 `IPlacementAi`,承载原来的 `Position SelectMove(Board board, Stone myStone)`,
并提供一个适配器把它包成 `IBoardGameAi`:适配器用 `INInARowRules.ReplayBoard(history)` 造盘、
把返回的 `Position` 包成 `MoveIntent.Place(...)`。

**既有五个落子类 AI 实现 MUST 一行不改。** 它们背后有一批很值钱的测试 —— 尤其一字棋 Hard 档
那套**穷举**验证(对每一个可达局面断言它落在博弈论最优值上)。为了换签名重写它们,
是拿一份已经证明过的东西去换一次纯机械改动的风险。

#### Scenario: 既有 AI 经适配器仍然工作
- **WHEN** 五子棋 Hard 档经适配器被调用
- **THEN** 它选出的点与直接调用 `Board` 版签名时相同

#### Scenario: 适配器只服务连 N 子棋种
- **WHEN** 适配器被构造
- **THEN** 它要求一个 `INInARowRules` —— 走子类棋种的 AI 不经过这条路

### Requirement: `XiangqiRules` 对外枚举合法着法

`XiangqiRules` SHALL 提供 `IReadOnlyList<MoveIntent> LegalMoves(IReadOnlyList<PlayedMove> history, Stone side)`,
返回该方在该局面下的**全部合法着法**(已排除会导致自将 / 照面的着法)。

它此前是私有的(`HasAnyLegalMove` 内部用)。对外暴露是因为 AI 需要它,而让 AI 自己再实现一遍
走法枚举,就是第二份真源 —— 两份迟早不一致,而不一致的表现是 **AI 走出规则会拒绝的棋**。

#### Scenario: 开局有合法着法
- **WHEN** 对开局局面枚举红方着法
- **THEN** 返回非空,且每一条都能被 `Apply` 接受

#### Scenario: 枚举与判负一致
- **WHEN** 某方无合法着法
- **THEN** `LegalMoves` 返回空,且 `Apply` 在对方走完后判该方负

### Requirement: 象棋 AI 提供三档难度

`Gewu.Domain` SHALL 提供 `XiangqiAiFactory : IGameAiFactory`(`GameKey == "xiangqi"`),
按 `BotDifficulty` 构造三档 AI。三档 MUST 都只走**合法**着法。

象棋**不可能穷举** —— 与一字棋 Hard 档的穷举 minimax 不同,这里只能限深搜索 + 评估函数。
因此本变更 MUST NOT 声称任何一档「不可战胜」或「最优」:那种断言在这里既做不到也验不了。
可验证的是:着法合法、看得见一步吃子、以及**深一档不弱于浅一档**。

#### Scenario: 三档都只走合法着法
- **WHEN** 任一难度在任意局面被调用
- **THEN** 返回的着法能被 `XiangqiRules.Apply` 接受

#### Scenario: 会吃白送的子
- **WHEN** 对方有一个子可以被白吃且不会被反吃
- **THEN** Medium 与 Hard 档 MUST 吃掉它

#### Scenario: 会避开一步将死
- **WHEN** 存在一步能解将
- **THEN** 任何一档都 MUST 走一步解将的棋(否则它走的是非法着法)
